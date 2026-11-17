using TMPro;
using UnityEngine;

// 방 가장자리의 문. 잠겼으면 창살이 내려간 그림, 열렸으면 뚫린 통로 그림이고 문 너머 방의 종류를 보여 준다.
public class DoorView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color lockedLabelColor = new Color(1f, 0.55f, 0.5f);
    [SerializeField] private Color openLabelColor = Color.white;

    public void Hide()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void Show(bool locked, string roomName)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        if (locked)
        {
            doorRenderer.sprite = closedSprite;
            label.color = lockedLabelColor;
        }
        else
        {
            doorRenderer.sprite = openSprite;
            label.color = openLabelColor;
        }
        label.text = roomName;
    }
}
