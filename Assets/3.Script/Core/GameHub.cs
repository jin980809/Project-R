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
    [Tooltip("예/아니오 확인 창(다음 층 포탈, 스킬 초기화)")]
    [SerializeField] private ConfirmUi confirmUi;
    [SerializeField] private SkillUi skillUi;
    // 로비 마을 화면(해금 상인, 도감, 설정). 전투 씬에서는 비운다.
    [SerializeField] private MetaUi metaUi;
    [SerializeField] private SoundManager sound;
    [SerializeField] private PauseMenuUi pauseMenu;
    [Tooltip("씬이 시작될 때 트는 배경음악")]
    [SerializeField] private MusicId sceneMusic = MusicId.Lobby;
    [SerializeField] private Vector2 arenaHalfSize = new Vector2(9.2f, 5f);
    [SerializeField] private Vector2[] spawnPoints;

    [Header("전투 씬 전용(로비에서는 비움)")]
    [SerializeField] private MonsterDirector monsterDirector;
    [SerializeField] private ProjectilePool projectilePool;
    [SerializeField] private SkillFxPlayer skillFx;
    [SerializeField] private FloorDirector floorDirector;
    [Tooltip("미니맵(M으로 큰 지도)")]
    [SerializeField] private MinimapUi minimap;

    [Header("로비 전용")]
    [SerializeField] private bool hasStartZone;
    [SerializeField] private Vector2 startZoneCenter;
    [SerializeField] private float startZoneRadius = 1f;
    [Tooltip("캐릭터 커스터마이징 화면(처음 실행, 미용실)")]
    [SerializeField] private CustomizeUi customizeUi;

    private readonly List<PlayerController> players = new List<PlayerController>(4);
    private readonly Collider2D[] hitBuffer = new Collider2D[32];
    private ContactFilter2D monsterFilter;
    private bool inputBlocked;
    private bool menuBlocked;
    private bool customizeBlocked;
    private readonly MetaProgress meta = new MetaProgress();

    public Camera MainCamera => mainCamera;
    public CameraFollow CameraFollow => cameraFollow;
    public HudController Hud => hud;
    public MonsterDirector MonsterDirector => monsterDirector;
    public ProjectilePool ProjectilePool => projectilePool;
    public SkillFxPlayer SkillFx => skillFx;
    public FloorDirector FloorDirector => floorDirector;
    public DamageTextPool DamageTextPool => damageTextPool;
    public ItemDropPool ItemDropPool => itemDropPool;
    public InventoryUi InventoryUi => inventoryUi;
    public FacilityUi FacilityUi => facilityUi;
    public ResultUi ResultUi => resultUi;
    public ConfirmUi ConfirmUi => confirmUi;
    public MinimapUi Minimap => minimap;
    public SkillUi SkillUi => skillUi;
    public MetaUi MetaUi => metaUi;
    public Vector2 ArenaHalfSize => arenaHalfSize;
    public Collider2D[] HitBuffer => hitBuffer;
    public int PlayerCount => players.Count;
    public bool IsCombatScene => monsterDirector != null;
    public bool InputBlocked => inputBlocked || menuBlocked || customizeBlocked;
    // 본인 플레이어의 입력(키 이름 표시와 키 설정 화면용). 본인 플레이어가 없으면 null.
    public GameInput LocalInput => localInput;
    private GameInput localInput;

    public void SetLocalInput(GameInput value)
    {
        localInput = value;
    }

    // 상호작용 등 바꿀 수 있는 키의 지금 이름. 본인 입력이 없으면 기본 키 이름.
    public string GetKeyName(int key, string fallback)
    {
        string name = fallback;
        if (localInput != null)
        {
            name = localInput.GetKeyName(key);
        }
        return name;
    }
    public CustomizeUi CustomizeUi => customizeUi;
    public SoundManager Sound => sound;
    public PauseMenuUi PauseMenu => pauseMenu;
    // 영혼석, 영구 해금, 도감, 설정(기기에 저장)
    public MetaProgress Meta => meta;

    // 잠깐 느려지는 연출(보스·방 마지막 몬스터 처치). 혼자 호스트일 때만 시간을 늦춘다(여럿이면 화면만 흔든다).
    private float slowMotionEnd;
    private bool slowMotionActive;

    public void RequestSlowMotion(float scale, float duration)
    {
        bool solo = Mirror.NetworkServer.active && players.Count <= 1;
        if (solo && Time.timeScale > 0f)
        {
            slowMotionActive = true;
            slowMotionEnd = Time.unscaledTime + duration;
            Time.timeScale = Mathf.Min(Time.timeScale, scale);
        }
    }

    private void LateUpdate()
    {
        // 일시정지(timeScale 0) 중에는 건드리지 않는다.
        if (slowMotionActive && Time.timeScale > 0f && Time.unscaledTime >= slowMotionEnd)
        {
            slowMotionActive = false;
            Time.timeScale = 1f;
        }
    }

    private void Awake()
    {
        meta.Load();
        sound.SetCamera(mainCamera);
        ApplySettings();
        sound.PlayMusic(sceneMusic);
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
        sound.ApplyVolumes(meta);
    }

    private void OnDestroy()
    {
        if (slowMotionActive)
        {
            Time.timeScale = 1f;
        }
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

    // Esc 메뉴나 커스터마이징 화면이 열려 있는 동안 캐릭터 조작을 막는다(주소 입력과 따로 센다).
    public void SetMenuBlocked(bool blocked)
    {
        menuBlocked = blocked;
    }

    // 커스터마이징 화면이 열려 있는 동안 조작을 막는다(Esc 메뉴와 따로 센다).
    public void SetCustomizeBlocked(bool blocked)
    {
        customizeBlocked = blocked;
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
