using System.Collections.Generic;
using Mirror;
using UnityEngine;

// 몬스터 풀, 웨이브 스폰, 일괄 갱신을 담당한다. 씬에 하나 있는 네트워크 오브젝트.
// 몬스터는 프리팹 하나를 종류와 무관하게 재사용하고, 종류는 MonsterDefinition 번호로 구분한다.
public class MonsterDirector : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private MonsterDefinition[] definitions;

    [Header("풀")]
    [SerializeField] private int prewarmCount = 24;

    [Header("스폰")]
    [SerializeField] private float firstSpawnDelay = 0.6f;
    [SerializeField] private float spawnInterval = 0.15f;
    [SerializeField] private float minSpawnDistance = 4f;
    [Tooltip("이 반지름 안에 장애물이 있으면 다른 자리를 고른다")]
    [SerializeField] private float spawnClearRadius = 0.6f;
    [SerializeField] private float hpScalePerExtraPlayer = 0.5f;

    [Header("드랍(플레이어마다 따로 굴림)")]
    [SerializeField] private float dropChance = 0.22f;
    [SerializeField] private int goldMin = 2;
    [SerializeField] private int goldMax = 5;
    [Tooltip("한 마리를 잡을 때마다 모든 플레이어가 받는 경험치")]
    [SerializeField] private int xpPerKill = 8;
    [Tooltip("등급별 경험치 배율(일반, 엘리트, 보스)")]
    [SerializeField] private int[] rankXpFactors = { 1, 4, 15 };

    [Header("타락한 모험가")]
    [Tooltip("엘리트가 타락한 모험가로 나올 확률")]
    [SerializeField] private float fallenEliteChance = 0.5f;
    [Tooltip("투사체가 거는 상태이상의 지속 시간과 0.5초마다의 피해 비율")]
    [SerializeField] private float shotStatusDuration = 3f;
    [SerializeField] private float shotDotFactor = 0.1f;

    [SyncVar] private int aliveCount;

    private readonly Stack<MonsterController> freeMonsters = new Stack<MonsterController>(32);
    private readonly List<MonsterController> activeMonsters = new List<MonsterController>(32);
    private SpawnHandlerDelegate spawnHandler;
    private UnSpawnDelegate unspawnHandler;
    private bool clientRegistered;
    private bool prewarmed;

    private float spawnTimer;
    private int pendingSpawnCount;
    private float groupHpScale = 1f;
    private float groupRarityBonus;
    private int groupKinds = 1;
    private float groupDamageScale = 1f;
    private MonsterRank pendingSpecialRank;
    private int specialDefinitionIndex;
    private bool specialFallen;
    private MonsterController currentBoss;
    private int totalKills;

    // 지금 방의 몬스터 무리가 모두 나왔고 모두 죽었는지
    public bool IsGroupFinished => pendingSpawnCount == 0 && aliveCount == 0;
    public int DefinitionCount => definitions.Length;
    public int AliveCount => aliveCount;
    public bool HasPendingSpawns => pendingSpawnCount > 0;
    // 이번 판에서 잡은 몬스터 수(서버)
    public int TotalKills => totalKills;
    // 지금 방에 살아 있는 보스(없으면 null). 화면 위 보스 체력 바가 쓴다.
    public MonsterController CurrentBoss => currentBoss;

    // 네트워크 오브젝트는 Mirror가 씬 검사를 끝낸 뒤(네트워크 시작 후)에 만들어야 한다.
    // Awake에서 미리 만들면 씬 오브젝트로 오인되어 오류가 난다.
    private void Prewarm()
    {
        if (!prewarmed)
        {
            prewarmed = true;
            for (int i = 0; i < prewarmCount; i++)
            {
                ReturnToPool(CreateInstance());
            }
        }
    }

    public MonsterDefinition GetDefinition(int index)
    {
        return definitions[index];
    }

    // ------------------------------------------------------------ 풀

    private MonsterController CreateInstance()
    {
        GameObject instance = Instantiate(monsterPrefab);
        instance.SetActive(false);
        MonsterController monster = instance.GetComponent<MonsterController>();
        monster.Setup(this, hub);
        return monster;
    }

    private MonsterController Rent()
    {
        MonsterController monster;
        if (freeMonsters.Count > 0)
        {
            monster = freeMonsters.Pop();
        }
        else
        {
            monster = CreateInstance();
        }
        monster.SetInPool(false);
        return monster;
    }

    // 풀 반납은 반드시 이 함수로만 한다.
    // 호스트에서는 NetworkServer.UnSpawn이 클라이언트용 해제 처리(ClientUnspawnMonster)까지 직접 부르므로,
    // 서버 쪽 반납과 겹쳐 같은 몬스터가 두 번 들어오지 않게 막는다.
    private void ReturnToPool(MonsterController monster)
    {
        if (!monster.InPool)
        {
            monster.SetInPool(true);
            freeMonsters.Push(monster);
        }
    }

    public void AddActive(MonsterController monster)
    {
        activeMonsters.Add(monster);
        if (monster.Rank == MonsterRank.Boss)
        {
            currentBoss = monster;
        }
    }

    public void RemoveActive(MonsterController monster)
    {
        if (currentBoss == monster)
        {
            currentBoss = null;
        }
        int index = activeMonsters.IndexOf(monster);
        if (index >= 0)
        {
            int last = activeMonsters.Count - 1;
            activeMonsters[index] = activeMonsters[last];
            activeMonsters.RemoveAt(last);
        }
    }

    // ------------------------------------------------------------ 클라이언트 스폰 처리(풀 사용)

    public void ClientRegisterPrefab()
    {
        if (!clientRegistered)
        {
            clientRegistered = true;
            if (spawnHandler == null)
            {
                spawnHandler = ClientSpawnMonster;
                unspawnHandler = ClientUnspawnMonster;
            }
            NetworkClient.RegisterPrefab(monsterPrefab, spawnHandler, unspawnHandler);
        }
    }

    public void ClientUnregisterPrefab()
    {
        if (clientRegistered)
        {
            clientRegistered = false;
            NetworkClient.UnregisterPrefab(monsterPrefab);
        }
    }

    private GameObject ClientSpawnMonster(SpawnMessage message)
    {
        MonsterController monster = Rent();
        monster.transform.SetPositionAndRotation(message.position, message.rotation);
        monster.gameObject.SetActive(true);
        return monster.gameObject;
    }

    private void ClientUnspawnMonster(GameObject spawned)
    {
        spawned.SetActive(false);
        MonsterController monster;
        if (spawned.TryGetComponent(out monster))
        {
            ReturnToPool(monster);
        }
    }

    // ------------------------------------------------------------ 서버

    public override void OnStartServer()
    {
        Prewarm();
        aliveCount = 0;
        pendingSpawnCount = 0;
    }

    public override void OnStopServer()
    {
        // 서버가 내려가면 Mirror가 스폰된 오브젝트를 파괴하므로 목록을 비운다.
        activeMonsters.Clear();
        freeMonsters.Clear();
        prewarmed = false;
    }

    public override void OnStartClient()
    {
        Prewarm();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        bool server = isServer;

        if (server)
        {
            ServerTickSpawn(deltaTime);
        }

        // 뒤에서부터 돌면 Tick 도중 자기 자신이 목록에서 빠져도 안전하다.
        for (int i = activeMonsters.Count - 1; i >= 0; i--)
        {
            MonsterController monster = activeMonsters[i];
            if (server)
            {
                monster.ServerTick(deltaTime);
            }
            monster.VisualTick(deltaTime);
        }
    }

    // 방에 들어갔을 때 FloorDirector가 부른다. count마리를 짧은 간격으로 차례로 내보낸다.
    // kinds는 섞어 낼 몬스터 종류 수(앞에서부터), rarityBonus는 드랍 등급 보정이다.
    // damageScale은 공격력 배율, specialRank가 Normal이 아니면 첫 마리가 그 등급으로 나온다(보스는 specialDefinition 종류).
    // fallenSpecial이 true면 그 보스는 타락한 모험가다. 엘리트는 확률로 타락한 모험가가 된다.
    public void ServerSpawnGroup(int count, float hpScale, int kinds, float rarityBonus, float damageScale, MonsterRank specialRank, int specialDefinition, bool fallenSpecial)
    {
        pendingSpawnCount = count;
        groupHpScale = hpScale;
        groupKinds = Mathf.Clamp(kinds, 1, definitions.Length);
        groupRarityBonus = rarityBonus;
        groupDamageScale = damageScale;
        pendingSpecialRank = specialRank;
        specialDefinitionIndex = specialDefinition;
        specialFallen = fallenSpecial;
        spawnTimer = firstSpawnDelay;
    }

    // 보스가 부하를 부를 때. 지금 방의 무리에 더해진다.
    public void ServerSpawnMinions(int count)
    {
        if (pendingSpawnCount == 0)
        {
            spawnTimer = spawnInterval;
        }
        pendingSpawnCount += count;
    }

    private void ServerTickSpawn(float deltaTime)
    {
        if (pendingSpawnCount > 0)
        {
            spawnTimer -= deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = spawnInterval;
                pendingSpawnCount--;
                ServerSpawnOne();
            }
        }
    }

    private void ServerSpawnOne()
    {
        int definitionIndex = Random.Range(0, groupKinds);
        float hpScale = groupHpScale * (1f + hpScalePerExtraPlayer * (hub.PlayerCount - 1));

        // 무리의 첫 마리는 엘리트나 보스일 수 있다(방 종류에 따라 FloorDirector가 정한다).
        MonsterRank rank = pendingSpecialRank;
        EliteModifier modifier = EliteModifier.None;
        pendingSpecialRank = MonsterRank.Normal;
        bool fallen = false;
        if (rank == MonsterRank.Elite)
        {
            modifier = (EliteModifier)Random.Range(1, 4);
            fallen = Random.value < fallenEliteChance;
        }
        else if (rank == MonsterRank.Boss)
        {
            definitionIndex = Mathf.Clamp(specialDefinitionIndex, 0, definitions.Length - 1);
            fallen = specialFallen;
        }
        // 타락한 모험가는 근접 공격 수치(첫 번째 몬스터 종류)를 바탕으로 한다.
        if (fallen)
        {
            definitionIndex = 0;
        }

        MonsterController monster = Rent();
        monster.transform.SetPositionAndRotation(PickSpawnPoint(), Quaternion.identity);
        monster.gameObject.SetActive(true);
        monster.ServerInit(definitionIndex, hpScale, groupDamageScale, rank, modifier, fallen);
        NetworkServer.Spawn(monster.gameObject);
        aliveCount++;
    }

    private Vector2 PickSpawnPoint()
    {
        Vector2 half = hub.ArenaHalfSize;
        Vector2 point = Vector2.zero;
        float minSqr = minSpawnDistance * minSpawnDistance;
        bool found = false;
        for (int attempt = 0; attempt < 20 && !found; attempt++)
        {
            point = new Vector2(Random.Range(-half.x + 1f, half.x - 1f), Random.Range(-half.y + 1f, half.y - 1f));
            // 방 장애물 위에는 세우지 않는다.
            found = Physics2D.OverlapCircle(point, spawnClearRadius, GameLayers.WallMask) == null;
            int count = hub.PlayerCount;
            for (int i = 0; i < count; i++)
            {
                Vector2 toPlayer = (Vector2)hub.GetPlayer(i).transform.position - point;
                if (toPlayer.sqrMagnitude < minSqr)
                {
                    found = false;
                }
            }
        }
        return point;
    }

    public void ServerOnMonsterKilled(MonsterController monster)
    {
        aliveCount = Mathf.Max(0, aliveCount - 1);
        totalKills++;

        // 드랍과 골드는 개인별: 플레이어마다 따로 굴린다.
        Vector2 position = monster.transform.position;
        for (int i = 0; i < hub.PlayerCount; i++)
        {
            int goldAmount = Random.Range(goldMin, goldMax + 1);
            hub.GetPlayer(i).Inventory.ServerOnMonsterKilled(position, dropChance, groupRarityBonus, goldAmount);
            hub.GetPlayer(i).Skills.ServerAddXp(xpPerKill * rankXpFactors[(int)monster.Rank]);
            hub.GetPlayer(i).Skills.ServerOnKill();
        }
    }

    public void ServerDespawn(MonsterController monster)
    {
        NetworkServer.UnSpawn(monster.gameObject);
        monster.gameObject.SetActive(false);
        ReturnToPool(monster);
    }

    // ------------------------------------------------------------ 투사체
    // 투사체는 네트워크 오브젝트로 만들지 않는다. 서버는 판정용, 클라이언트는 표시용을 각자 풀에서 꺼내 같은 궤적으로 움직인다.

    public void ServerFireProjectile(Vector2 origin, Vector2 direction, int definitionIndex, int damage, StatusKind status)
    {
        LaunchMonsterShot(origin, direction, definitionIndex, damage, true, status);
        RpcFireProjectile(origin, direction, definitionIndex);
    }

    private void LaunchMonsterShot(Vector2 origin, Vector2 direction, int definitionIndex, int damage, bool authoritative, StatusKind status)
    {
        MonsterDefinition definition = definitions[definitionIndex];
        ProjectileShot shot = new ProjectileShot();
        shot.origin = origin;
        shot.direction = direction;
        shot.speed = definition.ProjectileSpeed;
        shot.lifetime = definition.ProjectileLifetime;
        shot.radius = definition.ProjectileRadius;
        shot.damage = damage;
        shot.look = ProjectileLook.Spore;
        shot.color = definition.Tint;
        shot.hitsMonsters = false;
        shot.authoritative = authoritative;
        shot.playerStatus = status;
        shot.playerStatusDuration = shotStatusDuration;
        shot.playerStatusDot = Mathf.Max(1, Mathf.RoundToInt(damage * shotDotFactor));
        hub.ProjectilePool.Launch(shot);
    }

    [ClientRpc]
    private void RpcFireProjectile(Vector2 origin, Vector2 direction, int definitionIndex)
    {
        if (!isServer)
        {
            LaunchMonsterShot(origin, direction, definitionIndex, 0, false, StatusKind.None);
        }
    }
}
