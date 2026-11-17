using Mirror;
using UnityEngine;

// 직업, 레벨, 스킬 트리, 스킬 장착(Q, E, R).
// - 직업은 10개다. 0~8번은 WeaponKind 순서의 기본 직업이고, 9번 사제는 주술사(8번)와 같은 완드를 쓴다.
//   로비에서 숫자 키(1~9, 0)로 시작 직업을 고르면 그 무기를 받는다. 다른 직업은 그 무기 계열로 처치해 숙련도를 채운 뒤
//   시작 방의 제단에서 전직한다(완드는 F 주술사, G 사제). 스킬은 직업의 무기 계열을 들고 있을 때만 쓸 수 있다.
// - 몬스터를 잡으면 경험치를 얻고, 레벨이 오를 때마다 포인트를 1 받는다. 한 판이 끝나면 초기화된다.
// - 스킬 트리(갈래 3개 x 4단계)와 장착 상태는 직업별로 따로 기억한다. 스킬을 해금하면 빈 칸에 자동으로 장착된다.
// - 스킬 사용 판정(마나, 재사용 시간, 피해, 회복)은 서버가 하고, 시전 동작과 돌진 이동은 본인 클라이언트가 한다.
public class PlayerSkills : NetworkBehaviour
{
    public const int JobCount = 10;
    public const int PriestJob = 9;
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
    }

    [Header("참조")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerInventory inventory;
    [Tooltip("직업 순서(WeaponKind 순서 9개 + 사제)")]
    [SerializeField] private JobDefinition[] jobs;

    [Header("성장")]
    [SerializeField] private int maxLevel = 15;
    [SerializeField] private int startPoints = 1;
    [SerializeField] private int xpBase = 16;
    [SerializeField] private int xpPerLevel = 6;
    [SerializeField] private float muzzleDistance = 0.4f;
    [Tooltip("전직에 필요한 숙련도(그 무기 계열을 들고 처치한 수)")]
    [SerializeField] private int masteryRequired = 8;

    // 전투 씬에서의 직업
    [SyncVar(hook = nameof(OnJobChanged))] private int jobIndex;
    // 로비에서 고른 시작 직업
    [SyncVar(hook = nameof(OnJobChanged))] private int lobbyJob;
    // 무기 계열별 숙련도(그 계열을 들고 처치한 수, 전직 조건까지만 센다)
    private readonly SyncList<byte> mastery = new SyncList<byte>();
    [SyncVar] private int level = 1;
    [SyncVar] private int xp;
    [SyncVar(hook = nameof(OnPointsChanged))] private int skillPoints;
    // 직업 수 x 노드 수. 각 칸은 그 노드에 찍은 단계.
    private readonly SyncList<byte> nodeRanks = new SyncList<byte>();
    // 직업 수 x 장착 칸 수. 각 칸은 장착한 스킬 번호(없으면 EmptySlot).
    private readonly SyncList<byte> equipped = new SyncList<byte>();
    // 켜져 있는 강화 스킬(직업 번호 x 스킬 수 + 스킬 번호). 없으면 -1.
    [SyncVar(hook = nameof(OnBuffChanged))] private int activeBuff = -1;

    private GameHub hub;
    private GameNetworkManager manager;
    private int revision;

    // 본인 클라이언트: 스킬별 다시 쓸 수 있는 시각
    private readonly float[] localReadyTime = new float[JobDefinition.SkillCount];
    private readonly float[] localCooldown = new float[JobDefinition.SkillCount];
    // 서버
    private readonly float[] serverReadyTime = new float[JobDefinition.SkillCount];
    private readonly ZoneState[] zones = new ZoneState[MaxZones];
    private float buffEndTime;
    // 돌진 끝의 내려찍기(시전이 끝나는 시각에 그 자리에서)
    private bool slamPending;
    private float slamTime;
    private float slamRadius;
    private HitInfo slamHit;
    private SkillFx slamFx;
    private Color slamColor;

    public int Level => level;
    public int Xp => xp;
    public int SkillPoints => skillPoints;
    public int Revision => revision;
    public int MasteryRequired => masteryRequired;

    // 그 무기 계열의 기본 직업(완드는 주술사)
    public static int DefaultJobOf(int weaponKind)
    {
        return Mathf.Clamp(weaponKind, 0, WeaponDefinition.KindCount - 1);
    }

    public int JobIndex
    {
        get
        {
            int held = inventory.WeaponKindIndex;
            int index = DefaultJobOf(held);
            if (hub != null && hub.IsCombatScene)
            {
                index = Mathf.Clamp(jobIndex, 0, jobs.Length - 1);
            }
            else if (lobbyJob >= 0 && lobbyJob < jobs.Length && (int)jobs[lobbyJob].WeaponKind == held)
            {
                index = lobbyJob;
            }
            return index;
        }
    }

    // 직업의 무기 계열을 들고 있어야 스킬을 쓸 수 있다.
    public bool HoldingJobWeapon
    {
        get
        {
            return (int)Job.WeaponKind == inventory.WeaponKindIndex;
        }
    }

    public int GetMastery(int kindIndex)
    {
        int value = 0;
        if (kindIndex >= 0 && kindIndex < mastery.Count)
        {
            value = mastery[kindIndex];
        }
        return value;
    }

    public string GetJobName(int job)
    {
        return jobs[Mathf.Clamp(job, 0, jobs.Length - 1)].DisplayName;
    }

    public JobDefinition GetJob(int job)
    {
        return jobs[Mathf.Clamp(job, 0, jobs.Length - 1)];
    }

    public JobDefinition Job => jobs[JobIndex];

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
        int rank = 0;
        int index = JobIndex * JobDefinition.NodeCount + nodeIndex;
        if (index < nodeRanks.Count)
        {
            rank = nodeRanks[index];
        }
        return rank;
    }

    public bool IsSkillUnlocked(int skillIndex)
    {
        bool unlocked = false;
        if (skillIndex >= 0 && skillIndex < JobDefinition.SkillCount)
        {
            int node = Job.FindSkillNode(skillIndex);
            if (node >= 0)
            {
                unlocked = GetRank(node) > 0;
            }
        }
        return unlocked;
    }

    // 지금 직업의 장착 칸에 든 스킬 번호. 비었으면 -1.
    public int GetSlotSkill(int slot)
    {
        int skill = -1;
        int index = JobIndex * JobDefinition.SlotCount + slot;
        if (slot >= 0 && slot < JobDefinition.SlotCount && index < equipped.Count)
        {
            if (equipped[index] != JobDefinition.EmptySlot)
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
        for (int i = 0; i < JobDefinition.SlotCount && found < 0; i++)
        {
            if (GetSlotSkill(i) == skillIndex)
            {
                found = i;
            }
        }
        return found;
    }

    // 그 노드를 지금 찍을 수 있는지(포인트, 최대 단계, 같은 갈래의 앞 노드)
    public bool CanAllocate(int nodeIndex)
    {
        bool can = false;
        if (skillPoints > 0 && nodeIndex >= 0 && nodeIndex < JobDefinition.NodeCount)
        {
            if (GetRank(nodeIndex) < Job.GetNode(nodeIndex).maxRank)
            {
                bool first = nodeIndex % JobDefinition.BranchSize == 0;
                can = first || GetRank(nodeIndex - 1) > 0;
            }
        }
        return can;
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
        lobbyJob = manager.GetSlotStartJob(controller.Slot);
        int held = inventory.WeaponKindIndex;
        jobIndex = DefaultJobOf(held);
        if (lobbyJob >= 0 && lobbyJob < jobs.Length && (int)jobs[lobbyJob].WeaponKind == held)
        {
            jobIndex = lobbyJob;
        }
        for (int i = 0; i < WeaponDefinition.KindCount; i++)
        {
            mastery.Add(0);
        }
        int total = jobs.Length * JobDefinition.NodeCount;
        for (int i = 0; i < total; i++)
        {
            nodeRanks.Add(0);
        }
        int slots = jobs.Length * JobDefinition.SlotCount;
        for (int i = 0; i < slots; i++)
        {
            equipped.Add(JobDefinition.EmptySlot);
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

    private void OnJobChanged(int oldValue, int newValue)
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

    // 장비 능력치 위에 지금 직업의 노드와 켜진 강화 스킬을 더한다(PlayerInventory가 부른다).
    public void ModifyStats(ref EquipStats stats)
    {
        JobDefinition job = Job;
        for (int i = 0; i < JobDefinition.NodeCount; i++)
        {
            int rank = GetRank(i);
            if (rank > 0)
            {
                JobNode node = job.GetNode(i);
                if (node.effect != JobNodeEffect.UnlockSkill)
                {
                    ApplyEffect(ref stats, node.effect, node.value * rank);
                }
            }
        }
        if (activeBuff >= 0)
        {
            SkillDefinition buff = jobs[activeBuff / JobDefinition.SkillCount].GetSkill(activeBuff % JobDefinition.SkillCount);
            ApplyEffect(ref stats, buff.BuffEffect, buff.BuffValue);
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
        if (!blocked && !hub.IsCombatScene)
        {
            int pick = input.JobKeyPressed;
            if (pick >= 0 && pick < jobs.Length && pick != JobIndex)
            {
                CmdSetStartJob(pick);
            }
        }
    }

    // 장착 칸의 스킬을 쓸 수 있으면 서버에 알리고 그 스킬을 돌려준다. 못 쓰면 null.
    public SkillDefinition TryUse(int slot, Vector2 aim)
    {
        SkillDefinition used = null;
        int skillIndex = GetSlotSkill(slot);
        if (skillIndex >= 0 && !HoldingJobWeapon)
        {
            hub.Hud.ShowNotice("직업의 무기를 들어야 스킬을 쓸 수 있습니다");
        }
        else if (skillIndex >= 0 && Time.time >= localReadyTime[skillIndex])
        {
            SkillDefinition skill = Job.GetSkill(skillIndex);
            if (controller.Mana >= skill.ManaCost)
            {
                float cooldown = skill.Cooldown * inventory.Stats.cooldownMultiplier;
                localCooldown[skillIndex] = cooldown;
                localReadyTime[skillIndex] = Time.time + cooldown;
                used = skill;
                PlayCastFx(skill, controller.Center, aim);
                CmdUse(skillIndex, aim);
            }
            else
            {
                hub.Hud.ShowNotice("마나가 부족합니다");
            }
        }
        return used;
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

    // ------------------------------------------------------------ 서버

    // 처치할 때마다 지금 든 무기 계열의 숙련도가 오른다(전직 조건까지만).
    public void ServerAddMastery()
    {
        int held = inventory.WeaponKindIndex;
        if (held >= 0 && held < mastery.Count && mastery[held] < masteryRequired)
        {
            mastery[held] = (byte)(mastery[held] + 1);
        }
    }

    // 제단에서 전직할 수 있는 직업. alternate면 완드의 두 번째 직업(사제). 못 하면 -1.
    public int GetAltarTarget(bool alternate)
    {
        int held = inventory.WeaponKindIndex;
        int target = DefaultJobOf(held);
        if (alternate)
        {
            target = -1;
            if (held == (int)WeaponKind.Wand)
            {
                target = PriestJob;
            }
        }
        return target;
    }

    // 제단에서: 든 무기 계열의 숙련도가 찼으면 그 직업으로 바꾼다. 찍어 둔 노드는 직업별로 남는다.
    // 완드끼리(주술사와 사제)는 숙련도 없이 바꿀 수 있다.
    public bool ServerTryChangeJob(bool alternate)
    {
        int held = inventory.WeaponKindIndex;
        int target = GetAltarTarget(alternate);
        bool changed = false;
        if (target >= 0 && target != jobIndex)
        {
            bool sameWeapon = (int)jobs[jobIndex].WeaponKind == held;
            changed = sameWeapon || GetMastery(held) >= masteryRequired;
        }
        if (changed)
        {
            jobIndex = target;
            activeBuff = -1;
            inventory.RecomputeStats();
        }
        return changed;
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
        inventory.RecomputeStats();
    }

    [Command]
    private void CmdSetStartJob(int job)
    {
        if (!hub.IsCombatScene && job >= 0 && job < jobs.Length)
        {
            ServerSetStartJob(job);
        }
    }

    // 로비에서 시작 직업을 고른다(그 직업의 무기로 시작 장비를 바꾼다).
    public void ServerSetStartJob(int job)
    {
        if (manager == null)
        {
            manager = (GameNetworkManager)NetworkManager.singleton;
        }
        lobbyJob = job;
        manager.SetSlotStartJob(controller.Slot, job);
        inventory.ServerSetStartWeapon((int)jobs[job].WeaponKind);
    }

    [Command]
    private void CmdAllocate(int nodeIndex)
    {
        if (hub.IsCombatScene && CanAllocate(nodeIndex))
        {
            int index = JobIndex * JobDefinition.NodeCount + nodeIndex;
            skillPoints--;
            nodeRanks[index] = (byte)(nodeRanks[index] + 1);
            JobNode node = Job.GetNode(nodeIndex);
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
        for (int i = 0; i < JobDefinition.SlotCount && empty < 0; i++)
        {
            if (GetSlotSkill(i) < 0)
            {
                empty = i;
            }
        }
        if (empty >= 0)
        {
            equipped[JobIndex * JobDefinition.SlotCount + empty] = (byte)skillIndex;
        }
    }

    [Command]
    private void CmdEquip(int slot, int skillIndex)
    {
        if (slot >= 0 && slot < JobDefinition.SlotCount && IsSkillUnlocked(skillIndex))
        {
            int baseIndex = JobIndex * JobDefinition.SlotCount;
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
    private void CmdUse(int skillIndex, Vector2 aim)
    {
        bool valid = skillIndex >= 0 && skillIndex < JobDefinition.SkillCount && IsSkillUnlocked(skillIndex) && HoldingJobWeapon;
        if (valid && FindSlotOf(skillIndex) >= 0)
        {
            SkillDefinition skill = Job.GetSkill(skillIndex);
            // 통신 지연을 감안해 재사용 시간은 조금 너그럽게 본다.
            if (Time.time >= serverReadyTime[skillIndex] - 0.25f && controller.ServerBeginSkill(skill.ManaCost, skill.CastTime))
            {
                serverReadyTime[skillIndex] = Time.time + skill.Cooldown * inventory.Stats.cooldownMultiplier;
                Vector2 direction = aim.normalized;
                ServerExecute(skill, skillIndex, direction);
                RpcSkill(JobIndex, skillIndex, controller.Center, direction);
            }
        }
    }

    private void ServerExecute(SkillDefinition skill, int skillIndex, Vector2 aim)
    {
        float baseDamage = inventory.Weapon.Damage * skill.DamageFactor * inventory.Stats.skillDamageMultiplier;
        Vector2 center = controller.Center;
        if (skill.Kind == SkillKind.AreaAround)
        {
            ServerHitCircle(center, skill.Radius, RollSkillHit(skill, baseDamage));
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
                ServerHitLine(center, aim, skill.Range, skill.Radius, RollSkillHit(skill, baseDamage));
            }
            if (skill.ExplosionRadius > 0f)
            {
                slamPending = true;
                slamTime = Time.time + skill.CastTime;
                slamRadius = skill.ExplosionRadius;
                slamHit = RollSkillHit(skill, baseDamage);
                slamFx = skill.Fx;
                slamColor = skill.FxColor;
            }
        }
        else if (skill.Kind == SkillKind.Volley)
        {
            LaunchVolley(skill, center, aim, true, baseDamage);
        }
        else if (skill.Kind == SkillKind.Heal)
        {
            ServerHealAround(center, skill.Radius, skill.HealPercent);
        }
        else if (skill.Kind == SkillKind.Revive)
        {
            ServerReviveAround(center, skill.Radius, skill.HealPercent);
        }
        else if (skill.Kind == SkillKind.Zone)
        {
            ServerAddZone(skill, ZoneCenter(skill, center, aim), RollSkillHit(skill, baseDamage));
        }
        else
        {
            int code = JobIndex * JobDefinition.SkillCount + skillIndex;
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
    private static Vector2 ZoneCenter(SkillDefinition skill, Vector2 center, Vector2 aim)
    {
        return center + aim * skill.Range;
    }

    // 스킬 한 번의 타격 정보(치명타와 스킬에 붙은 상태이상 포함)
    private HitInfo RollSkillHit(SkillDefinition skill, float baseDamage)
    {
        HitInfo hit = inventory.RollHit(baseDamage);
        hit.status = skill.StatusKind;
        hit.statusDuration = skill.StatusDuration;
        hit.statusPower = skill.StatusPower;
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
        zone.healPercent = skill.HealPercent;
        zones[pick] = zone;
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
                else if (Time.time >= zones[i].nextTick)
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

    // 부채꼴로 여러 발. 서버는 판정용, 다른 클라이언트는 표시용으로 같은 궤적을 쏜다.
    private void LaunchVolley(SkillDefinition skill, Vector2 center, Vector2 aim, bool authoritative, float baseDamage)
    {
        if (hub.ProjectilePool != null)
        {
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
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
                shot.direction = direction;
                shot.speed = skill.ProjectileSpeed;
                shot.lifetime = skill.ProjectileLifetime;
                shot.radius = skill.Radius;
                shot.damage = 1;
                shot.explosionRadius = skill.ExplosionRadius;
                shot.look = skill.ProjectileLook;
                shot.color = skill.FxColor;
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
    private void RpcSkill(int job, int skillIndex, Vector2 center, Vector2 aim)
    {
        SkillDefinition skill = jobs[job].GetSkill(skillIndex);
        if (!isLocalPlayer)
        {
            controller.PlaySkillVisual(skill, aim);
            PlayCastFx(skill, center, aim);
        }
        if (!isServer && skill.Kind == SkillKind.Volley)
        {
            LaunchVolley(skill, center, aim, false, 0f);
        }
    }

    [ClientRpc]
    private void RpcSlam(Vector2 position, float radius, SkillFx fx, Color color)
    {
        if (hub.SkillFx != null)
        {
            hub.SkillFx.Play(fx, position, radius, color, 0f, null);
        }
    }

    // 스킬 종류에 맞춰 이펙트와 소리를 낸다(시전한 본인은 바로, 다른 사람은 Rpc로).
    private void PlayCastFx(SkillDefinition skill, Vector2 center, Vector2 aim)
    {
        hub.Sound.PlayAt(skill.Sound, center);
        SkillFxPlayer fxPlayer = hub.SkillFx;
        if (fxPlayer != null && skill.Fx != SkillFx.None)
        {
            if (skill.Kind == SkillKind.AreaAround || skill.Kind == SkillKind.Heal || skill.Kind == SkillKind.Revive)
            {
                fxPlayer.Play(skill.Fx, center, skill.Radius, skill.FxColor, 0f, null);
            }
            else if (skill.Kind == SkillKind.Buff)
            {
                float radius = Mathf.Max(0.8f, skill.Radius);
                fxPlayer.Play(skill.Fx, center, radius, skill.FxColor, 0f, null);
                fxPlayer.Play(SkillFx.Aura, center, 0.8f, skill.FxColor, skill.BuffDuration, controller.transform);
            }
            else if (skill.Kind == SkillKind.Zone)
            {
                fxPlayer.Play(skill.Fx, ZoneCenter(skill, center, aim), skill.Radius, skill.FxColor, skill.BuffDuration, null);
            }
            else if (skill.Kind == SkillKind.Dash)
            {
                fxPlayer.PlayDirected(SkillFx.Wind, center + aim * (skill.Range * 0.5f), skill.Range * 0.5f, aim, skill.FxColor);
            }
            else
            {
                fxPlayer.Play(skill.Fx, center + aim * 0.6f, 0.6f, skill.FxColor, 0f, null);
            }
        }
    }

    private void Update()
    {
        if (isServer)
        {
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
                RpcSlam(position, slamRadius, slamFx, slamColor);
            }
            ServerTickZones();
        }
    }
}
