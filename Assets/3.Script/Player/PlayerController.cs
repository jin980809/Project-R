using Mirror;
using UnityEngine;

public enum PlayerState : byte
{
    Normal,
    Attacking,
    Rolling,
    Dead,
    Charging,
    Blocking,
    Casting
}

public enum BufferedAction : byte
{
    None,
    Attack,
    Roll,
    // 스킬 키(Q, E, R). 공격·차지·방어·구르기·시전 중에 눌러도 잠깐 기억했다가 쓸 수 있을 때 바로 쓴다.
    Skill
}

// 플레이어 조작과 전투.
// 이동과 연출은 본인 클라이언트가 즉시 처리하고, 피해 판정과 체력·마나는 서버(호스트)가 확정한다.
// 공격 방식은 들고 있는 무기(WeaponDefinition)가 정한다.
public class PlayerController : NetworkBehaviour, IDamageable
{
    // Pixel 스프라이트는 한 변 0.04 유닛
    private const float PixelSize = 0.04f;
    public const byte StatusBurn = 1;
    public const byte StatusBleed = 2;
    public const byte StatusSlow = 4;
    public const byte StatusWeaken = 8;
    public const byte StatusStun = 16;
    // 물약 효과 비트(tonicMask)
    public const byte TonicPower = 1;
    public const byte TonicSwift = 2;
    public const byte TonicCleanse = 4;

    [Header("참조")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CharacterVisual visual;
    [Tooltip("상태이상·강화 표시")]
    [SerializeField] private StatusFxView statusView;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerSkills skills;
    [SerializeField] private PlayerFacility facility;
    [SerializeField] private SpriteRenderer slashRenderer;
    [SerializeField] private SpriteRenderer aimRenderer;
    [SerializeField] private SpriteRenderer guardRenderer;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float rollSpeed = 9f;
    [SerializeField] private float rollDuration = 0.38f;
    [SerializeField] private float rollInvulnTime = 0.32f;
    [SerializeField] private int maxRollCharges = 2;
    [SerializeField] private float rollRechargeTime = 1.1f;
    [Tooltip("스킬 키를 기억하는 시간(공격·구르기·시전 중에 눌러도 끝나자마자 나간다)")]
    [SerializeField] private float skillBufferTime = 0.5f;
    [SerializeField] private float inputBufferTime = 0.2f;

    [Header("무기")]
    [SerializeField] private float slashDuration = 0.14f;
    [SerializeField] private Vector2 centerOffset = new Vector2(0f, 0.35f);
    [SerializeField] private float muzzleDistance = 0.4f;
    [Tooltip("조준 각도를 서버로 보내는 최소 간격(초)")]
    [SerializeField] private float aimSendInterval = 0.05f;

    [Header("조준선(본인에게만 보임)")]
    [SerializeField] private float aimLineStart = 0.55f;
    [SerializeField] private float aimLineLength = 0.45f;
    [SerializeField] private float aimLineChargedLength = 1.6f;
    [SerializeField] private float aimLineWidth = 0.04f;
    [SerializeField] private Color aimColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color aimChargedColor = new Color(1f, 0.85f, 0.3f, 0.9f);

    [Header("커서 자리에 놓는 스킬 조준(본인에게만 보임)")]
    [Tooltip("사거리 원(키를 누르고 있는 동안 몸 둘레에)")]
    [SerializeField] private SpriteRenderer skillRangeRenderer;
    [Tooltip("놓일 자리 원(커서, 사거리 밖이면 사거리 끝)")]
    [SerializeField] private SpriteRenderer skillTargetRenderer;
    [SerializeField] private Color skillRangeColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color skillTargetColor = new Color(0.65f, 0.85f, 1f, 0.85f);

    [Header("방패")]
    [SerializeField] private float blockMoveFactor = 0.4f;
    [SerializeField] private float blockArcDegrees = 140f;
    [SerializeField] private float parryWindow = 0.2f;
    [Tooltip("막기를 풀고 이 시간 안에 다시 들면 패링이 되지 않는다(연타 방지).")]
    [SerializeField] private float parryRearmTime = 0.4f;

    [Header("강화 평타(좌클릭 차지)")]
    [Tooltip("평타를 누른 채 계속 들고 있으면 평타가 끝난 뒤 차지한다. 차지하는 동안의 이동 속도 배율")]
    [SerializeField] private float powerChargeMoveFactor = 0.45f;
    [Tooltip("강화 평타 동작 중 이동 속도 배율(창 돌진은 따로 나아간다)")]
    [SerializeField] private float powerMoveFactor = 0.15f;
    [Tooltip("창 돌진: 강화 평타 시간 가운데 앞으로 나아가는 앞부분의 비율")]
    [SerializeField] private float lungeTimeRatio = 0.45f;
    [Tooltip("내려찍기 화면 흔들림(본인 화면만): 세기, 시간")]
    [SerializeField] private float smashShakeAmplitude = 0.14f;
    [SerializeField] private float smashShakeTime = 0.2f;
    [Tooltip("강화 평타 화살·탄의 그림 크기 배율")]
    [SerializeField] private float powerShotScale = 1.8f;

    // CmdAttack의 charge가 이 값이면 강화 평타다(활은 다 당긴 차지를 놓으면 강화 평타가 된다).
    private const float PowerChargeValue = 2f;
    [SerializeField] private float parryStaggerTime = 1f;
    [SerializeField] private float parryInvulnTime = 0.3f;
    [SerializeField] private float blockAimSendDegrees = 12f;
    [SerializeField] private float guardDistance = 0.55f;
    [SerializeField] private Color guardColor = new Color(0.55f, 0.8f, 1f, 0.7f);
    [SerializeField] private float guardFlashTime = 0.18f;

    [Header("능력치")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int maxMana = 50;
    [SerializeField] private float manaRegenPerSecond = 2f;
    [SerializeField] private float hitInvulnTime = 0.5f;
    [SerializeField] private Color damageTextColor = new Color(1f, 0.35f, 0.3f);

    [Header("상태이상(몬스터가 건다)")]
    [SerializeField] private float statusTickInterval = 0.5f;
    [SerializeField] private float slowSpeedFactor = 0.65f;
    [SerializeField] private float weakenDamageFactor = 0.7f;

    [SyncVar(hook = nameof(OnHpChanged))] private int hp;
    [SyncVar] private int mana;
    [SyncVar(hook = nameof(OnDeadChanged))] private bool dead;
    [SyncVar(hook = nameof(OnLocomotionChanged))] private bool moving;
    [SyncVar(hook = nameof(OnFacingChanged))] private bool faceLeft;
    [SyncVar] private int slot;
    [SyncVar] private bool hostPlayer;
    [SyncVar] private bool blocking;
    [SyncVar] private Vector2 blockAim = Vector2.right;
    // 걸려 있는 상태이상 비트(StatusBurn 등). 몬스터의 공격이 건다.
    [SyncVar] private byte statusMask;
    // 상태이상별(상태 비트 순서: 화상, 출혈, 둔화, 약화, 기절) 끝나는 네트워크 시각과 처음 지속 시간(HUD 디버프 칸)
    public const int StatusCount = 5;
    private readonly SyncList<double> statusEndTimes = new SyncList<double>();
    private readonly SyncList<float> statusDurations = new SyncList<float>();
    // 마신 물약 효과 비트(TonicPower 등). 정화는 상태이상 면역 중.
    [SyncVar] private byte tonicMask;
    // 힘·신속·정화 물약이 끝나는 네트워크 시각(HUD 남은 시간 표시용)
    [SyncVar] private double powerEndTime;
    [SyncVar] private double swiftEndTime;
    [SyncVar] private double cleanseEndTime;
    // 힘·신속·정화 물약의 처음 지속 시간(HUD 회전 게이지 비율용)
    [SyncVar] private float powerDuration;
    [SyncVar] private float swiftDuration;
    [SyncVar] private float cleanseDuration;
    // 조준 각도(0~255 = 0~360도). 다른 사람 화면에서 무기가 가리키는 방향에 쓴다.
    [SyncVar(hook = nameof(OnAimChanged))] private byte aimByte;
    // 커스터마이징한 외형. 본인이 기기에 저장한 값을 접속할 때 보내고, 모두에게 같은 모습으로 보인다.
    [SyncVar(hook = nameof(OnLookChanged))] private CharacterLook look;

    private GameNetworkManager manager;
    private GameHub hub;
    private GameInput input;
    private WeaponDefinition weapon;

    // 본인 클라이언트 상태
    private PlayerState state;
    private float stateTimer;
    private Vector2 desiredVelocity;
    private Vector2 aimDirection = Vector2.right;
    private Vector2 rollDirection;
    private int rollCharges;
    private float rollRechargeTimer;
    private BufferedAction bufferedAction;
    private float bufferTimer;
    private bool localMoving;
    private bool localFaceLeft;
    private bool locomotionSent;
    private bool attackAllowed;
    // 강화 평타: 지금 공격이 강화 평타인지, 지금 차지가 강화 평타용(활 시위가 아닌)인지,
    // 평타를 누른 좌클릭을 아직 떼지 않았는지, 공격을 시작한 조준 방향(창 돌진 방향)
    private bool attackIsPower;
    private bool chargingPower;
    // 차징 스킬을 모으는 중인 장착 칸(-1이면 아님)과 그 스킬
    private int chargeSkillSlot = -1;
    private SkillDefinition chargeSkill;
    // 이번 시전의 차징 크기 배율(돌진 거리)
    private float castSizeScale = 1f;
    private bool holdingSincePress;
    private Vector2 attackAim = Vector2.right;
    private Vector2 sentBlockAim;
    private float blockAimSendDot;
    private SkillDefinition castSkill;
    private Vector2 castDirection;
    // 커서 자리에 놓는 스킬: 누르고 있는 칸(-1이면 조준 중 아님)과 그 스킬, 커서의 월드 위치, 뗄 때 정한 놓을 자리(몸 가운데 기준)
    private int aimingSlot = -1;
    private SkillDefinition aimingSkill;
    private Vector2 pointerWorld;
    private Vector2 skillTargetOffset;
    private byte sentAimByte;
    private float aimSendTimer;

    // 모든 클라이언트 공통 연출
    private bool slashFlip;
    private float slashTimer;
    private float remoteActionTimer;
    // 다른 사람 화면: 지금 동작이 몸 애니메이션을 쓰는지(구르기·스킬). 공격·차지는 무기 연출만 쓰므로 몸은 이동 상태를 따른다.
    private bool remoteActionUsesBody;
    private float guardFlashTimer;
    // 차지 빛(모든 화면): 모으는 중인지, 모은 시간, 다 모았는지
    private bool chargeGlowing;
    private float chargeGlowTimer;
    private float chargeGlowDuration = 1f;
    private bool chargeGlowFull;
    // 강화 평타의 맞는 순간 그림(내려찍기 땅 충격, 창 돌진 끝)을 띄울 때까지 남은 시간과 자리
    private float powerFxTimer;
    private Vector2 powerFxPosition;
    private float powerFxAngle;

    // 서버 상태
    private bool hasPendingHit;
    private float pendingHitTimer;
    private Vector2 pendingAim;
    private float pendingCharge;
    // 강화 평타인지와 시작한 자리(창 돌진은 출발점부터 끝까지 판정한다)
    private bool pendingPower;
    private Vector2 pendingOrigin;
    private float invulnerableUntil;
    private float manaAccumulator;
    private float serverAttackLock;
    // 서버: 지금 잠금이 기본 공격 때문인지(타격이 끝났으면 스킬로 끊을 수 있다).
    private bool serverLockFromAttack;
    private int bufferedSkill;
    private float blockStartTime;
    private float blockEndTime = -10f;
    private bool blockCanParry;
    private float blockMinDot;
    private float burnTime;
    private float bleedTime;
    private float slowTime;
    private float weakenTime;
    private float stunTime;
    private float powerTime;
    private float swiftTime;
    private float cleanseTime;
    private int burnDamage;
    private int bleedDamage;
    private float statusTickTimer;

    public int Hp => hp;
    public int MaxHp => maxHp + inventory.Stats.maxHpBonus;
    public int Mana => mana;
    public int MaxMana => maxMana + inventory.Stats.maxManaBonus;
    public bool IsDead => dead;
    public int RollCharges => rollCharges;
    public int MaxRollCharges => maxRollCharges;
    public int Slot => slot;
    public bool IsHostPlayer => hostPlayer;
    public int WeaponIndex => inventory.WeaponKindIndex;
    public PlayerInventory Inventory => inventory;
    public PlayerSkills Skills => skills;
    public Vector2 Center => (Vector2)transform.position + centerOffset;
    public PlayerFacility Facility => facility;
    public byte StatusMask => statusMask;

    // 상태이상(index = 상태 비트 순서)의 남은 시간과 처음 지속 시간(초)
    public float GetStatusRemaining(int index)
    {
        float remaining = 0f;
        if (index < statusEndTimes.Count)
        {
            remaining = Mathf.Max(0f, (float)(statusEndTimes[index] - NetworkTime.time));
        }
        return remaining;
    }

    public float GetStatusDuration(int index)
    {
        float duration = 0f;
        if (index < statusDurations.Count)
        {
            duration = statusDurations[index];
        }
        return duration;
    }
    public byte TonicMask => tonicMask;

    // 물약 효과의 남은 시간(초). 걸려 있지 않으면 0.
    public float GetTonicRemaining(byte tonic)
    {
        double endTime = cleanseEndTime;
        if (tonic == TonicPower)
        {
            endTime = powerEndTime;
        }
        else if (tonic == TonicSwift)
        {
            endTime = swiftEndTime;
        }
        return Mathf.Max(0f, (float)(endTime - NetworkTime.time));
    }

    // 물약 효과의 처음 지속 시간(초)
    public float GetTonicDuration(byte tonic)
    {
        float duration = cleanseDuration;
        if (tonic == TonicPower)
        {
            duration = powerDuration;
        }
        else if (tonic == TonicSwift)
        {
            duration = swiftDuration;
        }
        return duration;
    }
    // 본인 플레이어의 입력(키 설정 화면이 바꾼다). 다른 사람 캐릭터는 null.
    public GameInput Input => input;

    private float MoveSpeed
    {
        get
        {
            float speed = moveSpeed * inventory.Stats.moveSpeedMultiplier;
            if ((statusMask & StatusSlow) != 0)
            {
                speed *= slowSpeedFactor;
            }
            if ((tonicMask & TonicSwift) != 0)
            {
                speed *= 1f + inventory.Balance.SwiftSpeedBonus;
            }
            return speed;
        }
    }

    // 약화에 걸려 있으면 주는 피해가 줄어든다(PlayerInventory.RollHit이 곱한다).
    public float DamageDealtFactor
    {
        get
        {
            float factor = 1f;
            if ((statusMask & StatusWeaken) != 0)
            {
                factor = weakenDamageFactor;
            }
            if ((tonicMask & TonicPower) != 0)
            {
                factor *= 1f + inventory.Balance.PowerDamageBonus;
            }
            return factor;
        }
    }

    private float AttackDuration => weapon.Duration / inventory.Stats.attackSpeedMultiplier;
    private float AttackHitDelay => weapon.HitDelay / inventory.Stats.attackSpeedMultiplier;
    private float PowerDuration => weapon.PowerDuration / inventory.Stats.attackSpeedMultiplier;
    private float PowerHitDelay => weapon.PowerHitDelay / inventory.Stats.attackSpeedMultiplier;

    // 다 모으는 데 걸리는 시간(활은 시위 차지, 나머지는 강화 평타 차지)
    private float ChargeDuration
    {
        get
        {
            float duration = weapon.PowerChargeTime;
            if (weapon.UsesCharge)
            {
                duration = weapon.ChargeTime;
            }
            return duration;
        }
    }
    public WeaponDefinition Weapon => weapon;

    // 서버에서 스폰 직전에 호출. 자리 번호(0~3)와 호스트 여부를 정한다. 장비는 PlayerInventory가 자리 번호로 불러온다.
    public void ServerSetSlot(int newSlot, bool isHost)
    {
        slot = newSlot;
        hostPlayer = isHost;
    }

    // ------------------------------------------------------------ 생성과 해제

    private void ResolveHub()
    {
        if (hub == null)
        {
            manager = NetworkManager.singleton as GameNetworkManager;
            hub = manager.Hub;
        }
    }

    public override void OnStartServer()
    {
        ResolveHub();
        inventory.ServerInit(manager, hub);
        skills.ServerInit(hub);
        facility.ServerInit(hub);
        statusMask = 0;
        for (int i = 0; i < StatusCount; i++)
        {
            statusEndTimes.Add(0d);
            statusDurations.Add(0f);
        }
        blockMinDot = Mathf.Cos(blockArcDegrees * 0.5f * Mathf.Deg2Rad);
        hp = MaxHp;
        mana = MaxMana;
        dead = false;
        blocking = false;
        hub.AddPlayer(this);
    }

    public override void OnStopServer()
    {
        if (hub != null)
        {
            hub.RemovePlayer(this);
        }
    }

    public override void OnStartClient()
    {
        ResolveHub();
        hub.AddPlayer(this);
        visual.Init();
        inventory.ClientInit(manager, hub);
        skills.ClientInit(hub);
        facility.ClientInit(hub);
        slashRenderer.enabled = false;
        aimRenderer.enabled = false;
        guardRenderer.enabled = false;
        body.bodyType = RigidbodyType2D.Kinematic;
        rollCharges = maxRollCharges;
        blockAimSendDot = Mathf.Cos(blockAimSendDegrees * Mathf.Deg2Rad);
        ApplyRemoteLocomotion();
        visual.SetFacing(faceLeft);
        visual.SetAim(DecodeAim(aimByte), false);
        visual.SetAimEnabled(!dead);
        visual.ApplyLook(look);
        if (dead)
        {
            visual.PlayAction(CharacterAnim.Dead, 0.9f, 1f, true);
        }
    }

    public override void OnStopClient()
    {
        if (hub != null)
        {
            hub.RemovePlayer(this);
        }
    }

    public override void OnStartLocalPlayer()
    {
        body.bodyType = RigidbodyType2D.Dynamic;
        input = new GameInput();
        hub.Meta.ApplyKeyBindings(input);
        input.Enable();
        hub.SetLocalInput(input);
        state = PlayerState.Normal;
        hub.CameraFollow.SetTarget(transform);
        hub.Hud.Bind(this);
        hub.InventoryUi.Bind(inventory);
        hub.SkillUi.Bind(skills);
        if (hub.FacilityUi != null)
        {
            hub.FacilityUi.Bind(inventory);
        }
        facility.OwnerStart();
        skills.OwnerStart();

        // 저장한 외형이 있으면 보내고, 처음 실행이면 커스터마이징 화면부터 띄운다(로비에만 있다).
        MetaProgress meta = hub.Meta;
        if (meta.HasLook)
        {
            SubmitLook(meta.Look);
        }
        else if (hub.CustomizeUi != null)
        {
            hub.CustomizeUi.Open(this, true);
        }
    }

    // 커스터마이징 화면에서 고르는 동안 내 화면에만 보여 준다.
    public void PreviewLook(in CharacterLook value)
    {
        visual.ApplyLook(value);
    }

    public CharacterLook CurrentLook => look;
    public CharacterVisual Visual => visual;

    // 정한 외형을 모두에게 보낸다.
    public void SubmitLook(in CharacterLook value)
    {
        visual.ApplyLook(value);
        CmdSetLook(value);
    }

    [Command]
    private void CmdSetLook(CharacterLook value)
    {
        look = value;
    }

    private void OnLookChanged(CharacterLook oldValue, CharacterLook newValue)
    {
        visual.ApplyLook(newValue);
    }

    public override void OnStopLocalPlayer()
    {
        DisposeInput();
    }

    // 씬 전환으로 오브젝트가 바로 파괴되는 경우에도 입력을 정리한다.
    private void OnDestroy()
    {
        DisposeInput();
    }

    private void DisposeInput()
    {
        if (input != null)
        {
            if (hub != null && hub.LocalInput == input)
            {
                hub.SetLocalInput(null);
            }
            input.Dispose();
            input = null;
        }
    }

    // ------------------------------------------------------------ 매 프레임

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (isLocalPlayer)
        {
            OwnerUpdate(deltaTime);
        }
        else if (remoteActionTimer > 0f)
        {
            remoteActionTimer -= deltaTime;
            if (remoteActionTimer <= 0f)
            {
                if (!dead)
                {
                    ApplyRemoteLocomotion();
                }
            }
        }

        if (isServer)
        {
            ServerUpdate(deltaTime);
        }

        if (isClient)
        {
            visual.Tick(deltaTime);
            TickSlash(deltaTime);
            TickGuard(deltaTime);
            TickChargeGlow(deltaTime);
            if (statusView != null)
            {
                if (dead)
                {
                    statusView.SetState(0, -1);
                }
                else
                {
                    statusView.SetState(statusMask, skills.ActiveBuffVisual);
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            body.linearVelocity = desiredVelocity;
        }
    }

    // ------------------------------------------------------------ 본인 조작

    private void OwnerUpdate(float deltaTime)
    {
        if (input.MenuPressed)
        {
            HandleMenuKey();
        }
        if (input.MapPressed && !hub.InputBlocked && hub.Minimap != null)
        {
            hub.Minimap.ToggleBig();
        }

        // 주소 입력 중이거나 메뉴가 열려 있으면 조작을 막고, 마우스가 UI 위에 있으면 공격하지 않는다.
        bool blocked = hub.InputBlocked;
        attackAllowed = !blocked && !hub.IsPointerOverUi();

        RechargeRoll(deltaTime);
        ReadBufferedInput(deltaTime, blocked);
        UpdateAim();
        UpdateGroundAim(blocked);
        visual.SetAim(aimDirection, false);
        SendAim(deltaTime);
        Vector2 moveInput = Vector2.zero;
        if (!blocked)
        {
            moveInput = input.Move;
            ReadSceneInput();
            inventory.OwnerTick(input, blocked);
            skills.OwnerTick(input, blocked);
        }
        facility.OwnerTick(input, blocked);
        if (!blocked && input.RidePressed && state == PlayerState.Normal)
        {
            // Shift: 말에 타고 내린다(타 있는 동안 빨리 달린다).
            facility.CmdSetRiding(!facility.Riding);
        }

        // 기절 중에는 아무 행동도 할 수 없다.
        bool stunned = (statusMask & StatusStun) != 0 && state != PlayerState.Dead;
        if (stunned)
        {
            if (state != PlayerState.Normal)
            {
                EnterNormal();
            }
            bufferedAction = BufferedAction.None;
            desiredVelocity = Vector2.zero;
            SetLocalLocomotion(false, localFaceLeft);
        }
        else if (state == PlayerState.Normal)
        {
            TickNormal(moveInput, blocked);
        }
        else if (state == PlayerState.Attacking)
        {
            TickAttacking(moveInput, deltaTime);
        }
        else if (state == PlayerState.Rolling)
        {
            TickRolling(deltaTime);
        }
        else if (state == PlayerState.Charging)
        {
            if (chargeSkillSlot >= 0)
            {
                TickSkillCharging(moveInput, deltaTime);
            }
            else
            {
                TickCharging(moveInput, deltaTime);
            }
        }
        else if (state == PlayerState.Blocking)
        {
            TickBlocking(moveInput, blocked);
        }
        else if (state == PlayerState.Casting)
        {
            TickCasting(deltaTime);
        }
        else
        {
            desiredVelocity = Vector2.zero;
        }

        UpdateAimLine();
    }

    private void RechargeRoll(float deltaTime)
    {
        if (rollCharges < maxRollCharges)
        {
            rollRechargeTimer += deltaTime;
            if (rollRechargeTimer >= rollRechargeTime)
            {
                rollRechargeTimer = 0f;
                rollCharges++;
            }
        }
    }

    // 로비에서는 출발 지점에서 F로 게임 시작(호스트만), Esc는 로비로 돌아가기 또는 나가기.
    private void ReadSceneInput()
    {
        if (input.InteractPressed)
        {
            if (isServer && hub.IsInStartZone(transform.position))
            {
                manager.ServerStartRun();
            }
        }
    }

    // Esc: 열려 있는 창(가방, 시설, 노드맵, 마을 시설)이 있으면 그것을 닫고, 없으면 메뉴를 연다.
    // 메뉴가 열려 있을 때는 조작이 막혀 있으므로 막힘 여부와 관계없이 처리한다.
    private void HandleMenuKey()
    {
        if (hub.PauseMenu.IsOpen)
        {
            hub.PauseMenu.OnEscape();
        }
        else if (hub.CustomizeUi != null && hub.CustomizeUi.IsOpen)
        {
            // 처음 실행 때는 정해야 하므로 취소되지 않는다(메뉴는 열 수 있다).
            if (hub.CustomizeUi.CanCancel)
            {
                hub.CustomizeUi.Cancel();
            }
            else
            {
                hub.PauseMenu.Open();
            }
        }
        else if (hub.ConfirmUi != null && hub.ConfirmUi.IsOpen)
        {
            hub.ConfirmUi.Cancel();
        }
        else if (hub.Minimap != null && hub.Minimap.IsBig)
        {
            hub.Minimap.SetBig(false);
        }
        else if (hub.FacilityUi != null && hub.FacilityUi.IsOpen)
        {
            hub.FacilityUi.Close();
            hub.InventoryUi.Close();
        }
        else if (hub.InventoryUi.IsOpen)
        {
            hub.InventoryUi.Close();
        }
        else if (hub.SkillUi.IsTreeOpen)
        {
            hub.SkillUi.CloseTree();
        }
        else if (hub.MetaUi != null && hub.MetaUi.IsOpen)
        {
            hub.MetaUi.Close();
        }
        else
        {
            hub.PauseMenu.Open();
        }
    }

    // 공격·구르기 도중에 누른 입력을 잠시 기억했다가 이어서 실행한다.
    private void ReadBufferedInput(float deltaTime, bool blocked)
    {
        if (blocked)
        {
            bufferedAction = BufferedAction.None;
            bufferTimer = 0f;
        }
        else if (input.RollPressed)
        {
            bufferedAction = BufferedAction.Roll;
            bufferTimer = inputBufferTime;
        }
        else if (input.SkillKeyPressed >= 0 && !hub.SkillUi.IsTreeOpen)
        {
            int pressedSlot = input.SkillKeyPressed;
            SkillDefinition groundSkill = skills.GetGroundSkill(pressedSlot);
            // 차징 스킬은 커서 자리에 놓더라도 모으는 동안 조준한다(TickSkillCharging).
            if (groundSkill != null && groundSkill.IsCharge)
            {
                groundSkill = null;
            }
            bool groundReady = groundSkill != null && skills.GetSlotCooldown01(pressedSlot) <= 0f;
            // 마나가 모자라거나 맞는 무기가 아니면 범위를 보여 주지 않고 알림만 띄운다.
            if (groundReady)
            {
                groundReady = skills.CanBeginSkill(groundSkill);
            }
            if (groundReady)
            {
                // 커서 자리에 놓는 스킬: 누르고 있는 동안 사거리를 보여 주고, 떼면 나간다(UpdateGroundAim).
                aimingSlot = pressedSlot;
                aimingSkill = groundSkill;
            }
            else if (groundSkill == null)
            {
                bufferedAction = BufferedAction.Skill;
                bufferedSkill = pressedSlot;
                bufferTimer = skillBufferTime;
                skillTargetOffset = Vector2.zero;
            }
            // 커서 자리에 놓는 스킬이 아직 재사용 대기 중이면 사거리도 보여 주지 않고 아무것도 하지 않는다.
        }
        else if (input.AttackPressed && attackAllowed)
        {
            bufferedAction = BufferedAction.Attack;
            bufferTimer = inputBufferTime;
        }
        else if (bufferTimer > 0f)
        {
            bufferTimer -= deltaTime;
            if (bufferTimer <= 0f)
            {
                bufferedAction = BufferedAction.None;
            }
        }
    }

    private void UpdateAim()
    {
        Camera mainCamera = hub.MainCamera;
        Vector2 pointer = input.PointerPosition;
        Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, -mainCamera.transform.position.z));
        Vector2 toPointer = (Vector2)world - ((Vector2)transform.position + centerOffset);
        pointerWorld = world;
        if (toPointer.sqrMagnitude > 0.0001f)
        {
            aimDirection = toPointer.normalized;
        }
#if UNITY_EDITOR
        if (editorAimOverride.sqrMagnitude > 0.0001f)
        {
            aimDirection = editorAimOverride.normalized;
            // 시험용: 조준 값을 그대로 몸 가운데에서 커서까지의 거리로 본다("aim 3 1"이면 오른쪽 3, 위 1).
            pointerWorld = Center + editorAimOverride;
        }
#endif
    }

    // 커서 자리에 놓는 스킬의 조준: 키를 떼면 커서 자리(사거리 밖이면 사거리 끝)로 정해 스킬을 기억시킨다.
    // 우클릭·창 열기·조작 막힘이면 취소한다.
    private void UpdateGroundAim(bool blocked)
    {
        if (aimingSlot >= 0)
        {
            // 조준하는 동안 마나가 모자라게 되어도 범위를 거둔다.
            bool cancel = blocked || state == PlayerState.Dead || hub.SkillUi.IsTreeOpen || input.OffhandPressed || Mana < aimingSkill.ManaCost;
            if (cancel)
            {
                aimingSlot = -1;
            }
            else if (!input.IsSkillKeyHeld(aimingSlot))
            {
                skillTargetOffset = GetGroundOffset(aimingSkill);
                bufferedAction = BufferedAction.Skill;
                bufferedSkill = aimingSlot;
                bufferTimer = skillBufferTime;
                aimingSlot = -1;
            }
        }
        ShowGroundAim();
    }

    // 몸 가운데에서 커서까지(사거리를 넘으면 사거리 끝까지)
    private Vector2 GetGroundOffset(SkillDefinition skill)
    {
        return Vector2.ClampMagnitude(pointerWorld - Center, skill.Range);
    }

    private void ShowGroundAim()
    {
        if (skillRangeRenderer != null && skillTargetRenderer != null)
        {
            // 커서 자리에 놓는 차징 스킬(메테오)은 모으는 동안 조준을 보여 주고, 떨어질 범위가 모은 만큼 커진다.
            SkillDefinition shownSkill = aimingSkill;
            float sizeScale = 1f;
            bool chargingGround = state == PlayerState.Charging && chargeSkillSlot >= 0 && chargeSkill.GroundTarget;
            if (chargingGround)
            {
                shownSkill = chargeSkill;
                sizeScale = chargeSkill.GetChargeSize(SkillCharge01);
            }
            bool show = aimingSlot >= 0 || chargingGround;
            if (skillRangeRenderer.enabled != show)
            {
                skillRangeRenderer.enabled = show;
                skillTargetRenderer.enabled = show;
                skillRangeRenderer.color = skillRangeColor;
                skillTargetRenderer.color = skillTargetColor;
            }
            if (show)
            {
                Vector2 center = Center;
                float ringSize = skillRangeRenderer.sprite.bounds.size.x;
                float ringScale = shownSkill.Range * 2f / ringSize;
                skillRangeRenderer.transform.position = center;
                skillRangeRenderer.transform.localScale = new Vector3(ringScale, ringScale, 1f);
                float targetSize = skillTargetRenderer.sprite.bounds.size.x;
                float targetScale = Mathf.Max(0.3f, shownSkill.Radius * sizeScale) * 2f / targetSize;
                skillTargetRenderer.transform.position = center + GetGroundOffset(shownSkill);
                skillTargetRenderer.transform.localScale = new Vector3(targetScale, targetScale, 1f);
            }
        }
    }

#if UNITY_EDITOR
    // 에디터 검사 도구(EditorCommandWatcher의 aim 명령)가 조준을 고정할 때 쓴다. 0이면 마우스를 따른다.
    [System.NonSerialized] public Vector2 editorAimOverride;
#endif

    // 조준 각도가 바뀌었으면 일정 간격으로 서버에 알린다(다른 사람 화면의 무기 방향).
    private void SendAim(float deltaTime)
    {
        aimSendTimer -= deltaTime;
        if (aimSendTimer <= 0f)
        {
            byte encoded = EncodeAim(aimDirection);
            if (encoded != sentAimByte)
            {
                sentAimByte = encoded;
                aimSendTimer = aimSendInterval;
                CmdSetAim(encoded);
            }
        }
    }

    private byte EncodeAim(Vector2 aim)
    {
        float degrees = Mathf.Repeat(Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, 360f);
        return (byte)(Mathf.RoundToInt(degrees / 360f * 256f) & 255);
    }

    private Vector2 DecodeAim(byte value)
    {
        float radians = value * (360f / 256f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void TickNormal(Vector2 moveInput, bool blocked)
    {
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        // 좌클릭은 누를 때마다 한 번 벤다(꾹 눌러도 연타하지 않고, 계속 들고 있으면 평타 뒤 강화 평타를 모은다).
        // 활은 누르고 있는 동안 바로 시위를 당긴다.
        bool wantAttack = bufferedAction == BufferedAction.Attack || (input.AttackHeld && attackAllowed && weapon.UsesCharge);
        bool wantBlock = !blocked && inventory.ShieldActive && input.OffhandHeld;
        // 스킬 트리가 열려 있으면 Q, E, R은 장착에 쓴다(SkillUi가 처리하고, 버퍼에도 넣지 않는다).
        bool castStarted = !blocked && TryBufferedSkill();

        if (castStarted)
        {
            bufferedAction = BufferedAction.None;
        }
        else if (wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (wantAttack)
        {
            if (weapon.UsesCharge)
            {
                StartCharge();
            }
            else
            {
                StartAttack(1f);
            }
        }
        else if (wantBlock)
        {
            StartBlock();
        }
        else
        {
            desiredVelocity = moveInput * MoveSpeed;
            SetLocalLocomotion(moveInput.sqrMagnitude > 0.01f, aimDirection.x < 0f);
        }
    }

    // 기억해 둔 스킬 키를 쓴다. 재사용 대기 중이면 기억한 시간 동안 기다렸다가(살짝 일찍 눌러도 나가게),
    // 마나 부족·무기 불일치처럼 기다려도 안 되는 경우는 한 번 알리고 버린다.
    private bool TryBufferedSkill()
    {
        bool started = false;
        if (bufferedAction == BufferedAction.Skill && !skills.IsSlotCoolingDown(bufferedSkill))
        {
            bufferedAction = BufferedAction.None;
            bufferTimer = 0f;
            started = TryCast(bufferedSkill);
        }
        return started;
    }

    // 스킬(Q, E, R). 차징 스킬은 모으기를 시작하고, 나머지는 바로 시전한다.
    private bool TryCast(int skillIndex)
    {
        bool started = false;
        SkillDefinition charged = skills.GetChargeSkill(skillIndex);
        if (charged != null)
        {
            started = StartSkillCharge(skillIndex, charged);
        }
        else
        {
            started = CastSkill(skillIndex, 1f);
        }
        return started;
    }

    // 차징 스킬 모으기: 누르고 있는 동안 무기가 금빛으로 빛나며 젖혀지고, 떼면 모은 만큼 나간다.
    private bool StartSkillCharge(int slot, SkillDefinition skill)
    {
        bool started = skills.CanBeginSkill(skill);
        if (started)
        {
            Dismount();
            state = PlayerState.Charging;
            stateTimer = 0f;
            chargingPower = false;
            chargeSkillSlot = slot;
            chargeSkill = skill;
            PlaySkillChargeVisual(aimDirection, skill.ChargeTime);
            CmdSkillCharge(aimDirection, skill.ChargeTime);
        }
        return started;
    }

    // 차징 스킬을 모으는 중: 느리게 움직이고, 키를 떼면 모은 만큼 시전한다. 구르기·우클릭이면 버린다.
    private void TickSkillCharging(Vector2 moveInput, float deltaTime)
    {
        stateTimer += deltaTime;
        desiredVelocity = moveInput * (MoveSpeed * powerChargeMoveFactor);
        SetLocalLocomotion(moveInput.sqrMagnitude > 0.01f, aimDirection.x < 0f);
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        if (wantRoll)
        {
            EnterNormal();
            CmdEndCharge();
            StartRoll(moveInput);
        }
        else if (input.OffhandPressed || hub.SkillUi.IsTreeOpen)
        {
            EnterNormal();
            CmdEndCharge();
        }
        else if (!input.IsSkillKeyHeld(chargeSkillSlot))
        {
            int slot = chargeSkillSlot;
            SkillDefinition skill = chargeSkill;
            float charge = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, skill.ChargeTime));
            skillTargetOffset = Vector2.zero;
            if (skill.GroundTarget)
            {
                skillTargetOffset = GetGroundOffset(skill);
            }
            EnterNormal();
            if (!CastSkill(slot, charge))
            {
                CmdEndCharge();
            }
        }
    }

    // 차징 스킬을 모으는 중인지(HUD 차징 게이지)
    public bool IsChargingSkill => state == PlayerState.Charging && chargeSkillSlot >= 0;

    // 지금 모은 정도(0~1). 차징 스킬을 모으는 중이 아니면 1.
    public float SkillCharge01
    {
        get
        {
            float charge = 1f;
            if (chargeSkillSlot >= 0)
            {
                charge = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, chargeSkill.ChargeTime));
            }
            return charge;
        }
    }

    // 스킬을 시전 상태로 들어간다. 판정은 PlayerSkills가 서버에서 한다. charge01: 차징 스킬을 모은 정도.
    private bool CastSkill(int skillIndex, float charge01)
    {
        // 커서 자리에 놓는 스킬은 놓을 자리 쪽을 보고 시전한다.
        Vector2 castAim = aimDirection;
        if (skills.GetGroundSkill(skillIndex) != null && skillTargetOffset.sqrMagnitude > 0.01f)
        {
            castAim = skillTargetOffset.normalized;
        }
        SkillDefinition skill = skills.TryUse(skillIndex, castAim, skillTargetOffset, charge01);
        bool started = skill != null;
        if (started)
        {
            castSizeScale = skill.GetChargeSize(charge01);
            Dismount();
            state = PlayerState.Casting;
            stateTimer = 0f;
            castSkill = skill;
            castDirection = castAim;
            PlaySkillVisual(skill, castAim);
        }
        return started;
    }

    // 시전 중에는 멈춰 있고, 돌진 스킬이면 시전 시간 동안 조준 방향으로 정해진 거리를 이동한다.
    private void TickCasting(float deltaTime)
    {
        stateTimer += deltaTime;
        if (castSkill.Kind == SkillKind.Dash)
        {
            desiredVelocity = castDirection * (castSkill.Range * castSizeScale / castSkill.CastTime);
        }
        else
        {
            desiredVelocity = Vector2.zero;
        }
        if (stateTimer >= castSkill.CastTime)
        {
            EnterNormal();
        }
    }

    public void PlaySkillVisual(SkillDefinition skill, Vector2 aim)
    {
        EndChargeGlow();
        if (!isLocalPlayer)
        {
            remoteActionTimer = skill.CastTime;
            remoteActionUsesBody = true;
        }
        visual.SetFacing(aim.x < 0f);
        // 무기도 시전 방향을 겨누게 한다(마법 스킬은 무기 끝에서 나간다).
        visual.SetAim(aim, false);
        visual.PlayAction(skill.Anim, skill.AnimStartTime, skill.AnimSpeed, false);
        if (skill.ImpactDelay > 0f && !skill.GroundTarget)
        {
            // 뛰어올라 무기를 머리 위로 들었다가 맞는 순간 내려찍는다.
            visual.PlayLeapSmash(skill.CastTime, skill.ImpactDelay);
        }
    }

    private void TickAttacking(Vector2 moveInput, float deltaTime)
    {
        stateTimer += deltaTime;
        float duration = AttackDuration;
        float hitDelay = AttackHitDelay;
        desiredVelocity = moveInput * (MoveSpeed * weapon.MoveFactor);
        if (attackIsPower)
        {
            duration = PowerDuration;
            hitDelay = PowerHitDelay;
            desiredVelocity = moveInput * (MoveSpeed * powerMoveFactor);
            // 창 돌진: 앞부분 동안 공격 방향으로 정해진 거리를 나아간다.
            float lungeTime = duration * lungeTimeRatio;
            if (weapon.Power == PowerAttack.Lunge && stateTimer < lungeTime)
            {
                desiredVelocity = attackAim * (weapon.PowerReach / lungeTime);
            }
        }
        if (!input.AttackHeld)
        {
            holdingSincePress = false;
        }
        // 공격은 무기 연출만 쓰므로 몸은 걷기·대기 애니메이션을 계속 따른다(구르기 직후 공격해도 구르기 동작이 반복되지 않게).
        SetLocalLocomotion(moveInput.sqrMagnitude > 0.01f, aimDirection.x < 0f);

        bool canCancel = stateTimer >= hitDelay;
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        if (canCancel && wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (canCancel && TryBufferedSkill())
        {
            // 타격이 나간 뒤에는 스킬로 공격 동작을 끊는다.
        }
        else if (stateTimer >= duration)
        {
            // 평타를 누른 좌클릭을 계속 들고 있으면 강화 평타 차지로 넘어간다(활은 처음부터 시위 차지).
            bool keepHolding = holdingSincePress && input.AttackHeld && attackAllowed;
            if (!attackIsPower && keepHolding && weapon.Power != PowerAttack.None && !weapon.UsesCharge)
            {
                StartPowerCharge();
            }
            else
            {
                EnterNormal();
            }
        }
    }

    private void TickRolling(float deltaTime)
    {
        stateTimer += deltaTime;
        desiredVelocity = rollDirection * rollSpeed;
        if (stateTimer >= rollDuration)
        {
            EnterNormal();
        }
    }

    // 활: 누르고 있는 동안 차지하고, 떼면 차지한 만큼 강하게 쏜다. 구르기로 취소할 수 있다.
    private void TickCharging(Vector2 moveInput, float deltaTime)
    {
        stateTimer += deltaTime;
        float moveFactor = weapon.MoveFactor;
        if (chargingPower)
        {
            moveFactor = powerChargeMoveFactor;
        }
        desiredVelocity = moveInput * (MoveSpeed * moveFactor);
        // 차지 중에도 몸은 걷기·대기 애니메이션을 따른다(구르기 직후 차지하면 구르기 동작이 계속 반복되던 문제).
        SetLocalLocomotion(moveInput.sqrMagnitude > 0.01f, aimDirection.x < 0f);

        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        if (wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (TryBufferedSkill())
        {
            // 차지를 버리고 스킬을 쓴다.
        }
        else if (!(input.AttackHeld && attackAllowed))
        {
            float charge = GetCharge01();
            bool full = charge >= 1f && weapon.Power != PowerAttack.None;
            if (full)
            {
                StartPowerAttack();
            }
            else if (chargingPower)
            {
                // 다 모으기 전에 떼면 강화 평타는 나가지 않는다.
                EnterNormal();
                CmdEndCharge();
            }
            else
            {
                StartAttack(charge);
            }
        }
    }

    private float GetCharge01()
    {
        float charge = 1f;
        if (state == PlayerState.Charging)
        {
            charge = Mathf.Clamp01(stateTimer / ChargeDuration);
        }
        return charge;
    }

    // 방패: 우클릭을 누르는 동안 조준 방향의 공격을 막는다. 막 든 직후에 맞으면 패링이 된다.
    private void TickBlocking(Vector2 moveInput, bool blocked)
    {
        desiredVelocity = moveInput * (MoveSpeed * blockMoveFactor);
        SetLocalLocomotion(moveInput.sqrMagnitude > 0.01f, aimDirection.x < 0f);

        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        bool wantAttack = bufferedAction == BufferedAction.Attack;
        bool holding = !blocked && input.OffhandHeld;

        if (wantRoll)
        {
            CmdSetBlocking(false, aimDirection);
            StartRoll(moveInput);
        }
        else if (bufferedAction == BufferedAction.Skill && !skills.IsSlotCoolingDown(bufferedSkill) && TryBufferedSkill())
        {
            // 방패를 내리고 스킬(서버의 ServerBeginSkill이 방어를 끈다).
        }
        else if (wantAttack)
        {
            CmdSetBlocking(false, aimDirection);
            StartAttack(1f);
        }
        else if (!holding)
        {
            CmdSetBlocking(false, aimDirection);
            EnterNormal();
        }
        else if (Vector2.Dot(aimDirection, sentBlockAim) < blockAimSendDot)
        {
            sentBlockAim = aimDirection;
            CmdSetBlockAim(aimDirection);
        }
    }

    private void EnterNormal()
    {
        state = PlayerState.Normal;
        chargingPower = false;
        chargeSkillSlot = -1;
        EndChargeGlow();
        locomotionSent = false;
        desiredVelocity = Vector2.zero;
        visual.SetAimEnabled(true);
    }

    private void SetLocalLocomotion(bool isMoving, bool isFaceLeft)
    {
        bool changed = !locomotionSent || isMoving != localMoving || isFaceLeft != localFaceLeft;
        if (changed)
        {
            locomotionSent = true;
            localMoving = isMoving;
            localFaceLeft = isFaceLeft;
            visual.SetFacing(isFaceLeft);
            if (isMoving)
            {
                visual.PlayLoop(CharacterAnim.Run);
            }
            else
            {
                visual.PlayLoop(CharacterAnim.Idle);
            }
            CmdSetLocomotion(isMoving, isFaceLeft);
        }
    }

    // 말에 탄 채로는 싸우지 않는다. 공격·스킬·구르기·막기를 시작하면 내린다.
    private void Dismount()
    {
        if (facility.Riding)
        {
            facility.CmdSetRiding(false);
        }
    }

    private void StartAttack(float charge)
    {
        Dismount();
        state = PlayerState.Attacking;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        attackIsPower = false;
        chargingPower = false;
        chargeSkillSlot = -1;
        attackAim = aimDirection;
        // 이 평타를 누른 좌클릭을 계속 들고 있으면 평타가 끝난 뒤 강화 평타를 모은다.
        holdingSincePress = input.AttackHeld;
        PlayAttackVisual(aimDirection);
        CmdAttack(aimDirection, charge);
    }

    // 다 모은 차지를 놓았다: 강화 평타(활은 꿰뚫는 화살).
    private void StartPowerAttack()
    {
        Dismount();
        state = PlayerState.Attacking;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        attackIsPower = true;
        chargingPower = false;
        chargeSkillSlot = -1;
        holdingSincePress = false;
        attackAim = aimDirection;
        PlayPowerVisual(aimDirection);
        CmdAttack(aimDirection, PowerChargeValue);
    }

    private void StartCharge()
    {
        Dismount();
        state = PlayerState.Charging;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        chargingPower = false;
        chargeSkillSlot = -1;
        PlayChargeVisual(aimDirection);
        CmdCharge(aimDirection);
    }

    // 평타를 누른 채 들고 있으면 강화 평타를 모은다(활의 시위 차지와 같은 상태를 쓴다).
    private void StartPowerCharge()
    {
        state = PlayerState.Charging;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        chargingPower = true;
        PlayChargeVisual(aimDirection);
        CmdCharge(aimDirection);
    }

    private void StartBlock()
    {
        Dismount();
        state = PlayerState.Blocking;
        locomotionSent = false;
        sentBlockAim = aimDirection;
        CmdSetBlocking(true, aimDirection);
    }

    private void StartRoll(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude > 0.01f)
        {
            rollDirection = moveInput.normalized;
        }
        else
        {
            rollDirection = -aimDirection;
        }
        state = PlayerState.Rolling;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        rollCharges--;
        Dismount();
        PlayRollVisual(rollDirection);
        CmdRoll(rollDirection);
    }

    // 조준선: 평소에는 짧고 옅게, 활을 차지하는 동안에는 길어지며 색이 진해진다.
    private void UpdateAimLine()
    {
        bool show = state != PlayerState.Dead;
        if (aimRenderer.enabled != show)
        {
            aimRenderer.enabled = show;
        }
        if (show)
        {
            float charge = 0f;
            if (state == PlayerState.Charging)
            {
                charge = GetCharge01();
            }
            float length = Mathf.Lerp(aimLineLength, aimLineChargedLength, charge);
            Transform line = aimRenderer.transform;
            line.localPosition = centerOffset + aimDirection * (aimLineStart + length * 0.5f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg);
            line.localScale = new Vector3(length / PixelSize, aimLineWidth / PixelSize, 1f);
            aimRenderer.color = Color.Lerp(aimColor, aimChargedColor, charge);
        }
    }

    // ------------------------------------------------------------ 연출(모든 클라이언트 공통)

    private void PlayAttackVisual(Vector2 aim)
    {
        EndChargeGlow();
        visual.SetFacing(aim.x < 0f);
        // 패키지 공격 동작은 손을 머리 위로 올려 조준 회전과 어긋나므로 쓰지 않고, 무기 연출(휘두르기·찌르기·반동)만 쓴다.
        // 휘두르는 방향은 공격한 순간의 조준으로 맞춘다(다른 사람 화면도 같은 방향).
        visual.SetAim(aim, false);
        visual.SetAimEnabled(true);
        visual.PlayWeaponMotion(weapon.Motion, AttackDuration);
        hub.Sound.PlayAt(weapon.AttackSound, transform.position);

        SkillVfx vfx = weapon.Vfx;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        if (!weapon.IsRanged && vfx.Has(vfx.cast) && hub.SkillFx != null)
        {
            // 픽셀 휘두르기 그림: 몸 가운데를 기준으로 조준 쪽으로 돌려 그린다.
            // 그림은 위에서 아래로 베는 호라서, 무기가 아래에서 위로 올려 벨 차례면 위아래를 뒤집는다.
            bool upward = weapon.Motion == WeaponMotion.Swing && visual.SwingUpward;
            hub.SkillFx.PlayFrames(vfx.cast, vfx.castFps, false, Center, vfx.castSize, 0f, null, angle, true, upward);
        }
        else if (weapon.IsRanged && vfx.Has(vfx.muzzle) && hub.SkillFx != null)
        {
            Vector2 muzzle;
            if (!visual.TryGetMuzzle(aim, out muzzle))
            {
                muzzle = Center + aim * 0.7f;
            }
            hub.SkillFx.PlayFrames(vfx.muzzle, vfx.muzzleFps, false, muzzle, vfx.muzzleSize, 0f, null, angle, vfx.muzzleDirectional);
        }
        else if (!weapon.IsRanged)
        {
            slashFlip = !slashFlip;
            slashTimer = slashDuration;
            slashRenderer.enabled = true;
            slashRenderer.flipY = slashFlip;
            Transform slash = slashRenderer.transform;
            Vector2 scale = weapon.SlashScale;
            slash.localPosition = centerOffset + aim * (weapon.Range * 0.5f);
            slash.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            slash.localScale = new Vector3(scale.x, scale.y, 1f);
            slashRenderer.color = Color.white;
        }
    }

    // 강화 평타 연출(모든 화면). 검은 한 바퀴 돌려 베고, 창은 찌르며 내달리고, 도끼·둔기는 높이 들어 내려찍고,
    // 원거리는 평소 반동과 함께 큰 화살·탄(Projectile)이 나간다. 맞는 순간 그림(땅 충격, 돌진 끝)은 PowerHitDelay 뒤에 띄운다.
    private void PlayPowerVisual(Vector2 aim)
    {
        EndChargeGlow();
        visual.SetFacing(aim.x < 0f);
        visual.SetAim(aim, false);
        visual.SetAimEnabled(true);
        float duration = PowerDuration;
        PowerAttack kind = weapon.Power;
        SkillVfx vfx = weapon.PowerVfx;
        SkillFxPlayer fx = hub.SkillFx;
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        powerFxAngle = angle;
        if (kind == PowerAttack.Spin)
        {
            visual.PlayWeaponMotion(WeaponMotion.Spin, duration);
            hub.Sound.PlayAt(SoundId.SkillSlash, transform.position);
            if (fx != null && vfx.Has(vfx.cast))
            {
                fx.PlayFrames(vfx.cast, vfx.castFps, vfx.castGround, Center, weapon.PowerRadius * vfx.castScale, 0f, null, angle, vfx.castDirectional);
            }
        }
        else if (kind == PowerAttack.Lunge)
        {
            visual.PlayWeaponMotion(WeaponMotion.Lunge, duration);
            hub.Sound.PlayAt(SoundId.SkillDash, transform.position);
            float length = weapon.PowerReach + weapon.Range;
            if (fx != null && vfx.Has(vfx.trail))
            {
                fx.PlayFramesDirected(vfx.trail, vfx.trailFps, Center + aim * (length * 0.5f), length * 0.5f, vfx.trailWidth, aim);
            }
            powerFxTimer = PowerHitDelay;
            powerFxPosition = Center + aim * length;
        }
        else if (kind == PowerAttack.Wave)
        {
            // 검기: 젖힌 자리에서 크게 내려 베거나(검, 도끼, 둔기) 길게 찌르며(창) 검기를 날린다.
            WeaponMotion motion = WeaponMotion.Smash;
            if (weapon.Motion == WeaponMotion.Thrust)
            {
                motion = WeaponMotion.Lunge;
            }
            visual.PlayWeaponMotion(motion, duration);
            hub.Sound.PlayAt(SoundId.SkillSlash, transform.position);
        }
        else if (kind == PowerAttack.Smash)
        {
            visual.PlayWeaponMotion(WeaponMotion.Smash, duration);
            hub.Sound.PlayAt(SoundId.HeavySwing, transform.position);
            powerFxTimer = PowerHitDelay;
            powerFxPosition = Center + aim * weapon.PowerReach;
        }
        else
        {
            visual.PlayWeaponMotion(weapon.Motion, duration);
            hub.Sound.PlayAt(weapon.AttackSound, transform.position);
            SkillVfx muzzleVfx = weapon.Vfx;
            if (fx != null && muzzleVfx.Has(muzzleVfx.muzzle))
            {
                Vector2 muzzle;
                if (!visual.TryGetMuzzle(aim, out muzzle))
                {
                    muzzle = Center + aim * 0.7f;
                }
                fx.PlayFrames(muzzleVfx.muzzle, muzzleVfx.muzzleFps, false, muzzle, muzzleVfx.muzzleSize * 1.4f, 0f, null, angle, muzzleVfx.muzzleDirectional);
            }
        }
    }

    // 강화 평타가 맞는 순간: 내려찍기는 땅 충격 그림과 화면 흔들림, 창 돌진은 끝자리 그림.
    private void PlayPowerImpact()
    {
        SkillVfx vfx = weapon.PowerVfx;
        SkillFxPlayer fx = hub.SkillFx;
        if (weapon.Power == PowerAttack.Smash)
        {
            hub.Sound.PlayAt(SoundId.Explosion, powerFxPosition);
            if (fx != null && vfx.Has(vfx.cast))
            {
                fx.PlayFrames(vfx.cast, vfx.castFps, vfx.castGround, powerFxPosition, weapon.PowerRadius * vfx.castScale, 0f, null, powerFxAngle, vfx.castDirectional);
            }
            if (isLocalPlayer && hub.CameraFollow != null)
            {
                hub.CameraFollow.Shake(smashShakeAmplitude, smashShakeTime);
            }
        }
        else if (weapon.Power == PowerAttack.Lunge && fx != null && vfx.Has(vfx.impact))
        {
            fx.PlayFacing(vfx.impact, vfx.impactFps, vfx.impactGround, powerFxPosition, vfx.impactSize, 0f, null, powerFxAngle, vfx.impactDirectional, vfx.impactMirror);
        }
    }

    // 공격 클립의 시작 자세에서 멈춰 시위를 당긴 모습으로 쓴다. 강화 평타 차지도 같은 자리에서 시작한다(무기 빛과 자세).
    private void PlayChargeVisual(Vector2 aim)
    {
        visual.SetFacing(aim.x < 0f);
        visual.SetAim(aim, false);
        if (weapon.UsesCharge)
        {
            hub.Sound.PlayAt(SoundId.BowCharge, transform.position);
        }
        BeginChargeGlow(ChargeDuration);
    }

    // 차징 스킬 모으기 자세와 빛(모든 화면)
    private void PlaySkillChargeVisual(Vector2 aim, float duration)
    {
        visual.SetFacing(aim.x < 0f);
        visual.SetAim(aim, false);
        hub.Sound.PlayAt(SoundId.BowCharge, transform.position);
        BeginChargeGlow(duration);
    }

    private void BeginChargeGlow(float duration)
    {
        chargeGlowDuration = Mathf.Max(0.05f, duration);
        chargeGlowing = true;
        chargeGlowTimer = 0f;
        chargeGlowFull = false;
    }

    private void EndChargeGlow()
    {
        if (chargeGlowing)
        {
            chargeGlowing = false;
            visual.SetCharge(0f, weapon.Motion);
        }
    }

    // 차지 빛(모든 화면): 모으는 동안 무기 테두리가 점점 금빛으로 빛나며 젖혀지고, 다 모으면 소리가 난다.
    private void TickChargeGlow(float deltaTime)
    {
        if (chargeGlowing)
        {
            chargeGlowTimer += deltaTime;
            float amount = Mathf.Clamp01(chargeGlowTimer / chargeGlowDuration);
            if (amount >= 1f && !chargeGlowFull)
            {
                chargeGlowFull = true;
                hub.Sound.PlayAt(SoundId.NodeUnlock, transform.position);
            }
            visual.SetCharge(amount, weapon.Motion);
        }
        if (powerFxTimer > 0f)
        {
            powerFxTimer -= deltaTime;
            if (powerFxTimer <= 0f)
            {
                PlayPowerImpact();
            }
        }
    }

    private void PlayRollVisual(Vector2 direction)
    {
        EndChargeGlow();
        if (Mathf.Abs(direction.x) > 0.05f)
        {
            visual.SetFacing(direction.x < 0f);
        }
        // 구르는 동안은 패키지 애니메이션이 무기를 움직인다.
        visual.SetAimEnabled(false);
        visual.PlayAction(CharacterAnim.Roll, 0f, 0.5833f / rollDuration, false);
        hub.Sound.PlayAt(SoundId.Roll, transform.position);
    }

    private void TickSlash(float deltaTime)
    {
        if (slashTimer > 0f)
        {
            slashTimer -= deltaTime;
            if (slashTimer > 0f)
            {
                Color color = slashRenderer.color;
                color.a = slashTimer / slashDuration;
                slashRenderer.color = color;
            }
            else
            {
                slashRenderer.enabled = false;
            }
        }
    }

    // 막기 표시: 조준 방향 앞에 호 모양을 띄운다. 본인은 즉시, 다른 사람은 동기화된 값으로 본다.
    private void TickGuard(float deltaTime)
    {
        bool show = blocking;
        Vector2 direction = blockAim;
        if (isLocalPlayer)
        {
            show = state == PlayerState.Blocking;
            direction = aimDirection;
        }
        if (dead)
        {
            show = false;
        }

        if (guardRenderer.enabled != show)
        {
            guardRenderer.enabled = show;
        }
        if (show)
        {
            Transform guard = guardRenderer.transform;
            guard.localPosition = centerOffset + direction * guardDistance;
            guard.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            Color color = guardColor;
            if (guardFlashTimer > 0f)
            {
                guardFlashTimer -= deltaTime;
                color = Color.Lerp(guardColor, Color.white, Mathf.Clamp01(guardFlashTimer / guardFlashTime));
            }
            guardRenderer.color = color;
        }
        else
        {
            guardFlashTimer = 0f;
        }
    }

    private void ApplyRemoteLocomotion()
    {
        if (!isLocalPlayer)
        {
            visual.SetFacing(faceLeft);
            visual.SetAimEnabled(!dead);
            if (moving)
            {
                visual.PlayLoop(CharacterAnim.Run);
            }
            else
            {
                visual.PlayLoop(CharacterAnim.Idle);
            }
        }
    }

    // ------------------------------------------------------------ 네트워크: 본인 → 서버 → 다른 클라이언트

    [Command(channel = Channels.Unreliable)]
    private void CmdSetAim(byte value)
    {
        aimByte = value;
    }

    [Command]
    private void CmdSetLocomotion(bool isMoving, bool isFaceLeft)
    {
        moving = isMoving;
        faceLeft = isFaceLeft;
    }

    [Command]
    private void CmdAttack(Vector2 aim, float charge)
    {
        if (!dead)
        {
            if (serverAttackLock <= 0f)
            {
                // 강화 평타는 charge가 PowerChargeValue로 온다(그 무기에 강화 평타가 있을 때만 인정).
                bool power = charge >= PowerChargeValue - 0.01f && weapon.Power != PowerAttack.None;
                float duration = AttackDuration;
                float hitDelay = AttackHitDelay;
                pendingCharge = Mathf.Clamp01(charge);
                if (power)
                {
                    duration = PowerDuration;
                    hitDelay = PowerHitDelay;
                    pendingCharge = PowerChargeValue;
                }
                ServerStopBlocking();
                serverAttackLock = duration * 0.8f;
                serverLockFromAttack = true;
                pendingAim = aim.normalized;
                pendingPower = power;
                pendingOrigin = (Vector2)transform.position + centerOffset;
                pendingHitTimer = hitDelay;
                hasPendingHit = true;
                RpcAttack(pendingAim, power);
            }
        }
    }

    [ClientRpc(includeOwner = false)]
    private void RpcAttack(Vector2 aim, bool power)
    {
        remoteActionUsesBody = false;
        if (power)
        {
            remoteActionTimer = PowerDuration;
            PlayPowerVisual(aim);
        }
        else
        {
            remoteActionTimer = AttackDuration;
            PlayAttackVisual(aim);
        }
        PlayRemoteBodyLoop();
    }

    [Command]
    private void CmdCharge(Vector2 aim)
    {
        if (!dead && (weapon.UsesCharge || weapon.Power != PowerAttack.None))
        {
            ServerStopBlocking();
            RpcCharge(aim.normalized);
        }
    }

    // 차징 스킬을 모으기 시작했다(다른 사람 화면의 차지 빛·자세).
    [Command]
    private void CmdSkillCharge(Vector2 aim, float duration)
    {
        if (!dead)
        {
            ServerStopBlocking();
            RpcSkillCharge(aim.normalized, Mathf.Clamp(duration, 0.05f, 5f));
        }
    }

    [ClientRpc(includeOwner = false)]
    private void RpcSkillCharge(Vector2 aim, float duration)
    {
        remoteActionTimer = 10f;
        remoteActionUsesBody = false;
        PlaySkillChargeVisual(aim, duration);
        PlayRemoteBodyLoop();
    }

    // 다 모으기 전에 떼어 강화 평타가 취소됐다(다른 사람 화면의 차지 빛·자세를 끈다).
    [Command]
    private void CmdEndCharge()
    {
        RpcEndCharge();
    }

    [ClientRpc(includeOwner = false)]
    private void RpcEndCharge()
    {
        EndChargeGlow();
        if (remoteActionTimer > 0.01f)
        {
            remoteActionTimer = 0.01f;
        }
    }

    // 차지 자세는 다음 동작(발사, 구르기, 사망)이 올 때까지 유지한다. 오래 들고 있으면 평소 자세로 돌아간다.
    [ClientRpc(includeOwner = false)]
    private void RpcCharge(Vector2 aim)
    {
        remoteActionTimer = 10f;
        remoteActionUsesBody = false;
        PlayChargeVisual(aim);
        PlayRemoteBodyLoop();
    }

    [Command]
    private void CmdRoll(Vector2 direction)
    {
        if (!dead)
        {
            ServerStopBlocking();
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + rollInvulnTime);
            hasPendingHit = false;
            RpcRoll(direction);
        }
    }

    [ClientRpc(includeOwner = false)]
    private void RpcRoll(Vector2 direction)
    {
        remoteActionTimer = rollDuration;
        remoteActionUsesBody = true;
        PlayRollVisual(direction);
    }

    [Command]
    private void CmdSetBlocking(bool value, Vector2 aim)
    {
        if (value)
        {
            if (!dead && inventory.ShieldActive && !blocking)
            {
                blocking = true;
                blockAim = aim.normalized;
                blockStartTime = Time.time;
                blockCanParry = Time.time - blockEndTime >= parryRearmTime;
            }
        }
        else
        {
            ServerStopBlocking();
        }
    }

    [Command]
    private void CmdSetBlockAim(Vector2 aim)
    {
        if (blocking)
        {
            blockAim = aim.normalized;
        }
    }

    [ClientRpc]
    private void RpcParry()
    {
        guardFlashTimer = guardFlashTime;
        hub.Sound.PlayAt(SoundId.Parry, transform.position);
        if (isLocalPlayer)
        {
            hub.CameraFollow.Shake(0.1f, 0.12f);
        }
    }

    [ClientRpc]
    private void RpcFire(Vector2 origin, Vector2 aim, int firedWeaponIndex, float charge)
    {
        // 호스트는 서버 판정용 투사체가 화면에도 보이므로 따로 만들지 않는다.
        if (!isServer)
        {
            LaunchWeaponShot(origin, aim, inventory.GetWeaponDefinition(firedWeaponIndex), charge, false);
        }
    }

    [TargetRpc]
    private void TargetTeleport(Vector2 position)
    {
        transform.position = position;
        body.position = position;
        hub.CameraFollow.SetTarget(transform);
    }

    // ------------------------------------------------------------ 서버 판정

    // 몬스터의 공격이 상태이상을 건다(서버). dotDamage는 화상·출혈이 0.5초마다 주는 피해.
    public void ServerApplyStatus(StatusKind kind, float duration, int dotDamage)
    {
        if (!dead && kind != StatusKind.None && cleanseTime <= 0f)
        {
            if (kind == StatusKind.Burn)
            {
                burnTime = duration;
                burnDamage = Mathf.Max(burnDamage, dotDamage);
            }
            else if (kind == StatusKind.Bleed)
            {
                bleedTime = duration;
                bleedDamage = Mathf.Max(bleedDamage, dotDamage);
            }
            else if (kind == StatusKind.Slow)
            {
                slowTime = duration;
            }
            else if (kind == StatusKind.Weaken)
            {
                weakenTime = duration;
            }
            else
            {
                stunTime = duration;
                ServerStopBlocking();
                hasPendingHit = false;
            }
            int index = StatusIndex(kind);
            if (index >= 0 && index < statusEndTimes.Count)
            {
                statusEndTimes[index] = NetworkTime.time + duration;
                statusDurations[index] = duration;
            }
            ServerRefreshStatusMask();
        }
    }

    // 상태 비트 순서(화상 0, 출혈 1, 둔화 2, 약화 3, 기절 4). 플레이어에게 걸리지 않는 종류는 -1.
    private int StatusIndex(StatusKind kind)
    {
        int index = -1;
        if (kind == StatusKind.Burn)
        {
            index = 0;
        }
        else if (kind == StatusKind.Bleed)
        {
            index = 1;
        }
        else if (kind == StatusKind.Slow)
        {
            index = 2;
        }
        else if (kind == StatusKind.Weaken)
        {
            index = 3;
        }
        else if (kind == StatusKind.Stun)
        {
            index = 4;
        }
        return index;
    }

    private void ServerClearStatus()
    {
        burnTime = 0f;
        bleedTime = 0f;
        slowTime = 0f;
        weakenTime = 0f;
        stunTime = 0f;
        ServerRefreshStatusMask();
    }

    private void ServerRefreshStatusMask()
    {
        byte mask = 0;
        if (burnTime > 0f)
        {
            mask |= StatusBurn;
        }
        else
        {
            burnDamage = 0;
        }
        if (bleedTime > 0f)
        {
            mask |= StatusBleed;
        }
        else
        {
            bleedDamage = 0;
        }
        if (slowTime > 0f)
        {
            mask |= StatusSlow;
        }
        if (weakenTime > 0f)
        {
            mask |= StatusWeaken;
        }
        if (stunTime > 0f)
        {
            mask |= StatusStun;
        }
        if (mask != statusMask)
        {
            statusMask = mask;
        }
    }

    private void ServerTickStatus(float deltaTime)
    {
        if (statusMask != 0)
        {
            burnTime -= deltaTime;
            bleedTime -= deltaTime;
            slowTime -= deltaTime;
            weakenTime -= deltaTime;
            stunTime -= deltaTime;
            statusTickTimer -= deltaTime;
            if (statusTickTimer <= 0f)
            {
                statusTickTimer = statusTickInterval;
                int damage = 0;
                if (burnTime > 0f)
                {
                    damage += burnDamage;
                }
                if (bleedTime > 0f)
                {
                    damage += bleedDamage;
                }
                if (damage > 0 && !dead)
                {
                    // 지속 피해는 무적 시간이나 방패를 무시한다.
                    hp = Mathf.Max(0, hp - damage);
                    if (hp == 0)
                    {
                        dead = true;
                        hasPendingHit = false;
                        ServerStopBlocking();
                        facility.ServerDismount();
                    }
                }
            }
            if (dead)
            {
                ServerClearStatus();
            }
            else
            {
                ServerRefreshStatusMask();
            }
        }
    }

    // 정화 포션: 상태이상을 모두 풀고 immuneTime 동안 걸리지 않는다(서버).
    public void ServerCleanse(float immuneTime)
    {
        ServerClearStatus();
        cleanseTime = immuneTime;
        cleanseEndTime = NetworkTime.time + immuneTime;
        cleanseDuration = immuneTime;
        ServerRefreshTonicMask();
    }

    // 힘·신속의 물약(서버). 다시 마시면 시간이 처음부터 다시 흐른다.
    public void ServerApplyTonic(PotionKind kind, float duration)
    {
        if (kind == PotionKind.Power)
        {
            powerTime = duration;
            powerEndTime = NetworkTime.time + duration;
            powerDuration = duration;
        }
        else if (kind == PotionKind.Swift)
        {
            swiftTime = duration;
            swiftEndTime = NetworkTime.time + duration;
            swiftDuration = duration;
        }
        ServerRefreshTonicMask();
    }

    private void ServerTickTonics(float deltaTime)
    {
        if (tonicMask != 0)
        {
            powerTime -= deltaTime;
            swiftTime -= deltaTime;
            cleanseTime -= deltaTime;
            ServerRefreshTonicMask();
        }
    }

    private void ServerRefreshTonicMask()
    {
        byte mask = 0;
        if (powerTime > 0f)
        {
            mask |= TonicPower;
        }
        if (swiftTime > 0f)
        {
            mask |= TonicSwift;
        }
        if (cleanseTime > 0f)
        {
            mask |= TonicCleanse;
        }
        if (mask != tonicMask)
        {
            tonicMask = mask;
        }
    }

    private void ServerUpdate(float deltaTime)
    {
        ServerTickStatus(deltaTime);
        ServerTickTonics(deltaTime);

        if (serverAttackLock > 0f)
        {
            serverAttackLock -= deltaTime;
        }

        if (hasPendingHit)
        {
            pendingHitTimer -= deltaTime;
            if (pendingHitTimer <= 0f)
            {
                hasPendingHit = false;
                if (weapon.IsRanged)
                {
                    ServerFire();
                }
                else if (pendingPower && weapon.Power == PowerAttack.Wave)
                {
                    // 검기: 근접 무기지만 투사체로 날린다.
                    ServerFire();
                }
                else if (pendingPower)
                {
                    ResolvePowerHit();
                }
                else
                {
                    ResolveMeleeHit();
                }
            }
        }

        if (!dead && mana < MaxMana)
        {
            manaAccumulator += manaRegenPerSecond * deltaTime;
            if (manaAccumulator >= 1f)
            {
                int gain = (int)manaAccumulator;
                manaAccumulator -= gain;
                mana = Mathf.Min(MaxMana, mana + gain);
            }
        }
    }

    // 장비가 바뀌어 무기·능력치가 다시 계산된 직후 PlayerInventory가 부른다(서버와 클라이언트 모두).
    public void OnEquipmentChanged()
    {
        WeaponDefinition previousWeapon = weapon;
        weapon = inventory.Weapon;
        if (isServer)
        {
            if (hp > MaxHp)
            {
                hp = MaxHp;
            }
            if (mana > MaxMana)
            {
                mana = MaxMana;
            }
            if (blocking && !inventory.ShieldActive)
            {
                ServerStopBlocking();
            }
        }
        if (isLocalPlayer)
        {
            // 차지나 막기 도중에 무기가 바뀌면 평소 상태로 돌아간다.
            bool chargeBroken = state == PlayerState.Charging && previousWeapon != weapon;
            bool blockBroken = state == PlayerState.Blocking && !inventory.ShieldActive;
            if (chargeBroken || blockBroken)
            {
                EnterNormal();
            }
            if (chargeBroken)
            {
                CmdEndCharge();
            }
        }
    }

    // 공격 동작 중이거나 죽어 있으면 장비를 바꿀 수 없다.
    public bool ServerCanSwapEquipment()
    {
        return !dead && serverAttackLock <= 0f;
    }

    public void ServerOnEquipmentSwapped()
    {
        ServerStopBlocking();
        hasPendingHit = false;
    }

    private void ServerStopBlocking()
    {
        if (blocking)
        {
            blocking = false;
            blockEndTime = Time.time;
        }
    }

    // 근접 판정. 검은 부채꼴, 창은 조준 방향으로 뻗은 직선(반폭 LineHalfWidth)이다.
    private void ResolveMeleeHit()
    {
        Vector2 center = (Vector2)transform.position + centerOffset;
        float range = weapon.Range;
        int count = hub.OverlapMonsters(center, range);
        Collider2D[] buffer = hub.HitBuffer;
        float minDot = Mathf.Cos(weapon.ArcDegrees * 0.5f * Mathf.Deg2Rad);
        float halfWidth = weapon.LineHalfWidth;
        // 한 번 휘두를 때 피해량(치명타 포함)은 한 번만 정한다.
        HitInfo hit = inventory.RollWeaponHit(weapon, 1f);
        int hitCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider2D target = buffer[i];
            MonsterController monster;
            if (target.TryGetComponent(out monster))
            {
                Vector2 toTarget = (Vector2)target.bounds.center - center;
                float distance = toTarget.magnitude;
                bool inside = distance < 0.45f;
                if (!inside)
                {
                    if (halfWidth > 0f)
                    {
                        float along = Vector2.Dot(toTarget, pendingAim);
                        float side = Mathf.Abs(toTarget.x * pendingAim.y - toTarget.y * pendingAim.x);
                        inside = along >= 0f && side <= halfWidth + target.bounds.extents.x;
                    }
                    else
                    {
                        inside = Vector2.Dot(toTarget / distance, pendingAim) >= minDot;
                    }
                }
                if (inside)
                {
                    monster.ServerTakeHit(hit, pendingAim, netId);
                    hitCount++;
                }
            }
        }

        if (hitCount > 0)
        {
            ServerOnAttackHit();
        }
    }

    // 강화 평타 판정. 회전 베기는 몸 둘레 원, 내려찍기는 앞쪽 원(기절), 창 돌진은 출발점부터 돌진 끝 + 창 길이까지의 직선.
    private void ResolvePowerHit()
    {
        PowerAttack kind = weapon.Power;
        Vector2 center = (Vector2)transform.position + centerOffset;
        Vector2 origin = center;
        float radius = weapon.PowerRadius;
        float length = 0f;
        float halfWidth = Mathf.Max(0.45f, weapon.LineHalfWidth + 0.15f);
        if (kind == PowerAttack.Smash)
        {
            center += pendingAim * weapon.PowerReach;
        }
        else if (kind == PowerAttack.Lunge)
        {
            origin = pendingOrigin;
            length = weapon.PowerReach + weapon.Range;
            center = origin + pendingAim * (length * 0.5f);
            radius = length * 0.5f + halfWidth + 0.5f;
        }
        HitInfo hit = inventory.RollWeaponHit(weapon, weapon.PowerDamageFactor);
        if (kind == PowerAttack.Smash)
        {
            hit.status = StatusKind.Stun;
            hit.statusDuration = weapon.PowerStunTime;
            hit.statusPower = 0f;
        }

        int count = hub.OverlapMonsters(center, radius);
        Collider2D[] buffer = hub.HitBuffer;
        int hitCount = 0;
        for (int i = 0; i < count; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster))
            {
                Vector2 targetCenter = buffer[i].bounds.center;
                bool inside = true;
                Vector2 direction = pendingAim;
                if (kind == PowerAttack.Lunge)
                {
                    Vector2 toTarget = targetCenter - origin;
                    float along = Vector2.Dot(toTarget, pendingAim);
                    float side = Mathf.Abs(toTarget.x * pendingAim.y - toTarget.y * pendingAim.x);
                    inside = along >= -0.3f && along <= length + 0.3f && side <= halfWidth + buffer[i].bounds.extents.x;
                }
                else
                {
                    Vector2 away = targetCenter - center;
                    if (away.sqrMagnitude > 0.0001f)
                    {
                        direction = away.normalized;
                    }
                }
                if (inside)
                {
                    monster.ServerTakeHit(hit, direction, netId);
                    hitCount++;
                }
            }
        }

        if (hitCount > 0)
        {
            ServerOnAttackHit();
        }
    }

    // 원거리 발사. 로비에는 투사체 풀이 없으므로 동작만 한다.
    private void ServerFire()
    {
        if (hub.ProjectilePool != null)
        {
            Vector2 origin = (Vector2)transform.position + pendingAim * muzzleDistance;
            Vector2 tip;
            if (visual.TryGetMagicTip(pendingAim, out tip))
            {
                // 지팡이·완드: 판정도 무기 끝에서 나간다(위치는 그림 높이만큼 아래인 바닥 기준).
                origin = tip - new Vector2(0f, Projectile.SpriteHeight);
            }
            LaunchWeaponShot(origin, pendingAim, weapon, pendingCharge, true);
            RpcFire(origin, pendingAim, inventory.WeaponKindIndex, pendingCharge);
        }
    }

    private void LaunchWeaponShot(Vector2 origin, Vector2 aim, WeaponDefinition firedWeapon, float charge, bool authoritative)
    {
        if (hub.ProjectilePool != null)
        {
            float factor = 1f;
            if (firedWeapon.UsesCharge)
            {
                factor = Mathf.Lerp(firedWeapon.MinChargeFactor, 1f, charge);
            }
            // 강화 평타(활은 다 당긴 차지): 더 세고 크게, 꿰뚫거나(활·석궁·총) 크게 터진다(지팡이·완드).
            bool power = charge >= PowerChargeValue - 0.01f && firedWeapon.Power != PowerAttack.None;
            float damageFactor = factor;
            if (power)
            {
                damageFactor = factor * firedWeapon.PowerDamageFactor;
            }

            ProjectileShot shot = new ProjectileShot();
            shot.origin = origin;
            shot.direction = aim;
            shot.speed = firedWeapon.ProjectileSpeed * Mathf.Lerp(0.6f, 1f, factor);
            shot.lifetime = firedWeapon.ProjectileLifetime;
            shot.radius = firedWeapon.ProjectileRadius;
            shot.damage = 1;
            shot.explosionRadius = firedWeapon.ExplosionRadius;
            shot.look = firedWeapon.ProjectileLook;
            shot.color = firedWeapon.ProjectileColor;
            shot.vfx = firedWeapon.Vfx;
            if (power)
            {
                shot.radius *= 1.5f;
                shot.visualScale = powerShotScale;
                shot.pierce = firedWeapon.PowerPierce;
                if (firedWeapon.Power == PowerAttack.BigShot)
                {
                    shot.explosionRadius = firedWeapon.PowerRadius;
                }
                if (firedWeapon.PowerVfx.HasAny)
                {
                    shot.vfx = firedWeapon.PowerVfx;
                }
            }
            bool wave = power && firedWeapon.Power == PowerAttack.Wave;
            if (wave)
            {
                // 검기: 근접 무기에는 투사체 값이 없으므로 강화 평타 값으로 날린다(그림은 이미 월드 크기에 맞게 그렸다).
                shot.speed = firedWeapon.PowerShotSpeed;
                shot.lifetime = firedWeapon.PowerReach / Mathf.Max(0.1f, firedWeapon.PowerShotSpeed);
                shot.radius = firedWeapon.PowerRadius;
                shot.explosionRadius = 0f;
                shot.visualScale = 1f;
                shot.color = Color.white;
            }
            shot.hitsMonsters = true;
            shot.authoritative = authoritative;
            // 그림은 무기 끝(활 가운데, 지팡이 머리, 총구)에서 나가 보이게 한다. 판정 위치는 그대로다.
            Vector2 muzzle;
            if (!wave && visual.TryGetMuzzle(aim, out muzzle))
            {
                shot.visualOffset = Vector2.ClampMagnitude(muzzle - (origin + new Vector2(0f, Projectile.SpriteHeight)), 1.6f);
            }
            if (authoritative)
            {
                shot.hit = inventory.RollWeaponHit(firedWeapon, damageFactor);
                shot.damage = shot.hit.damage;
                shot.owner = this;
            }
            hub.ProjectilePool.Launch(shot);
        }
    }

    // 기본 공격이 하나라도 맞았을 때 서버에서 호출. 마나를 회복한다.
    public void ServerOnAttackHit()
    {
        if (!dead)
        {
            mana = Mathf.Min(MaxMana, mana + weapon.ManaOnHit + inventory.Stats.manaOnHitBonus);
        }
    }

    public void ServerTakeDamage(int amount, Vector2 hitDirection, uint attackerNetId)
    {
        if (!dead)
        {
            if (Time.time >= invulnerableUntil)
            {
                int finalAmount = Mathf.Max(1, Mathf.RoundToInt(amount * inventory.Stats.damageTakenMultiplier));
                bool parried = false;
                if (blocking)
                {
                    // 공격이 날아온 방향의 반대쪽이 방패를 든 방향과 맞아야 막힌다.
                    bool fromFront = Vector2.Dot(-hitDirection, blockAim) >= blockMinDot;
                    if (fromFront)
                    {
                        if (blockCanParry && Time.time - blockStartTime <= parryWindow)
                        {
                            parried = true;
                        }
                        else
                        {
                            finalAmount = Mathf.Max(1, Mathf.RoundToInt(finalAmount * inventory.Stats.blockDamageFactor));
                        }
                    }
                }

                if (parried)
                {
                    invulnerableUntil = Time.time + parryInvulnTime;
                    ServerStaggerAttacker(attackerNetId);
                    RpcParry();
                }
                else
                {
                    invulnerableUntil = Time.time + hitInvulnTime;
                    hp = Mathf.Max(0, hp - finalAmount);
                    if (hp == 0)
                    {
                        dead = true;
                        hasPendingHit = false;
                        ServerStopBlocking();
                        facility.ServerDismount();
                    }
                }
            }
        }
    }

    // 패링에 성공하면 때린 몬스터를 경직시킨다. 투사체(attackerNetId 0)는 막기만 한다.
    private void ServerStaggerAttacker(uint attackerNetId)
    {
        if (attackerNetId != 0)
        {
            NetworkIdentity attacker;
            if (NetworkServer.spawned.TryGetValue(attackerNetId, out attacker))
            {
                MonsterController monster;
                if (attacker.TryGetComponent(out monster))
                {
                    monster.ServerStagger(parryStaggerTime, blockAim);
                }
            }
        }
    }

    // 스킬을 쓸 수 있으면 마나를 쓰고 시전 시간 동안 다른 공격을 막는다(서버, PlayerSkills가 부른다).
    public bool ServerBeginSkill(int manaCost, float castTime)
    {
        // 기본 공격의 타격이 이미 나갔으면 남은 공격 동작은 스킬로 끊을 수 있다(본인 화면과 같은 규칙).
        bool unlocked = serverAttackLock <= 0.15f || (serverLockFromAttack && !hasPendingHit);
        bool started = !dead && mana >= manaCost && unlocked;
        if (started)
        {
            serverLockFromAttack = false;
            mana -= manaCost;
            ServerStopBlocking();
            hasPendingHit = false;
            serverAttackLock = castTime;
        }
        return started;
    }

    public void ServerSetInvulnerable(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    // 포션 등으로 체력을 회복한다(서버).
    public void ServerHeal(int amount)
    {
        if (!dead)
        {
            hp = Mathf.Min(MaxHp, hp + amount);
        }
    }

    // 마나를 채운다(서버, 주술사 각성).
    public void ServerRestoreMana(int amount)
    {
        if (!dead)
        {
            mana = Mathf.Min(MaxMana, mana + amount);
        }
    }

    // 방을 끝냈을 때 쓰러져 있던 플레이어를 되살린다(FloorDirector가 부른다).
    public void ServerRevive(Vector2 position, float hpFraction)
    {
        hp = Mathf.Max(1, Mathf.RoundToInt(MaxHp * hpFraction));
        invulnerableUntil = Time.time + 1.5f;
        ServerClearStatus();
        dead = false;
        TargetTeleport(position);
    }

    // 방을 옮길 때 서버가 위치를 정해 준다. 이동은 본인 클라이언트가 주도하므로 본인에게 알려 옮긴다.
    public void ServerTeleport(Vector2 position)
    {
        hasPendingHit = false;
        ServerStopBlocking();
        TargetTeleport(position);
    }

    // ------------------------------------------------------------ SyncVar 훅(클라이언트 연출)

    private void OnHpChanged(int oldValue, int newValue)
    {
        if (newValue < oldValue)
        {
            if (hub != null)
            {
                hub.DamageTextPool.Show((Vector2)transform.position + new Vector2(0f, 1.1f), oldValue - newValue, damageTextColor);
                hub.Sound.PlayAt(SoundId.PlayerHurt, transform.position);
                if (isLocalPlayer)
                {
                    hub.CameraFollow.Shake(0.12f, 0.18f);
                }
            }
        }
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        if (hub != null)
        {
            visual.SetAimEnabled(!newValue);
            if (newValue)
            {
                hub.Sound.PlayAt(SoundId.PlayerDie, transform.position);
                visual.PlayAction(CharacterAnim.Dead, 0f, 1f, true);
                if (isLocalPlayer)
                {
                    state = PlayerState.Dead;
                    desiredVelocity = Vector2.zero;
                }
            }
            else
            {
                hub.Sound.PlayAt(SoundId.Revive, transform.position);
                visual.PlayLoop(CharacterAnim.Idle);
                if (isLocalPlayer)
                {
                    EnterNormal();
                }
            }
        }
    }

    private void OnAimChanged(byte oldValue, byte newValue)
    {
        if (hub != null && !isLocalPlayer)
        {
            visual.SetAim(DecodeAim(newValue), true);
        }
    }

    private void OnLocomotionChanged(bool oldValue, bool newValue)
    {
        if (hub != null)
        {
            if (remoteActionTimer <= 0f)
            {
                ApplyRemoteLocomotion();
            }
            else if (!remoteActionUsesBody)
            {
                PlayRemoteBodyLoop();
            }
        }
    }

    // 다른 사람 화면: 무기 연출만 쓰는 동작(공격·차지) 중에도 몸은 이동 상태에 맞는 반복 애니메이션.
    private void PlayRemoteBodyLoop()
    {
        if (!isLocalPlayer && !dead)
        {
            if (moving)
            {
                visual.PlayLoop(CharacterAnim.Run);
            }
            else
            {
                visual.PlayLoop(CharacterAnim.Idle);
            }
        }
    }

    private void OnFacingChanged(bool oldValue, bool newValue)
    {
        if (hub != null)
        {
            if (remoteActionTimer <= 0f)
            {
                ApplyRemoteLocomotion();
            }
        }
    }
}
