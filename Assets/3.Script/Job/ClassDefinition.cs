using System;
using System.Text;
using UnityEngine;

public enum JobNodeEffect : byte
{
    // value는 해금할 스킬 번호(계열 안에서 0~23)
    UnlockSkill,
    DamagePercent,
    AttackSpeedPercent,
    MoveSpeedPercent,
    CritChancePercent,
    MaxHp,
    DefensePercent,
    MaxMana,
    ManaOnHit,
    // 스킬 재사용 시간 감소
    CooldownPercent,
    SkillDamagePercent
}

// 직업 가지를 전부 찍었을 때(각성) 붙는 고유 효과. 수치는 JobBranch의 awakenValueA, awakenValueB.
public enum JobAwakening : byte
{
    // 검사: 받는 피해 -A%, 처치 시 최대 체력의 B% 회복
    Guardian,
    // 창기사: 이동 속도 +A%, 돌진 스킬 재사용 시간 -B%
    Dragoon,
    // 광전사: 체력이 절반 이하일 때 공격력 +A%
    Frenzy,
    // 레인저: 이동 속도 +A%, 치명타 확률 +B%
    Hunter,
    // 저격수: 치명타 피해 +A%
    Marksman,
    // 건슬링어: 공격 속도 +A%, 적중 시 마나 +B
    Gunner,
    // 원소술사: 스킬이 거는 상태 이상 지속 시간 +A%
    Elemental,
    // 주술사: 처치 시 마나 +A
    Hexer,
    // 사제: 회복·부활 효과 +A%
    Saint
}

// 스킬 트리의 노드 하나. 한 단계(rank)마다 value만큼 효과가 쌓인다.
// 부모 노드를 찍어야 찍을 수 있다(부모가 -1이면 직업 머리 노드라 바로 찍을 수 있다).
[Serializable]
public class JobNode
{
    public string displayName = "노드";
    public JobNodeEffect effect;
    public float value = 5f;
    public int maxRank = 1;
    public int parent = -1;
    // 이 노드가 속한 직업 가지(0~2)
    public int job;
    // 트리 화면에서의 칸(x 열 0~11, y 행 0~5)
    public Vector2 cell;
}

// 계열 안의 직업 하나(트리의 큰 가지). 가지의 노드를 전부 찍으면 각성한다.
[Serializable]
public class JobBranch
{
    public string displayName = "직업";
    // 트리 화면에 이름 옆에 적는 무기 이름(예: "도끼·둔기")
    public string weaponText;
    public JobAwakening awakening;
    public float awakenValueA;
    public float awakenValueB;
    [TextArea]
    public string awakeningText;
}

// 계열 하나(전사, 사수, 마법사). 계열의 무기라면 어느 것을 들어도 이 트리에서 배운 스킬을 모두 쓸 수 있다.
// 트리는 직업 가지 3개로 갈라지고, 가지마다 머리 노드에서 다시 여러 갈래로 갈라진다(다시 합쳐지지 않는다).
// 직업 가지를 전부 찍으면 각성: 그 직업 스킬의 피해와 재사용 시간이 좋아지고 고유 효과가 붙는다.
[CreateAssetMenu(menuName = "Tower/Class Definition", fileName = "Class")]
public class ClassDefinition : ScriptableObject
{
    public const int MaxSkills = 24;
    public const int MaxNodes = 42;
    public const int JobCount = 3;
    public const int SlotCount = 3;
    public const int Columns = 12;
    public const int Rows = 6;
    public const byte EmptySlot = 255;

    [SerializeField] private string displayName = "계열";
    [TextArea]
    [SerializeField] private string description;
    [Tooltip("이 계열의 무기. 첫 번째가 로비에서 받는 시작 무기다.")]
    [SerializeField] private WeaponKind[] weaponKinds = new WeaponKind[0];
    [SerializeField] private JobBranch[] jobBranches = new JobBranch[JobCount];
    [SerializeField] private SkillDefinition[] skills = new SkillDefinition[0];
    [SerializeField] private JobNode[] nodes = new JobNode[0];
    [Tooltip("각성하면 그 직업 스킬의 피해가 오르는 비율(%)")]
    [SerializeField] private float awakenSkillDamagePercent = 30f;
    [Tooltip("각성하면 그 직업 스킬의 재사용 시간이 줄어드는 비율(%)")]
    [SerializeField] private float awakenCooldownPercent = 20f;
    [Tooltip("JobNodeEffect 순서. {0}에 수치가 들어간다(스킬 해금은 따로 처리).")]
    [SerializeField] private string[] effectFormats =
    {
        "", "공격력 +{0}%", "공격 속도 +{0}%", "이동 속도 +{0}%", "치명타 확률 +{0}%", "최대 체력 +{0}",
        "받는 피해 -{0}%", "최대 마나 +{0}", "적중 시 마나 +{0}", "재사용 시간 -{0}%", "스킬 피해 +{0}%"
    };

    public string DisplayName => displayName;
    public string Description => description;
    public int SkillCount => skills.Length;
    public int NodeCount => nodes.Length;
    public float AwakenSkillDamagePercent => awakenSkillDamagePercent;
    public float AwakenCooldownPercent => awakenCooldownPercent;
    public WeaponKind StartWeapon => weaponKinds[0];

    public bool UsesWeapon(int weaponKind)
    {
        bool uses = false;
        for (int i = 0; i < weaponKinds.Length && !uses; i++)
        {
            uses = (int)weaponKinds[i] == weaponKind;
        }
        return uses;
    }

    public JobBranch GetJob(int job)
    {
        return jobBranches[job];
    }

    public SkillDefinition GetSkill(int index)
    {
        return skills[index];
    }

    public JobNode GetNode(int index)
    {
        return nodes[index];
    }

    // 그 스킬을 해금하는 노드 번호. 없으면 -1.
    public int FindSkillNode(int skillIndex)
    {
        int found = -1;
        for (int i = 0; i < nodes.Length && found < 0; i++)
        {
            if (nodes[i].effect == JobNodeEffect.UnlockSkill && Mathf.RoundToInt(nodes[i].value) == skillIndex)
            {
                found = i;
            }
        }
        return found;
    }

    // 그 스킬이 속한 직업 가지. 트리에 없으면 -1.
    public int GetSkillJob(int skillIndex)
    {
        int job = -1;
        int node = FindSkillNode(skillIndex);
        if (node >= 0)
        {
            job = nodes[node].job;
        }
        return job;
    }

    // 직업 가지를 전부 찍는 데 드는 포인트
    public int GetJobCost(int job)
    {
        int cost = 0;
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].job == job)
            {
                cost += nodes[i].maxRank;
            }
        }
        return cost;
    }

    // "이름 1/1" 다음 줄에 효과 설명을 적는다.
    public void AppendNodeText(StringBuilder builder, int index, int rank)
    {
        JobNode node = nodes[index];
        builder.Append(node.displayName).Append("  ").Append(rank).Append('/').Append(node.maxRank)
            .Append("   (").Append(jobBranches[node.job].displayName).Append(")\n");
        if (node.effect == JobNodeEffect.UnlockSkill)
        {
            SkillDefinition skill = skills[Mathf.RoundToInt(node.value)];
            builder.Append("스킬: ").Append(skill.DisplayName).Append("  (마나 ").Append(skill.ManaCost)
                .Append(", ").Append(skill.Cooldown).Append("초)\n").Append(skill.Description);
        }
        else
        {
            builder.AppendFormat(effectFormats[(int)node.effect], node.value);
            if (node.maxRank > 1)
            {
                builder.Append(" (단계마다)");
            }
        }
    }

    // 능력치 노드의 효과만 짧게(트리 칸 안에 적는다). 스킬 노드는 적지 않는다.
    public void AppendNodeShort(StringBuilder builder, int index)
    {
        JobNode node = nodes[index];
        if (node.effect != JobNodeEffect.UnlockSkill)
        {
            builder.AppendFormat(effectFormats[(int)node.effect], node.value);
        }
    }

    // 직업 가지 머리 위에 마우스를 올렸을 때: 무기, 각성 조건과 효과
    public void AppendJobText(StringBuilder builder, int job, int spent, bool awakened)
    {
        JobBranch branch = jobBranches[job];
        builder.Append(branch.displayName).Append("  (").Append(branch.weaponText).Append(")   ")
            .Append(spent).Append('/').Append(GetJobCost(job)).Append(" 포인트\n");
        if (awakened)
        {
            builder.Append("<color=#FFD24A>각성함</color>  ");
        }
        else
        {
            builder.Append("가지를 전부 찍으면 각성  ");
        }
        builder.Append(branch.displayName).Append(" 스킬 피해 +").Append(awakenSkillDamagePercent)
            .Append("%, 재사용 시간 -").Append(awakenCooldownPercent).Append("%\n").Append(branch.awakeningText);
    }
}
