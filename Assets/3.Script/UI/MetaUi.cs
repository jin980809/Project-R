using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로비 마을의 세 시설 화면: 해금 상인(영혼석으로 영구 강화), 도감(처치한 몬스터와 얻어 본 장비), 설정.
// 시설 가까이에서 F로 열고, 같은 패널을 시설마다 다르게 쓴다. 모든 값은 MetaProgress(기기 저장)에 있다.
public class MetaUi : MonoBehaviour
{
    private const int TabUnlock = 0;
    private const int TabCodex = 1;
    private const int TabSettings = 2;

    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Button[] actionButtons;
    [SerializeField] private TMP_Text[] actionLabels;
    [SerializeField] private Button closeButton;

    [Header("시설 위치(해금 상인, 도감, 설정 순서)")]
    [SerializeField] private Vector2[] stationPositions;
    [SerializeField] private string[] stationNames = { "해금 상인", "도감", "설정" };
    [SerializeField] private float stationRange = 1.4f;

    [Header("영구 해금(최대 체력, 포션, 시작 골드 순서)")]
    [SerializeField] private string[] unlockNames = { "최대 체력 +10", "포션 +1", "시작 골드 +30" };
    [Tooltip("단계별 비용(영혼석)")]
    [SerializeField] private int[] unlockCosts = { 20, 40, 60 };

    [Header("도감")]
    [SerializeField] private string[] monsterNames = { "슬라임", "박쥐", "독버섯", "타락한 모험가" };
    [SerializeField] private string[] slotNames = { "무기", "방패", "투구", "갑옷" };
    [SerializeField] private string[] rarityNames = { "일반", "희귀", "영웅", "전설" };

    private readonly StringBuilder builder = new StringBuilder(512);
    private int tab;

    public bool IsOpen => panel.activeSelf;

    private void Awake()
    {
        actionButtons[0].onClick.AddListener(OnClickAction0);
        actionButtons[1].onClick.AddListener(OnClickAction1);
        actionButtons[2].onClick.AddListener(OnClickAction2);
        closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        actionButtons[0].onClick.RemoveListener(OnClickAction0);
        actionButtons[1].onClick.RemoveListener(OnClickAction1);
        actionButtons[2].onClick.RemoveListener(OnClickAction2);
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
    }

    private void OnClickAction0()
    {
        OnAction(0);
    }

    private void OnClickAction1()
    {
        OnAction(1);
    }

    private void OnClickAction2()
    {
        OnAction(2);
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
            else if (meta.TryBuyUnlock(index, unlockCosts[level]))
            {
                meta.Save();
            }
            else
            {
                hub.Hud.ShowNotice("영혼석이 부족합니다");
            }
        }
        else if (tab == TabSettings)
        {
            if (index == 0)
            {
                Screen.fullScreen = !Screen.fullScreen;
            }
            else if (index == 1)
            {
                meta.SetShakeEnabled(!meta.ShakeEnabled);
            }
            else
            {
                meta.SetDamageTextEnabled(!meta.DamageTextEnabled);
            }
            meta.Save();
            hub.ApplySettings();
        }
        Refresh();
    }

    private void SetToggleLabel(TMP_Text label, string name, bool value)
    {
        builder.Clear();
        builder.Append(name).Append("  ");
        if (value)
        {
            builder.Append("켜짐");
        }
        else
        {
            builder.Append("꺼짐");
        }
        label.SetText(builder);
    }

    private void Refresh()
    {
        MetaProgress meta = hub.Meta;
        titleLabel.text = stationNames[tab];
        bool showButtons = tab != TabCodex;
        for (int i = 0; i < actionButtons.Length; i++)
        {
            actionButtons[i].gameObject.SetActive(showButtons);
            actionButtons[i].interactable = true;
        }

        if (tab == TabUnlock)
        {
            for (int i = 0; i < MetaProgress.UnlockCount; i++)
            {
                int level = meta.GetUnlockLevel(i);
                builder.Clear();
                builder.Append(unlockNames[i]).Append("  (").Append(level).Append('/').Append(MetaProgress.MaxUnlockLevel).Append(")  ");
                if (level >= MetaProgress.MaxUnlockLevel)
                {
                    builder.Append("최대");
                    actionButtons[i].interactable = false;
                }
                else
                {
                    builder.Append("영혼석 ").Append(unlockCosts[level]);
                    actionButtons[i].interactable = meta.Soulstones >= unlockCosts[level];
                }
                actionLabels[i].SetText(builder);
            }
            builder.Clear();
            builder.Append("보유 영혼석  ").Append(meta.Soulstones).Append("\n\n");
            builder.Append("영혼석은 판이 끝날 때 얻습니다.\n해금한 효과는 다음 판부터 계속 적용됩니다.");
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
        else
        {
            SetToggleLabel(actionLabels[0], "전체 화면", Screen.fullScreen);
            SetToggleLabel(actionLabels[1], "화면 흔들림", meta.ShakeEnabled);
            SetToggleLabel(actionLabels[2], "피해 숫자", meta.DamageTextEnabled);
            builder.Clear();
            builder.Append("버튼을 누르면 켜고 끕니다.\n설정은 이 기기에 저장됩니다.");
        }
        bodyLabel.SetText(builder);
    }
}
