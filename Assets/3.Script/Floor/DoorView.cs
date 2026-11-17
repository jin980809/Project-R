using TMPro;
using UnityEngine;

// 방 가장자리의 문 표시. 잠겼는지와 문 너머 방의 종류를 보여 준다.
public class DoorView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer frameRenderer;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color lockedColor = new Color(0.85f, 0.25f, 0.2f, 0.9f);
    [SerializeField] private Color openColor = new Color(0.4f, 0.9f, 0.5f, 0.9f);

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
            frameRenderer.color = lockedColor;
        }
        else
        {
            frameRenderer.color = openColor;
        }
        label.text = roomName;
    }
}
