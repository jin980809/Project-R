using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 예/아니오를 묻는 작은 창(다음 층 포탈, 스킬 트리 초기화 등). 화면 가운데에 뜬다.
// 상호작용 키(F)나 예 버튼은 수락, Esc나 아니오 버튼은 취소. 열려 있어도 이동은 막지 않는다.
public class ConfirmUi : MonoBehaviour
{
    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private Button yesButton;
    [SerializeField] private TMP_Text yesLabel;
    [SerializeField] private Button noButton;

    private System.Action onYes;
    private System.Action onNo;
    private int closedFrame = -10;

    public bool IsOpen => panel.activeSelf;
    // 열려 있거나 이번 프레임에 닫혔는지(같은 F 입력으로 다른 상호작용이 겹치지 않게)
    public bool IsBusy => panel.activeSelf || closedFrame == Time.frameCount;

    private void Awake()
    {
        yesButton.onClick.AddListener(Accept);
        noButton.onClick.AddListener(Cancel);
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();
    }

    // yes, no는 미리 만들어 둔 콜백(열 때마다 새로 만들지 않는다). no는 비워도 된다.
    public void Show(string message, string yesText, System.Action yes, System.Action no)
    {
        messageLabel.text = message;
        yesLabel.text = yesText + "  (" + hub.GetKeyName(GameInput.KeyInteract, "F") + ")";
        onYes = yes;
        onNo = no;
        panel.SetActive(true);
        transform.SetAsLastSibling();
    }

    // 지금 띄운 창이 그 콜백의 것인지(포탈에서 멀어지면 그 창만 닫는다)
    public bool IsShowing(System.Action yes)
    {
        return panel.activeSelf && onYes == yes;
    }

    public void Accept()
    {
        System.Action callback = onYes;
        Hide();
        if (callback != null)
        {
            callback();
        }
    }

    public void Cancel()
    {
        System.Action callback = onNo;
        Hide();
        if (callback != null)
        {
            callback();
        }
    }

    // 콜백 없이 닫는다.
    public void Hide()
    {
        onYes = null;
        onNo = null;
        if (panel.activeSelf)
        {
            closedFrame = Time.frameCount;
        }
        panel.SetActive(false);
    }

    private void Update()
    {
        if (panel.activeSelf && hub.LocalInput != null && hub.LocalInput.InteractPressed && !hub.InputBlocked)
        {
            Accept();
        }
    }
}
