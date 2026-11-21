using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 체력, 마나, 구르기 충전, 층·방 이름, 무기 이름, 골드, 줍기 안내와 알림 표시. 값이 바뀔 때만 UI를 갱신한다.
public class HudController : MonoBehaviour
{
    [SerializeField] private GameHub hub;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image manaFill;
    [SerializeField] private Image[] rollPips;
    [SerializeField] private TMP_Text waveLabel;
    [SerializeField] private TMP_Text weaponLabel;
    [SerializeField] private TMP_Text goldLabel;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private TMP_Text noticeLabel;
    [Header("보스 체력 바(전투 씬에서만, 화면 위 가운데)")]
    [SerializeField] private GameObject bossBarRoot;
    [SerializeField] private Image bossFill;
    [SerializeField] private TMP_Text bossLabel;
    [Header("걸린 상태이상 표시")]
    [Header("포션 칸 2개(화면 아래 가운데, 스킬 칸 오른쪽)")]
    [SerializeField] private Image[] potionIcons;
    [SerializeField] private TMP_Text[] potionCounts;
    [SerializeField] private TMP_Text[] potionKeys;
    [Header("걸린 강화 아이콘(스킬 강화, 물약)")]
    [SerializeField] private GameObject[] buffSlots;
    [SerializeField] private Image[] buffIcons;
    [Tooltip("지난 시간만큼 시계 방향으로 덮이는 어두운 부채꼴(Filled Radial360)")]
    [SerializeField] private Image[] buffGauges;
    [Header("걸린 상태이상 아이콘(화상, 출혈, 둔화, 약화, 기절). 버프와 다른 붉은 테두리 칸")]
    [SerializeField] private GameObject[] debuffSlots;
    [SerializeField] private Image[] debuffIcons;
    [SerializeField] private Image[] debuffGauges;
    [Tooltip("상태 비트 순서의 아이콘")]
    [SerializeField] private Sprite[] statusSprites;
    [Header("차징 스킬 게이지(모으는 동안 캐릭터 머리 위)")]
    [SerializeField] private RectTransform chargeGaugeRoot;
    [SerializeField] private Image chargeGaugeFill;
    [Tooltip("캐릭터 발밑에서 게이지까지(유닛)")]
    [SerializeField] private Vector2 chargeGaugeOffset = new Vector2(0f, 1.75f);
    [SerializeField] private Color chargeFillColor = new Color(0.45f, 0.82f, 1f);
    [Tooltip("다 모으면 이 색과 흰색 사이로 깜박인다")]
    [SerializeField] private Color chargeFullColor = new Color(1f, 0.84f, 0.28f);
    [SerializeField] private float chargeBlinkRate = 12f;
    [Tooltip("남은 시간이 이보다 적으면 아이콘이 깜박인다")]
    [SerializeField] private float buffBlinkTime = 3f;
    [SerializeField] private float noticeDuration = 1.6f;
    [SerializeField] private Color pipReadyColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color pipEmptyColor = new Color(1f, 1f, 1f, 0.2f);

    // 코드로 만드는 화면(유물 선택)이 글꼴을 빌려 쓴다.
    public TMP_Text NoticeLabel => noticeLabel;

    private readonly StringBuilder builder = new StringBuilder(64);
    private PlayerController player;
    private int lastHp = -1;
    private int lastMaxHp = -1;
    private int lastMana = -1;
    private int lastMaxMana = -1;
    private int lastRollCharges = -1;
    private int lastRevision = -1;
    private int lastGold = -1;
    private int lastOre = -1;
    private int lastKeysRevision = -1;
    private readonly PotionKind[] lastQuick = new PotionKind[GameInput.PotionSlotCount];
    private int soulstones;
    private float noticeTimer;
    private MonsterController shownBoss;
    private int lastBossHp = -1;
    private bool lastBossEnraged;
    private Canvas hudCanvas;

    private void Awake()
    {
        hudCanvas = GetComponent<Canvas>();
        if (chargeGaugeRoot != null)
        {
            chargeGaugeRoot.gameObject.SetActive(false);
        }
    }

    public void Bind(PlayerController localPlayer)
    {
        player = localPlayer;
        soulstones = -1;
        lastRevision = -1;
        lastHp = -1;
        lastMana = -1;
        lastRollCharges = -1;
        lastGold = -1;
        lastKeysRevision = -1;
        ClearPrompt();
        noticeLabel.enabled = false;
    }

    public void SetPrompt(StringBuilder text, Color color)
    {
        promptLabel.SetText(text);
        promptLabel.color = color;
        promptLabel.enabled = true;
    }

    public void ClearPrompt()
    {
        promptLabel.enabled = false;
    }

    // 화면 위쪽 가운데 글자(층과 방 이름)
    public void SetTopLabel(StringBuilder text)
    {
        waveLabel.SetText(text);
    }

    public void ShowNotice(string text)
    {
        noticeLabel.text = text;
        noticeLabel.enabled = true;
        noticeTimer = noticeDuration;
    }

    private void Update()
    {
        if (player != null)
        {
            int maxHp = player.MaxHp;
            if (player.Hp != lastHp || maxHp != lastMaxHp)
            {
                lastHp = player.Hp;
                lastMaxHp = maxHp;
                hpFill.fillAmount = (float)lastHp / maxHp;
            }

            int maxMana = player.MaxMana;
            if (player.Mana != lastMana || maxMana != lastMaxMana)
            {
                lastMana = player.Mana;
                lastMaxMana = maxMana;
                manaFill.fillAmount = (float)lastMana / maxMana;
            }

            // 장비가 바뀔 때만 글자를 다시 만든다.
            PlayerInventory inventory = player.Inventory;
            int keysRevision = 0;
            if (player.Input != null)
            {
                keysRevision = player.Input.KeysRevision;
            }
            bool quickChanged = inventory.GetQuickPotion(0) != lastQuick[0] || inventory.GetQuickPotion(1) != lastQuick[1];
            if (inventory.Revision != lastRevision || keysRevision != lastKeysRevision || quickChanged)
            {
                lastRevision = inventory.Revision;
                lastKeysRevision = keysRevision;
                RefreshPotionSlots(inventory);
                ItemData weaponItem = inventory.GetEquipped((int)ItemSlot.Weapon);
                builder.Clear();
                if (weaponItem.Exists)
                {
                    inventory.Balance.AppendName(builder, weaponItem);
                    weaponLabel.color = inventory.Balance.GetRarityColor(weaponItem.rarity);
                }
                string bagKey = hub.GetKeyName(GameInput.KeyInventory, "Tab");
                if (hub.IsCombatScene)
                {
                    builder.Append("   ").Append(bagKey).Append(" 가방");
                }
                else
                {
                    builder.Append("   ").Append(bagKey).Append(" 가방");
                }
                weaponLabel.SetText(builder);
            }

            if (inventory.Gold != lastGold || inventory.Ore != lastOre || hub.Meta.Soulstones != soulstones)
            {
                lastGold = inventory.Gold;
                lastOre = inventory.Ore;
                soulstones = hub.Meta.Soulstones;
                goldLabel.SetText("{0} G    광석 {1}    영혼석 {2}", lastGold, lastOre, soulstones);
            }

            if (player.RollCharges != lastRollCharges)
            {
                lastRollCharges = player.RollCharges;
                for (int i = 0; i < rollPips.Length; i++)
                {
                    if (i < lastRollCharges)
                    {
                        rollPips[i].color = pipReadyColor;
                    }
                    else
                    {
                        rollPips[i].color = pipEmptyColor;
                    }
                }
            }

        }

        if (player != null)
        {
            TickBuffIcons();
            TickDebuffIcons();
        }
        TickChargeGauge();
        TickBossBar();

        if (noticeTimer > 0f)
        {
            noticeTimer -= Time.deltaTime;
            if (noticeTimer <= 0f)
            {
                noticeLabel.enabled = false;
            }
        }
    }

    // 차징 스킬을 모으는 동안 캐릭터 머리 위에 모은 정도를 막대로 보여 준다. 다 모으면 금색으로 깜박인다.
    private void TickChargeGauge()
    {
        if (chargeGaugeRoot != null)
        {
            bool show = player != null && player.IsChargingSkill && hub.MainCamera != null;
            if (chargeGaugeRoot.gameObject.activeSelf != show)
            {
                chargeGaugeRoot.gameObject.SetActive(show);
            }
            if (show)
            {
                float charge = player.SkillCharge01;
                chargeGaugeFill.fillAmount = charge;
                Color color = chargeFillColor;
                if (charge >= 1f)
                {
                    color = chargeFullColor;
                    if (Mathf.Repeat(Time.time * chargeBlinkRate, 2f) < 1f)
                    {
                        color = Color.Lerp(chargeFullColor, Color.white, 0.6f);
                    }
                }
                chargeGaugeFill.color = color;
                Vector3 world = player.transform.position + (Vector3)chargeGaugeOffset;
                Vector3 screen = hub.MainCamera.WorldToScreenPoint(world);
                float scale = 1f;
                if (hudCanvas != null)
                {
                    scale = hudCanvas.scaleFactor;
                }
                chargeGaugeRoot.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale);
            }
        }
    }

    // 걸린 강화(스킬 강화, 힘·신속·정화 물약): 아이콘을 차례로 띄우고, 지난 시간만큼 어두운 부채꼴이 시계 방향으로 덮인다.
    private void TickBuffIcons()
    {
        int shown = 0;
        SkillDefinition buff = player.Skills.ActiveBuffSkill;
        if (buff != null)
        {
            shown = ShowBuffIcon(shown, buff.Icon, player.Skills.BuffRemaining, player.Skills.BuffDuration);
        }
        shown = ShowTonicIcon(shown, PlayerController.TonicPower, PotionKind.Power);
        shown = ShowTonicIcon(shown, PlayerController.TonicSwift, PotionKind.Swift);
        shown = ShowTonicIcon(shown, PlayerController.TonicCleanse, PotionKind.Cleanse);
        for (int i = shown; i < buffSlots.Length; i++)
        {
            if (buffSlots[i].activeSelf)
            {
                buffSlots[i].SetActive(false);
            }
        }
    }

    // 걸린 상태이상: 붉은 테두리 칸에 아이콘을 차례로 띄우고, 지난 시간만큼 어두운 부채꼴이 덮인다.
    private void TickDebuffIcons()
    {
        int shown = 0;
        int statusMask = player.StatusMask;
        for (int i = 0; i < PlayerController.StatusCount && i < statusSprites.Length; i++)
        {
            if ((statusMask & (1 << i)) != 0)
            {
                shown = ShowEffectIcon(debuffSlots, debuffIcons, debuffGauges, shown, statusSprites[i], player.GetStatusRemaining(i), player.GetStatusDuration(i));
            }
        }
        for (int i = shown; i < debuffSlots.Length; i++)
        {
            if (debuffSlots[i].activeSelf)
            {
                debuffSlots[i].SetActive(false);
            }
        }
    }

    private int ShowTonicIcon(int index, byte tonic, PotionKind kind)
    {
        int next = index;
        if ((player.TonicMask & tonic) != 0)
        {
            next = ShowBuffIcon(index, player.Inventory.Balance.GetPotionIcon(kind), player.GetTonicRemaining(tonic), player.GetTonicDuration(tonic));
        }
        return next;
    }

    private int ShowBuffIcon(int index, Sprite icon, float remaining, float duration)
    {
        return ShowEffectIcon(buffSlots, buffIcons, buffGauges, index, icon, remaining, duration);
    }

    // index 칸에 아이콘을 띄운다. 띄웠으면 다음 칸 번호를 돌려준다.
    private int ShowEffectIcon(GameObject[] slots, Image[] icons, Image[] gauges, int index, Sprite icon, float remaining, float duration)
    {
        int next = index;
        if (index < slots.Length && remaining > 0f)
        {
            if (!slots[index].activeSelf)
            {
                slots[index].SetActive(true);
            }
            if (icons[index].sprite != icon)
            {
                icons[index].sprite = icon;
            }
            float ratio = 0f;
            if (duration > 0.01f)
            {
                ratio = Mathf.Clamp01(remaining / duration);
            }
            gauges[index].fillAmount = 1f - ratio;
            // 끝나기 직전에는 아이콘이 깜박인다.
            Color color = Color.white;
            if (remaining < buffBlinkTime)
            {
                color.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            }
            icons[index].color = color;
            next = index + 1;
        }
        return next;
    }

    // 포션 칸: 등록한 포션의 아이콘, 가방에 든 개수, 키 이름
    private void RefreshPotionSlots(PlayerInventory inventory)
    {
        for (int i = 0; i < potionIcons.Length; i++)
        {
            PotionKind kind = inventory.GetQuickPotion(i);
            lastQuick[i] = kind;
            int count = 0;
            if (kind != PotionKind.None)
            {
                count = inventory.CountPotions(kind);
                potionIcons[i].sprite = inventory.Balance.GetPotionIcon(kind);
                potionIcons[i].enabled = true;
            }
            else
            {
                potionIcons[i].enabled = false;
            }
            float alpha = 1f;
            if (count == 0)
            {
                alpha = 0.3f;
            }
            potionIcons[i].color = new Color(1f, 1f, 1f, alpha);
            if (kind != PotionKind.None)
            {
                potionCounts[i].SetText("{0}", count);
            }
            else
            {
                potionCounts[i].text = string.Empty;
            }
            potionKeys[i].text = hub.GetKeyName(GameInput.KeyPotion1 + i, (i + 1).ToString());
        }
    }

    // 보스가 살아 있는 동안 화면 위에 이름과 체력을 보여 준다.
    private void TickBossBar()
    {
        MonsterController boss = null;
        if (hub.MonsterDirector != null)
        {
            boss = hub.MonsterDirector.CurrentBoss;
            if (boss != null && boss.Hp <= 0)
            {
                boss = null;
            }
        }
        if (boss != shownBoss)
        {
            shownBoss = boss;
            lastBossHp = -1;
            bossBarRoot.SetActive(boss != null);
        }
        if (boss != null && (boss.Hp != lastBossHp || boss.Enraged != lastBossEnraged))
        {
            lastBossHp = boss.Hp;
            lastBossEnraged = boss.Enraged;
            bossFill.fillAmount = (float)boss.Hp / boss.MaxHp;
            builder.Clear();
            builder.Append(boss.DisplayName);
            if (boss.Enraged)
            {
                builder.Append("  (격노)");
            }
            bossLabel.SetText(builder);
        }
    }
}
