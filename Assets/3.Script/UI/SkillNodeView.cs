using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 노드맵의 노드 한 칸. 마우스를 올리면 설명을, 누르면 찍기를 SkillUi에 알린다.
public class SkillNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private SkillUi owner;
    [SerializeField] private Image frame;
    [SerializeField] private TMP_Text label;
    [SerializeField] private int index;

    public int Index => index;

    public void Show(StringBuilder text, Color frameColor)
    {
        label.SetText(text);
        frame.color = frameColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner.OnNodeHover(index, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner.OnNodeHover(index, false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner.OnNodeClicked(index);
        }
    }
}
