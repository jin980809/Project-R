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

    [Header("웨이브(1단계 시험용)")]
    [SerializeField] private float firstWaveDelay = 1.5f;
    [SerializeField] private float waveInterval = 2.5f;
    [SerializeField] private float spawnInterval = 0.15f;
    [SerializeField] private int baseCount = 3;
    [SerializeField] private int countPerWave = 2;
    [SerializeField] private int maxPerWave = 24;
    [SerializeField] private float minSpawnDistance = 4f;
    [SerializeField] private float hpScalePerWave = 0.12f;
    [SerializeField] private float hpScalePerExtraPlayer = 0.5f;

    [SyncVar] private int waveNumber;
    [SyncVar] private int aliveCount;

    private readonly Stack<MonsterController> freeMonsters = new Stack<MonsterController>(32);
    private readonly List<MonsterController> activeMonsters = new List<MonsterController>(32);
    private SpawnHandlerDelegate spawnHandler;
    private UnSpawnDelegate unspawnHandler;
    private bool clientRegistered;
    private bool prewarmed;

    private float waveTimer;
    private float spawnTimer;
    private int pendingSpawnCount;

    public int WaveNumber => waveNumber;
    public int AliveCount => aliveCount;

    // 네트워크 오브젝트는 Mirror가 씬 검사를 끝낸 뒤(네트워크 시작 후)에 만들어야 한다.
    // Awake에서 미리 만들면 씬 오브젝트로 오인되어 오류가 난다.
    private void Prewarm()
    {
        if (!prewarmed)
        {
            prewarmed = true;
            for (int i = 0; i < prewarmCount; i++)
            {
                freeMonsters.Push(CreateInstance());
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
        return monster;
    }

    public void AddActive(MonsterController monster)
    {
        activeMonsters.Add(monster);
    }

    public void RemoveActive(MonsterController monster)
    {
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
            freeMonsters.Push(monster);
        }
    }

    // ------------------------------------------------------------ 서버

    public override void OnStartServer()
    {
        Prewarm();
        waveNumber = 0;
        aliveCount = 0;
        pendingSpawnCount = 0;
        waveTimer = firstWaveDelay;
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
            ServerTickWaves(deltaTime);
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

    private void ServerTickWaves(float deltaTime)
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
        else if (aliveCount == 0)
        {
            if (hub.PlayerCount > 0)
            {
                waveTimer -= deltaTime;
                if (waveTimer <= 0f)
                {
                    waveNumber++;
                    pendingSpawnCount = Mathf.Min(maxPerWave, baseCount + countPerWave * (waveNumber - 1));
                    spawnTimer = 0f;
                    waveTimer = waveInterval;
                }
            }
        }
    }

    private void ServerSpawnOne()
    {
        int kinds = Mathf.Clamp(waveNumber, 1, definitions.Length);
        int definitionIndex = Random.Range(0, kinds);
        float hpScale = 1f + hpScalePerWave * (waveNumber - 1) + hpScalePerExtraPlayer * (hub.PlayerCount - 1);

        MonsterController monster = Rent();
        monster.transform.SetPositionAndRotation(PickSpawnPoint(), Quaternion.identity);
        monster.gameObject.SetActive(true);
        monster.ServerInit(definitionIndex, hpScale);
        NetworkServer.Spawn(monster.gameObject);
        aliveCount++;
    }

    private Vector2 PickSpawnPoint()
    {
        Vector2 half = hub.ArenaHalfSize;
        Vector2 point = Vector2.zero;
        float minSqr = minSpawnDistance * minSpawnDistance;
        bool found = false;
        for (int attempt = 0; attempt < 10 && !found; attempt++)
        {
            point = new Vector2(Random.Range(-half.x + 1f, half.x - 1f), Random.Range(-half.y + 1f, half.y - 1f));
            found = true;
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
    }

    public void ServerDespawn(MonsterController monster)
    {
        NetworkServer.UnSpawn(monster.gameObject);
        monster.gameObject.SetActive(false);
        freeMonsters.Push(monster);
    }

    // ------------------------------------------------------------ 투사체
    // 투사체는 네트워크 오브젝트로 만들지 않는다. 서버는 판정용, 클라이언트는 표시용을 각자 풀에서 꺼내 같은 궤적으로 움직인다.

    public void ServerFireProjectile(Vector2 origin, Vector2 direction, int definitionIndex)
    {
        hub.ProjectilePool.Launch(origin, direction, definitions[definitionIndex], true);
        RpcFireProjectile(origin, direction, definitionIndex);
    }

    [ClientRpc]
    private void RpcFireProjectile(Vector2 origin, Vector2 direction, int definitionIndex)
    {
        if (!isServer)
        {
            hub.ProjectilePool.Launch(origin, direction, definitions[definitionIndex], false);
        }
    }
}
