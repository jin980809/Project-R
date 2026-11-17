using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상점과 대장간 화면. 방 종류에 따라 같은 패널을 다르게 쓴다.
// 상점: 진열품 4칸(좌클릭 구매)과 포션 구매. 가방 칸을 우클릭하면 판다.
// 대장간: 장착 4칸 중 하나를 고르고 강화(광석)하거나 옵션을 다시 굴린다(골드).
public class FacilityUi : SlotPanel
{
    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private InventorySlotView[] slots;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Button[] buttons;
    [SerializeField] private TMP_Text[] buttonLabels;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private Color emptyFrameColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color selectedFrameColor = Color.white;
    [SerializeField] private string[] equipNames = { "무기", "보조", "투구", "갑옷" };

    private readonly StringBuilder builder = new StringBuilder(256);
    private PlayerInventory inventory;
    private RoomType mode;
    private int selected = -1;
    private int hoveredIndex = -1;
    private int lastRevision = -1;
    private int lastGold = -1;
    private int lastOre = -1;
    private int lastPotions = -1;

    public bool IsOpen => panel.activeSelf;
    public bool IsShopOpen => panel.activeSelf && mode == RoomType.Shop;

    private void Awake()
    {
        buttons[0].onClick.AddListener(OnClickButton0);
        buttons[1].onClick.AddListener(OnClickButton1);
        buttons[2].onClick.AddListener(OnClickButton2);
    }

    private void OnDestroy()
    {
        buttons[0].onClick.RemoveListener(OnClickButton0);
        buttons[1].onClick.RemoveListener(OnClickButton1);
        buttons[2].onClick.RemoveListener(OnClickButton2);
    }

    public void Bind(PlayerInventory localInventory)
    {
        inventory = localInventory;
    }

    public void Open(RoomType roomType)
    {
        mode = roomType;
        selected = -1;
        hoveredIndex = -1;
        lastRevision = -1;
        panel.SetActive(true);
    }

    public void Close()
    {
        if (panel.activeSelf)
        {
            panel.SetActive(false);
        }
    }

    private void Update()
    {
        if (panel.activeSelf && inventory != null)
        {
            // 방을 옮기면 닫는다.
            if (hub.FloorDirector.CurrentRoomType != mode)
            {
                Close();
            }
            else
            {
                bool changed = inventory.Revision != lastRevision || inventory.Gold != lastGold
                    || inventory.Ore != lastOre || inventory.Potions != lastPotions;
                if (changed)
                {
                    lastRevision = inventory.Revision;
                    lastGold = inventory.Gold;
                    lastOre = inventory.Ore;
                    lastPotions = inventory.Potions;
                    Refresh();
                }
            }
        }
    }

    private ItemData GetSlotItem(int index)
    {
        ItemData item;
        if (mode == RoomType.Shop)
        {
            item = inventory.GetShopItem(index);
        }
        else
        {
            item = inventory.GetEquipped(index);
        }
        return item;
    }

    private void Refresh()
    {
        ItemBalance balance = inventory.Balance;
        bool shop = mode == RoomType.Shop;
        if (shop)
        {
            titleLabel.text = "상점";
        }
        else
        {
            titleLabel.text = "대장간";
        }

        for (int i = 0; i < slots.Length; i++)
        {
            ItemData item = GetSlotItem(i);
            builder.Clear();
            if (item.Exists)
            {
                Color frame = balance.GetRarityColor(item.rarity);
                if (!shop && i == selected)
                {
                    frame = selectedFrameColor;
                }
                slots[i].ShowItem(inventory.GetIcon(item), frame, 1f);
                if (shop)
                {
                    builder.Append(balance.GetBuyPrice(item)).Append(" G");
                }
                else
                {
                    builder.Append(equipNames[i]).Append(" +").Append(item.level);
                }
            }
            else
            {
                slots[i].ShowEmpty(emptyFrameColor);
                if (shop)
                {
                    builder.Append("품절");
                }
                else
                {
                    builder.Append(equipNames[i]);
                }
            }
            slotLabels[i].SetText(builder);
        }

        if (shop)
        {
            builder.Clear();
            builder.Append("포션 구매  ").Append(balance.PotionPrice).Append(" G   (보유 ")
                .Append(inventory.Potions).Append('/').Append(inventory.MaxPotions).Append(')');
            buttonLabels[0].SetText(builder);
            buttons[0].interactable = inventory.Potions < inventory.MaxPotions && inventory.Gold >= balance.PotionPrice;
            buttons[1].gameObject.SetActive(false);
            buttons[2].gameObject.SetActive(false);
        }
        else
        {
            buttons[1].gameObject.SetActive(true);
            buttons[2].gameObject.SetActive(true);
            ItemData item = new ItemData();
            if (selected >= 0)
            {
                item = inventory.GetEquipped(selected);
            }

            builder.Clear();
            if (balance.CanUpgrade(item))
            {
                int cost = balance.GetUpgradeOreCost(item);
                builder.Append("강화  광석 ").Append(cost).Append("   (보유 ").Append(inventory.Ore).Append(')');
                buttons[0].interactable = inventory.Ore >= cost;
            }
            else if (item.Exists)
            {
                builder.Append("강화 완료");
                buttons[0].interactable = false;
            }
            else
            {
                builder.Append("강화할 장비를 고르세요   (광석 ").Append(inventory.Ore).Append(')');
                buttons[0].interactable = false;
            }
            buttonLabels[0].SetText(builder);

            RefreshRerollButton(1, 0, item);
            RefreshRerollButton(2, 1, item);
        }
        RefreshDetail();
    }

    private void RefreshRerollButton(int buttonIndex, int optionIndex, in ItemData item)
    {
        ItemBalance balance = inventory.Balance;
        builder.Clear();
        builder.Append("옵션 ").Append(optionIndex + 1).Append(" 재설정");
        if (balance.HasOption(item, optionIndex))
        {
            int cost = balance.GetRerollCost(item);
            builder.Append("  ").Append(cost).Append(" G");
            buttons[buttonIndex].interactable = inventory.Gold >= cost;
        }
        else
        {
            builder.Append("  (옵션 없음)");
            buttons[buttonIndex].interactable = false;
        }
        buttonLabels[buttonIndex].SetText(builder);
    }

    private void RefreshDetail()
    {
        builder.Clear();
        int index = hoveredIndex;
        if (index < 0 && mode == RoomType.Forge)
        {
            index = selected;
        }
        if (index >= 0)
        {
            inventory.Balance.AppendDescription(builder, GetSlotItem(index), inventory.Weapons);
        }
        else if (mode == RoomType.Shop)
        {
            builder.Append("좌클릭으로 구매합니다.\n가방 칸을 우클릭하면 팝니다.");
        }
        else
        {
            builder.Append("장착한 장비를 골라 강화하거나\n옵션을 다시 굴립니다.");
        }
        detailLabel.SetText(builder);
    }

    public override void OnSlotHover(InventorySlotView view, bool entered)
    {
        if (entered)
        {
            hoveredIndex = view.Index;
        }
        else if (hoveredIndex == view.Index)
        {
            hoveredIndex = -1;
        }
        if (inventory != null)
        {
            RefreshDetail();
        }
    }

    public override void OnSlotClicked(InventorySlotView view, bool rightButton)
    {
        if (inventory != null && !rightButton)
        {
            if (mode == RoomType.Shop)
            {
                inventory.RequestBuy(view.Index);
            }
            else if (inventory.GetEquipped(view.Index).Exists)
            {
                selected = view.Index;
                Refresh();
            }
        }
    }

    private void OnClickButton0()
    {
        if (mode == RoomType.Shop)
        {
            inventory.RequestBuyPotion();
        }
        else if (selected >= 0)
        {
            inventory.RequestUpgrade(selected);
        }
    }

    private void OnClickButton1()
    {
        if (mode == RoomType.Forge && selected >= 0)
        {
            inventory.RequestReroll(selected, 0);
        }
    }

    private void OnClickButton2()
    {
        if (mode == RoomType.Forge && selected >= 0)
        {
            inventory.RequestReroll(selected, 1);
        }
    }
}
