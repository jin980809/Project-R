using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 스킬 트리의 노드 한 칸 또는 장착 칸(Q, E, R) 하나. 마우스를 올리면 설명을, 누르면 찍기·장착을 SkillUi에 알린다.
public class SkillNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private SkillUi owner;
    [SerializeField] private Image frame;
    [Tooltip("선택했을 때 테두리처럼 보이는 뒷판")]
    [SerializeField] private Image outline;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private int index;
    [Tooltip("true면 장착 칸(index는 칸 번호)")]
    [SerializeField] private bool isSlot;

    public int Index => index;

    public void Show(StringBuilder text, Color frameColor, Sprite iconSprite, Color iconColor, bool selected)
    {
        label.SetText(text);
        frame.color = frameColor;
        if (icon != null)
        {
            icon.enabled = iconSprite != null;
            icon.sprite = iconSprite;
            icon.color = iconColor;
        }
        if (outline != null)
        {
            outline.enabled = selected;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isSlot)
        {
            owner.OnNodeHover(index, true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isSlot)
        {
            owner.OnNodeHover(index, false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (isSlot)
            {
                owner.OnSlotClicked(index);
            }
            else
            {
                owner.OnNodeClicked(index);
            }
        }
    }
}
