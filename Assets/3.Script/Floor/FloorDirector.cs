using System.Text;
using Mirror;
using UnityEngine;

public enum RoomType : byte
{
    Start,
    Combat,
    Elite,
    Treasure,
    Shop,
    Forge,
    Boss,
    // 낚시터: 쉬어 가는 미니게임(체력이나 골드)
    Fishing,
    // 광맥: 강화 재료(광석)를 캔다
    Ore,
    // 마구간: 탈것(한 판 동안 유지되는 패시브)을 얻는다
    Stable
}

// 방 하나의 정보. 네트워크로 그대로 보내는 값 형식이다.
public struct RoomInfo
{
    // 격자 위치(0 ~ GridSize-1)
    public byte x;
    public byte y;
    public RoomType type;
    // 문이 있는 방향 비트: 1 북(+y), 2 동(+x), 4 남(-y), 8 서(-x)
    public byte doors;
    // 시작 방에서 몇 번 건너왔는지(깊을수록 적이 강하다)
    public byte depth;
    public byte visited;
    public byte cleared;
}

// 한 층(방 8~10개가 문으로 이어진 구조)을 만들고 진행을 관리한다.
// - 층 구조는 서버(호스트)가 시드로 만들어 방 목록째로 동기화한다.
// - 모두 같은 방에 있다. 살아 있는 전원이 문 앞에 모이면 다음 방으로 이동한다.
// - 전투 방에 들어가면 문이 잠기고, 적을 전멸시키면 열리며 쓰러진 동료가 부활한다.
// - 보스 방을 끝내면 계단이 열리고, 전원이 계단에 모이면 다음 층으로 올라간다.
// - 전원이 쓰러지면 런 실패로 로비에 돌아간다.
public class FloorDirector : NetworkBehaviour
{
    public const int GridSize = 5;
    private const int DirectionCount = 4;

    [Header("참조")]
    [SerializeField] private GameHub hub;
    [SerializeField] private MonsterDirector monsterDirector;
    [Tooltip("북, 동, 남, 서 순서")]
    [SerializeField] private DoorView[] doors;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private GameObject chestObject;
    [SerializeField] private GameObject stairsObject;
    [SerializeField] private GameObject facilityObject;
    [SerializeField] private TMPro.TMP_Text facilityLabel;
    [SerializeField] private RoomDecor decor;

    [Header("층 구조")]
    [SerializeField] private int minRooms = 10;
    [SerializeField] private int maxRooms = 12;
    [SerializeField] private int maxFloors = 3;
    [Tooltip("RoomType 순서(시작, 전투, 엘리트, 보물, 상점, 대장간, 보스, 낚시터, 광맥, 마구간)")]
    [SerializeField] private string[] roomNames = { "시작", "전투", "엘리트", "보물", "상점", "대장간", "보스", "낚시터", "광맥", "마구간" };
    [SerializeField] private Color[] roomTints =
    {
        Color.white, Color.white, new Color(1f, 0.85f, 0.85f), new Color(1f, 0.97f, 0.75f),
        new Color(0.85f, 0.95f, 1f), new Color(1f, 0.88f, 0.75f), new Color(0.85f, 0.75f, 0.9f),
        new Color(0.78f, 0.92f, 1f), new Color(0.88f, 0.86f, 0.95f), new Color(0.95f, 0.9f, 0.78f)
    };

    [Header("이동")]
    [SerializeField] private float doorInset = 0.5f;
    [SerializeField] private float doorRadius = 1.3f;
    [SerializeField] private float doorHoldTime = 0.5f;
    [SerializeField] private float entryDistance = 1.8f;
    [SerializeField] private float stairsRadius = 1.2f;
    [SerializeField] private float chestRadius = 1.1f;

    [Header("전투 방")]
    [SerializeField] private int baseMonsterCount = 3;
    [SerializeField] private int monstersPerFloor = 2;
    [SerializeField] private int maxMonsterCount = 14;
    [SerializeField] private float hpScalePerDepth = 0.12f;
    [SerializeField] private float hpScalePerFloor = 0.5f;
    [SerializeField] private int eliteExtraCount = 2;
    [SerializeField] private float eliteHpScale = 1.2f;
    [SerializeField] private float eliteRarityBonus = 1.5f;
    [Tooltip("보스와 함께 나오는 부하 수")]
    [SerializeField] private int bossMonsterCount = 3;
    [SerializeField] private float damageScalePerFloor = 0.2f;
    [SerializeField] private float bossHpScale = 1f;
    [SerializeField] private float bossRarityBonus = 3f;
    [SerializeField] private float treasureRarityBonus = 1f;
    [SerializeField] private int treasureItemCount = 2;
    [SerializeField] private float reviveHpFraction = 0.5f;
    [SerializeField] private float shopRarityBonus = 0.6f;
    [Tooltip("방을 끝냈을 때 받는 광석: 전투, 엘리트, 보스, 보물 상자 순서")]
    [SerializeField] private int[] oreRewards = { 1, 2, 3, 2 };
    [SerializeField] private float runEndDelay = 7f;
    [Header("영혼석(판이 끝날 때 받는 메타 재화)")]
    [SerializeField] private int soulPerFloor = 10;
    [SerializeField] private int killsPerSoul = 5;
    [SerializeField] private int soulVictoryBonus = 30;

    private readonly SyncList<RoomInfo> rooms = new SyncList<RoomInfo>();
    [SyncVar(hook = nameof(OnStateChanged))] private int currentRoom = -1;
    [SyncVar(hook = nameof(OnStateChanged))] private int floorNumber;
    [SyncVar(hook = nameof(OnRoomLockChanged))] private bool roomLocked;
    [SyncVar(hook = nameof(OnChestChanged))] private bool chestAvailable;
    [SyncVar(hook = nameof(OnStairsChanged))] private bool stairsOpen;

    private readonly StringBuilder labelBuilder = new StringBuilder(32);
    private GameNetworkManager manager;
    private bool viewDirty = true;
    private int revision;

    // 서버
    private System.Random random;
    private readonly int[] grid = new int[GridSize * GridSize];
    private int holdDirection = -1;
    private float holdTimer;
    private bool runEnded;
    private float runEndTimer;
    private float runStartTime;

    public int RoomCount => rooms.Count;
    public int CurrentRoom => currentRoom;
    public int FloorNumber => floorNumber;
    public bool RoomLocked => roomLocked;
    public Vector2 FacilityPosition => facilityObject.transform.position;

    public RoomType CurrentRoomType
    {
        get
        {
            RoomType type = RoomType.Start;
            if (currentRoom >= 0 && currentRoom < rooms.Count)
            {
                type = rooms[currentRoom].type;
            }
            return type;
        }
    }
    // 방 목록이나 현재 방이 바뀔 때마다 늘어난다(미니맵이 바뀐 때만 다시 그리도록).
    public int Revision => revision;

    public RoomInfo GetRoom(int index)
    {
        return rooms[index];
    }

    public string GetRoomName(RoomType type)
    {
        return roomNames[(int)type];
    }

    // ------------------------------------------------------------ 방향 계산

    private int DirectionX(int direction)
    {
        int x = 0;
        if (direction == 1)
        {
            x = 1;
        }
        else if (direction == 3)
        {
            x = -1;
        }
        return x;
    }

    private int DirectionY(int direction)
    {
        int y = 0;
        if (direction == 0)
        {
            y = 1;
        }
        else if (direction == 2)
        {
            y = -1;
        }
        return y;
    }

    private Vector2 GetDoorPosition(int direction)
    {
        Vector2 half = hub.ArenaHalfSize;
        return new Vector2(DirectionX(direction) * (half.x - doorInset), DirectionY(direction) * (half.y - doorInset));
    }

    // 이 방에서 direction 쪽 문으로 이어진 방 번호. 없으면 -1.
    public int FindNeighbor(int roomIndex, int direction)
    {
        int found = -1;
        RoomInfo room = rooms[roomIndex];
        if ((room.doors & (1 << direction)) != 0)
        {
            int targetX = room.x + DirectionX(direction);
            int targetY = room.y + DirectionY(direction);
            for (int i = 0; i < rooms.Count && found < 0; i++)
            {
                RoomInfo other = rooms[i];
                if (other.x == targetX && other.y == targetY)
                {
                    found = i;
                }
            }
        }
        return found;
    }

    // ------------------------------------------------------------ 서버: 층 만들기

    public override void OnStartServer()
    {
        manager = NetworkManager.singleton as GameNetworkManager;
        random = new System.Random(System.Environment.TickCount);
        runStartTime = Time.time;
        floorNumber = 1;
        runEnded = false;
        ServerGenerateFloor();
        ServerEnterRoom(0, -1);
    }

    // 시작 방에서 무작위로 가지를 뻗어 방을 놓는다. 가장 깊은 방이 보스 방이 된다.
    private void ServerGenerateFloor()
    {
        rooms.Clear();
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = -1;
        }

        int roomCount = random.Next(minRooms, maxRooms + 1);
        RoomInfo start = new RoomInfo();
        start.x = GridSize / 2;
        start.y = GridSize / 2;
        start.type = RoomType.Start;
        rooms.Add(start);
        grid[start.y * GridSize + start.x] = 0;

        int guard = 0;
        while (rooms.Count < roomCount && guard < 2000)
        {
            guard++;
            int parentIndex = random.Next(0, rooms.Count);
            int direction = random.Next(0, DirectionCount);
            RoomInfo parent = rooms[parentIndex];
            int x = parent.x + DirectionX(direction);
            int y = parent.y + DirectionY(direction);
            bool inside = x >= 0 && x < GridSize && y >= 0 && y < GridSize;
            if (inside && grid[y * GridSize + x] < 0)
            {
                RoomInfo room = new RoomInfo();
                room.x = (byte)x;
                room.y = (byte)y;
                room.type = RoomType.Combat;
                room.depth = (byte)(parent.depth + 1);
                room.doors = (byte)(1 << ((direction + 2) % DirectionCount));
                parent.doors = (byte)(parent.doors | (1 << direction));
                rooms[parentIndex] = parent;
                grid[y * GridSize + x] = rooms.Count;
                rooms.Add(room);
            }
        }

        // 가장 깊은 방(항상 막다른 방이다)이 보스 방
        int bossIndex = 1;
        for (int i = 1; i < rooms.Count; i++)
        {
            if (rooms[i].depth > rooms[bossIndex].depth)
            {
                bossIndex = i;
            }
        }
        ServerSetRoomType(bossIndex, RoomType.Boss);

        // 나머지 전투 방 가운데 무작위로 보물, 상점, 대장간, 엘리트를 하나씩 둔다.
        ServerAssignSpecial(RoomType.Treasure);
        ServerAssignSpecial(RoomType.Shop);
        ServerAssignSpecial(RoomType.Forge);
        ServerAssignSpecial(RoomType.Elite);

        // 낚시터, 광맥, 마구간 가운데 둘만 나온다.
        int skipped = random.Next(0, 3);
        for (int i = 0; i < 3; i++)
        {
            if (i != skipped)
            {
                ServerAssignSpecial((RoomType)((int)RoomType.Fishing + i));
            }
        }
    }

    private void ServerSetRoomType(int index, RoomType type)
    {
        RoomInfo room = rooms[index];
        room.type = type;
        rooms[index] = room;
    }

    private void ServerAssignSpecial(RoomType type)
    {
        int combatCount = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].type == RoomType.Combat)
            {
                combatCount++;
            }
        }
        // 전투 방은 적어도 두 개 남긴다.
        if (combatCount > 2)
        {
            int pick = random.Next(0, combatCount);
            bool assigned = false;
            for (int i = 0; i < rooms.Count && !assigned; i++)
            {
                if (rooms[i].type == RoomType.Combat)
                {
                    if (pick == 0)
                    {
                        ServerSetRoomType(i, type);
                        assigned = true;
                    }
                    pick--;
                }
            }
        }
    }

    // ------------------------------------------------------------ 서버: 방 진행

    // fromDirection은 지나온 문의 방향(이전 방 기준). -1이면 층의 시작이라 가운데에 선다.
    private void ServerEnterRoom(int index, int fromDirection)
    {
        RoomInfo room = rooms[index];
        bool firstVisit = room.visited == 0;
        room.visited = 1;
        currentRoom = index;
        holdDirection = -1;
        holdTimer = 0f;
        stairsOpen = room.type == RoomType.Boss && room.cleared != 0;
        chestAvailable = room.type == RoomType.Treasure && room.cleared == 0;

        bool combat = room.type == RoomType.Combat || room.type == RoomType.Elite || room.type == RoomType.Boss;
        bool newShop = room.type == RoomType.Shop && room.cleared == 0;
        // 몬스터와 사람을 세우기 전에 장애물을 놓아 겹치지 않게 한다.
        decor.Apply(floorNumber, index, room.type);
        if (room.cleared == 0)
        {
            if (combat)
            {
                roomLocked = true;
                ServerSpawnRoomMonsters(room);
            }
            else if (room.type != RoomType.Treasure)
            {
                room.cleared = 1;
            }
        }
        rooms[index] = room;

        // 들어온 문의 반대편(새 방 기준으로는 지나온 방향의 맞은편 문) 안쪽에 세운다.
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            player.Inventory.ServerClearDrops();
            player.Facility.ServerOnRoomEntered(firstVisit, room.type);
            if (newShop)
            {
                player.Inventory.ServerSetShopStock(shopRarityBonus + 0.3f * (floorNumber - 1));
            }
            Vector2 position = hub.GetSpawnPoint(player.Slot);
            if (fromDirection >= 0)
            {
                int entryDirection = (fromDirection + 2) % DirectionCount;
                Vector2 door = GetDoorPosition(entryDirection);
                Vector2 inward = new Vector2(-DirectionX(entryDirection), -DirectionY(entryDirection));
                Vector2 side = new Vector2(inward.y, -inward.x);
                position = door + inward * entryDistance + side * ((player.Slot - 1.5f) * 0.7f);
            }
            player.ServerTeleport(position);
        }
        revision++;
        viewDirty = true;
    }

    private void ServerSpawnRoomMonsters(in RoomInfo room)
    {
        int count = Mathf.Min(maxMonsterCount, baseMonsterCount + room.depth + monstersPerFloor * (floorNumber - 1));
        float hpScale = 1f + hpScalePerDepth * room.depth + hpScalePerFloor * (floorNumber - 1);
        int kinds = floorNumber + room.depth;
        float rarityBonus = 0.2f * (floorNumber - 1);
        float damageScale = 1f + damageScalePerFloor * (floorNumber - 1);
        MonsterRank specialRank = MonsterRank.Normal;
        int specialDefinition = 0;
        bool fallenBoss = false;
        if (room.type == RoomType.Elite)
        {
            // 엘리트 한 마리와 일반 무리
            count += eliteExtraCount;
            hpScale *= eliteHpScale;
            rarityBonus += 0.5f;
            specialRank = MonsterRank.Elite;
        }
        else if (room.type == RoomType.Boss)
        {
            // 보스 한 마리와 부하 몇 마리. 보스의 종류는 층마다 바뀐다.
            count = 1 + bossMonsterCount + floorNumber - 1;
            hpScale *= bossHpScale;
            kinds = monsterDirector.DefinitionCount;
            rarityBonus += 0.5f;
            specialRank = MonsterRank.Boss;
            specialDefinition = (floorNumber - 1) % monsterDirector.DefinitionCount;
            // 짝수 층의 보스는 타락한 모험가다.
            fallenBoss = floorNumber % 2 == 0;
        }
        monsterDirector.ServerSpawnGroup(count, hpScale, kinds, rarityBonus, damageScale, specialRank, specialDefinition, fallenBoss);
    }

    // 방을 끝냈을 때: 문이 열리고, 쓰러진 동료가 부활하고, 방 종류에 따라 보상이 나온다.
    private void ServerClearRoom()
    {
        RoomInfo room = rooms[currentRoom];
        room.cleared = 1;
        rooms[currentRoom] = room;
        roomLocked = false;

        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            Vector2 offset = new Vector2((player.Slot - 1.5f) * 0.8f, -0.6f);
            int oreIndex = 0;
            if (room.type == RoomType.Elite)
            {
                oreIndex = 1;
            }
            else if (room.type == RoomType.Boss)
            {
                oreIndex = 2;
            }
            player.Inventory.ServerAddOre(oreRewards[oreIndex]);
            if (player.IsDead)
            {
                player.ServerRevive(offset, reviveHpFraction);
            }
            if (room.type == RoomType.Elite)
            {
                player.Inventory.ServerOfferDrop(player.Inventory.Balance.CreateRandom(eliteRarityBonus), offset + new Vector2(0f, 1.2f));
            }
            else if (room.type == RoomType.Boss)
            {
                player.Inventory.ServerOfferDrop(player.Inventory.Balance.CreateRandom(bossRarityBonus), offset + new Vector2(-0.4f, 1.2f));
                player.Inventory.ServerOfferDrop(player.Inventory.Balance.CreateRandom(bossRarityBonus), offset + new Vector2(0.4f, 1.2f));
            }
        }
        if (room.type == RoomType.Boss)
        {
            stairsOpen = true;
        }
        revision++;
        viewDirty = true;
    }

    private void ServerOpenChest()
    {
        chestAvailable = false;
        RoomInfo room = rooms[currentRoom];
        room.cleared = 1;
        rooms[currentRoom] = room;
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerInventory inventory = hub.GetPlayer(i).Inventory;
            inventory.ServerAddOre(oreRewards[3]);
            for (int n = 0; n < treasureItemCount; n++)
            {
                float angle = (i * treasureItemCount + n) * 1.1f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.3f;
                inventory.ServerOfferDrop(inventory.Balance.CreateRandom(treasureRarityBonus + 0.3f * (floorNumber - 1)), offset);
            }
        }
        revision++;
    }

    // direction 쪽 문으로 이동한다. 문이 없거나 잠겨 있으면 false.
    public bool ServerTravel(int direction)
    {
        bool moved = false;
        if (!roomLocked && !runEnded && direction >= 0 && direction < DirectionCount)
        {
            int neighbor = FindNeighbor(currentRoom, direction);
            if (neighbor >= 0)
            {
                ServerEnterRoom(neighbor, direction);
                moved = true;
            }
        }
        return moved;
    }

    // 계단: 다음 층으로. 마지막 층이었다면 탑을 정복한 것이다.
    public void ServerClimb()
    {
        if (stairsOpen && !runEnded)
        {
            if (floorNumber >= maxFloors)
            {
                ServerEndRun(true);
            }
            else
            {
                floorNumber++;
                for (int i = 0; i < hub.PlayerCount; i++)
                {
                    hub.GetPlayer(i).Inventory.ServerRefillPotions();
                }
                ServerGenerateFloor();
                ServerEnterRoom(0, -1);
            }
        }
    }

    private void ServerEndRun(bool victory)
    {
        runEnded = true;
        runEndTimer = runEndDelay;
        int kills = monsterDirector.TotalKills;
        int soul = soulPerFloor * (floorNumber - 1) + kills / killsPerSoul;
        if (victory)
        {
            soul += soulPerFloor + soulVictoryBonus;
        }
        RpcRunEnded(victory, floorNumber, kills, Mathf.RoundToInt(Time.time - runStartTime), soul);
    }

    // 결과 화면을 띄우고, 각자 자기 기기에 영혼석을 저장한다.
    [ClientRpc]
    private void RpcRunEnded(bool victory, int floor, int kills, int seconds, int soulGain)
    {
        hub.Meta.AddSoulstones(soulGain);
        hub.Meta.Save();
        int soulTotal = hub.Meta.Soulstones;
        int level = 1;
        int gold = 0;
        if (NetworkClient.localPlayer != null)
        {
            PlayerController player;
            if (NetworkClient.localPlayer.TryGetComponent(out player))
            {
                level = player.Skills.Level;
                gold = player.Inventory.Gold;
            }
        }
        hub.ResultUi.Show(victory, floor, kills, seconds, level, gold, soulGain, soulTotal);
    }

    private void Update()
    {
        if (isServer)
        {
            ServerTick(Time.deltaTime);
        }
        if (isClient && viewDirty)
        {
            if (currentRoom >= 0 && currentRoom < rooms.Count)
            {
                viewDirty = false;
                RefreshView();
            }
        }
    }

    private void ServerTick(float deltaTime)
    {
        if (runEnded)
        {
            runEndTimer -= deltaTime;
            if (runEndTimer <= 0f)
            {
                runEndTimer = float.MaxValue;
                manager.ServerReturnToLobby();
            }
        }
        else if (hub.PlayerCount > 0)
        {
            int aliveCount = 0;
            int count = hub.PlayerCount;
            for (int i = 0; i < count; i++)
            {
                if (!hub.GetPlayer(i).IsDead)
                {
                    aliveCount++;
                }
            }

            if (aliveCount == 0)
            {
                ServerEndRun(false);
            }
            else if (roomLocked)
            {
                if (monsterDirector.IsGroupFinished)
                {
                    ServerClearRoom();
                }
            }
            else
            {
                ServerTickGathering(deltaTime);
            }
        }
    }

    // 살아 있는 전원이 같은 문(또는 계단) 앞에 잠시 모여 있으면 이동한다. 보물 상자는 누구든 다가가면 열린다.
    private void ServerTickGathering(float deltaTime)
    {
        if (chestAvailable && ServerAnyAliveWithin(Vector2.zero, chestRadius))
        {
            ServerOpenChest();
        }

        int gathered = -1;
        for (int direction = 0; direction < DirectionCount && gathered < 0; direction++)
        {
            if (FindNeighbor(currentRoom, direction) >= 0)
            {
                if (ServerAllAliveWithin(GetDoorPosition(direction), doorRadius))
                {
                    gathered = direction;
                }
            }
        }
        // 계단은 방향 번호 4로 취급한다.
        if (gathered < 0 && stairsOpen)
        {
            if (ServerAllAliveWithin(Vector2.zero, stairsRadius))
            {
                gathered = DirectionCount;
            }
        }

        if (gathered >= 0 && gathered == holdDirection)
        {
            holdTimer += deltaTime;
            if (holdTimer >= doorHoldTime)
            {
                holdTimer = 0f;
                holdDirection = -1;
                if (gathered == DirectionCount)
                {
                    ServerClimb();
                }
                else
                {
                    ServerTravel(gathered);
                }
            }
        }
        else
        {
            holdDirection = gathered;
            holdTimer = 0f;
        }
    }

    private bool ServerAllAliveWithin(Vector2 point, float radius)
    {
        bool all = true;
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            if (!player.IsDead)
            {
                if (((Vector2)player.transform.position - point).sqrMagnitude > radius * radius)
                {
                    all = false;
                }
            }
        }
        return all;
    }

    private bool ServerAnyAliveWithin(Vector2 point, float radius)
    {
        bool any = false;
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            if (!player.IsDead)
            {
                if (((Vector2)player.transform.position - point).sqrMagnitude <= radius * radius)
                {
                    any = true;
                }
            }
        }
        return any;
    }

    // ------------------------------------------------------------ 클라이언트 표시

    public override void OnStartClient()
    {
        rooms.OnChange = OnRoomsChanged;
        viewDirty = true;
    }

    private void OnRoomsChanged(SyncList<RoomInfo>.Operation operation, int index, RoomInfo room)
    {
        revision++;
        viewDirty = true;
    }

    private void OnStateChanged(int oldValue, int newValue)
    {
        revision++;
        viewDirty = true;
    }

    // 문이 잠기면 철컥, 적을 다 잡아 열리면 방 정리 소리와 문 열리는 소리
    private void OnRoomLockChanged(bool oldValue, bool newValue)
    {
        viewDirty = true;
        if (newValue && !oldValue)
        {
            hub.Sound.Play(SoundId.DoorLock);
        }
        else if (oldValue && !newValue)
        {
            hub.Sound.Play(SoundId.RoomClear);
            hub.Sound.Play(SoundId.DoorOpen);
        }
    }

    private void OnChestChanged(bool oldValue, bool newValue)
    {
        viewDirty = true;
        if (oldValue && !newValue)
        {
            hub.Sound.Play(SoundId.ChestOpen);
        }
    }

    private void OnStairsChanged(bool oldValue, bool newValue)
    {
        viewDirty = true;
        if (newValue && !oldValue)
        {
            hub.Sound.Play(SoundId.StairsOpen);
        }
    }

    // 문, 배경 색, 상자·계단·시설 표시, 화면 위쪽 방 이름을 현재 방에 맞춘다.
    private void RefreshView()
    {
        RoomInfo room = rooms[currentRoom];
        for (int direction = 0; direction < doors.Length; direction++)
        {
            int neighbor = FindNeighbor(currentRoom, direction);
            if (neighbor >= 0)
            {
                doors[direction].Show(roomLocked, roomNames[(int)rooms[neighbor].type]);
            }
            else
            {
                doors[direction].Hide();
            }
        }

        backgroundRenderer.color = roomTints[(int)room.type];
        decor.Apply(floorNumber, currentRoom, room.type);
        // 보스와 싸우는 동안만 보스 음악(같은 곡이면 그대로 이어진다)
        if (room.type == RoomType.Boss && roomLocked)
        {
            hub.Sound.PlayMusic(MusicId.Boss);
        }
        else
        {
            hub.Sound.PlayMusic(MusicId.Combat);
        }
        chestObject.SetActive(chestAvailable);
        stairsObject.SetActive(stairsOpen);
        bool facility = room.type == RoomType.Shop || room.type == RoomType.Forge || room.type == RoomType.Start
            || room.type == RoomType.Fishing || room.type == RoomType.Ore || room.type == RoomType.Stable;
        facilityObject.SetActive(facility);
        if (facility)
        {
            if (room.type == RoomType.Start)
            {
                facilityLabel.text = "전직 제단";
            }
            else
            {
                facilityLabel.text = roomNames[(int)room.type] + "  (F)";
            }
        }

        labelBuilder.Clear();
        labelBuilder.Append(floorNumber).Append("층  ").Append(roomNames[(int)room.type]);
        hub.Hud.SetTopLabel(labelBuilder);
    }
}
