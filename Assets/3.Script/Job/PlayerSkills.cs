using Mirror;
using UnityEngine;

// 직업, 레벨, 노드맵, 스킬(Q, E, R).
// - 직업은 들고 있는 무기 계열이 정한다(검 = 검사, 창 = 창기사, 활 = 궁수, 지팡이 = 마법사).
//   판을 시작할 때 든 무기 계열이 첫 직업이고, 다른 계열은 그 무기로 처치해 숙련도를 채운 뒤 시작 방의 제단에서 전직한다.
//   스킬은 직업의 무기 계열을 들고 있을 때만 쓸 수 있다.
// - 몬스터를 잡으면 경험치를 얻고, 레벨이 오를 때마다 노드 포인트를 1 받는다. 한 판이 끝나면 초기화된다.
// - 노드는 직업별로 따로 기억한다. 무기를 바꾸면 그 직업에 찍어 둔 노드가 적용된다.
// - 스킬 사용 판정(마나, 재사용 시간, 피해)은 서버가 하고, 시전 동작과 돌진 이동은 본인 클라이언트가 한다.
public class PlayerSkills : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerInventory inventory;
    [Tooltip("WeaponKind 순서(검, 창, 활, 지팡이)")]
    [SerializeField] private JobDefinition[] jobs;
    [SerializeField] private SpriteRenderer areaFxRenderer;

    [Header("성장")]
    [SerializeField] private int maxLevel = 15;
    [SerializeField] private int startPoints = 1;
    [SerializeField] private int xpBase = 16;
    [SerializeField] private int xpPerLevel = 6;
    [SerializeField] private float areaFxTime = 0.25f;
    [SerializeField] private float muzzleDistance = 0.4f;
    [Tooltip("전직에 필요한 숙련도(그 무기 계열을 들고 처치한 수)")]
    [SerializeField] private int masteryRequired = 8;

    // 전투 씬에서의 직업. 로비에서는 든 무기 계열을 그대로 따른다.
    [SyncVar(hook = nameof(OnJobChanged))] private int jobIndex;
    // 무기 계열별 숙련도(그 계열을 들고 처치한 수, 전직 조건까지만 센다)
    private readonly SyncList<byte> mastery = new SyncList<byte>();
    [SyncVar] private int level = 1;
    [SyncVar] private int xp;
    [SyncVar(hook = nameof(OnPointsChanged))] private int skillPoints;
    // 직업 수 x 노드 수. 각 칸은 그 노드에 찍은 단계.
    private readonly SyncList<byte> nodeRanks = new SyncList<byte>();
    // 켜져 있는 강화 스킬(직업 번호 x 3 + 스킬 번호). 없으면 -1.
    [SyncVar(hook = nameof(OnBuffChanged))] private int activeBuff = -1;

    private GameHub hub;
    private int revision;
    private float areaFxTimer;
    private Color areaFxColor;

    // 본인 클라이언트: 스킬 칸별 다시 쓸 수 있는 시각
    private readonly float[] localReadyTime = new float[JobDefinition.SkillCount];
    private readonly float[] localCooldown = new float[JobDefinition.SkillCount];
    // 서버
    private readonly float[] serverReadyTime = new float[JobDefinition.SkillCount];
    private float buffEndTime;

    public int Level => level;
    public int Xp => xp;
    public int SkillPoints => skillPoints;
    public int Revision => revision;
    public int MasteryRequired => masteryRequired;

    public int JobIndex
    {
        get
        {
            int index = Mathf.Clamp(inventory.WeaponKindIndex, 0, jobs.Length - 1);
            if (hub != null && hub.IsCombatScene)
            {
                index = Mathf.Clamp(jobIndex, 0, jobs.Length - 1);
            }
            return index;
        }
    }

    // 직업의 무기 계열을 들고 있어야 스킬을 쓸 수 있다.
    public bool HoldingJobWeapon
    {
        get
        {
            return JobIndex == inventory.WeaponKindIndex;
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

    public string GetJobName(int kindIndex)
    {
        return jobs[Mathf.Clamp(kindIndex, 0, jobs.Length - 1)].DisplayName;
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
        JobDefinition job = Job;
        for (int i = 0; i < JobDefinition.NodeCount && !unlocked; i++)
        {
            JobNode node = job.GetNode(i);
            if (node.effect == JobNodeEffect.UnlockSkill && Mathf.RoundToInt(node.value) == skillIndex)
            {
                unlocked = GetRank(i) > 0;
            }
        }
        return unlocked;
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

    // 남은 재사용 시간의 비율(1 = 방금 씀, 0 = 사용 가능). 본인 화면용.
    public float GetCooldown01(int skillIndex)
    {
        float ratio = 0f;
        float remaining = localReadyTime[skillIndex] - Time.time;
        if (remaining > 0f && localCooldown[skillIndex] > 0f)
        {
            ratio = Mathf.Clamp01(remaining / localCooldown[skillIndex]);
        }
        return ratio;
    }

    // ------------------------------------------------------------ 초기화(PlayerController가 순서를 정해 부른다)

    public void ServerInit(GameHub sceneHub)
    {
        hub = sceneHub;
        level = 1;
        xp = 0;
        skillPoints = startPoints;
        activeBuff = -1;
        jobIndex = Mathf.Clamp(inventory.WeaponKindIndex, 0, jobs.Length - 1);
        for (int i = 0; i < jobs.Length; i++)
        {
            mastery.Add(0);
        }
        int total = jobs.Length * JobDefinition.NodeCount;
        for (int i = 0; i < total; i++)
        {
            nodeRanks.Add(0);
        }
    }

    public void ClientInit(GameHub sceneHub)
    {
        hub = sceneHub;
        nodeRanks.OnChange = OnNodeRanksChanged;
        mastery.OnChange = OnMasteryChanged;
        areaFxRenderer.enabled = false;
        // 나중에 들어온 클라이언트는 처음 받은 노드 값으로 한 번 계산해 둔다.
        inventory.RecomputeStats();
    }

    private void OnNodeRanksChanged(SyncList<byte>.Operation operation, int index, byte value)
    {
        revision++;
        inventory.RecomputeStats();
    }

    private void OnMasteryChanged(SyncList<byte>.Operation operation, int index, byte value)
    {
        revision++;
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
                ApplyEffect(ref stats, node.effect, node.value * rank);
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
    }

    // 스킬을 쓸 수 있으면 서버에 알리고 그 스킬을 돌려준다. 못 쓰면 null.
    public SkillDefinition TryUse(int skillIndex, Vector2 aim)
    {
        SkillDefinition used = null;
        if (IsSkillUnlocked(skillIndex) && !HoldingJobWeapon)
        {
            hub.Hud.ShowNotice("직업의 무기를 들어야 스킬을 쓸 수 있습니다");
        }
        else if (IsSkillUnlocked(skillIndex) && Time.time >= localReadyTime[skillIndex])
        {
            SkillDefinition skill = Job.GetSkill(skillIndex);
            if (controller.Mana >= skill.ManaCost)
            {
                float cooldown = skill.Cooldown * inventory.Stats.cooldownMultiplier;
                localCooldown[skillIndex] = cooldown;
                localReadyTime[skillIndex] = Time.time + cooldown;
                used = skill;
                PlayFx(skill);
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

    // 제단에서: 든 무기 계열의 숙련도가 찼으면 그 직업으로 바꾼다. 찍어 둔 노드는 직업별로 남는다.
    public bool ServerTryChangeJob()
    {
        int held = inventory.WeaponKindIndex;
        bool changed = held >= 0 && held < jobs.Length && held != jobIndex && GetMastery(held) >= masteryRequired;
        if (changed)
        {
            jobIndex = held;
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

    [Command]
    private void CmdAllocate(int nodeIndex)
    {
        if (hub.IsCombatScene && CanAllocate(nodeIndex))
        {
            int index = JobIndex * JobDefinition.NodeCount + nodeIndex;
            skillPoints--;
            nodeRanks[index] = (byte)(nodeRanks[index] + 1);
            inventory.RecomputeStats();
        }
    }

    [Command]
    private void CmdUse(int skillIndex, Vector2 aim)
    {
        if (skillIndex >= 0 && skillIndex < JobDefinition.SkillCount && IsSkillUnlocked(skillIndex) && HoldingJobWeapon)
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
            if (skill.DamageFactor > 0f)
            {
                ServerHitLine(center, aim, skill.Range, skill.Radius, RollSkillHit(skill, baseDamage));
            }
        }
        else if (skill.Kind == SkillKind.Volley)
        {
            LaunchVolley(skill, center, aim, true, baseDamage);
        }
        else
        {
            activeBuff = JobIndex * JobDefinition.SkillCount + skillIndex;
            buffEndTime = Time.time + skill.BuffDuration;
            inventory.RecomputeStats();
        }
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
    private void RpcSkill(int jobIndex, int skillIndex, Vector2 center, Vector2 aim)
    {
        SkillDefinition skill = jobs[jobIndex].GetSkill(skillIndex);
        if (!isLocalPlayer)
        {
            controller.PlaySkillVisual(skill, aim);
            PlayFx(skill);
        }
        if (!isServer && skill.Kind == SkillKind.Volley)
        {
            LaunchVolley(skill, center, aim, false, 0f);
        }
    }

    // 범위 스킬은 반경만큼의 원을, 강화 스킬은 몸 주변에 작은 원을 잠깐 보여 준다.
    private void PlayFx(SkillDefinition skill)
    {
        bool show = skill.Kind == SkillKind.AreaAround || skill.Kind == SkillKind.Buff;
        if (show)
        {
            float radius = 0.9f;
            if (skill.Kind == SkillKind.AreaAround)
            {
                radius = skill.Radius;
            }
            // Circle 스프라이트는 지름 0.64 유닛
            float scale = radius * 2f / 0.64f;
            areaFxRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            areaFxColor = skill.FxColor;
            areaFxTimer = areaFxTime;
            areaFxRenderer.enabled = true;
        }
    }

    private void Update()
    {
        if (areaFxTimer > 0f)
        {
            areaFxTimer -= Time.deltaTime;
            if (areaFxTimer > 0f)
            {
                Color color = areaFxColor;
                color.a = 0.6f * areaFxTimer / areaFxTime;
                areaFxRenderer.color = color;
            }
            else
            {
                areaFxRenderer.enabled = false;
            }
        }

        if (isServer && activeBuff >= 0)
        {
            if (Time.time >= buffEndTime)
            {
                activeBuff = -1;
                inventory.RecomputeStats();
            }
        }
    }
}
