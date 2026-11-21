using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 가방 화면(Tab). 장착 4칸과 가방 12칸을 보여 준다.
// 가방 칸 좌클릭: 장착(같은 부위와 교체), 우클릭: 버리기. 장착 칸 좌클릭: 가방으로 내리기(무기는 제외).
// 끌어 놓기: 가방 칸끼리 자리 바꾸기, 가방 → 장착 칸 장착, 장착 칸 → 가방 칸 내리기, 가방 창 밖에 놓으면 버리기(상점 창 위면 팔기).
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
    [SerializeField] private Color potionFrameColor = new Color(0.55f, 0.85f, 0.6f, 0.9f);
    [SerializeField] private GameHub hub;
    [Tooltip("끌고 있는 아이템 그림(패널 맨 위에 있고 마우스를 따라간다)")]
    [SerializeField] private Image dragIcon;

    private readonly StringBuilder builder = new StringBuilder(256);
    // 끌고 있는 칸(없으면 null)
    private InventorySlotView dragging;
    private PlayerInventory inventory;
    private InventorySlotView hovered;
    private int lastRevision = -1;
    private PotionKind lastQuick0;
    private PotionKind lastQuick1;
    private int lastKeysRevision = -1;

    public bool IsOpen => panel.activeSelf;

    // 마우스를 올린 가방 칸에 포션이 있으면 그 칸 번호(등록 키를 누르면 그 칸에 등록한다). 없으면 -1.
    public int HoveredPotionBagIndex
    {
        get
        {
            int index = -1;
            if (panel.activeSelf && inventory != null && hovered != null && !hovered.IsEquipSlot)
            {
                ItemData item = inventory.GetBagItem(hovered.Index);
                if (item.Exists && item.slot == ItemSlot.Potion)
                {
                    index = hovered.Index;
                }
            }
            return index;
        }
    }

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
        CancelDrag();
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
            int keysRevision = 0;
            if (hub.LocalInput != null)
            {
                keysRevision = hub.LocalInput.KeysRevision;
            }
            bool changed = inventory.Revision != lastRevision || inventory.GetQuickPotion(0) != lastQuick0
                || inventory.GetQuickPotion(1) != lastQuick1 || keysRevision != lastKeysRevision;
            if (changed)
            {
                lastRevision = inventory.Revision;
                lastQuick0 = inventory.GetQuickPotion(0);
                lastQuick1 = inventory.GetQuickPotion(1);
                lastKeysRevision = keysRevision;
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
            ItemData item = inventory.GetBagItem(i);
            ShowSlot(bagSlots[i], item, 1f);
            // 포션: 개수와 등록 칸 키를 적는다.
            if (item.Exists && item.slot == ItemSlot.Potion)
            {
                bagSlots[i].SetCount(item.count);
                if (inventory.GetQuickPotion(0) == (PotionKind)item.kind)
                {
                    bagSlots[i].SetMark(hub.GetKeyName(GameInput.KeyPotion1, "1"));
                }
                else if (inventory.GetQuickPotion(1) == (PotionKind)item.kind)
                {
                    bagSlots[i].SetMark(hub.GetKeyName(GameInput.KeyPotion2, "2"));
                }
                else
                {
                    bagSlots[i].SetMark(null);
                }
            }
        }
        RefreshDetail();
    }

    private void ShowSlot(InventorySlotView view, in ItemData item, float iconAlpha)
    {
        if (item.Exists)
        {
            ItemBalance balance = inventory.Balance;
            Color frameColor = balance.GetRarityColor(item.rarity);
            if (item.slot == ItemSlot.Potion)
            {
                frameColor = potionFrameColor;
            }
            view.ShowItem(inventory.GetIcon(item), frameColor, iconAlpha);
            view.SetMark(null);
            if (item.slot == ItemSlot.Potion)
            {
                view.SetLevel(0, Color.white);
            }
            else
            {
                view.SetLevel(item.level, balance.GetUpgradeBorderColor(item.level));
            }
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
            else if (inventory.GetBagItem(view.Index).slot == ItemSlot.Potion)
            {
                inventory.RequestUseBagPotion(view.Index);
            }
            else
            {
                inventory.RequestEquip(view.Index);
            }
        }
    }

    // ------------------------------------------------------------ 끌어 놓기

    public override void OnSlotBeginDrag(InventorySlotView view, PointerEventData eventData)
    {
        if (inventory != null)
        {
            ItemData item = GetViewItem(view);
            if (item.Exists)
            {
                dragging = view;
                dragIcon.sprite = inventory.GetIcon(item);
                dragIcon.enabled = true;
                dragIcon.transform.SetAsLastSibling();
                MoveDragIcon(eventData);
            }
        }
    }

    public override void OnSlotDrag(InventorySlotView view, PointerEventData eventData)
    {
        if (dragging == view)
        {
            MoveDragIcon(eventData);
        }
    }

    public override void OnSlotEndDrag(InventorySlotView view, PointerEventData eventData)
    {
        if (dragging == view)
        {
            CancelDrag();
            if (inventory != null)
            {
                DropItem(view, eventData.pointerCurrentRaycast.gameObject);
            }
        }
    }

    private ItemData GetViewItem(InventorySlotView view)
    {
        ItemData item;
        if (view.IsEquipSlot)
        {
            item = inventory.GetEquipped(view.Index);
        }
        else
        {
            item = inventory.GetBagItem(view.Index);
        }
        return item;
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
        dragging = null;
        if (dragIcon != null && dragIcon.enabled)
        {
            dragIcon.enabled = false;
        }
    }

    // 놓은 곳: 이 화면의 칸이면 옮기기·장착·내리기, 가방 창 안의 빈 곳이면 취소, 창 밖이면 버리기(상점 창 위면 팔기).
    private void DropItem(InventorySlotView source, GameObject target)
    {
        InventorySlotView targetView = null;
        bool insidePanel = false;
        if (target != null)
        {
            targetView = target.GetComponentInParent<InventorySlotView>();
            insidePanel = target.transform.IsChildOf(panel.transform);
        }
        if (targetView != null && targetView.Owner == this)
        {
            DropOnSlot(source, targetView);
        }
        else if (!insidePanel && !source.IsEquipSlot)
        {
            if (facilityUi != null && facilityUi.IsShopOpen && target != null && facilityUi.Contains(target))
            {
                inventory.RequestSell(source.Index);
            }
            else
            {
                inventory.RequestDiscard(source.Index);
            }
        }
    }

    private void DropOnSlot(InventorySlotView source, InventorySlotView target)
    {
        if (target != source)
        {
            if (!source.IsEquipSlot && !target.IsEquipSlot)
            {
                inventory.RequestMoveBag(source.Index, target.Index);
            }
            else if (!source.IsEquipSlot)
            {
                ItemData item = inventory.GetBagItem(source.Index);
                if (item.slot != ItemSlot.Potion && (int)item.slot == target.Index)
                {
                    inventory.RequestEquip(source.Index);
                }
                else
                {
                    hub.Hud.ShowNotice("그 칸에는 낄 수 없습니다");
                }
            }
            else if (!target.IsEquipSlot)
            {
                ItemData bagItem = inventory.GetBagItem(target.Index);
                bool sameSlot = bagItem.Exists && bagItem.slot != ItemSlot.Potion && (int)bagItem.slot == source.Index;
                if (source.Index == (int)ItemSlot.Weapon && !sameSlot)
                {
                    hub.Hud.ShowNotice("무기는 다른 무기와만 바꿀 수 있습니다");
                }
                else
                {
                    inventory.RequestUnequipTo(source.Index, target.Index);
                }
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
                if (item.Exists && item.slot == ItemSlot.Potion)
                {
                    builder.Append("\n좌클릭: 마시기   우클릭: ");
                    if (facilityUi != null && facilityUi.IsShopOpen)
                    {
                        builder.Append("팔기");
                    }
                    else
                    {
                        builder.Append("버리기");
                    }
                    builder.Append("\n마우스를 올린 채 ").Append(hub.GetKeyName(GameInput.KeyPotion1, "1")).Append(" / ")
                        .Append(hub.GetKeyName(GameInput.KeyPotion2, "2")).Append(": 포션 칸에 등록");
                }
                else if (item.Exists)
                {
                    builder.Append("\n[현재 장착]\n");
                    balance.AppendDescription(builder, inventory.GetEquipped((int)item.slot), inventory.Weapons);
                }
            }
        }
        detailLabel.SetText(builder);
    }
}
