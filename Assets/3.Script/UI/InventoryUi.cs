using System.Text;
using TMPro;
using UnityEngine;

// 가방 화면(Tab). 장착 4칸과 가방 12칸을 보여 준다.
// 가방 칸 좌클릭: 장착(같은 부위와 교체), 우클릭: 버리기. 장착 칸 좌클릭: 가방으로 내리기(무기는 제외).
public class InventoryUi : SlotPanel
{
    [SerializeField] private GameObject panel;
    [Tooltip("전투 씬에만 있다(로비에서는 비움). 상점이 열려 있으면 우클릭이 판매가 된다.")]
    [SerializeField] private FacilityUi facilityUi;
    [SerializeField] private InventorySlotView[] equipSlots;
    [SerializeField] private InventorySlotView[] bagSlots;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private Color emptyFrameColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private float inactiveIconAlpha = 0.35f;

    private readonly StringBuilder builder = new StringBuilder(256);
    private PlayerInventory inventory;
    private InventorySlotView hovered;
    private int lastRevision = -1;

    public bool IsOpen => panel.activeSelf;

    public void Bind(PlayerInventory localInventory)
    {
        inventory = localInventory;
        lastRevision = -1;
    }

    public void Toggle()
    {
        if (panel.activeSelf)
        {
            Close();
        }
        else
        {
            panel.SetActive(true);
            lastRevision = -1;
        }
    }

    public void Open()
    {
        if (!panel.activeSelf)
        {
            panel.SetActive(true);
            lastRevision = -1;
        }
    }

    public void Close()
    {
        hovered = null;
        panel.SetActive(false);
        if (facilityUi != null)
        {
            facilityUi.Close();
        }
    }

    private void Update()
    {
        if (panel.activeSelf && inventory != null)
        {
            if (inventory.Revision != lastRevision)
            {
                lastRevision = inventory.Revision;
                Refresh();
            }
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < equipSlots.Length; i++)
        {
            ItemData item = inventory.GetEquipped(i);
            float alpha = 1f;
            // 양손 무기를 들고 있으면 방패는 쓰이지 않는다.
            if (i == (int)ItemSlot.Offhand && !inventory.ShieldActive)
            {
                alpha = inactiveIconAlpha;
            }
            ShowSlot(equipSlots[i], item, alpha);
        }
        for (int i = 0; i < bagSlots.Length; i++)
        {
            ShowSlot(bagSlots[i], inventory.GetBagItem(i), 1f);
        }
        RefreshDetail();
    }

    private void ShowSlot(InventorySlotView view, in ItemData item, float iconAlpha)
    {
        if (item.Exists)
        {
            view.ShowItem(inventory.GetIcon(item), inventory.Balance.GetRarityColor(item.rarity), iconAlpha);
        }
        else
        {
            view.ShowEmpty(emptyFrameColor);
        }
    }

    public override void OnSlotHover(InventorySlotView view, bool entered)
    {
        if (entered)
        {
            hovered = view;
        }
        else if (hovered == view)
        {
            hovered = null;
        }
        if (inventory != null)
        {
            RefreshDetail();
        }
    }

    public override void OnSlotClicked(InventorySlotView view, bool rightButton)
    {
        if (inventory != null)
        {
            if (view.IsEquipSlot)
            {
                if (!rightButton)
                {
                    inventory.RequestUnequip(view.Index);
                }
            }
            else if (rightButton)
            {
                if (facilityUi != null && facilityUi.IsShopOpen)
                {
                    inventory.RequestSell(view.Index);
                }
                else
                {
                    inventory.RequestDiscard(view.Index);
                }
            }
            else
            {
                inventory.RequestEquip(view.Index);
            }
        }
    }

    // 마우스를 올린 칸의 설명. 가방 칸이면 지금 장착한 같은 부위와 비교할 수 있게 함께 적는다.
    private void RefreshDetail()
    {
        builder.Clear();
        if (hovered != null)
        {
            ItemBalance balance = inventory.Balance;
            if (hovered.IsEquipSlot)
            {
                ItemData item = inventory.GetEquipped(hovered.Index);
                balance.AppendDescription(builder, item, inventory.Weapons);
                if (item.Exists && hovered.Index == (int)ItemSlot.Offhand && !inventory.ShieldActive)
                {
                    builder.Append("(양손 무기를 들고 있어 쓰이지 않음)\n");
                }
            }
            else
            {
                ItemData item = inventory.GetBagItem(hovered.Index);
                balance.AppendDescription(builder, item, inventory.Weapons);
                if (item.Exists)
                {
                    builder.Append("\n[현재 장착]\n");
                    balance.AppendDescription(builder, inventory.GetEquipped((int)item.slot), inventory.Weapons);
                }
            }
        }
        detailLabel.SetText(builder);
    }
}
