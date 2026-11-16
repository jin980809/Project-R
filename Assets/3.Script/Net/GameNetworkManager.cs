using Mirror;
using UnityEngine;

public enum PendingNetworkAction : byte
{
    None,
    StartHost,
    StartClient
}

public enum LobbyStatus : byte
{
    None,
    Connecting,
    ConnectFailed,
    Disconnected
}

// 게임은 항상 로비에서 시작하고, 로비는 "혼자 있는 호스트"다. 싱글과 멀티가 같은 코드 경로를 탄다.
// - 초대받은 사람은 자기 로비(호스트)를 내리고 상대 주소로 접속한다.
// - 호스트가 출발하면 모두 전투 씬으로 넘어간다. 진행 중인 판에는 새로 들어올 수 없다.
// - 접속이 끊기거나 나가면 자기 로비로 돌아와 다시 호스트가 된다.
public class GameNetworkManager : NetworkManager
{
    public const string LobbyScenePath = "Assets/1.Scene/Lobby.unity";
    public const string GameScenePath = "Assets/1.Scene/Game.unity";
    public const int MaxPlayers = 4;

    [Header("Tower")]
    [SerializeField] private bool startHostOnPlay = true;

    private readonly bool[] slotUsed = new bool[MaxPlayers];
    private readonly int[] slotConnection = new int[MaxPlayers];
    private GameHub hub;
    private PendingNetworkAction pendingAction;
    private int pendingDelayFrames;
    private LobbyStatus status;
    private bool wasConnected;

    public LobbyStatus Status => status;
    public bool IsRunInProgress => Utils.IsSceneActive(GameScenePath);

    // 현재 씬의 허브. 씬이 바뀌면 이전 허브는 파괴되므로 그때 다시 찾는다(씬당 한 번).
    public GameHub Hub
    {
        get
        {
            if (hub == null)
            {
                hub = FindFirstObjectByType<GameHub>();
            }
            return hub;
        }
    }

    public override void Start()
    {
        base.Start();

        // 개발용: 실행 인자 -join <주소> 가 있으면 바로 그 로비로 접속한다.
        string joinAddress = ReadJoinArgument();
        if (joinAddress != null)
        {
            JoinLobby(joinAddress);
        }
        else if (startHostOnPlay)
        {
            if (!NetworkServer.active && !NetworkClient.active)
            {
                StartHost();
            }
        }
    }

    private string ReadJoinArgument()
    {
        string address = null;
        string[] arguments = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == "-join")
            {
                address = arguments[i + 1];
            }
        }
        return address;
    }

    public override void Update()
    {
        base.Update();

        // 호스트 종료와 접속 시작을 같은 프레임에 하지 않도록, 네트워크가 완전히 내려간 뒤에 실행한다.
        if (pendingAction != PendingNetworkAction.None)
        {
            bool idle = !NetworkServer.active && !NetworkClient.active && loadingSceneAsync == null;
            if (idle)
            {
                if (pendingDelayFrames > 0)
                {
                    pendingDelayFrames--;
                }
                else
                {
                    PendingNetworkAction action = pendingAction;
                    pendingAction = PendingNetworkAction.None;
                    if (action == PendingNetworkAction.StartHost)
                    {
                        StartHost();
                    }
                    else
                    {
                        StartClient();
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------ 로비 조작

    // 초대받은 주소로 접속한다. 자기 로비는 내린다.
    public void JoinLobby(string address)
    {
        string trimmed = address.Trim();
        if (trimmed.Length > 0)
        {
            networkAddress = trimmed;
            status = LobbyStatus.Connecting;
            wasConnected = false;
            pendingAction = PendingNetworkAction.StartClient;
            pendingDelayFrames = 2;
            StopCurrent();
        }
    }

    // 남의 로비나 진행 중인 판에서 나와 자기 로비로 돌아간다.
    public void LeaveToOwnLobby()
    {
        status = LobbyStatus.None;
        pendingAction = PendingNetworkAction.StartHost;
        pendingDelayFrames = 2;
        StopCurrent();
    }

    private void StopCurrent()
    {
        if (NetworkServer.active)
        {
            StopHost();
        }
        else if (NetworkClient.active)
        {
            StopClient();
        }
    }

    public void ServerStartRun()
    {
        if (NetworkServer.active && !IsRunInProgress)
        {
            ServerChangeScene(GameScenePath);
        }
    }

    public void ServerReturnToLobby()
    {
        if (NetworkServer.active && IsRunInProgress)
        {
            ServerChangeScene(LobbyScenePath);
        }
    }

    // Esc: 전투 중이면 호스트는 모두를 데리고 로비로, 참가자는 혼자 자기 로비로 나간다.
    public void RequestExit()
    {
        if (IsRunInProgress)
        {
            if (NetworkServer.active)
            {
                ServerReturnToLobby();
            }
            else
            {
                LeaveToOwnLobby();
            }
        }
    }

    // ------------------------------------------------------------ 서버

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        bool isLocal = conn is LocalConnectionToClient;
        if (!isLocal)
        {
            if (IsRunInProgress || numPlayers >= MaxPlayers)
            {
                conn.Disconnect();
            }
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        int slot = AssignSlot(conn.connectionId);
        Vector2 position = Hub.GetSpawnPoint(slot);
        GameObject player = Instantiate(playerPrefab, position, Quaternion.identity);
        PlayerController controller = player.GetComponent<PlayerController>();
        controller.ServerSetSlot(slot, conn is LocalConnectionToClient);
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (slotUsed[i] && slotConnection[i] == conn.connectionId)
            {
                slotUsed[i] = false;
            }
        }
        base.OnServerDisconnect(conn);
    }

    public override void OnStopServer()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            slotUsed[i] = false;
        }
    }

    // 자리 번호는 씬이 바뀌어도 유지된다.
    private int AssignSlot(int connectionId)
    {
        int slot = -1;
        for (int i = 0; i < MaxPlayers && slot < 0; i++)
        {
            if (slotUsed[i] && slotConnection[i] == connectionId)
            {
                slot = i;
            }
        }
        for (int i = 0; i < MaxPlayers && slot < 0; i++)
        {
            if (!slotUsed[i])
            {
                slotUsed[i] = true;
                slotConnection[i] = connectionId;
                slot = i;
            }
        }
        if (slot < 0)
        {
            slot = 0;
        }
        return slot;
    }

    // ------------------------------------------------------------ 클라이언트

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        wasConnected = true;
        status = LobbyStatus.None;
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        // 참가자로 있다가 끊긴 경우(접속 실패 포함)에만 자기 로비로 돌아간다.
        if (!NetworkServer.active)
        {
            if (pendingAction == PendingNetworkAction.None)
            {
                if (wasConnected)
                {
                    status = LobbyStatus.Disconnected;
                }
                else
                {
                    status = LobbyStatus.ConnectFailed;
                }
                pendingAction = PendingNetworkAction.StartHost;
                pendingDelayFrames = 2;
            }
        }
        wasConnected = false;
    }
}
