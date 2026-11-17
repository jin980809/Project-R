using System;
using System.Text;
using UnityEngine;

public enum JobNodeEffect : byte
{
    // value는 해금할 스킬 번호(0 Q, 1 E, 2 R)
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

// 노드맵의 노드 하나. 한 단계(rank)마다 value만큼 효과가 쌓인다.
[Serializable]
public class JobNode
{
    public string displayName = "노드";
    public JobNodeEffect effect;
    public float value = 5f;
    public int maxRank = 1;
}

// 직업 하나: 무기 계열에 묶인 1차 직업. 스킬 3개(Q, E, R)와 노드맵(갈래 2개 x 4단계)을 가진다.
// 노드는 갈래 A가 0~3번, 갈래 B가 4~7번이고, 같은 갈래의 앞 노드를 찍어야 다음 노드를 찍을 수 있다.
[CreateAssetMenu(menuName = "Tower/Job Definition", fileName = "Job")]
public class JobDefinition : ScriptableObject
{
    public const int SkillCount = 3;
    public const int BranchSize = 4;
    public const int NodeCount = BranchSize * 2;

    [SerializeField] private string displayName = "직업";
    [SerializeField] private string[] branchNames = { "갈래 A", "갈래 B" };
    [SerializeField] private SkillDefinition[] skills = new SkillDefinition[SkillCount];
    [SerializeField] private JobNode[] nodes = new JobNode[NodeCount];
    [Tooltip("JobNodeEffect 순서. {0}에 수치가 들어간다(스킬 해금은 따로 처리).")]
    [SerializeField] private string[] effectFormats =
    {
        "", "공격력 +{0}%", "공격 속도 +{0}%", "이동 속도 +{0}%", "치명타 확률 +{0}%", "최대 체력 +{0}",
        "받는 피해 -{0}%", "최대 마나 +{0}", "적중 시 마나 +{0}", "재사용 시간 -{0}%", "스킬 피해 +{0}%"
    };

    public string DisplayName => displayName;

    public string GetBranchName(int branch)
    {
        return branchNames[branch];
    }

    public SkillDefinition GetSkill(int index)
    {
        return skills[index];
    }

    public JobNode GetNode(int index)
    {
        return nodes[index];
    }

    // "이름 1/3" 다음 줄에 효과 설명을 적는다.
    public void AppendNodeText(StringBuilder builder, int index, int rank)
    {
        JobNode node = nodes[index];
        builder.Append(node.displayName).Append("  ").Append(rank).Append('/').Append(node.maxRank).Append('\n');
        if (node.effect == JobNodeEffect.UnlockSkill)
        {
            builder.Append("스킬 해금: ").Append(skills[Mathf.RoundToInt(node.value)].DisplayName);
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
}
