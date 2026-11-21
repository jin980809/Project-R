using Mirror;
using UnityEngine;

// 계열, 레벨, 스킬 트리, 스킬 장착(Q, E, R).
// - 계열은 3개(0 전사, 1 사수, 2 마법사)이고, 계열마다 직업 가지 3개로 갈라지는 스킬 트리가 하나씩 있다.
//   계열의 무기라면 어느 것을 들어도 그 트리에서 배운 스킬을 모두 쓸 수 있다(예: 검을 들고 창기사 스킬).
//   로비에서 숫자 키(1~3)로 시작 계열을 고르면 그 계열의 시작 무기를 받는다. 다른 계열은 그 계열 무기로 처치해 숙련도를 채운 뒤
//   시작 방의 제단에서 바꾼다.
// - 몬스터를 잡으면 경험치를 얻고, 레벨이 오를 때마다 포인트를 1 받는다. 한 판이 끝나면 초기화된다.
// - 스킬 트리와 장착 상태는 계열별로 따로 기억한다. 스킬을 해금하면 빈 칸에 자동으로 장착된다.
// - 직업 가지를 전부 찍으면 각성: 그 직업 스킬의 피해·재사용 시간이 좋아지고 직업마다 고유 효과가 붙는다.
// - 스킬 사용 판정(마나, 재사용 시간, 피해, 회복)은 서버가 하고, 시전 동작과 돌진 이동은 본인 클라이언트가 한다.
public class PlayerSkills : NetworkBehaviour
{
    public const int ClassCount = 3;
    private const int MaxZones = 6;
    private const float ZoneTick = 0.5f;

    // 서버가 관리하는 장판 하나
    private struct ZoneState
    {
        public bool active;
        public Vector2 position;
        public float radius;
        public float endTime;
        public float nextTick;
        public HitInfo hit;
        public bool damages;
        public float healPercent;
        // 블랙홀: 초당 가운데로 끌어당기는 거리
        public float pullSpeed;
    }

    [Header("참조")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerInventory inventory;
    [Tooltip("계열 순서(전사, 사수, 마법사)")]
    [SerializeField] private ClassDefinition[] classes;

    [Header("성장")]
    [SerializeField] private int maxLevel = 20;
    [SerializeField] private int startPoints = 1;
    [SerializeField] private int xpBase = 16;
    [SerializeField] private int xpPerLevel = 3;
    [SerializeField] private float muzzleDistance = 0.4f;
    [Tooltip("뛰어올라 내려찍는 스킬이 땅을 칠 때 본인 화면 흔들림 세기와 시간")]
    [SerializeField] private float impactShakeAmplitude = 0.16f;
    [SerializeField] private float impactShakeTime = 0.22f;
    [Tooltip("계열을 바꾸는 데 필요한 숙련도(그 계열 무기를 들고 처치한 수)")]
    [SerializeField] private int masteryRequired = 8;
    [Tooltip("던전에서 스킬 트리 초기화 비용: 층 x 이 골드")]
    [SerializeField] private int resetGoldPerFloor = 50;

    // 전투 씬에서의 계열
    [SyncVar(hook = nameof(OnClassChanged))] private int classIndex;
    // 로비에서 고른 시작 계열
    [SyncVar(hook = nameof(OnClassChanged))] private int lobbyClass;
    // 계열별 숙련도(그 계열 무기를 들고 처치한 수, 바꾸는 조건까지만 센다)
    private readonly SyncList<byte> mastery = new SyncList<byte>();
    [SyncVar] private int level = 1;
    [SyncVar] private int xp;
    [SyncVar(hook = nameof(OnPointsChanged))] private int skillPoints;
    // 계열 수 x 최대 노드 수. 각 칸은 그 노드에 찍은 단계.
    private readonly SyncList<byte> nodeRanks = new SyncList<byte>();
    // 계열 수 x 장착 칸 수. 각 칸은 장착한 스킬 번호(없으면 EmptySlot).
    private readonly SyncList<byte> equipped = new SyncList<byte>();
    // 켜져 있는 강화 스킬(계열 번호 x 최대 스킬 수 + 스킬 번호). 없으면 -1.
    [SyncVar(hook = nameof(OnBuffChanged))] private int activeBuff = -1;
    // 강화가 끝나는 네트워크 시각(HUD 남은 시간 표시용)
    [SyncVar] private double buffEndNetworkTime;
    [SyncVar] private float buffDuration;

    private GameHub hub;
    private GameNetworkManager manager;
    private int revision;

    // 본인 클라이언트: 스킬별 다시 쓸 수 있는 시각
    private readonly float[] localReadyTime = new float[ClassDefinition.MaxSkills];
    private readonly float[] localCooldown = new float[ClassDefinition.MaxSkills];
    // 서버
    private readonly float[] serverReadyTime = new float[ClassDefinition.MaxSkills];
    private readonly ZoneState[] zones = new ZoneState[MaxZones];
    private float buffEndTime;
    // 돌진 끝의 내려찍기(시전이 끝나는 시각에 그 자리에서)
    // 뛰어올라 내려찍는 범위 공격(impactDelay): 서버는 그 순간에 판정하고, 화면은 그 순간에 이펙트를 띄운다.
    private bool areaPending;
    private float areaTime;
    private Vector2 areaPosition;
    private Vector2 areaAim;
    private float areaRadius;
    private float areaArc;
    private HitInfo areaHit;
    // 이번 시전의 차징 배율(차징 스킬이 아니면 1). 시전할 때마다(본인, 서버, 다른 사람 화면) 새로 정한다.
    private float castSize = 1f;
    private float castDamage = 1f;
    private float delayedFxSize = 1f;
    private SkillDefinition delayedFxSkill;
    private float delayedFxTime;
    private Vector2 delayedFxCenter;
    private Vector2 delayedFxAim;
    private Vector2 delayedFxGround;
    private bool slamPending;
    private float slamTime;
    private float slamRadius;
    private HitInfo slamHit;
    private int slamClass;
    private int slamSkill;

    public int Level => level;
    public int Xp => xp;
    public int SkillPoints => skillPoints;
    public int Revision => revision;
    public int MasteryRequired => masteryRequired;

    // 그 무기를 쓰는 계열. 어느 계열에도 없으면 0(전사).
    public int ClassOf(int weaponKind)
    {
        int found = 0;
        for (int i = classes.Length - 1; i >= 0; i--)
        {
            if (classes[i].UsesWeapon(weaponKind))
            {
                found = i;
            }
        }
        return found;
    }

    public int ClassIndex
    {
        get
        {
            int held = inventory.WeaponKindIndex;
            int index = ClassOf(held);
            if (hub != null && hub.IsCombatScene)
            {
                index = Mathf.Clamp(classIndex, 0, classes.Length - 1);
            }
            else if (lobbyClass >= 0 && lobbyClass < classes.Length && classes[lobbyClass].UsesWeapon(held))
            {
                index = lobbyClass;
            }
            return index;
        }
    }

    // 계열의 무기(어느 것이든)를 들고 있어야 스킬을 쓸 수 있다.
    public bool HoldingClassWeapon
    {
        get
        {
            return Class.UsesWeapon(inventory.WeaponKindIndex);
        }
    }

    public int GetMastery(int classNumber)
    {
        int value = 0;
        if (classNumber >= 0 && classNumber < mastery.Count)
        {
            value = mastery[classNumber];
        }
        return value;
    }

    public string GetClassName(int classNumber)
    {
        return classes[Mathf.Clamp(classNumber, 0, classes.Length - 1)].DisplayName;
    }

    public ClassDefinition Class => classes[ClassIndex];

    public int XpToNext
    {
        get
        {
            return xpBase + xpPerLevel * (level - 1);
        }
    }

    public bool IsMaxLevel => level >= maxLevel;

    public int GetRank(int nodeIndex)
    {
        return GetRankOf(ClassIndex, nodeIndex);
    }

    private int GetRankOf(int classNumber, int nodeIndex)
    {
        int rank = 0;
        int index = classNumber * ClassDefinition.MaxNodes + nodeIndex;
        if (nodeIndex >= 0 && nodeIndex < ClassDefinition.MaxNodes && index < nodeRanks.Count)
        {
            rank = nodeRanks[index];
        }
        return rank;
    }

    // 지금 계열의 직업 가지에 쓴 포인트
    public int GetJobSpent(int job)
    {
        ClassDefinition definition = Class;
        int spent = 0;
        for (int i = 0; i < definition.NodeCount; i++)
        {
            if (definition.GetNode(i).job == job)
            {
                spent += GetRank(i);
            }
        }
        return spent;
    }

    // 지금 계열의 직업 가지를 전부 찍었는지(각성)
    public bool IsJobAwakened(int job)
    {
        ClassDefinition definition = Class;
        bool any = false;
        bool full = true;
        for (int i = 0; i < definition.NodeCount; i++)
        {
            JobNode node = definition.GetNode(i);
            if (node.job == job)
            {
                any = true;
                if (GetRank(i) < node.maxRank)
                {
                    full = false;
                }
            }
        }
        return any && full;
    }

    // 지금 계열에서 그 고유 효과를 가진 직업이 각성했으면 그 직업 가지를 돌려준다.
    private bool TryGetAwakening(JobAwakening kind, out JobBranch branch)
    {
        ClassDefinition definition = Class;
        branch = null;
        bool found = false;
        for (int job = 0; job < ClassDefinition.JobCount && !found; job++)
        {
            JobBranch candidate = definition.GetJob(job);
            if (candidate.awakening == kind && IsJobAwakened(job))
            {
                branch = candidate;
                found = true;
            }
        }
        return found;
    }

    public bool IsSkillUnlocked(int skillIndex)
    {
        bool unlocked = false;
        if (skillIndex >= 0 && skillIndex < Class.SkillCount)
        {
            int node = Class.FindSkillNode(skillIndex);
            if (node >= 0)
            {
                unlocked = GetRank(node) > 0;
            }
        }
        return unlocked;
    }

    // 지금 계열의 장착 칸에 든 스킬 번호. 비었으면 -1.
    public int GetSlotSkill(int slot)
    {
        int skill = -1;
        int index = ClassIndex * ClassDefinition.SlotCount + slot;
        if (slot >= 0 && slot < ClassDefinition.SlotCount && index < equipped.Count)
        {
            if (equipped[index] != ClassDefinition.EmptySlot)
            {
                skill = equipped[index];
            }
        }
        return skill;
    }

    // 그 스킬이 장착된 칸. 없으면 -1.
    public int FindSlotOf(int skillIndex)
    {
        int found = -1;
        for (int i = 0; i < ClassDefinition.SlotCount && found < 0; i++)
        {
            if (GetSlotSkill(i) == skillIndex)
            {
                found = i;
            }
        }
        return found;
    }

    // 그 노드를 지금 찍을 수 있는지(포인트, 최대 단계, 부모 노드)
    public bool CanAllocate(int nodeIndex)
    {
        bool can = false;
        ClassDefinition definition = Class;
        if (skillPoints > 0 && nodeIndex >= 0 && nodeIndex < definition.NodeCount)
        {
            JobNode node = definition.GetNode(nodeIndex);
            if (GetRank(nodeIndex) < node.maxRank)
            {
                can = node.parent < 0 || GetRank(node.parent) > 0;
            }
        }
        return can;
    }

    // 그 스킬의 재사용 시간(장비·노드 배율, 각성한 직업이면 더 짧게, 창기사 각성이면 돌진은 더 짧게)
    private float GetSkillCooldown(SkillDefinition skill, int skillIndex)
    {
        ClassDefinition definition = Class;
        float cooldown = skill.Cooldown * inventory.Stats.cooldownMultiplier;
        int job = definition.GetSkillJob(skillIndex);
        if (job >= 0 && IsJobAwakened(job))
        {
            cooldown *= 1f - definition.AwakenCooldownPercent * 0.01f;
        }
        JobBranch dragoon;
        if (skill.Kind == SkillKind.Dash && TryGetAwakening(JobAwakening.Dragoon, out dragoon))
        {
            cooldown *= 1f - dragoon.awakenValueB * 0.01f;
        }
        return cooldown;
    }

    // 그 스킬의 피해 배율(각성한 직업의 스킬이면 더 세게)
    private float GetSkillJobFactor(int skillIndex)
    {
        ClassDefinition definition = Class;
        float factor = 1f;
        int job = definition.GetSkillJob(skillIndex);
        if (job >= 0 && IsJobAwakened(job))
        {
            factor = 1f + definition.AwakenSkillDamagePercent * 0.01f;
        }
        return factor;
    }

    // 광전사 각성: 체력이 절반 이하면 모든 공격이 세진다(PlayerInventory.RollHit이 곱한다).
    public float ConditionalDamageFactor
    {
        get
        {
            float factor = 1f;
            JobBranch frenzy;
            if (controller.Hp * 2 <= controller.MaxHp && TryGetAwakening(JobAwakening.Frenzy, out frenzy))
            {
                factor = 1f + frenzy.awakenValueA * 0.01f;
            }
            return factor;
        }
    }

    // 사제 각성: 회복·부활 효과 배율
    private float HealFactor
    {
        get
        {
            float factor = 1f;
            JobBranch saint;
            if (TryGetAwakening(JobAwakening.Saint, out saint))
            {
                factor = 1f + saint.awakenValueA * 0.01f;
            }
            return factor;
        }
    }

    // 장착 칸의 남은 재사용 시간 비율(1 = 방금 씀, 0 = 사용 가능). 본인 화면용.
    public float GetSlotCooldown01(int slot)
    {
        float ratio = 0f;
        int skillIndex = GetSlotSkill(slot);
        if (skillIndex >= 0)
        {
            float remaining = localReadyTime[skillIndex] - Time.time;
            if (remaining > 0f && localCooldown[skillIndex] > 0f)
            {
                ratio = Mathf.Clamp01(remaining / localCooldown[skillIndex]);
            }
        }
        return ratio;
    }

    // ------------------------------------------------------------ 초기화(PlayerController가 순서를 정해 부른다)

    public void ServerInit(GameHub sceneHub)
    {
        hub = sceneHub;
        manager = (GameNetworkManager)NetworkManager.singleton;
        level = 1;
        xp = 0;
        skillPoints = startPoints;
        activeBuff = -1;
        lobbyClass = manager.GetSlotStartClass(controller.Slot);
        int held = inventory.WeaponKindIndex;
        classIndex = ClassOf(held);
        if (lobbyClass >= 0 && lobbyClass < classes.Length && classes[lobbyClass].UsesWeapon(held))
        {
            classIndex = lobbyClass;
        }
        for (int i = 0; i < ClassCount; i++)
        {
            mastery.Add(0);
        }
        int total = ClassCount * ClassDefinition.MaxNodes;
        for (int i = 0; i < total; i++)
        {
            nodeRanks.Add(0);
        }
        int slots = ClassCount * ClassDefinition.SlotCount;
        for (int i = 0; i < slots; i++)
        {
            equipped.Add(ClassDefinition.EmptySlot);
        }
    }

    public void ClientInit(GameHub sceneHub)
    {
        hub = sceneHub;
        nodeRanks.OnChange = OnListChanged;
        mastery.OnChange = OnListChanged;
        equipped.OnChange = OnListChanged;
        // 나중에 들어온 클라이언트는 처음 받은 노드 값으로 한 번 계산해 둔다.
        inventory.RecomputeStats();
    }

    private void OnListChanged(SyncList<byte>.Operation operation, int index, byte value)
    {
        revision++;
        inventory.RecomputeStats();
    }

    private void OnClassChanged(int oldValue, int newValue)
    {
        revision++;
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
    }

    private void OnPointsChanged(int oldValue, int newValue)
    {
        revision++;
    }

    private void OnBuffChanged(int oldValue, int newValue)
    {
        revision++;
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
    }

    // ------------------------------------------------------------ 능력치

    // 장비 능력치 위에 지금 계열의 노드, 각성 효과, 켜진 강화 스킬을 더한다(PlayerInventory가 부른다).
    public void ModifyStats(ref EquipStats stats)
    {
        ClassDefinition definition = Class;
        for (int i = 0; i < definition.NodeCount; i++)
        {
            int rank = GetRank(i);
            if (rank > 0)
            {
                JobNode node = definition.GetNode(i);
                if (node.effect != JobNodeEffect.UnlockSkill)
                {
                    ApplyEffect(ref stats, node.effect, node.value * rank);
                }
            }
        }
        for (int job = 0; job < ClassDefinition.JobCount; job++)
        {
            if (IsJobAwakened(job))
            {
                ApplyAwakening(ref stats, definition.GetJob(job));
            }
        }
        if (activeBuff >= 0)
        {
            SkillDefinition buff = classes[activeBuff / ClassDefinition.MaxSkills].GetSkill(activeBuff % ClassDefinition.MaxSkills);
            ApplyEffect(ref stats, buff.BuffEffect, buff.BuffValue);
        }
    }

    // 각성 고유 효과 가운데 능력치로 되는 것(처치·피해 계산·회복에 붙는 것은 그 자리에서 따로 본다)
    private void ApplyAwakening(ref EquipStats stats, JobBranch branch)
    {
        if (branch.awakening == JobAwakening.Guardian)
        {
            ApplyEffect(ref stats, JobNodeEffect.DefensePercent, branch.awakenValueA);
        }
        else if (branch.awakening == JobAwakening.Dragoon)
        {
            ApplyEffect(ref stats, JobNodeEffect.MoveSpeedPercent, branch.awakenValueA);
        }
        else if (branch.awakening == JobAwakening.Hunter)
        {
            ApplyEffect(ref stats, JobNodeEffect.MoveSpeedPercent, branch.awakenValueA);
            ApplyEffect(ref stats, JobNodeEffect.CritChancePercent, branch.awakenValueB);
        }
        else if (branch.awakening == JobAwakening.Marksman)
        {
            stats.critDamageBonus += branch.awakenValueA * 0.01f;
        }
        else if (branch.awakening == JobAwakening.Gunner)
        {
            ApplyEffect(ref stats, JobNodeEffect.AttackSpeedPercent, branch.awakenValueA);
            ApplyEffect(ref stats, JobNodeEffect.ManaOnHit, branch.awakenValueB);
        }
    }

    private void ApplyEffect(ref EquipStats stats, JobNodeEffect effect, float value)
    {
        if (effect == JobNodeEffect.DamagePercent)
        {
            stats.damageMultiplier *= 1f + value * 0.01f;
        }
        else if (effect == JobNodeEffect.AttackSpeedPercent)
        {
            stats.attackSpeedMultiplier *= 1f + value * 0.01f;
        }
        else if (effect == JobNodeEffect.MoveSpeedPercent)
        {
            stats.moveSpeedMultiplier *= 1f + value * 0.01f;
        }
        else if (effect == JobNodeEffect.CritChancePercent)
        {
            stats.critChance += value * 0.01f;
        }
        else if (effect == JobNodeEffect.MaxHp)
        {
            stats.maxHpBonus += Mathf.RoundToInt(value);
        }
        else if (effect == JobNodeEffect.DefensePercent)
        {
            stats.damageTakenMultiplier = Mathf.Max(0.2f, stats.damageTakenMultiplier * (1f - value * 0.01f));
        }
        else if (effect == JobNodeEffect.MaxMana)
        {
            stats.maxManaBonus += Mathf.RoundToInt(value);
        }
        else if (effect == JobNodeEffect.ManaOnHit)
        {
            stats.manaOnHitBonus += Mathf.RoundToInt(value);
        }
        else if (effect == JobNodeEffect.CooldownPercent)
        {
            stats.cooldownMultiplier = Mathf.Max(0.4f, stats.cooldownMultiplier * (1f - value * 0.01f));
        }
        else if (effect == JobNodeEffect.SkillDamagePercent)
        {
            stats.skillDamageMultiplier *= 1f + value * 0.01f;
        }
    }

    // ------------------------------------------------------------ 본인 조작

    public void OwnerTick(GameInput input, bool blocked)
    {
        if (!blocked && input.SkillTreePressed)
        {
            hub.SkillUi.ToggleTree();
        }
        if (!blocked && hub.SkillUi.IsTreeOpen)
        {
            int slotKey = input.SkillKeyPressed;
            if (slotKey >= 0)
            {
                hub.SkillUi.OnSlotKey(slotKey);
            }
        }
    }

    // 로비에 들어오면 기기에 저장한 시작 계열(전직 교관에서 고른 것)을 서버에 알린다.
    public void OwnerStart()
    {
        if (!hub.IsCombatScene)
        {
            int saved = hub.Meta.StartClass;
            if (saved >= 0 && saved < classes.Length && saved != lobbyClass)
            {
                CmdSetStartClass(saved);
            }
        }
    }

    // 로비 전직 교관에서 시작 계열을 고른다(기기에 저장해 다음 판에도 쓴다).
    public void RequestStartClass(int classNumber)
    {
        if (classNumber >= 0 && classNumber < classes.Length)
        {
            hub.Meta.SetStartClass(classNumber);
            hub.Meta.Save();
            CmdSetStartClass(classNumber);
        }
    }

    // 던전 스킬 트리 초기화 비용(층마다 비싸진다)
    public int ResetTreeCost
    {
        get
        {
            int floorNumber = 1;
            if (hub.FloorDirector != null)
            {
                floorNumber = Mathf.Max(1, hub.FloorDirector.FloorNumber);
            }
            return resetGoldPerFloor * floorNumber;
        }
    }

    // 찍어 둔 포인트가 있는지(초기화 버튼)
    public bool HasSpentPoints
    {
        get
        {
            bool spent = false;
            for (int i = 0; i < nodeRanks.Count && !spent; i++)
            {
                spent = nodeRanks[i] > 0;
            }
            return spent;
        }
    }

    public void RequestResetTree()
    {
        CmdResetTree();
    }

    // 골드를 내고 찍은 노드를 모두 되돌린다. 포인트는 모두 돌려받고 장착한 스킬은 빠진다. 전투 중에는 안 된다.
    [Command]
    private void CmdResetTree()
    {
        FloorDirector floor = hub.FloorDirector;
        int cost = ResetTreeCost;
        if (!hub.IsCombatScene || floor == null)
        {
            TargetResetResult(3);
        }
        else if (floor.RoomLocked)
        {
            TargetResetResult(1);
        }
        else if (!HasSpentPoints)
        {
            TargetResetResult(3);
        }
        else if (inventory.Gold < cost)
        {
            TargetResetResult(2);
        }
        else
        {
            inventory.ServerAddGold(-cost);
            int refund = 0;
            for (int i = 0; i < nodeRanks.Count; i++)
            {
                if (nodeRanks[i] > 0)
                {
                    refund += nodeRanks[i];
                    nodeRanks[i] = 0;
                }
            }
            for (int i = 0; i < equipped.Count; i++)
            {
                if (equipped[i] != ClassDefinition.EmptySlot)
                {
                    equipped[i] = ClassDefinition.EmptySlot;
                }
            }
            skillPoints += refund;
            activeBuff = -1;
            inventory.RecomputeStats();
            TargetResetResult(0);
        }
    }

    [TargetRpc]
    private void TargetResetResult(byte code)
    {
        if (code == 0)
        {
            hub.Hud.ShowNotice("스킬 트리를 초기화했습니다");
        }
        else if (code == 1)
        {
            hub.Hud.ShowNotice("전투 중에는 초기화할 수 없습니다");
        }
        else if (code == 2)
        {
            hub.Hud.ShowNotice("골드가 부족합니다");
        }
        else
        {
            hub.Hud.ShowNotice("되돌릴 포인트가 없습니다");
        }
    }

    // 그 칸의 스킬이 차징 스킬이면 그 스킬을, 아니면 null.
    public SkillDefinition GetChargeSkill(int slot)
    {
        SkillDefinition found = null;
        int skillIndex = GetSlotSkill(slot);
        if (skillIndex >= 0 && skillIndex < Class.SkillCount)
        {
            SkillDefinition skill = Class.GetSkill(skillIndex);
            if (skill.IsCharge)
            {
                found = skill;
            }
        }
        return found;
    }

    // 스킬을 시작해도 되는지(무기·마나): 차징을 시작하거나 커서 자리 조준(범위 표시)을 시작하기 전에 묻는다. 안 되면 알림을 띄운다.
    public bool CanBeginSkill(SkillDefinition skill)
    {
        bool ok = false;
        if (!HoldingClassWeapon)
        {
            hub.Hud.ShowNotice("계열의 무기를 들어야 스킬을 쓸 수 있습니다");
        }
        else if (controller.Mana < skill.ManaCost)
        {
            hub.Hud.ShowNotice("마나가 부족합니다");
        }
        else
        {
            ok = true;
        }
        return ok;
    }

    // 모은 정도를 1바이트로(0~255) 보낸다.
    private byte ChargeToByte(float charge01)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(charge01) * 255f);
    }

    private void SetCastCharge(SkillDefinition skill, byte charge)
    {
        float charge01 = charge / 255f;
        castSize = skill.GetChargeSize(charge01);
        castDamage = skill.GetChargeDamage(charge01);
    }

    // 장착 칸의 스킬을 쓸 수 있으면 서버에 알리고 그 스킬을 돌려준다. 못 쓰면 null.
    // 그 칸의 스킬이 커서 자리에 놓는 스킬이면 그 스킬을, 아니면 null(조준 표시용).
    public SkillDefinition GetGroundSkill(int slot)
    {
        SkillDefinition found = null;
        int skillIndex = GetSlotSkill(slot);
        if (skillIndex >= 0 && skillIndex < Class.SkillCount)
        {
            SkillDefinition skill = Class.GetSkill(skillIndex);
            if (skill.GroundTarget)
            {
                found = skill;
            }
        }
        return found;
    }

    // targetOffset: 몸 가운데에서 커서까지(커서 자리에 놓는 스킬만 쓴다. 사거리를 넘으면 사거리 끝으로 줄인다).
    // charge01: 차징 스킬을 모은 정도(차징 스킬이 아니면 무시된다).
    public SkillDefinition TryUse(int slot, Vector2 aim, Vector2 targetOffset, float charge01)
    {
        SkillDefinition used = null;
        int skillIndex = GetSlotSkill(slot);
        if (skillIndex >= 0 && !HoldingClassWeapon)
        {
            hub.Hud.ShowNotice("계열의 무기를 들어야 스킬을 쓸 수 있습니다");
        }
        else if (skillIndex >= 0 && Time.time >= localReadyTime[skillIndex])
        {
            SkillDefinition skill = Class.GetSkill(skillIndex);
            if (controller.Mana >= skill.ManaCost)
            {
                float cooldown = GetSkillCooldown(skill, skillIndex);
                localCooldown[skillIndex] = cooldown;
                localReadyTime[skillIndex] = Time.time + cooldown;
                used = skill;
                byte charge = ChargeToByte(charge01);
                SetCastCharge(skill, charge);
                PlayCastFx(skill, controller.Center, aim, targetOffset);
                CmdUse(skillIndex, aim, targetOffset, charge);
            }
            else
            {
                hub.Hud.ShowNotice("마나가 부족합니다");
            }
        }
        return used;
    }

    // 그 칸의 스킬이 재사용 대기 중인지(본인 화면 기준).
    public bool IsSlotCoolingDown(int slot)
    {
        int skillIndex = GetSlotSkill(slot);
        return skillIndex >= 0 && Time.time < localReadyTime[skillIndex];
    }

    public void RequestAllocate(int nodeIndex)
    {
        CmdAllocate(nodeIndex);
    }

    // 스킬을 장착 칸에 넣는다(다른 칸에 있으면 서로 바꾼다).
    public void RequestEquip(int slot, int skillIndex)
    {
        CmdEquip(slot, skillIndex);
    }

    // 스킬 창에서 장착 칸을 끌어 밖에 놓으면 그 칸을 비운다.
    public void RequestUnequipSlot(int slot)
    {
        CmdUnequipSlot(slot);
    }

    // ------------------------------------------------------------ 서버

    // 방의 몬스터가 쓰러질 때마다(MonsterDirector가 방에 있는 모두에게 부른다): 숙련도와 각성 효과
    public void ServerOnKill()
    {
        ServerAddMastery();
        JobBranch branch;
        if (TryGetAwakening(JobAwakening.Guardian, out branch) && !controller.IsDead)
        {
            controller.ServerHeal(Mathf.Max(1, Mathf.RoundToInt(controller.MaxHp * branch.awakenValueB * 0.01f)));
        }
        if (TryGetAwakening(JobAwakening.Hexer, out branch) && !controller.IsDead)
        {
            controller.ServerRestoreMana(Mathf.RoundToInt(branch.awakenValueA));
        }
    }

    // 처치할 때마다 지금 든 무기의 계열 숙련도가 오른다(계열을 바꾸는 조건까지만).
    public void ServerAddMastery()
    {
        int heldClass = ClassOf(inventory.WeaponKindIndex);
        if (heldClass < mastery.Count && mastery[heldClass] < masteryRequired)
        {
            mastery[heldClass] = (byte)(mastery[heldClass] + 1);
        }
    }

    public void ServerAddXp(int amount)
    {
        if (level < maxLevel)
        {
            xp += amount;
            while (level < maxLevel && xp >= XpToNext)
            {
                xp -= XpToNext;
                level++;
                skillPoints++;
            }
            if (level >= maxLevel)
            {
                xp = 0;
            }
        }
    }

    // 다른 플레이어의 강화 스킬이 이 플레이어에게 걸린다.
    public void ServerApplyBuff(int buffCode, float duration)
    {
        activeBuff = buffCode;
        buffEndTime = Time.time + duration;
        buffEndNetworkTime = NetworkTime.time + duration;
        buffDuration = duration;
        inventory.RecomputeStats();
    }

    [Command]
    private void CmdSetStartClass(int classNumber)
    {
        if (!hub.IsCombatScene && classNumber >= 0 && classNumber < classes.Length)
        {
            ServerSetStartClass(classNumber);
        }
    }

    // 로비에서 시작 계열을 고른다(그 계열의 시작 무기로 시작 장비를 바꾼다).
    public void ServerSetStartClass(int classNumber)
    {
        if (manager == null)
        {
            manager = (GameNetworkManager)NetworkManager.singleton;
        }
        lobbyClass = classNumber;
        manager.SetSlotStartClass(controller.Slot, classNumber);
        inventory.ServerSetStartWeapon((int)classes[classNumber].StartWeapon);
    }

    [Command]
    private void CmdAllocate(int nodeIndex)
    {
        if (hub.IsCombatScene && CanAllocate(nodeIndex))
        {
            int index = ClassIndex * ClassDefinition.MaxNodes + nodeIndex;
            skillPoints--;
            nodeRanks[index] = (byte)(nodeRanks[index] + 1);
            JobNode node = Class.GetNode(nodeIndex);
            if (node.effect == JobNodeEffect.UnlockSkill)
            {
                ServerAutoEquip(Mathf.RoundToInt(node.value));
            }
            inventory.RecomputeStats();
        }
    }

    // 새로 해금한 스킬을 비어 있는 첫 칸에 넣는다.
    private void ServerAutoEquip(int skillIndex)
    {
        int empty = -1;
        for (int i = 0; i < ClassDefinition.SlotCount && empty < 0; i++)
        {
            if (GetSlotSkill(i) < 0)
            {
                empty = i;
            }
        }
        if (empty >= 0)
        {
            equipped[ClassIndex * ClassDefinition.SlotCount + empty] = (byte)skillIndex;
        }
    }

    [Command]
    private void CmdEquip(int slot, int skillIndex)
    {
        if (slot >= 0 && slot < ClassDefinition.SlotCount && IsSkillUnlocked(skillIndex))
        {
            int baseIndex = ClassIndex * ClassDefinition.SlotCount;
            int previousSlot = FindSlotOf(skillIndex);
            byte current = equipped[baseIndex + slot];
            if (previousSlot >= 0 && previousSlot != slot)
            {
                equipped[baseIndex + previousSlot] = current;
            }
            equipped[baseIndex + slot] = (byte)skillIndex;
        }
    }

    [Command]
    private void CmdUnequipSlot(int slot)
    {
        if (slot >= 0 && slot < ClassDefinition.SlotCount)
        {
            equipped[ClassIndex * ClassDefinition.SlotCount + slot] = ClassDefinition.EmptySlot;
        }
    }

    [Command]
    private void CmdUse(int skillIndex, Vector2 aim, Vector2 targetOffset, byte charge)
    {
        bool valid = skillIndex >= 0 && skillIndex < Class.SkillCount && IsSkillUnlocked(skillIndex) && HoldingClassWeapon;
        if (valid && FindSlotOf(skillIndex) >= 0)
        {
            SkillDefinition skill = Class.GetSkill(skillIndex);
            // 통신 지연을 감안해 재사용 시간은 조금 너그럽게 본다.
            if (Time.time >= serverReadyTime[skillIndex] - 0.25f && controller.ServerBeginSkill(skill.ManaCost, skill.CastTime))
            {
                serverReadyTime[skillIndex] = Time.time + GetSkillCooldown(skill, skillIndex);
                Vector2 direction = aim.normalized;
                SetCastCharge(skill, charge);
                ServerExecute(skill, skillIndex, direction, targetOffset);
                RpcSkill(ClassIndex, skillIndex, controller.Center, direction, targetOffset, charge);
            }
            else
            {
                // 서버가 받아 주지 않았으면(동작 중·통신 지연) 본인 화면의 재사용 대기를 되돌린다.
                TargetSkillRejected(skillIndex);
            }
        }
    }

    [TargetRpc]
    private void TargetSkillRejected(int skillIndex)
    {
        localReadyTime[skillIndex] = 0f;
        localCooldown[skillIndex] = 0f;
    }

    private void ServerExecute(SkillDefinition skill, int skillIndex, Vector2 aim, Vector2 targetOffset)
    {
        float baseDamage = inventory.Weapon.Damage * skill.DamageFactor * inventory.Stats.skillDamageMultiplier * GetSkillJobFactor(skillIndex) * castDamage;
        Vector2 center = controller.Center;
        float areaRadiusNow = skill.Radius * castSize;
        if (skill.Kind == SkillKind.AreaAround)
        {
            // 마법형 범위(서리 폭발, 심판)는 커서 자리에서 터진다.
            Vector2 at = center;
            if (skill.GroundTarget)
            {
                at = GroundPoint(skill, center, aim, targetOffset);
            }
            if (skill.ImpactDelay > 0f)
            {
                // 뛰어올랐다가 무기를 내려찍는 순간에 맞는다(Update에서 판정).
                areaPending = true;
                areaTime = Time.time + skill.ImpactDelay;
                areaPosition = at;
                areaAim = aim;
                areaRadius = areaRadiusNow;
                areaArc = 0f;
                if (!skill.GroundTarget)
                {
                    areaArc = skill.ArcDegrees;
                }
                areaHit = RollSkillHit(skill, baseDamage);
            }
            else if (skill.ArcDegrees > 0f && !skill.GroundTarget)
            {
                ServerHitArc(at, aim, areaRadiusNow, skill.ArcDegrees, RollSkillHit(skill, baseDamage));
            }
            else
            {
                ServerHitCircle(at, areaRadiusNow, RollSkillHit(skill, baseDamage));
            }
            // 빛으로 치는 범위(성광)는 같은 범위의 아군도 회복한다.
            if (skill.HealPercent > 0f)
            {
                ServerHealAround(at, areaRadiusNow, skill.HealPercent * HealFactor);
            }
        }
        else if (skill.Kind == SkillKind.Dash)
        {
            if (skill.Invulnerable)
            {
                controller.ServerSetInvulnerable(skill.CastTime + 0.1f);
            }
            // 착지만 치는 도약(길 반폭 0)은 지나간 길을 치지 않는다.
            if (skill.DamageFactor > 0f && skill.Radius > 0f)
            {
                ServerHitLine(center, aim, skill.Range * castSize, skill.Radius, RollSkillHit(skill, baseDamage));
            }
            if (skill.ExplosionRadius > 0f)
            {
                slamPending = true;
                slamTime = Time.time + skill.CastTime;
                slamRadius = skill.ExplosionRadius * castSize;
                slamHit = RollSkillHit(skill, baseDamage);
                slamClass = ClassIndex;
                slamSkill = skillIndex;
            }
        }
        else if (skill.Kind == SkillKind.Volley)
        {
            LaunchVolley(skill, center, aim, true, baseDamage);
        }
        else if (skill.Kind == SkillKind.Heal)
        {
            ServerHealAround(center, skill.Radius, skill.HealPercent * HealFactor);
        }
        else if (skill.Kind == SkillKind.Revive)
        {
            ServerReviveAround(center, skill.Radius, skill.HealPercent * HealFactor);
        }
        else if (skill.Kind == SkillKind.Zone)
        {
            ServerAddZone(skill, GroundPoint(skill, center, aim, targetOffset), RollSkillHit(skill, baseDamage));
        }
        else
        {
            int code = ClassIndex * ClassDefinition.MaxSkills + skillIndex;
            if (skill.Radius > 0f)
            {
                int count = hub.PlayerCount;
                for (int i = 0; i < count; i++)
                {
                    PlayerController player = hub.GetPlayer(i);
                    Vector2 offset = player.Center - center;
                    if (!player.IsDead && offset.sqrMagnitude <= skill.Radius * skill.Radius)
                    {
                        player.Skills.ServerApplyBuff(code, skill.BuffDuration);
                    }
                }
            }
            else
            {
                ServerApplyBuff(code, skill.BuffDuration);
            }
        }
    }

    // 장판은 조준 방향으로 사거리만큼 떨어진 곳에 깔린다(사거리 0이면 발밑).
    private Vector2 ZoneCenter(SkillDefinition skill, Vector2 center, Vector2 aim)
    {
        return center + aim * skill.Range;
    }

    // 커서 자리에 놓는 스킬은 커서 자리(사거리 밖이면 사거리 끝), 아니면 예전처럼 조준 방향 사거리 끝.
    private Vector2 GroundPoint(SkillDefinition skill, Vector2 center, Vector2 aim, Vector2 targetOffset)
    {
        Vector2 point = ZoneCenter(skill, center, aim);
        if (skill.GroundTarget)
        {
            point = center + Vector2.ClampMagnitude(targetOffset, skill.Range);
        }
        return point;
    }

    // 스킬 한 번의 타격 정보(치명타와 스킬에 붙은 상태이상 포함)
    private HitInfo RollSkillHit(SkillDefinition skill, float baseDamage)
    {
        HitInfo hit = inventory.RollHit(baseDamage);
        hit.status = skill.StatusKind;
        hit.statusDuration = skill.StatusDuration;
        hit.statusPower = skill.StatusPower;
        // 원소술사 각성: 스킬이 거는 상태 이상이 더 오래 간다.
        JobBranch elemental;
        if (TryGetAwakening(JobAwakening.Elemental, out elemental))
        {
            hit.statusDuration *= 1f + elemental.awakenValueA * 0.01f;
        }
        return hit;
    }

    private void ServerHitCircle(Vector2 center, float radius, in HitInfo hit)
    {
        int count = hub.OverlapMonsters(center, radius);
        Collider2D[] buffer = hub.HitBuffer;
        for (int i = 0; i < count; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster))
            {
                Vector2 push = (Vector2)buffer[i].bounds.center - center;
                monster.ServerTakeHit(hit, push.normalized, netId);
            }
        }
    }

    // center에서 aim 쪽으로 펼친 부채꼴(반지름 radius, 전체 각도 arcDegrees) 안의 몬스터를 친다.
    private void ServerHitArc(Vector2 center, Vector2 aim, float radius, float arcDegrees, in HitInfo hit)
    {
        float minDot = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
        int count = hub.OverlapMonsters(center, radius);
        Collider2D[] buffer = hub.HitBuffer;
        for (int i = 0; i < count; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster))
            {
                Vector2 push = (Vector2)buffer[i].bounds.center - center;
                // 몸에 붙은 몬스터는 각도와 상관없이 맞는다.
                bool inside = push.sqrMagnitude < 0.36f || Vector2.Dot(push.normalized, aim) >= minDot;
                if (inside)
                {
                    monster.ServerTakeHit(hit, push.normalized, netId);
                }
            }
        }
    }

    // origin에서 direction으로 length만큼 뻗은 길(반폭 halfWidth) 안의 몬스터를 친다.
    private void ServerHitLine(Vector2 origin, Vector2 direction, float length, float halfWidth, in HitInfo hit)
    {
        Vector2 middle = origin + direction * (length * 0.5f);
        int count = hub.OverlapMonsters(middle, length * 0.5f + halfWidth);
        Collider2D[] buffer = hub.HitBuffer;
        for (int i = 0; i < count; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster))
            {
                Vector2 toTarget = (Vector2)buffer[i].bounds.center - origin;
                float along = Vector2.Dot(toTarget, direction);
                float side = Mathf.Abs(toTarget.x * direction.y - toTarget.y * direction.x);
                if (along >= -0.3f && along <= length + 0.3f && side <= halfWidth + buffer[i].bounds.extents.x)
                {
                    monster.ServerTakeHit(hit, direction, netId);
                }
            }
        }
    }

    // 반경 안의 살아 있는 아군(자신 포함)을 최대 체력의 percent%만큼 회복한다.
    private void ServerHealAround(Vector2 center, float radius, float percent)
    {
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            Vector2 offset = player.Center - center;
            if (!player.IsDead && offset.sqrMagnitude <= radius * radius)
            {
                player.ServerHeal(Mathf.Max(1, Mathf.RoundToInt(player.MaxHp * percent * 0.01f)));
            }
        }
    }

    private void ServerReviveAround(Vector2 center, float radius, float percent)
    {
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            Vector2 offset = player.Center - center;
            if (player.IsDead && offset.sqrMagnitude <= radius * radius)
            {
                player.ServerRevive(player.transform.position, percent * 0.01f);
            }
        }
    }

    private void ServerAddZone(SkillDefinition skill, Vector2 position, in HitInfo hit)
    {
        // 빈 칸이 없으면 가장 먼저 끝나는 장판을 덮어쓴다.
        int pick = 0;
        float earliest = float.MaxValue;
        for (int i = 0; i < MaxZones; i++)
        {
            float end = zones[i].endTime;
            if (!zones[i].active)
            {
                end = float.MinValue;
            }
            if (end < earliest)
            {
                earliest = end;
                pick = i;
            }
        }
        ZoneState zone = new ZoneState();
        zone.active = true;
        zone.position = position;
        zone.radius = skill.Radius;
        zone.endTime = Time.time + skill.BuffDuration;
        zone.nextTick = Time.time;
        zone.hit = hit;
        zone.damages = skill.DamageFactor > 0f;
        zone.healPercent = skill.HealPercent * HealFactor;
        zone.pullSpeed = skill.PullSpeed;
        zones[pick] = zone;
    }

    // 방을 옮기면 깔아 둔 장판을 모두 없앤다(다음 방까지 따라와 피해를 주던 문제). FloorDirector가 부른다.
    public void ServerClearZones()
    {
        for (int i = 0; i < MaxZones; i++)
        {
            zones[i].active = false;
        }
    }

    private void ServerTickZones()
    {
        for (int i = 0; i < MaxZones; i++)
        {
            if (zones[i].active)
            {
                if (Time.time >= zones[i].endTime)
                {
                    zones[i].active = false;
                }
                else if (zones[i].pullSpeed > 0f)
                {
                    ServerPullMonsters(zones[i].position, zones[i].radius, zones[i].pullSpeed * Time.deltaTime);
                }
                if (zones[i].active && Time.time >= zones[i].nextTick)
                {
                    zones[i].nextTick += ZoneTick;
                    if (zones[i].damages)
                    {
                        ServerHitCircle(zones[i].position, zones[i].radius, zones[i].hit);
                    }
                    if (zones[i].healPercent > 0f)
                    {
                        ServerHealAround(zones[i].position, zones[i].radius, zones[i].healPercent);
                    }
                }
            }
        }
    }

    // 블랙홀: 반경의 1.3배 안의 몬스터를 가운데로 조금씩 끌어당긴다(보스는 버틴다).
    private void ServerPullMonsters(Vector2 center, float radius, float distance)
    {
        int count = hub.OverlapMonsters(center, radius * 1.3f);
        Collider2D[] buffer = hub.HitBuffer;
        for (int i = 0; i < count; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster))
            {
                monster.ServerPull(center, distance);
            }
        }
    }

    // 부채꼴로 여러 발. 서버는 판정용, 다른 클라이언트는 표시용으로 같은 궤적을 쏜다.
    private void LaunchVolley(SkillDefinition skill, Vector2 center, Vector2 aim, bool authoritative, float baseDamage)
    {
        if (hub.ProjectilePool != null)
        {
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            // 지팡이·완드 스킬은 무기 끝에서 부채꼴로 나간다.
            Vector2 tip;
            bool fromTip = controller.Visual.TryGetMagicTip(aim, out tip);
            for (int i = 0; i < skill.Count; i++)
            {
                float offset = 0f;
                if (skill.Count > 1)
                {
                    offset = Mathf.Lerp(-skill.SpreadDegrees * 0.5f, skill.SpreadDegrees * 0.5f, i / (float)(skill.Count - 1));
                }
                float radians = (baseAngle + offset) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

                ProjectileShot shot = new ProjectileShot();
                shot.origin = center + direction * muzzleDistance - new Vector2(0f, 0.35f);
                if (fromTip)
                {
                    shot.origin = tip - new Vector2(0f, Projectile.SpriteHeight);
                }
                shot.direction = direction;
                shot.speed = skill.ProjectileSpeed;
                shot.lifetime = skill.ProjectileLifetime;
                shot.radius = skill.Radius * castSize;
                shot.damage = 1;
                shot.explosionRadius = skill.ExplosionRadius * castSize;
                shot.pierce = skill.Pierce;
                shot.visualScale = castSize;
                shot.look = skill.ProjectileLook;
                shot.color = skill.FxColor;
                shot.vfx = skill.Vfx;
                shot.hitsMonsters = true;
                shot.authoritative = authoritative;
                if (authoritative)
                {
                    shot.hit = RollSkillHit(skill, baseDamage);
                    shot.damage = shot.hit.damage;
                }
                hub.ProjectilePool.Launch(shot);
            }
        }
    }

    [ClientRpc]
    private void RpcSkill(int classNumber, int skillIndex, Vector2 center, Vector2 aim, Vector2 targetOffset, byte charge)
    {
        SkillDefinition skill = classes[classNumber].GetSkill(skillIndex);
        if (!isServer)
        {
            SetCastCharge(skill, charge);
        }
        else if (!isLocalPlayer)
        {
            // 호스트 화면의 다른 사람 스킬: 서버 판정에 이미 정해 둔 배율을 그대로 쓴다.
            SetCastCharge(skill, charge);
        }
        if (!isLocalPlayer)
        {
            controller.PlaySkillVisual(skill, aim);
            PlayCastFx(skill, center, aim, targetOffset);
        }
        if (!isServer && skill.Kind == SkillKind.Volley)
        {
            LaunchVolley(skill, center, aim, false, 0f);
        }
    }

    [ClientRpc]
    private void RpcSlam(Vector2 position, float radius, int classNumber, int skillIndex)
    {
        if (hub.SkillFx != null)
        {
            SkillDefinition skill = classes[classNumber].GetSkill(skillIndex);
            SkillVfx vfx = skill.Vfx;
            if (vfx.Has(vfx.impact))
            {
                hub.SkillFx.PlayFrames(vfx.impact, vfx.impactFps, vfx.impactGround, position, radius * vfx.impactScale, 0f, null, 0f, false);
            }
            else
            {
                hub.SkillFx.Play(skill.Fx, position, radius, skill.FxColor, 0f, null);
            }
        }
    }

    // 스킬 종류에 맞춰 이펙트와 소리를 낸다(시전한 본인은 바로, 다른 사람은 Rpc로).
    // 방향이 있는 이펙트는 조준 쪽으로 돌린다. 강화의 지속 기운은 StatusFxView가 activeBuff를 보고 따로 그린다.
    private void PlayCastFx(SkillDefinition skill, Vector2 center, Vector2 aim, Vector2 targetOffset)
    {
        hub.Sound.PlayAt(skill.Sound, center);
        SkillFxPlayer fxPlayer = hub.SkillFx;
        Vector2 ground = GroundPoint(skill, center, aim, targetOffset);
        // 뛰어올라 내려찍는 범위 공격은 맞는 순간에 띄운다. 커서 자리에 떨어지는 범위(메테오)는 떨어지는 그림부터 바로 띄운다.
        bool delayed = skill.ImpactDelay > 0f && skill.Kind == SkillKind.AreaAround && !skill.GroundTarget;
        if (delayed)
        {
            // 내려찍는 순간(impactDelay 뒤)에 이펙트를 띄운다(TickDelayedFx).
            delayedFxSkill = skill;
            delayedFxTime = Time.time + skill.ImpactDelay;
            delayedFxCenter = center;
            delayedFxAim = aim;
            delayedFxGround = ground;
            delayedFxSize = castSize;
        }
        else if (fxPlayer != null && skill.Vfx.HasAny)
        {
            PlaySkillVfx(fxPlayer, skill, center, aim, ground);
        }
        else if (fxPlayer != null && skill.Fx != SkillFx.None)
        {
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            if (skill.Kind == SkillKind.AreaAround || skill.Kind == SkillKind.Heal || skill.Kind == SkillKind.Revive)
            {
                if (fxPlayer.IsDirectional(skill.Fx))
                {
                    fxPlayer.Play(skill.Fx, center + aim * ((skill.Radius * castSize) * 0.3f), (skill.Radius * castSize), skill.FxColor, 0f, null, angle);
                }
                else if (skill.GroundTarget)
                {
                    fxPlayer.Play(skill.Fx, ground, (skill.Radius * castSize), skill.FxColor, 0f, null);
                }
                else
                {
                    fxPlayer.Play(skill.Fx, center, (skill.Radius * castSize), skill.FxColor, 0f, null);
                }
            }
            else if (skill.Kind == SkillKind.Buff)
            {
                float radius = Mathf.Max(0.9f, (skill.Radius * castSize));
                fxPlayer.Play(skill.Fx, center, radius, skill.FxColor, 0f, null);
            }
            else if (skill.Kind == SkillKind.Zone)
            {
                fxPlayer.Play(skill.Fx, ground, (skill.Radius * castSize), skill.FxColor, skill.BuffDuration, null);
            }
            else if (skill.Kind == SkillKind.Dash)
            {
                // 길: 창끝(꿰뚫기) 또는 바람 자국. 그 밖의 이펙트는 도착 자리에 터뜨린다(착지 찍기는 RpcSlam이 맡는다).
                SkillFx trail = SkillFx.Wind;
                if (skill.Fx == SkillFx.Pierce)
                {
                    trail = SkillFx.Pierce;
                }
                fxPlayer.PlayDirected(trail, center + aim * ((skill.Range * castSize) * 0.5f), (skill.Range * castSize) * 0.5f, aim, skill.FxColor);
                bool landing = skill.ExplosionRadius > 0f;
                if (!landing && skill.Fx != SkillFx.Wind && skill.Fx != SkillFx.Pierce)
                {
                    Vector2 end = center + aim * (skill.Range * castSize);
                    float size = Mathf.Max(1f, (skill.Radius * castSize) * 1.6f);
                    if (fxPlayer.IsDirectional(skill.Fx))
                    {
                        fxPlayer.Play(skill.Fx, end, size, skill.FxColor, 0f, null, angle);
                    }
                    else
                    {
                        fxPlayer.Play(skill.Fx, end, size, skill.FxColor, 0f, null);
                    }
                    if (skill.Fx == SkillFx.Arcane)
                    {
                        fxPlayer.Play(skill.Fx, center, 0.9f, skill.FxColor, 0f, null);
                    }
                }
            }
            else
            {
                fxPlayer.Play(skill.Fx, center + aim * 0.6f, 0.6f, skill.FxColor, 0f, null);
            }
        }
    }

    // 스킬마다 따로 그린 컬러 이펙트. 범위·회복·강화는 시전 자리, 장판은 지속 시간 동안 반복,
    // 돌진은 출발 자리 + 길 + 끝 자리(착지 찍기는 RpcSlam), 투사체는 무기 앞 섬광(맞은 자리는 Projectile이 띄운다).
    private void PlaySkillVfx(SkillFxPlayer fxPlayer, SkillDefinition skill, Vector2 center, Vector2 aim, Vector2 ground)
    {
        SkillVfx vfx = skill.Vfx;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        if (skill.Kind == SkillKind.AreaAround || skill.Kind == SkillKind.Heal || skill.Kind == SkillKind.Revive)
        {
            Vector2 at = center;
            if (vfx.castDirectional)
            {
                at = center + aim * ((skill.Radius * castSize) * 0.3f);
            }
            if (skill.GroundTarget)
            {
                at = ground;
            }
            fxPlayer.PlayFacing(vfx.cast, vfx.castFps, vfx.castGround, at, (skill.Radius * castSize) * vfx.castScale, 0f, null, angle, vfx.castDirectional, vfx.castMirror);
        }
        else if (skill.Kind == SkillKind.Buff)
        {
            float radius = Mathf.Max(0.9f, (skill.Radius * castSize)) * vfx.castScale;
            fxPlayer.PlayFrames(vfx.cast, vfx.castFps, vfx.castGround, center, radius, 0f, null, angle, vfx.castDirectional);
        }
        else if (skill.Kind == SkillKind.Zone)
        {
            fxPlayer.PlayZone(vfx, ground, (skill.Radius * castSize) * vfx.castScale, skill.BuffDuration);
        }
        else if (skill.Kind == SkillKind.Dash)
        {
            if (vfx.Has(vfx.cast))
            {
                fxPlayer.PlayFrames(vfx.cast, vfx.castFps, vfx.castGround, center, vfx.castSize, 0f, null, angle, vfx.castDirectional);
            }
            if (vfx.Has(vfx.trail))
            {
                fxPlayer.PlayFramesDirected(vfx.trail, vfx.trailFps, center + aim * ((skill.Range * castSize) * 0.5f), (skill.Range * castSize) * 0.5f, vfx.trailWidth, aim);
            }
            bool landing = skill.ExplosionRadius > 0f;
            if (!landing && vfx.Has(vfx.impact))
            {
                Vector2 end = center + aim * (skill.Range * castSize);
                fxPlayer.PlayFacing(vfx.impact, vfx.impactFps, vfx.impactGround, end, vfx.impactSize, 0f, null, angle, vfx.impactDirectional, vfx.impactMirror);
            }
        }
        else
        {
            Vector2 muzzle = center + aim * 0.7f;
            Vector2 tip;
            if (controller.Visual.TryGetMagicTip(aim, out tip))
            {
                muzzle = tip;
            }
            if (vfx.Has(vfx.muzzle))
            {
                fxPlayer.PlayFrames(vfx.muzzle, vfx.muzzleFps, false, muzzle, vfx.muzzleSize, 0f, null, angle, vfx.muzzleDirectional);
            }
            // 빛줄기 투사체(레일건): 투사체가 날아갈 거리만큼 길 그림을 총구에서 조준 쪽으로 깐다.
            if (skill.Kind == SkillKind.Volley && vfx.Has(vfx.trail))
            {
                float halfLength = skill.ProjectileSpeed * skill.ProjectileLifetime * 0.5f;
                fxPlayer.PlayFramesDirected(vfx.trail, vfx.trailFps, muzzle + aim * halfLength, halfLength, vfx.trailWidth * castSize, aim);
            }
        }
    }

    // 지금 걸린 강화 스킬(HUD 표시용). 없으면 null.
    public SkillDefinition ActiveBuffSkill
    {
        get
        {
            SkillDefinition buff = null;
            if (activeBuff >= 0)
            {
                buff = classes[activeBuff / ClassDefinition.MaxSkills].GetSkill(activeBuff % ClassDefinition.MaxSkills);
            }
            return buff;
        }
    }

    // 강화의 남은 시간(초)
    public float BuffRemaining => Mathf.Max(0f, (float)(buffEndNetworkTime - NetworkTime.time));
    // 강화가 처음 걸렸을 때의 지속 시간(HUD 회전 게이지 비율용)
    public float BuffDuration => buffDuration;

    // 지금 걸린 강화의 종류(StatusFxView 표시용). 0 공격, 1 방어, 2 속도, 3 치명, -1 없음
    public int ActiveBuffVisual
    {
        get
        {
            int kind = -1;
            if (activeBuff >= 0)
            {
                SkillDefinition buff = classes[activeBuff / ClassDefinition.MaxSkills].GetSkill(activeBuff % ClassDefinition.MaxSkills);
                JobNodeEffect effect = buff.BuffEffect;
                if (effect == JobNodeEffect.DamagePercent || effect == JobNodeEffect.SkillDamagePercent)
                {
                    kind = 0;
                }
                else if (effect == JobNodeEffect.DefensePercent || effect == JobNodeEffect.MaxHp)
                {
                    kind = 1;
                }
                else if (effect == JobNodeEffect.AttackSpeedPercent || effect == JobNodeEffect.MoveSpeedPercent || effect == JobNodeEffect.CooldownPercent)
                {
                    kind = 2;
                }
                else
                {
                    kind = 3;
                }
            }
            return kind;
        }
    }

    // 내려찍는 순간: 이펙트, 묵직한 소리, 본인 화면 흔들림
    private void TickDelayedFx()
    {
        if (delayedFxSkill != null && Time.time >= delayedFxTime)
        {
            SkillDefinition skill = delayedFxSkill;
            delayedFxSkill = null;
            castSize = delayedFxSize;
            SkillFxPlayer fxPlayer = hub.SkillFx;
            if (!controller.IsDead && fxPlayer != null)
            {
                if (skill.Vfx.HasAny)
                {
                    PlaySkillVfx(fxPlayer, skill, delayedFxCenter, delayedFxAim, delayedFxGround);
                }
                else if (skill.Fx != SkillFx.None)
                {
                    fxPlayer.Play(skill.Fx, delayedFxCenter, skill.Radius, skill.FxColor, 0f, null);
                }
                hub.Sound.PlayAt(SoundId.Explosion, delayedFxCenter);
                if (isLocalPlayer && hub.CameraFollow != null)
                {
                    hub.CameraFollow.Shake(impactShakeAmplitude, impactShakeTime);
                }
            }
        }
    }

    private void Update()
    {
        if (isClient)
        {
            TickDelayedFx();
        }
        if (isServer)
        {
            if (areaPending && Time.time >= areaTime)
            {
                areaPending = false;
                if (!controller.IsDead)
                {
                    if (areaArc > 0f)
                    {
                        ServerHitArc(areaPosition, areaAim, areaRadius, areaArc, areaHit);
                    }
                    else
                    {
                        ServerHitCircle(areaPosition, areaRadius, areaHit);
                    }
                }
            }
            if (activeBuff >= 0 && Time.time >= buffEndTime)
            {
                activeBuff = -1;
                inventory.RecomputeStats();
            }
            if (slamPending && Time.time >= slamTime)
            {
                slamPending = false;
                Vector2 position = controller.Center;
                ServerHitCircle(position, slamRadius, slamHit);
                RpcSlam(position, slamRadius, slamClass, slamSkill);
            }
            ServerTickZones();
        }
    }
}
