using UnityEngine;
using UnityEngine.UI;

// 캔버스 아래 모든 버튼에 클릭 소리를 붙인다(시작할 때 한 번만 등록).
public class UiClickSound : MonoBehaviour
{
    [SerializeField] private SoundManager sound;

    private Button[] buttons;

    private void Awake()
    {
        buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].onClick.AddListener(PlayClick);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].onClick.RemoveListener(PlayClick);
            }
        }
    }

    private void PlayClick()
    {
        sound.Play(SoundId.UiClick);
    }
}
