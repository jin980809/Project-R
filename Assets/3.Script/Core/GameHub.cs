using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 씬마다 하나 있는 참조 허브(로비 씬, 전투 씬 각각).
// 네트워크로 생성되는 오브젝트는 GameNetworkManager.Hub를 통해 현재 씬의 허브에 접근한다.
public class GameHub : MonoBehaviour
{
    [Header("공통")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private HudController hud;
    [SerializeField] private DamageTextPool damageTextPool;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private ItemDropPool itemDropPool;
    [SerializeField] private InventoryUi inventoryUi;
    [SerializeField] private FacilityUi facilityUi;
    [SerializeField] private ResultUi resultUi;
    [SerializeField] private SkillUi skillUi;
    // 로비 마을 화면(해금 상인, 도감, 설정). 전투 씬에서는 비운다.
    [SerializeField] private MetaUi metaUi;
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(9.2f, 5f);
    [SerializeField] private Vector2[] spawnPoints;

    [Header("전투 씬 전용(로비에서는 비움)")]
    [SerializeField] private MonsterDirector monsterDirector;
    [SerializeField] private ProjectilePool projectilePool;
    [SerializeField] private FloorDirector floorDirector;

    [Header("로비 전용")]
    [SerializeField] private bool hasStartZone;
    [SerializeField] private Vector2 startZoneCenter;
    [SerializeField] private float startZoneRadius = 1f;

    private readonly List<PlayerController> players = new List<PlayerController>(4);
    private readonly Collider2D[] hitBuffer = new Collider2D[32];
    private ContactFilter2D monsterFilter;
    private bool inputBlocked;
    private readonly MetaProgress meta = new MetaProgress();

    public Camera MainCamera => mainCamera;
    public CameraFollow CameraFollow => cameraFollow;
    public HudController Hud => hud;
    public MonsterDirector MonsterDirector => monsterDirector;
    public ProjectilePool ProjectilePool => projectilePool;
    public FloorDirector FloorDirector => floorDirector;
    public DamageTextPool DamageTextPool => damageTextPool;
    public ItemDropPool ItemDropPool => itemDropPool;
    public InventoryUi InventoryUi => inventoryUi;
    public FacilityUi FacilityUi => facilityUi;
    public ResultUi ResultUi => resultUi;
    public SkillUi SkillUi => skillUi;
    public MetaUi MetaUi => metaUi;
    public Vector2 ArenaHalfSize => arenaHalfSize;
    public Collider2D[] HitBuffer => hitBuffer;
    public int PlayerCount => players.Count;
    public bool IsCombatScene => monsterDirector != null;
    public bool InputBlocked => inputBlocked;
    // 영혼석, 영구 해금, 도감, 설정(기기에 저장)
    public MetaProgress Meta => meta;

    private void Awake()
    {
        meta.Load();
        ApplySettings();
        Physics2D.IgnoreLayerCollision(GameLayers.Player, GameLayers.Monster, true);
        Physics2D.IgnoreLayerCollision(GameLayers.Player, GameLayers.Player, true);

        monsterFilter = new ContactFilter2D();
        monsterFilter.SetLayerMask(GameLayers.MonsterMask);
        monsterFilter.useTriggers = false;

        // 몬스터 스폰 메시지가 오기 전에 풀을 쓰는 스폰 처리를 등록해 둔다.
        if (monsterDirector != null)
        {
            monsterDirector.ClientRegisterPrefab();
        }
    }

    // 설정(화면 흔들림, 피해 숫자)을 실제 동작에 반영한다. 설정을 바꾼 뒤에도 부른다.
    public void ApplySettings()
    {
        cameraFollow.SetShakeEnabled(meta.ShakeEnabled);
        damageTextPool.SetShowEnabled(meta.DamageTextEnabled);
    }

    private void OnDestroy()
    {
        meta.Save();
        if (monsterDirector != null)
        {
            monsterDirector.ClientUnregisterPrefab();
        }
    }

    public PlayerController GetPlayer(int index)
    {
        return players[index];
    }

    public void AddPlayer(PlayerController player)
    {
        if (!players.Contains(player))
        {
            players.Add(player);
        }
    }

    public void RemovePlayer(PlayerController player)
    {
        players.Remove(player);
    }

    public Vector2 GetSpawnPoint(int slot)
    {
        Vector2 point = Vector2.zero;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            point = spawnPoints[Mathf.Abs(slot) % spawnPoints.Length];
        }
        return point;
    }

    // 주소 입력처럼 키보드를 UI가 쓰는 동안 캐릭터 조작을 막는다.
    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
    }

    public bool IsPointerOverUi()
    {
        bool over = false;
        if (eventSystem != null)
        {
            over = eventSystem.IsPointerOverGameObject();
        }
        return over;
    }

    public bool IsInStartZone(Vector2 point)
    {
        bool inside = false;
        if (hasStartZone)
        {
            inside = (point - startZoneCenter).sqrMagnitude <= startZoneRadius * startZoneRadius;
        }
        return inside;
    }

    // 결과는 HitBuffer에 담기고 개수를 반환한다. 할당 없음.
    public int OverlapMonsters(Vector2 center, float radius)
    {
        return Physics2D.OverlapCircle(center, radius, monsterFilter, hitBuffer);
    }

    public PlayerController FindNearestAlivePlayer(Vector2 from)
    {
        PlayerController nearest = null;
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerController player = players[i];
            if (!player.IsDead)
            {
                float sqr = ((Vector2)player.transform.position - from).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = player;
                }
            }
        }
        return nearest;
    }

    public bool IsInsideArena(Vector2 point, float margin)
    {
        bool insideX = Mathf.Abs(point.x) <= arenaHalfSize.x - margin;
        bool insideY = Mathf.Abs(point.y) <= arenaHalfSize.y - margin;
        return insideX && insideY;
    }
}
