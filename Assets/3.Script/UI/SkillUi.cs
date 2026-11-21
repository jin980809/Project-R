using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스킬 표시 줄(화면 아래 가운데: Q, E, R 칸과 레벨·경험치)과 스킬 트리 화면(K).
// 스킬 트리는 계열 이름에서 직업 가지 3개가 뻗어 내려가고, 가지마다 머리 노드에서 다시 여러 갈래로 갈라진다.
// 노드 자리와 연결선은 계열 데이터(JobNode.cell, parent)를 보고 계열이 바뀔 때마다 다시 놓는다.
// 왼쪽 클릭으로 포인트를 써서 노드를 찍는다. 해금한 스킬 노드를 고른 뒤 장착 칸을 누르거나,
// 스킬 노드에 마우스를 올리고 Q, E, R을 누르면 그 칸에 장착한다.
// 해금한 스킬 노드(또는 장착 칸)를 끌어 장착 칸이나 화면 아래 스킬 줄에 놓아도 장착하고, 장착 칸을 끌어 칸 밖에 놓으면 비운다.
public class SkillUi : MonoBehaviour
{
    [SerializeField] private GameHub hub;

    [Header("스킬 표시 줄")]
    [SerializeField] private Image[] skillFrames;
    [Tooltip("재사용 대기 중에 칸을 덮는 어두운 그림(아래에서 위로 채움)")]
    [SerializeField] private Image[] skillFills;
    [SerializeField] private TMP_Text[] skillLabels;
    [Tooltip("스킬 칸 왼쪽 위 키 글자(키 설정에 따라 바뀐다)")]
    [SerializeField] private TMP_Text[] skillKeyLabels;
    [SerializeField] private Image[] skillIcons;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private Image xpFill;
    [Tooltip("로비에서만 보이는 시작 계열 목록(숫자 키)")]
    [SerializeField] private TMP_Text jobListLabel;
    [SerializeField] private Color lockedSkillColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private float unlockedSkillAlpha = 0.75f;
    [Tooltip("SkillFx 순서의 스킬 아이콘")]
    [SerializeField] private Sprite[] fxIcons;

    [Header("스킬 트리")]
    [SerializeField] private GameObject treePanel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text rootLabel;
    [Tooltip("직업 가지 머리(이름, 쓴 포인트, 각성). index는 ClassDefinition.MaxNodes + 직업 번호")]
    [SerializeField] private SkillNodeView[] jobViews;
    [Tooltip("계열 이름에서 직업 가지 머리로 뻗는 선(가지마다 하나)")]
    [SerializeField] private Image[] jobLinks;
    [SerializeField] private SkillNodeView[] nodeViews;
    [Tooltip("노드 i로 들어오는 연결선(부모 노드 또는 직업 가지 머리에서)")]
    [SerializeField] private Image[] nodeLinks;
    [SerializeField] private SkillNodeView[] slotViews;
    [SerializeField] private TMP_Text detailLabel;
    [Tooltip("던전에서 골드를 내고 트리를 초기화하는 버튼(로비에서는 숨긴다)")]
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text resetLabel;
    [Tooltip("노드 칸 (0, 0)의 왼쪽 위(패널 왼쪽 위 기준, 아래로 +)")]
    [SerializeField] private Vector2 cellOrigin = new Vector2(24f, 196f);
    [Tooltip("노드 칸 사이 간격(열, 행)")]
    [SerializeField] private Vector2 cellPitch = new Vector2(106f, 76f);
    [SerializeField] private Vector2 nodeSize = new Vector2(98f, 70f);
    [Tooltip("끌고 있는 스킬 그림(트리 패널 맨 위에 있고 마우스를 따라간다)")]
    [SerializeField] private Image dragIcon;
    [Tooltip("직업 가지 머리 상자의 아래쪽 끝 y(머리 노드로 내려가는 선이 시작하는 곳)")]
    [SerializeField] private float jobBottom = 170f;
    [SerializeField] private float linkWidth = 4f;
    [SerializeField] private Color lockedNodeColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    [SerializeField] private Color availableNodeColor = new Color(0.42f, 0.42f, 0.48f, 1f);
    [SerializeField] private Color lockedLinkColor = new Color(0.3f, 0.3f, 0.32f, 1f);
    [SerializeField] private Color awakenedColor = new Color(0.85f, 0.65f, 0.15f, 1f);
    [SerializeField] private Color[] branchColors =
    {
        new Color(0.8f, 0.35f, 0.25f, 1f), new Color(0.25f, 0.5f, 0.85f, 1f), new Color(0.35f, 0.7f, 0.35f, 1f)
    };

    private readonly StringBuilder builder = new StringBuilder(256);
    private PlayerSkills skills;
    // 마우스를 올린 노드(ClassDefinition.MaxNodes 이상이면 직업 가지 머리)
    private int hoveredNode = -1;
    // 장착하려고 고른 스킬 번호(-1이면 없음)
    private int selectedSkill = -1;
    // 끌고 있는 스킬 번호와 끌어 온 장착 칸(노드에서 끌었으면 -1)
    private int dragSkill = -1;
    private int dragFromSlot = -1;
    private int lastRevision = -1;
    private int lastClass = -1;
    private int lastLevel = -1;
    private int lastXp = -1;
    private int lastPoints = -1;
    private int lastKeysRevision = -1;
    // 노드 자리를 놓은 계열(-1이면 아직)
    private int laidOutClass = -1;

    private System.Action resetYes;

    public bool IsTreeOpen => treePanel.activeSelf;

    private void Awake()
    {
        resetButton.onClick.AddListener(OnClickReset);
        resetYes = ConfirmReset;
    }

    private void OnDestroy()
    {
        resetButton.onClick.RemoveListener(OnClickReset);
    }

    private void OnClickReset()
    {
        if (skills != null && hub.ConfirmUi != null)
        {
            hub.ConfirmUi.Show("스킬 트리를 초기화할까요?\n<size=70%>골드 " + skills.ResetTreeCost
                + "  ·  찍은 포인트를 모두 돌려받고 장착한 스킬이 빠집니다</size>", "초기화", resetYes, null);
        }
    }

    private void ConfirmReset()
    {
        if (skills != null)
        {
            skills.RequestResetTree();
        }
    }

    public void Bind(PlayerSkills localSkills)
    {
        skills = localSkills;
        lastRevision = -1;
        laidOutClass = -1;
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
            // 트리는 화면 대부분을 덮으므로 가방·시설 창보다 위에 그린다.
            transform.SetAsLastSibling();
            selectedSkill = -1;
            lastRevision = -1;
        }
    }

    public void CloseTree()
    {
        hoveredNode = -1;
        selectedSkill = -1;
        CancelDrag();
        treePanel.SetActive(false);
    }

    private Sprite GetIcon(SkillDefinition skill)
    {
        Sprite sprite = skill.Icon;
        int index = (int)skill.Fx;
        if (sprite == null && fxIcons != null && index < fxIcons.Length)
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

            int keysRevision = 0;
            if (hub.LocalInput != null)
            {
                keysRevision = hub.LocalInput.KeysRevision;
            }
            bool changed = skills.Revision != lastRevision || skills.ClassIndex != lastClass || skills.Level != lastLevel
                || skills.Xp != lastXp || skills.SkillPoints != lastPoints || keysRevision != lastKeysRevision;
            if (changed)
            {
                if (keysRevision != lastKeysRevision)
                {
                    lastKeysRevision = keysRevision;
                    for (int i = 0; i < skillKeyLabels.Length; i++)
                    {
                        skillKeyLabels[i].text = hub.GetKeyName(GameInput.KeySkill1 + i, skillKeyLabels[i].text);
                    }
                }
                if (skills.ClassIndex != lastClass)
                {
                    selectedSkill = -1;
                }
                lastRevision = skills.Revision;
                lastClass = skills.ClassIndex;
                lastLevel = skills.Level;
                lastXp = skills.Xp;
                lastPoints = skills.SkillPoints;
                Refresh();
            }
        }
    }

    private void Refresh()
    {
        ClassDefinition definition = skills.Class;

        builder.Clear();
        builder.Append("Lv ").Append(skills.Level).Append("  ").Append(definition.DisplayName);
        if (skills.SkillPoints > 0)
        {
            builder.Append("    포인트 ").Append(skills.SkillPoints).Append(" (").Append(hub.GetKeyName(GameInput.KeySkillTree, "K")).Append(')');
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
                SkillDefinition skill = definition.GetSkill(skillIndex);
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
            RefreshTree(definition);
        }
    }

    // 로비: "시작 계열  1 전사  2 사수  3 마법사", 지금 계열은 노란색
    private void RefreshJobList()
    {
        bool lobby = !hub.IsCombatScene;
        jobListLabel.gameObject.SetActive(lobby);
        if (lobby)
        {
            // 계열은 숫자 키 대신 전직 교관(로비 NPC)에게서 바꾼다.
            builder.Clear();
            builder.Append("시작 계열  <color=#FFD24A>").Append(skills.GetClassName(skills.ClassIndex))
                .Append("</color>   <size=15><color=#A8A8A8>전직 교관에게서 바꿀 수 있습니다</color></size>");
            jobListLabel.SetText(builder);
        }
    }

    private Vector2 CellTopLeft(Vector2 cell)
    {
        return cellOrigin + new Vector2(cell.x * cellPitch.x, cell.y * cellPitch.y);
    }

    // 직업 가지 머리 상자의 아래쪽 가운데(가지마다 4열을 쓰고, 머리는 그 가운데)
    private Vector2 JobBottomCenter(int job)
    {
        float center = cellOrigin.x + (job * 4f + 1.5f) * cellPitch.x + nodeSize.x * 0.5f;
        return new Vector2(center, jobBottom);
    }

    // 패널 왼쪽 위 기준(아래로 +)의 두 점을 잇는 선. 선 그림은 왼쪽 가운데가 기준점이다.
    private void PlaceLink(Image link, Vector2 from, Vector2 to)
    {
        RectTransform rect = link.rectTransform;
        Vector2 delta = to - from;
        rect.anchoredPosition = new Vector2(from.x, -from.y);
        rect.sizeDelta = new Vector2(delta.magnitude, linkWidth);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // 계열이 바뀌면 노드와 연결선을 그 계열의 자리에 놓는다(쓰지 않는 칸은 숨긴다).
    private void LayoutTree(ClassDefinition definition)
    {
        laidOutClass = skills.ClassIndex;
        for (int i = 0; i < nodeViews.Length; i++)
        {
            bool used = i < definition.NodeCount;
            nodeViews[i].SetVisible(used);
            nodeLinks[i].gameObject.SetActive(used);
            if (used)
            {
                JobNode node = definition.GetNode(i);
                Vector2 topLeft = CellTopLeft(node.cell);
                nodeViews[i].Place(topLeft);
                Vector2 to = topLeft + new Vector2(nodeSize.x * 0.5f, 0f);
                Vector2 from = JobBottomCenter(node.job);
                if (node.parent >= 0)
                {
                    from = CellTopLeft(definition.GetNode(node.parent).cell) + new Vector2(nodeSize.x * 0.5f, nodeSize.y);
                }
                PlaceLink(nodeLinks[i], from, to);
            }
        }
    }

    private void RefreshTree(ClassDefinition definition)
    {
        if (laidOutClass != skills.ClassIndex)
        {
            LayoutTree(definition);
        }
        builder.Clear();
        builder.Append("스킬 트리     남은 포인트 ").Append(skills.SkillPoints)
            .Append("     <size=17><color=#A8A8A8>계열 무기라면 무엇을 들어도 배운 스킬을 모두 씁니다</color></size>");
        titleLabel.SetText(builder);
        rootLabel.SetText(definition.DisplayName);
        bool dungeon = hub.IsCombatScene;
        resetButton.gameObject.SetActive(dungeon);
        if (dungeon)
        {
            builder.Clear();
            builder.Append("초기화  (골드 ").Append(skills.ResetTreeCost).Append(')');
            resetLabel.SetText(builder);
            resetButton.interactable = skills.HasSpentPoints;
        }

        for (int job = 0; job < jobViews.Length; job++)
        {
            JobBranch branch = definition.GetJob(job);
            int spent = skills.GetJobSpent(job);
            bool awakened = skills.IsJobAwakened(job);
            Color color = lockedNodeColor;
            Color linkColor = lockedLinkColor;
            if (awakened)
            {
                color = awakenedColor;
                linkColor = awakenedColor;
            }
            else if (spent > 0)
            {
                color = branchColors[job];
                linkColor = branchColors[job];
            }
            jobLinks[job].color = linkColor;
            builder.Clear();
            builder.Append(branch.displayName).Append(" <size=15>(").Append(branch.weaponText).Append(")</size>\n<size=15>");
            if (awakened)
            {
                builder.Append("각성");
            }
            else
            {
                builder.Append(spent).Append('/').Append(definition.GetJobCost(job));
            }
            builder.Append("</size>");
            jobViews[job].Show(builder, color, null, Color.white, false);
        }

        for (int i = 0; i < definition.NodeCount && i < nodeViews.Length; i++)
        {
            JobNode node = definition.GetNode(i);
            int rank = skills.GetRank(i);
            Color color = lockedNodeColor;
            Color linkColor = lockedLinkColor;
            if (rank > 0)
            {
                color = branchColors[node.job];
                linkColor = branchColors[node.job];
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
                SkillDefinition skill = definition.GetSkill(skillIndex);
                icon = GetIcon(skill);
                iconColor = GetIconColor(skill);
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
                    builder.Append(" [").Append(SlotKey(slot)).Append(']');
                }
            }
            else
            {
                builder.Append(node.displayName).Append("\n<size=12><color=#C8C8C8>");
                definition.AppendNodeShort(builder, i);
                builder.Append("</color></size>");
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
                SkillDefinition skill = definition.GetSkill(skillIndex);
                builder.Append(skill.DisplayName);
                icon = GetIcon(skill);
                iconColor = GetIconColor(skill);
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

    private char SlotKey(int slot)
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
        ClassDefinition definition = skills.Class;
        if (hoveredNode >= ClassDefinition.MaxNodes)
        {
            int job = hoveredNode - ClassDefinition.MaxNodes;
            definition.AppendJobText(builder, job, skills.GetJobSpent(job), skills.IsJobAwakened(job));
        }
        else if (hoveredNode >= 0 && hoveredNode < definition.NodeCount)
        {
            definition.AppendNodeText(builder, hoveredNode, skills.GetRank(hoveredNode));
        }
        else if (selectedSkill >= 0)
        {
            builder.Append(definition.GetSkill(selectedSkill).DisplayName).Append(": 왼쪽 Q, E, R 칸을 눌러 장착합니다.");
        }
        else
        {
            builder.Append("레벨이 오를 때마다 포인트를 1 받습니다. 왼쪽 클릭으로 노드를 찍고, 위의 노드를 찍어야 아래로 이어진 노드가 열립니다.\n")
                .Append("직업 가지 여러 개를 섞어 찍을 수 있고, 한 가지를 전부 찍으면 각성합니다(직업 이름에 마우스를 올리면 효과).\n")
                .Append("해금한 스킬은 장착 칸(또는 화면 아래 스킬 줄)으로 끌어 놓거나, 클릭한 뒤 장착 칸을 누르거나, 마우스를 올리고 Q, E, R을 눌러 장착합니다. 장착 칸을 끌어 밖에 놓으면 뺍니다. 한 판이 끝나면 초기화됩니다.");
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

    // 해금한 스킬 노드를 누르면 장착할 스킬로 고르고, 아니면 포인트를 써서 찍는다(직업 가지 머리는 누를 수 없다).
    public void OnNodeClicked(int index)
    {
        if (skills != null && index < skills.Class.NodeCount)
        {
            JobNode node = skills.Class.GetNode(index);
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
                RefreshTree(skills.Class);
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

    // 스킬마다 그린 아이콘은 제 색 그대로, 예전 이펙트 종류 아이콘(흰 그림)은 스킬 색으로 칠한다.
    private Color GetIconColor(SkillDefinition skill)
    {
        Color color = skill.FxColor;
        if (skill.Icon != null)
        {
            color = Color.white;
        }
        return color;
    }

    // ------------------------------------------------------------ 끌어 놓기

    public void OnViewBeginDrag(SkillNodeView view, PointerEventData eventData)
    {
        int skillIndex = -1;
        if (skills != null)
        {
            if (view.IsSlot)
            {
                skillIndex = skills.GetSlotSkill(view.Index);
            }
            else if (view.Index < skills.Class.NodeCount)
            {
                JobNode node = skills.Class.GetNode(view.Index);
                if (node.effect == JobNodeEffect.UnlockSkill && skills.GetRank(view.Index) > 0)
                {
                    skillIndex = Mathf.RoundToInt(node.value);
                }
            }
        }
        if (skillIndex >= 0)
        {
            SkillDefinition skill = skills.Class.GetSkill(skillIndex);
            dragSkill = skillIndex;
            dragFromSlot = -1;
            if (view.IsSlot)
            {
                dragFromSlot = view.Index;
            }
            dragIcon.sprite = GetIcon(skill);
            dragIcon.color = GetIconColor(skill);
            dragIcon.enabled = true;
            dragIcon.transform.SetAsLastSibling();
            MoveDragIcon(eventData);
        }
    }

    public void OnViewDrag(PointerEventData eventData)
    {
        if (dragSkill >= 0)
        {
            MoveDragIcon(eventData);
        }
    }

    public void OnViewEndDrag(PointerEventData eventData)
    {
        if (dragSkill >= 0 && skills != null)
        {
            int targetSlot = FindDropSlot(eventData.pointerCurrentRaycast.gameObject, eventData.position);
            if (targetSlot >= 0)
            {
                skills.RequestEquip(targetSlot, dragSkill);
            }
            else if (dragFromSlot >= 0)
            {
                skills.RequestUnequipSlot(dragFromSlot);
            }
            selectedSkill = -1;
        }
        CancelDrag();
    }

    // 놓은 곳의 장착 칸 번호(트리 아래 장착 칸 또는 화면 아래 스킬 줄). 칸이 아니면 -1.
    // 스킬 줄은 입력을 받지 않는 그림이라(전투 클릭을 막지 않게) 화면 위치로 판단한다.
    private int FindDropSlot(GameObject target, Vector2 screenPosition)
    {
        int slot = -1;
        if (target != null)
        {
            SkillNodeView view = target.GetComponentInParent<SkillNodeView>();
            if (view != null && view.IsSlot)
            {
                slot = view.Index;
            }
        }
        for (int i = 0; i < skillFrames.Length && slot < 0; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(skillFrames[i].rectTransform, screenPosition, null))
            {
                slot = i;
            }
        }
        return slot;
    }

    private void MoveDragIcon(PointerEventData eventData)
    {
        RectTransform parent = (RectTransform)dragIcon.transform.parent;
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out local))
        {
            dragIcon.rectTransform.localPosition = local;
        }
    }

    private void CancelDrag()
    {
        dragSkill = -1;
        dragFromSlot = -1;
        if (dragIcon != null && dragIcon.enabled)
        {
            dragIcon.enabled = false;
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
        if (skills != null && hoveredNode >= 0 && hoveredNode < skills.Class.NodeCount)
        {
            JobNode node = skills.Class.GetNode(hoveredNode);
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
