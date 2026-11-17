using UnityEngine;

// 아이템 칸(InventorySlotView)을 가진 화면의 공통 부모. 칸이 마우스 입력을 여기로 알린다.
public abstract class SlotPanel : MonoBehaviour
{
    public abstract void OnSlotHover(InventorySlotView view, bool entered);

    public abstract void OnSlotClicked(InventorySlotView view, bool rightButton);
}
