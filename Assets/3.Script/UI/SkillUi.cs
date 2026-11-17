using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬 표시 줄(화면 아래 가운데: Q, E, R 칸과 레벨·경험치)과 노드맵 화면(K).
// 노드맵은 갈래 2개 x 4단계. 왼쪽 클릭으로 포인트를 써서 노드를 찍는다.
public class SkillUi : MonoBehaviour
{
    [SerializeField] private GameHub hub;

    [Header("스킬 표시 줄")]
    [SerializeField] private Image[] skillFrames;
    [Tooltip("재사용 대기 중에 칸을 덮는 어두운 그림(아래에서 위로 채움)")]
    [SerializeField] private Image[] skillFills;
    [SerializeField] private TMP_Text[] skillLabels;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private Image xpFill;
    [SerializeField] private Color lockedSkillColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private float unlockedSkillAlpha = 0.75f;

    [Header("노드맵")]
    [SerializeField] private GameObject treePanel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text[] branchLabels;
    [SerializeField] private SkillNodeView[] nodeViews;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private Color lockedNodeColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Color availableNodeColor = new Color(0.45f, 0.45f, 0.5f, 1f);
    [SerializeField] private Color[] allocatedNodeColors = { new Color(0.8f, 0.35f, 0.25f, 1f), new Color(0.25f, 0.5f, 0.85f, 1f) };

    private readonly StringBuilder builder = new StringBuilder(256);
    private PlayerSkills skills;
    private int hoveredNode = -1;
    private int lastRevision = -1;
    private int lastJob = -1;
    private int lastLevel = -1;
    private int lastXp = -1;
    private int lastPoints = -1;

    public bool IsTreeOpen => treePanel.activeSelf;

    public void Bind(PlayerSkills localSkills)
    {
        skills = localSkills;
        lastRevision = -1;
    }

    public void ToggleTree()
    {
        if (treePanel.activeSelf)
        {
            CloseTree();
        }
        else
        {
            treePanel.SetActive(true);
            lastRevision = -1;
        }
    }

    public void CloseTree()
    {
        hoveredNode = -1;
        treePanel.SetActive(false);
    }

    private void Update()
    {
        if (skills != null)
        {
            for (int i = 0; i < skillFills.Length; i++)
            {
                skillFills[i].fillAmount = skills.GetCooldown01(i);
            }

            bool changed = skills.Revision != lastRevision || skills.JobIndex != lastJob || skills.Level != lastLevel
                || skills.Xp != lastXp || skills.SkillPoints != lastPoints;
            if (changed)
            {
                lastRevision = skills.Revision;
                lastJob = skills.JobIndex;
                lastLevel = skills.Level;
                lastXp = skills.Xp;
                lastPoints = skills.SkillPoints;
                Refresh();
            }
        }
    }

    private void Refresh()
    {
        JobDefinition job = skills.Job;

        builder.Clear();
        builder.Append("Lv ").Append(skills.Level).Append("  ").Append(job.DisplayName);
        if (skills.SkillPoints > 0)
        {
            builder.Append("    포인트 ").Append(skills.SkillPoints).Append(" (K)");
        }
        levelLabel.SetText(builder);
        if (skills.IsMaxLevel)
        {
            xpFill.fillAmount = 1f;
        }
        else
        {
            xpFill.fillAmount = skills.Xp / (float)skills.XpToNext;
        }

        for (int i = 0; i < skillFrames.Length; i++)
        {
            SkillDefinition skill = job.GetSkill(i);
            builder.Clear();
            if (skills.IsSkillUnlocked(i))
            {
                Color color = skill.FxColor * 0.45f;
                color.a = unlockedSkillAlpha;
                skillFrames[i].color = color;
                builder.Append(skill.DisplayName).Append('\n').Append(skill.ManaCost);
            }
            else
            {
                skillFrames[i].color = lockedSkillColor;
                builder.Append("잠김");
            }
            skillLabels[i].SetText(builder);
        }

        if (treePanel.activeSelf)
        {
            builder.Clear();
            builder.Append(job.DisplayName).Append(" 노드맵     남은 포인트 ").Append(skills.SkillPoints);
            titleLabel.SetText(builder);
            for (int i = 0; i < branchLabels.Length; i++)
            {
                branchLabels[i].text = job.GetBranchName(i);
            }
            for (int i = 0; i < nodeViews.Length; i++)
            {
                JobNode node = job.GetNode(i);
                int rank = skills.GetRank(i);
                Color color = lockedNodeColor;
                if (rank > 0)
                {
                    color = allocatedNodeColors[i / JobDefinition.BranchSize];
                }
                else if (skills.CanAllocate(i))
                {
                    color = availableNodeColor;
                }
                builder.Clear();
                builder.Append(node.displayName).Append("  ").Append(rank).Append('/').Append(node.maxRank);
                nodeViews[i].Show(builder, color);
            }
            RefreshDetail();
        }
    }

    private void RefreshDetail()
    {
        builder.Clear();
        if (hoveredNode >= 0)
        {
            JobDefinition job = skills.Job;
            job.AppendNodeText(builder, hoveredNode, skills.GetRank(hoveredNode));
            JobNode node = job.GetNode(hoveredNode);
            if (node.effect == JobNodeEffect.UnlockSkill)
            {
                SkillDefinition skill = job.GetSkill(Mathf.RoundToInt(node.value));
                builder.Append("\n마나 ").Append(skill.ManaCost).Append("   재사용 ").Append(skill.Cooldown).Append("초");
            }
        }
        else
        {
            builder.Append("레벨이 오를 때마다 포인트를 1 받습니다. 왼쪽 클릭으로 노드를 찍습니다.\n같은 갈래의 앞 노드를 찍어야 다음 노드가 열립니다. 한 판이 끝나면 초기화됩니다.");
        }
        detailLabel.SetText(builder);
    }

    public void OnNodeHover(int index, bool entered)
    {
        if (entered)
        {
            hoveredNode = index;
        }
        else if (hoveredNode == index)
        {
            hoveredNode = -1;
        }
        if (skills != null)
        {
            RefreshDetail();
        }
    }

    public void OnNodeClicked(int index)
    {
        if (skills != null)
        {
            if (!hub.IsCombatScene)
            {
                hub.Hud.ShowNotice("노드는 탑에 들어간 뒤에 찍을 수 있습니다");
            }
            else if (skills.CanAllocate(index))
            {
                skills.RequestAllocate(index);
            }
        }
    }
}
