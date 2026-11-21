using System.Text;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로비 마을의 시설 화면: 해금 상점(영혼석으로 영구 강화), 도서관(도감: 처치한 몬스터와 얻어 본 장비).
// 시설 가까이에서 F로 열고, 같은 패널을 시설마다 다르게 쓴다. 모든 값은 MetaProgress(기기 저장)에 있다.
// 미용실(BarberStation)은 위치만 여기서 찾고 화면은 CustomizeUi가 맡는다. 설정은 Esc 메뉴로 옮겼다.
// 해금 상점은 능력치판(9종 x 5단계, 단계마다 비싸진다)이고, 초기화하면 쓴 영혼석의 80%를 돌려받는다.
// 전직 교관(ClassStation)에서 시작 계열을 고른다. 고른 계열은 기기에 저장해 다음 판에도 쓴다.
public class MetaUi : MonoBehaviour
{
    private const int TabUnlock = 0;
    private const int TabCodex = 1;
    public const int BarberStation = 2;
    public const int ClassStation = 3;

    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [Tooltip("능력치판 줄(MetaProgress.UnlockCount개). 전직 교관에서는 앞의 계열 수만큼 계열 버튼으로 쓴다")]
    [SerializeField] private Button[] actionButtons;
    [SerializeField] private TMP_Text[] actionLabels;
    [Tooltip("능력치판 초기화")]
    [SerializeField] private Button resetButton;
    [SerializeField] private TMP_Text resetLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Color currentClassColor = new Color(0.75f, 0.55f, 0.15f);
    [SerializeField] private Color buttonColor = new Color(0.3f, 0.45f, 0.75f);

    [Header("시설 위치(해금 상점, 도서관, 미용실, 전직 교관 순서)")]
    [SerializeField] private Vector2[] stationPositions;
    [SerializeField] private string[] stationNames = { "해금 상점", "도서관", "미용실", "전직 교관" };
    [SerializeField] private float stationRange = 1.4f;

    [Header("능력치판(MetaProgress.Unlock 순서, 단계마다 오르는 양)")]
    [SerializeField] private string[] unlockNames =
    {
        "최대 체력 +10", "포션 +1", "시작 골드 +30", "공격력 +3%", "받는 피해 -3%",
        "이동 속도 +2%", "치명타 확률 +2%", "최대 마나 +6", "재사용 대기 -3%"
    };

    [Header("도감")]
    [SerializeField] private string[] monsterNames = { "슬라임", "박쥐", "독버섯", "타락한 모험가", "불꽃 슬라임", "서리 박쥐", "폭탄 버섯" };
    [SerializeField] private string[] slotNames = { "무기", "방패", "투구", "갑옷" };
    [SerializeField] private string[] rarityNames = { "일반", "희귀", "영웅", "전설" };

    private readonly StringBuilder builder = new StringBuilder(512);
    private int tab;
    private System.Action resetYes;
    // 전직 교관 화면에 그린 계열(바뀌면 다시 그린다)
    private int shownClass = -1;

    public bool IsOpen => panel.activeSelf;

    private void Awake()
    {
        for (int i = 0; i < actionButtons.Length; i++)
        {
            int index = i;
            actionButtons[i].onClick.AddListener(() => OnAction(index));
        }
        resetButton.onClick.AddListener(OnClickReset);
        closeButton.onClick.AddListener(Close);
        resetYes = ResetUnlocks;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < actionButtons.Length; i++)
        {
            actionButtons[i].onClick.RemoveAllListeners();
        }
        resetButton.onClick.RemoveListener(OnClickReset);
        closeButton.onClick.RemoveListener(Close);
    }

    // 그 위치에서 쓸 수 있는 시설 번호. 없으면 -1.
    public int FindStation(Vector2 position)
    {
        int found = -1;
        float rangeSqr = stationRange * stationRange;
        for (int i = 0; i < stationPositions.Length && found < 0; i++)
        {
            if ((stationPositions[i] - position).sqrMagnitude <= rangeSqr)
            {
                found = i;
            }
        }
        return found;
    }

    public string GetStationName(int index)
    {
        return stationNames[index];
    }

    // 같은 시설에서 다시 누르면 닫힌다.
    public void Toggle(int station)
    {
        if (panel.activeSelf && tab == station)
        {
            Close();
        }
        else
        {
            tab = station;
            panel.SetActive(true);
            Refresh();
        }
    }

    public void Close()
    {
        if (panel.activeSelf)
        {
            panel.SetActive(false);
        }
        if (hub.ConfirmUi != null && hub.ConfirmUi.IsShowing(resetYes))
        {
            hub.ConfirmUi.Hide();
        }
    }

    private void Update()
    {
        // 계열을 고르면 서버에서 값이 돌아온 뒤 다시 그린다.
        if (panel.activeSelf && tab == ClassStation)
        {
            PlayerSkills skills = GetLocalSkills();
            if (skills != null && skills.ClassIndex != shownClass)
            {
                Refresh();
            }
        }
    }

    private PlayerSkills GetLocalSkills()
    {
        PlayerSkills skills = null;
        if (NetworkClient.localPlayer != null)
        {
            NetworkClient.localPlayer.TryGetComponent(out skills);
        }
        return skills;
    }

    private void OnAction(int index)
    {
        MetaProgress meta = hub.Meta;
        if (tab == TabUnlock)
        {
            int level = meta.GetUnlockLevel(index);
            if (level >= MetaProgress.MaxUnlockLevel)
            {
                hub.Hud.ShowNotice("이미 최대 단계입니다");
            }
            else if (meta.TryBuyUnlock(index, meta.GetUnlockCost(index)))
            {
                meta.Save();
            }
            else
            {
                hub.Hud.ShowNotice("영혼석이 부족합니다");
            }
        }
        else if (tab == ClassStation)
        {
            PlayerSkills skills = GetLocalSkills();
            if (skills != null && index < PlayerSkills.ClassCount && index != skills.ClassIndex)
            {
                skills.RequestStartClass(index);
                hub.Hud.ShowNotice(skills.GetClassName(index) + "(으)로 전직했습니다");
            }
        }
        Refresh();
    }

    private void OnClickReset()
    {
        MetaProgress meta = hub.Meta;
        if (meta.GetSpentSoulstones() <= 0)
        {
            hub.Hud.ShowNotice("되돌릴 능력치가 없습니다");
        }
        else if (hub.ConfirmUi != null)
        {
            hub.ConfirmUi.Show("능력치를 모두 초기화할까요?\n<size=70%>쓴 영혼석 " + meta.GetSpentSoulstones() + " 가운데 "
                + meta.GetResetRefund() + "만 돌려받습니다</size>", "초기화", resetYes, null);
        }
    }

    private void ResetUnlocks()
    {
        int refund = hub.Meta.ResetUnlocks();
        hub.Meta.Save();
        hub.Hud.ShowNotice("능력치를 초기화했습니다 (영혼석 +" + refund + ")");
        if (panel.activeSelf)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        MetaProgress meta = hub.Meta;
        titleLabel.text = stationNames[tab];
        // 화면마다 필요한 만큼만 패널 높이를 쓴다(위쪽 가운데 기준으로 줄인다).
        RectTransform panelRect = (RectTransform)panel.transform;
        float height = 560f;
        if (tab == TabUnlock)
        {
            height = 720f;
        }
        else if (tab == ClassStation)
        {
            height = 340f;
        }
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, height);
        int buttonCount = 0;
        if (tab == TabUnlock)
        {
            buttonCount = MetaProgress.UnlockCount;
        }
        else if (tab == ClassStation)
        {
            buttonCount = PlayerSkills.ClassCount;
        }
        for (int i = 0; i < actionButtons.Length; i++)
        {
            actionButtons[i].gameObject.SetActive(i < buttonCount);
            actionButtons[i].interactable = true;
            actionButtons[i].image.color = buttonColor;
        }
        resetButton.gameObject.SetActive(tab == TabUnlock);

        if (tab == TabUnlock)
        {
            for (int i = 0; i < MetaProgress.UnlockCount; i++)
            {
                int level = meta.GetUnlockLevel(i);
                builder.Clear();
                builder.Append(unlockNames[i]).Append("   Lv ").Append(level).Append('/').Append(MetaProgress.MaxUnlockLevel).Append("   ");
                if (level >= MetaProgress.MaxUnlockLevel)
                {
                    builder.Append("최대");
                    actionButtons[i].interactable = false;
                }
                else
                {
                    int cost = meta.GetUnlockCost(i);
                    builder.Append("영혼석 ").Append(cost);
                    actionButtons[i].interactable = meta.Soulstones >= cost;
                }
                actionLabels[i].SetText(builder);
            }
            int refund = meta.GetResetRefund();
            builder.Clear();
            builder.Append("초기화  (영혼석 +").Append(refund).Append(')');
            resetLabel.SetText(builder);
            resetButton.interactable = refund > 0;
            builder.Clear();
            builder.Append("보유 영혼석  ").Append(meta.Soulstones).Append('\n');
            builder.Append("<size=80%>단계를 올린 능력치는 다음 판부터 계속 적용됩니다.\n초기화하면 쓴 영혼석의 ")
                .Append(MetaProgress.ResetRefundPercent).Append("%만 돌려받습니다.</size>");
        }
        else if (tab == ClassStation)
        {
            PlayerSkills skills = GetLocalSkills();
            builder.Clear();
            if (skills != null)
            {
                shownClass = skills.ClassIndex;
                for (int i = 0; i < PlayerSkills.ClassCount; i++)
                {
                    bool current = i == skills.ClassIndex;
                    actionLabels[i].text = skills.GetClassName(i);
                    if (current)
                    {
                        actionButtons[i].image.color = currentClassColor;
                        actionLabels[i].text = skills.GetClassName(i) + "  (지금)";
                    }
                }
                builder.Append("지금 시작 계열  <color=#FFD24A>").Append(skills.GetClassName(skills.ClassIndex)).Append("</color>\n");
            }
            builder.Append("<size=80%>고른 계열의 시작 무기를 들고 탑에 오릅니다.\n고른 계열은 다음 판에도 기억합니다.</size>");
        }
        else if (tab == TabCodex)
        {
            builder.Clear();
            builder.Append("처치한 몬스터\n");
            for (int i = 0; i < monsterNames.Length; i++)
            {
                int kills = meta.GetMonsterKills(i);
                if (kills > 0)
                {
                    builder.Append("  ").Append(monsterNames[i]).Append("    ").Append(kills).Append("마리\n");
                }
                else
                {
                    builder.Append("  ???\n");
                }
            }
            builder.Append("\n얻어 본 장비\n");
            for (int slot = 0; slot < MetaProgress.ItemSlotKinds; slot++)
            {
                builder.Append("  ").Append(slotNames[slot]).Append("    ");
                for (int rarity = 0; rarity < MetaProgress.RarityKinds; rarity++)
                {
                    if (meta.IsItemFound(slot, rarity))
                    {
                        builder.Append(rarityNames[rarity]).Append("  ");
                    }
                    else
                    {
                        builder.Append("--  ");
                    }
                }
                builder.Append('\n');
            }
        }
        bodyLabel.SetText(builder);
    }
}
