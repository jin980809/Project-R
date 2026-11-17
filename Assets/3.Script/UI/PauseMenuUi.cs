using System.Collections.Generic;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Esc 메뉴: 계속하기, 설정, 나가기, 게임 종료. 로비와 전투 씬 모두에 있다.
// - 혼자 플레이(호스트 혼자)일 때만 게임을 멈춘다(Time.timeScale = 0). 여럿이면 메뉴만 띄운다.
// - 설정은 MetaProgress(기기 저장)에 남고, 해상도와 전체 화면은 Unity가 기억한다.
public class PauseMenuUi : MonoBehaviour
{
    private const int RowFullScreen = 0;
    private const int RowResolution = 1;
    private const int RowShake = 2;
    private const int RowDamageText = 3;
    private const int RowMasterVolume = 4;
    private const int RowEffectsVolume = 5;
    private const int RowMusicVolume = 6;
    public const int RowCount = 7;

    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private TMP_Text titleLabel;
    [Tooltip("페이지마다 높이를 바꾸는 가운데 상자")]
    [SerializeField] private RectTransform box;
    [SerializeField] private float settingsHeight = 640f;
    [Tooltip("메인 버튼의 첫 줄 위치와 줄 간격, 버튼 높이, 아래 여백")]
    [SerializeField] private float buttonTop = 100f;
    [SerializeField] private float buttonStep = 76f;
    [SerializeField] private float buttonHeight = 60f;
    [SerializeField] private float bottomMargin = 40f;

    [Header("메인")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text exitLabel;
    [SerializeField] private Button quitButton;

    [Header("설정(RowFullScreen 순서로 한 줄씩)")]
    [SerializeField] private TMP_Text[] rowValues;
    [SerializeField] private Button[] rowPrevButtons;
    [SerializeField] private Button[] rowNextButtons;
    [SerializeField] private Button backButton;

    private readonly StringBuilder builder = new StringBuilder(32);
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>(16);
    private bool paused;
    private bool exitVisible;

    public bool IsOpen => panel.activeSelf;

    private void Awake()
    {
        resumeButton.onClick.AddListener(Close);
        settingsButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(OnExit);
        quitButton.onClick.AddListener(OnQuit);
        backButton.onClick.AddListener(OpenMain);
        for (int i = 0; i < RowCount; i++)
        {
            int row = i;
            rowPrevButtons[i].onClick.AddListener(() => Step(row, -1));
            rowNextButtons[i].onClick.AddListener(() => Step(row, 1));
        }
        CollectResolutions();
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        resumeButton.onClick.RemoveAllListeners();
        settingsButton.onClick.RemoveAllListeners();
        exitButton.onClick.RemoveAllListeners();
        quitButton.onClick.RemoveAllListeners();
        backButton.onClick.RemoveAllListeners();
        for (int i = 0; i < RowCount; i++)
        {
            rowPrevButtons[i].onClick.RemoveAllListeners();
            rowNextButtons[i].onClick.RemoveAllListeners();
        }
        // 씬이 바뀌어도 멈춘 시간이 남지 않게 한다.
        Resume();
        if (hub != null)
        {
            hub.SetMenuBlocked(false);
        }
    }

    // 모니터가 지원하는 해상도에서 같은 크기를 하나로 묶는다(주사율만 다른 항목 제외).
    private void CollectResolutions()
    {
        Resolution[] all = Screen.resolutions;
        for (int i = 0; i < all.Length; i++)
        {
            Vector2Int size = new Vector2Int(all[i].width, all[i].height);
            if (size.x >= 1024 && !resolutions.Contains(size))
            {
                resolutions.Add(size);
            }
        }
        if (resolutions.Count == 0)
        {
            resolutions.Add(new Vector2Int(Screen.width, Screen.height));
        }
    }

    // Esc를 눌렀을 때 PlayerController가 부른다. 설정 화면이면 메인으로, 메인이면 닫는다.
    public void OnEscape()
    {
        if (!panel.activeSelf)
        {
            Open();
        }
        else if (settingsPage.activeSelf)
        {
            OpenMain();
        }
        else
        {
            Close();
        }
    }

    public void Open()
    {
        panel.SetActive(true);
        hub.SetMenuBlocked(true);
        hub.Sound.Play(SoundId.UiOpen);
        // 혼자일 때만 멈춘다.
        if (NetworkServer.active && hub.PlayerCount <= 1)
        {
            paused = true;
            Time.timeScale = 0f;
        }
        OpenMain();
    }

    public void Close()
    {
        if (panel.activeSelf)
        {
            panel.SetActive(false);
            hub.SetMenuBlocked(false);
            hub.Sound.Play(SoundId.UiClose);
        }
        Resume();
    }

    private void Resume()
    {
        if (paused)
        {
            paused = false;
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        // 멈춘 사이에 다른 사람이 들어오면 다시 흐르게 한다.
        if (paused && hub.PlayerCount > 1)
        {
            Resume();
        }
    }

    private void OpenMain()
    {
        mainPage.SetActive(true);
        settingsPage.SetActive(false);
        titleLabel.text = "메뉴";
        RefreshExit();
    }

    private void OpenSettings()
    {
        mainPage.SetActive(false);
        settingsPage.SetActive(true);
        titleLabel.text = "설정";
        SetBoxHeight(settingsHeight);
        RefreshSettings();
    }

    // 나가기 버튼: 전투 중 호스트는 모두 로비로, 참가자는 파티에서 나가 자기 로비로. 로비의 호스트는 나갈 곳이 없어 숨긴다.
    private void RefreshExit()
    {
        GameNetworkManager manager = NetworkManager.singleton as GameNetworkManager;
        bool host = NetworkServer.active;
        bool inRun = manager != null && manager.IsRunInProgress;
        exitVisible = true;
        if (inRun && host)
        {
            exitLabel.text = "로비로 돌아가기";
        }
        else if (!host)
        {
            exitLabel.text = "파티에서 나가기";
        }
        else
        {
            exitVisible = false;
        }
        exitButton.gameObject.SetActive(exitVisible);

        // 나가기 버튼이 숨으면 게임 종료 버튼을 한 줄 당기고 상자 높이를 줄인다.
        int quitRow = 2;
        if (exitVisible)
        {
            quitRow = 3;
        }
        RectTransform quitRect = (RectTransform)quitButton.transform;
        Vector2 position = quitRect.anchoredPosition;
        position.y = -(buttonTop + buttonStep * quitRow);
        quitRect.anchoredPosition = position;
        SetBoxHeight(buttonTop + buttonStep * quitRow + buttonHeight + bottomMargin);
    }

    private void SetBoxHeight(float height)
    {
        Vector2 size = box.sizeDelta;
        size.y = height;
        box.sizeDelta = size;
    }

    private void OnExit()
    {
        GameNetworkManager manager = NetworkManager.singleton as GameNetworkManager;
        Close();
        if (manager != null)
        {
            if (manager.IsRunInProgress)
            {
                manager.RequestExit();
            }
            else if (!NetworkServer.active)
            {
                manager.LeaveToOwnLobby();
            }
        }
    }

    private void OnQuit()
    {
        hub.Meta.Save();
        Application.Quit();
    }

    private void Step(int row, int delta)
    {
        MetaProgress meta = hub.Meta;
        if (row == RowFullScreen)
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
        else if (row == RowResolution)
        {
            int current = FindResolutionIndex();
            int next = (current + delta + resolutions.Count) % resolutions.Count;
            Vector2Int size = resolutions[next];
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
        }
        else if (row == RowShake)
        {
            meta.SetShakeEnabled(!meta.ShakeEnabled);
        }
        else if (row == RowDamageText)
        {
            meta.SetDamageTextEnabled(!meta.DamageTextEnabled);
        }
        else
        {
            int channel = row - RowMasterVolume;
            meta.SetVolumeStep(channel, meta.GetVolumeStep(channel) + delta);
        }
        meta.Save();
        hub.ApplySettings();
        RefreshSettings();
    }

    private int FindResolutionIndex()
    {
        int found = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < resolutions.Count; i++)
        {
            float distance = Mathf.Abs(resolutions[i].x - Screen.width) + Mathf.Abs(resolutions[i].y - Screen.height);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                found = i;
            }
        }
        return found;
    }

    private void RefreshSettings()
    {
        MetaProgress meta = hub.Meta;
        SetOnOff(rowValues[RowFullScreen], Screen.fullScreen);
        // 창 크기를 바꾼 직후에는 Screen.width가 다음 프레임에 바뀌므로 목록의 값을 보여 준다.
        Vector2Int size = resolutions[FindResolutionIndex()];
        builder.Clear();
        builder.Append(size.x).Append(" x ").Append(size.y);
        rowValues[RowResolution].SetText(builder);
        SetOnOff(rowValues[RowShake], meta.ShakeEnabled);
        SetOnOff(rowValues[RowDamageText], meta.DamageTextEnabled);
        for (int channel = 0; channel < MetaProgress.VolumeChannels; channel++)
        {
            builder.Clear();
            builder.Append(meta.GetVolumeStep(channel) * 10).Append('%');
            rowValues[RowMasterVolume + channel].SetText(builder);
        }
    }

    private void SetOnOff(TMP_Text label, bool value)
    {
        if (value)
        {
            label.text = "켜짐";
        }
        else
        {
            label.text = "꺼짐";
        }
    }
}
