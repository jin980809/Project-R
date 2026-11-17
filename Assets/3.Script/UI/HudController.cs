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
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private float noticeDuration = 1.6f;
    [SerializeField] private Color pipReadyColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color pipEmptyColor = new Color(1f, 1f, 1f, 0.2f);

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
    private int lastPotions = -1;
    private int soulstones;
    private float noticeTimer;
    private MonsterController shownBoss;
    private int lastBossHp = -1;
    private bool lastBossEnraged;
    private int lastStatusMask = -1;

    public void Bind(PlayerController localPlayer)
    {
        player = localPlayer;
        soulstones = -1;
        lastRevision = -1;
        lastHp = -1;
        lastMana = -1;
        lastRollCharges = -1;
        lastGold = -1;
        lastStatusMask = -1;
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
            if (inventory.Revision != lastRevision)
            {
                lastRevision = inventory.Revision;
                ItemData weaponItem = inventory.GetEquipped((int)ItemSlot.Weapon);
                builder.Clear();
                if (weaponItem.Exists)
                {
                    inventory.Balance.AppendName(builder, weaponItem);
                    weaponLabel.color = inventory.Balance.GetRarityColor(weaponItem.rarity);
                }
                if (hub.IsCombatScene)
                {
                    builder.Append("   Tab 가방");
                }
                else
                {
                    builder.Append("   1~4 시작 무기 · Tab 가방");
                }
                weaponLabel.SetText(builder);
            }

            if (inventory.Gold != lastGold || inventory.Ore != lastOre || inventory.Potions != lastPotions || hub.Meta.Soulstones != soulstones)
            {
                lastGold = inventory.Gold;
                lastOre = inventory.Ore;
                lastPotions = inventory.Potions;
                soulstones = hub.Meta.Soulstones;
                goldLabel.SetText("{0} G    광석 {1}    포션 {2} (H)    영혼석 {3}", lastGold, lastOre, lastPotions, soulstones);
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

            int statusMask = player.StatusMask;
            if (statusMask != lastStatusMask)
            {
                lastStatusMask = statusMask;
                builder.Clear();
                if ((statusMask & PlayerController.StatusBurn) != 0)
                {
                    builder.Append("화상  ");
                }
                if ((statusMask & PlayerController.StatusBleed) != 0)
                {
                    builder.Append("출혈  ");
                }
                if ((statusMask & PlayerController.StatusSlow) != 0)
                {
                    builder.Append("둔화  ");
                }
                if ((statusMask & PlayerController.StatusWeaken) != 0)
                {
                    builder.Append("약화  ");
                }
                if ((statusMask & PlayerController.StatusStun) != 0)
                {
                    builder.Append("기절  ");
                }
                statusLabel.SetText(builder);
            }
        }

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
