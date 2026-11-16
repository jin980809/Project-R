using System.Net;
using System.Net.Sockets;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 로비 패널: 참가자 목록, 초대 주소 복사, 주소로 참가, 게임 시작(호스트) 또는 나가기(참가자).
// 표시 내용은 상태가 바뀔 때만 갱신한다.
public class LobbyUi : MonoBehaviour
{
    [SerializeField] private GameHub hub;
    [SerializeField] private TMP_Text[] playerRows;
    [SerializeField] private TMP_Text addressLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionLabel;

    private GameNetworkManager manager;
    private string localAddress;
    private int lastSignature = -1;
    private bool lastInputFocused;

    private void Awake()
    {
        copyButton.onClick.AddListener(OnClickCopy);
        joinButton.onClick.AddListener(OnClickJoin);
        actionButton.onClick.AddListener(OnClickAction);
    }

    private void Start()
    {
        manager = NetworkManager.singleton as GameNetworkManager;
        localAddress = FindLocalAddress();
    }

    private void OnDestroy()
    {
        copyButton.onClick.RemoveListener(OnClickCopy);
        joinButton.onClick.RemoveListener(OnClickJoin);
        actionButton.onClick.RemoveListener(OnClickAction);
    }

    private void Update()
    {
        bool focused = addressInput.isFocused;
        if (focused != lastInputFocused)
        {
            lastInputFocused = focused;
            hub.SetInputBlocked(focused);
        }

        int signature = BuildSignature();
        if (signature != lastSignature)
        {
            lastSignature = signature;
            Refresh();
        }
    }

    // 화면에 영향을 주는 값들을 하나의 정수로 접어서, 바뀌었을 때만 다시 그린다.
    private int BuildSignature()
    {
        int signature = 17;
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            int value = player.Slot * 4;
            if (player.IsHostPlayer)
            {
                value += 1;
            }
            if (player.isLocalPlayer)
            {
                value += 2;
            }
            signature = signature * 31 + value + 1;
        }
        signature = signature * 31 + (int)manager.Status;
        if (NetworkServer.active)
        {
            signature = signature * 31 + 1;
        }
        if (NetworkClient.isConnected)
        {
            signature = signature * 31 + 2;
        }
        return signature;
    }

    private void Refresh()
    {
        for (int i = 0; i < playerRows.Length; i++)
        {
            playerRows[i].SetText("{0}. 비어 있음", i + 1);
            playerRows[i].alpha = 0.4f;
        }

        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            int slot = player.Slot;
            if (slot >= 0 && slot < playerRows.Length)
            {
                TMP_Text row = playerRows[slot];
                row.alpha = 1f;
                if (player.IsHostPlayer && player.isLocalPlayer)
                {
                    row.SetText("{0}. 플레이어 {0} (호스트, 나)", slot + 1);
                }
                else if (player.IsHostPlayer)
                {
                    row.SetText("{0}. 플레이어 {0} (호스트)", slot + 1);
                }
                else if (player.isLocalPlayer)
                {
                    row.SetText("{0}. 플레이어 {0} (나)", slot + 1);
                }
                else
                {
                    row.SetText("{0}. 플레이어 {0}", slot + 1);
                }
            }
        }

        bool hosting = NetworkServer.active;
        if (hosting)
        {
            addressLabel.text = "초대 주소  " + localAddress;
            actionLabel.text = "게임 시작";
        }
        else
        {
            addressLabel.text = "다른 사람의 로비에 참가 중";
            actionLabel.text = "로비 나가기";
        }
        copyButton.interactable = hosting;
        actionButton.interactable = hosting || NetworkClient.isConnected;

        LobbyStatus status = manager.Status;
        if (status == LobbyStatus.Connecting)
        {
            statusLabel.text = "접속하는 중...";
        }
        else if (status == LobbyStatus.ConnectFailed)
        {
            statusLabel.text = "접속하지 못했습니다. 주소를 확인하거나, 이미 출발한 로비일 수 있습니다.";
        }
        else if (status == LobbyStatus.Disconnected)
        {
            statusLabel.text = "호스트와 연결이 끊어져 내 로비로 돌아왔습니다.";
        }
        else if (hosting)
        {
            statusLabel.text = "주소를 친구에게 보내 초대하세요. 출발 지점에서 F를 눌러도 시작합니다.";
        }
        else
        {
            statusLabel.text = "호스트가 출발하면 함께 시작합니다.";
        }
    }

    private void OnClickCopy()
    {
        GUIUtility.systemCopyBuffer = localAddress;
        statusLabel.text = "초대 주소를 복사했습니다.";
    }

    private void OnClickJoin()
    {
        manager.JoinLobby(addressInput.text);
    }

    private void OnClickAction()
    {
        if (NetworkServer.active)
        {
            manager.ServerStartRun();
        }
        else
        {
            manager.LeaveToOwnLobby();
        }
    }

    // 같은 공유기 안에서 쓸 수 있는 내 주소(IPv4). 로비를 열 때 한 번만 찾는다.
    private string FindLocalAddress()
    {
        string address = "127.0.0.1";
        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());
            bool found = false;
            for (int i = 0; i < addresses.Length && !found; i++)
            {
                if (addresses[i].AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addresses[i]))
                {
                    address = addresses[i].ToString();
                    found = true;
                }
            }
        }
        catch (SocketException)
        {
            address = "127.0.0.1";
        }
        return address;
    }
}
