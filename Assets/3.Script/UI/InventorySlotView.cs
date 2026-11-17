using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 가방 화면의 칸 하나. 마우스를 올리면 설명을, 누르면 장착·해제·버리기를 InventoryUi에 알린다.
public class InventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private SlotPanel owner;
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private bool equipSlot;
    [SerializeField] private int index;

    public bool IsEquipSlot => equipSlot;
    public int Index => index;

    public void ShowItem(Sprite sprite, Color frameColor, float iconAlpha)
    {
        frame.color = frameColor;
        icon.sprite = sprite;
        icon.color = new Color(1f, 1f, 1f, iconAlpha);
        if (!icon.enabled)
        {
            icon.enabled = true;
        }
    }

    public void ShowEmpty(Color frameColor)
    {
        frame.color = frameColor;
        if (icon.enabled)
        {
            icon.enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner.OnSlotHover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner.OnSlotHover(this, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        owner.OnSlotClicked(this, eventData.button == PointerEventData.InputButton.Right);
    }
}
