using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 가방 화면의 칸 하나. 마우스를 올리면 설명을, 누르면 장착·해제·버리기를 InventoryUi에 알린다.
// 끌어 놓기 입력도 같은 화면으로 넘긴다(놓은 곳은 화면이 판단한다).
public class InventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private SlotPanel owner;
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private bool equipSlot;
    [SerializeField] private int index;
    [Tooltip("포션 개수·강화 단계 글자(오른쪽 아래). 없으면 비워 둔다.")]
    [SerializeField] private TMP_Text countLabel;
    [Tooltip("등록 칸 번호 글자(왼쪽 위). 없으면 비워 둔다.")]
    [SerializeField] private TMP_Text markLabel;
    [Tooltip("강화 단계 색 안쪽 테두리(등급 테두리 안). 없으면 비워 둔다.")]
    [SerializeField] private Image tierRing;
    [Tooltip("칸 바탕(테두리 안). 아이템이 있으면 등급 색을 어둡게 섞어 칠한다.")]
    [SerializeField] private Image back;
    [SerializeField] private Color emptyBackColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
    [Tooltip("바탕에 등급 색을 섞는 정도(0이면 빈 칸 바탕 그대로)")]
    [SerializeField] private float rarityBackTint = 0.42f;
    [Tooltip("강화하지 않은 아이템의 테두리 색(강화 단계가 있으면 SetLevel이 그 색으로 바꾼다)")]
    [SerializeField] private Color plainBorderColor = new Color(0.32f, 0.32f, 0.36f, 1f);

    public bool IsEquipSlot => equipSlot;
    public SlotPanel Owner => owner;
    public int Index => index;

    // 바탕은 backColor(등급 색)를 어둡게 섞어 칠하고, 테두리는 강화하지 않은 색으로 둔다(강화 단계는 SetLevel).
    public void ShowItem(Sprite sprite, Color backColor, float iconAlpha)
    {
        frame.color = plainBorderColor;
        if (back != null)
        {
            Color tinted = Color.Lerp(emptyBackColor, backColor, rarityBackTint);
            tinted.a = emptyBackColor.a;
            back.color = tinted;
        }
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
        if (back != null)
        {
            back.color = emptyBackColor;
        }
        if (icon.enabled)
        {
            icon.enabled = false;
        }
        SetCount(0);
        SetMark(null);
        SetLevel(0, Color.clear);
    }

    // 0 이하면 글자를 숨긴다.
    public void SetCount(int count)
    {
        if (countLabel != null)
        {
            if (count > 0)
            {
                countLabel.SetText("{0}", count);
                countLabel.enabled = true;
            }
            else if (countLabel.enabled)
            {
                countLabel.enabled = false;
            }
        }
    }

    // 강화 단계(+3)를 적고 안쪽 테두리를 단계 색으로 칠한다. 0이면 숨긴다.
    // 강화 단계: 테두리를 단계 색으로 칠하고 +N 글자를 띄운다. 0이면 테두리는 강화하지 않은 색.
    public void SetLevel(int level, Color color)
    {
        if (level > 0)
        {
            frame.color = color;
        }
        else
        {
            frame.color = plainBorderColor;
        }
        if (countLabel != null)
        {
            if (level > 0)
            {
                countLabel.SetText("+{0}", level);
                countLabel.color = color;
                countLabel.enabled = true;
            }
            else if (countLabel.enabled)
            {
                countLabel.enabled = false;
            }
        }
    }

    // 대장간에서 고른 칸: 테두리 안쪽에 color 고리를 두른다(가방 칸에는 쓰지 않는다).
    public void SetSelected(bool selected, Color color)
    {
        if (tierRing != null)
        {
            if (selected)
            {
                tierRing.color = color;
            }
            if (tierRing.enabled != selected)
            {
                tierRing.enabled = selected;
            }
        }
    }

    // 등록 칸 번호 같은 짧은 표시. null이나 빈 문자열이면 숨긴다.
    public void SetMark(string text)
    {
        if (markLabel != null)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (markLabel.enabled)
                {
                    markLabel.enabled = false;
                }
            }
            else
            {
                markLabel.text = text;
                markLabel.enabled = true;
            }
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner.OnSlotBeginDrag(this, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner.OnSlotDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner.OnSlotEndDrag(this, eventData);
    }
}
