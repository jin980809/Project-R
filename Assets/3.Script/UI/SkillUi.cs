using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스킬 표시 줄(화면 아래 가운데: Q, E, R 칸과 레벨·경험치)과 스킬 트리 화면(K).
// 스킬 트리는 직업 이름에서 갈래 3개가 뻗어 내려가고, 갈래마다 [스킬, 능력치, 스킬, 능력치] 4단계다.
// 왼쪽 클릭으로 포인트를 써서 노드를 찍는다. 해금한 스킬 노드를 고른 뒤 아래 장착 칸을 누르거나,
// 스킬 노드에 마우스를 올리고 Q, E, R을 누르면 그 칸에 장착한다.
public class SkillUi : MonoBehaviour
{
    [SerializeField] private GameHub hub;

    [Header("스킬 표시 줄")]
    [SerializeField] private Image[] skillFrames;
    [Tooltip("재사용 대기 중에 칸을 덮는 어두운 그림(아래에서 위로 채움)")]
    [SerializeField] private Image[] skillFills;
    [SerializeField] private TMP_Text[] skillLabels;
    [SerializeField] private Image[] skillIcons;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private Image xpFill;
    [Tooltip("로비에서만 보이는 시작 직업 목록(숫자 키)")]
    [SerializeField] private TMP_Text jobListLabel;
    [SerializeField] private Color lockedSkillColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private float unlockedSkillAlpha = 0.75f;
    [Tooltip("SkillFx 순서의 스킬 아이콘")]
    [SerializeField] private Sprite[] fxIcons;

    [Header("스킬 트리")]
    [SerializeField] private GameObject treePanel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text rootLabel;
    [SerializeField] private TMP_Text[] branchLabels;
    [SerializeField] private SkillNodeView[] nodeViews;
    [Tooltip("노드 i로 들어오는 연결선(단계 0은 갈래 머리에서 내려오는 선)")]
    [SerializeField] private Image[] nodeLinks;
    [Tooltip("직업 이름에서 갈래로 뻗는 선(갈래마다 하나)")]
    [SerializeField] private Image[] branchLinks;
    [SerializeField] private SkillNodeView[] slotViews;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private Color lockedNodeColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    [SerializeField] private Color availableNodeColor = new Color(0.42f, 0.42f, 0.48f, 1f);
    [SerializeField] private Color lockedLinkColor = new Color(0.3f, 0.3f, 0.32f, 1f);
    [SerializeField] private Color[] branchColors =
    {
        new Color(0.8f, 0.35f, 0.25f, 1f), new Color(0.25f, 0.5f, 0.85f, 1f), new Color(0.35f, 0.7f, 0.35f, 1f)
    };

    private readonly StringBuilder builder = new StringBuilder(256);
    private PlayerSkills skills;
    private int hoveredNode = -1;
    // 장착하려고 고른 스킬 번호(-1이면 없음)
    private int selectedSkill = -1;
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
            selectedSkill = -1;
            lastRevision = -1;
        }
    }

    public void CloseTree()
    {
        hoveredNode = -1;
        selectedSkill = -1;
        treePanel.SetActive(false);
    }

    private Sprite GetIcon(SkillDefinition skill)
    {
        Sprite sprite = null;
        int index = (int)skill.Fx;
        if (fxIcons != null && index < fxIcons.Length)
        {
            sprite = fxIcons[index];
        }
        return sprite;
    }

    private void Update()
    {
        if (skills != null)
        {
            for (int i = 0; i < skillFills.Length; i++)
            {
                skillFills[i].fillAmount = skills.GetSlotCooldown01(i);
            }

            bool changed = skills.Revision != lastRevision || skills.JobIndex != lastJob || skills.Level != lastLevel
                || skills.Xp != lastXp || skills.SkillPoints != lastPoints;
            if (changed)
            {
                if (skills.JobIndex != lastJob)
                {
                    selectedSkill = -1;
                }
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
        RefreshJobList();
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
            int skillIndex = skills.GetSlotSkill(i);
            builder.Clear();
            if (skillIndex >= 0)
            {
                SkillDefinition skill = job.GetSkill(skillIndex);
                Color color = skill.FxColor * 0.45f;
                color.a = unlockedSkillAlpha;
                skillFrames[i].color = color;
                skillIcons[i].enabled = GetIcon(skill) != null;
                skillIcons[i].sprite = GetIcon(skill);
                builder.Append(skill.ManaCost);
            }
            else
            {
                skillFrames[i].color = lockedSkillColor;
                skillIcons[i].enabled = false;
                builder.Append("비어 있음");
            }
            skillLabels[i].SetText(builder);
        }

        if (treePanel.activeSelf)
        {
            RefreshTree(job);
        }
    }

    // 로비: " 1 검사  2 창기사 ... 0 사제", 지금 직업은 노란색
    private void RefreshJobList()
    {
        bool lobby = !hub.IsCombatScene;
        jobListLabel.gameObject.SetActive(lobby);
        if (lobby)
        {
            builder.Clear();
            builder.Append("시작 직업  ");
            for (int i = 0; i < PlayerSkills.JobCount; i++)
            {
                bool current = i == skills.JobIndex;
                if (current)
                {
                    builder.Append("<color=#FFD24A>");
                }
                builder.Append((i + 1) % 10).Append(' ').Append(skills.GetJobName(i));
                if (current)
                {
                    builder.Append("</color>");
                }
                builder.Append("   ");
            }
            jobListLabel.SetText(builder);
        }
    }

    private void RefreshTree(JobDefinition job)
    {
        builder.Clear();
        builder.Append("스킬 트리     남은 포인트 ").Append(skills.SkillPoints);
        titleLabel.SetText(builder);
        rootLabel.SetText(job.DisplayName);
        for (int branch = 0; branch < branchLabels.Length; branch++)
        {
            branchLabels[branch].text = job.GetBranchName(branch);
            bool started = skills.GetRank(branch * JobDefinition.BranchSize) > 0;
            Color linkColor = lockedLinkColor;
            if (started)
            {
                linkColor = branchColors[branch];
            }
            branchLinks[branch].color = linkColor;
        }

        for (int i = 0; i < nodeViews.Length; i++)
        {
            JobNode node = job.GetNode(i);
            int rank = skills.GetRank(i);
            int branch = i / JobDefinition.BranchSize;
            Color color = lockedNodeColor;
            Color linkColor = lockedLinkColor;
            if (rank > 0)
            {
                color = branchColors[branch];
                linkColor = branchColors[branch];
            }
            else if (skills.CanAllocate(i))
            {
                color = availableNodeColor;
                linkColor = availableNodeColor;
            }
            nodeLinks[i].color = linkColor;

            builder.Clear();
            Sprite icon = null;
            Color iconColor = Color.white;
            bool selected = false;
            if (node.effect == JobNodeEffect.UnlockSkill)
            {
                int skillIndex = Mathf.RoundToInt(node.value);
                SkillDefinition skill = job.GetSkill(skillIndex);
                icon = GetIcon(skill);
                iconColor = skill.FxColor;
                if (rank == 0)
                {
                    iconColor *= 0.5f;
                    iconColor.a = 1f;
                }
                selected = skillIndex == selectedSkill;
                builder.Append(skill.DisplayName);
                int slot = skills.FindSlotOf(skillIndex);
                if (slot >= 0)
                {
                    builder.Append("  [").Append(SlotKey(slot)).Append(']');
                }
            }
            else
            {
                builder.Append(node.displayName).Append("  ").Append(rank).Append('/').Append(node.maxRank);
            }
            nodeViews[i].Show(builder, color, icon, iconColor, selected);
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            int skillIndex = skills.GetSlotSkill(i);
            builder.Clear();
            builder.Append(SlotKey(i)).Append("  ");
            Sprite icon = null;
            Color iconColor = Color.white;
            Color frame = lockedNodeColor;
            if (skillIndex >= 0)
            {
                SkillDefinition skill = job.GetSkill(skillIndex);
                builder.Append(skill.DisplayName);
                icon = GetIcon(skill);
                iconColor = skill.FxColor;
                frame = availableNodeColor;
            }
            else
            {
                builder.Append("비어 있음");
            }
            slotViews[i].Show(builder, frame, icon, iconColor, selectedSkill >= 0);
        }
        RefreshDetail();
    }

    private static char SlotKey(int slot)
    {
        char key = 'R';
        if (slot == 0)
        {
            key = 'Q';
        }
        else if (slot == 1)
        {
            key = 'E';
        }
        return key;
    }

    private void RefreshDetail()
    {
        builder.Clear();
        if (hoveredNode >= 0)
        {
            skills.Job.AppendNodeText(builder, hoveredNode, skills.GetRank(hoveredNode));
        }
        else if (selectedSkill >= 0)
        {
            builder.Append(skills.Job.GetSkill(selectedSkill).DisplayName).Append(": 아래 Q, E, R 칸을 눌러 장착합니다.");
        }
        else
        {
            builder.Append("레벨이 오를 때마다 포인트를 1 받습니다. 왼쪽 클릭으로 노드를 찍고, 같은 갈래의 앞 노드를 찍어야 다음 노드가 열립니다.\n")
                .Append("해금한 스킬은 클릭한 뒤 아래 칸을 누르거나, 마우스를 올리고 Q, E, R을 눌러 장착합니다. 한 판이 끝나면 초기화됩니다.");
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

    // 해금한 스킬 노드를 누르면 장착할 스킬로 고르고, 아니면 포인트를 써서 찍는다.
    public void OnNodeClicked(int index)
    {
        if (skills != null)
        {
            JobNode node = skills.Job.GetNode(index);
            bool unlockedSkill = node.effect == JobNodeEffect.UnlockSkill && skills.GetRank(index) > 0;
            if (unlockedSkill)
            {
                int skillIndex = Mathf.RoundToInt(node.value);
                if (selectedSkill == skillIndex)
                {
                    selectedSkill = -1;
                }
                else
                {
                    selectedSkill = skillIndex;
                }
                RefreshTree(skills.Job);
            }
            else if (!hub.IsCombatScene)
            {
                hub.Hud.ShowNotice("스킬 트리는 탑에 들어간 뒤에 찍을 수 있습니다");
            }
            else if (skills.CanAllocate(index))
            {
                skills.RequestAllocate(index);
            }
        }
    }

    public void OnSlotClicked(int slot)
    {
        if (skills != null && selectedSkill >= 0)
        {
            skills.RequestEquip(slot, selectedSkill);
            selectedSkill = -1;
        }
    }

    // 트리가 열려 있을 때 Q, E, R(PlayerSkills가 넘겨준다): 마우스를 올린 해금 스킬을 그 칸에 장착한다.
    public void OnSlotKey(int slot)
    {
        if (skills != null && hoveredNode >= 0)
        {
            JobNode node = skills.Job.GetNode(hoveredNode);
            if (node.effect == JobNodeEffect.UnlockSkill && skills.GetRank(hoveredNode) > 0)
            {
                skills.RequestEquip(slot, Mathf.RoundToInt(node.value));
            }
        }
        else if (skills != null && selectedSkill >= 0)
        {
            OnSlotClicked(slot);
        }
    }
}
