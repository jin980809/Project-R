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
    Roll
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

    [Header("참조")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CharacterVisual visual;
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

    [Header("방패")]
    [SerializeField] private float blockMoveFactor = 0.4f;
    [SerializeField] private float blockArcDegrees = 140f;
    [SerializeField] private float parryWindow = 0.2f;
    [Tooltip("막기를 풀고 이 시간 안에 다시 들면 패링이 되지 않는다(연타 방지).")]
    [SerializeField] private float parryRearmTime = 0.4f;
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
    private Vector2 sentBlockAim;
    private float blockAimSendDot;
    private SkillDefinition castSkill;
    private Vector2 castDirection;
    private byte sentAimByte;
    private float aimSendTimer;

    // 모든 클라이언트 공통 연출
    private bool slashFlip;
    private float slashTimer;
    private float remoteActionTimer;
    private float guardFlashTimer;

    // 서버 상태
    private bool hasPendingHit;
    private float pendingHitTimer;
    private Vector2 pendingAim;
    private float pendingCharge;
    private float invulnerableUntil;
    private float manaAccumulator;
    private float serverAttackLock;
    private float blockStartTime;
    private float blockEndTime = -10f;
    private bool blockCanParry;
    private float blockMinDot;
    private float burnTime;
    private float bleedTime;
    private float slowTime;
    private float weakenTime;
    private float stunTime;
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

    private float MoveSpeed
    {
        get
        {
            float speed = moveSpeed * inventory.Stats.moveSpeedMultiplier;
            if ((statusMask & StatusSlow) != 0)
            {
                speed *= slowSpeedFactor;
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
            return factor;
        }
    }

    private float AttackDuration => weapon.Duration / inventory.Stats.attackSpeedMultiplier;
    private float AttackHitDelay => weapon.HitDelay / inventory.Stats.attackSpeedMultiplier;
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
        input.Enable();
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

        // 주소 입력 중이거나 메뉴가 열려 있으면 조작을 막고, 마우스가 UI 위에 있으면 공격하지 않는다.
        bool blocked = hub.InputBlocked;
        attackAllowed = !blocked && !hub.IsPointerOverUi();

        RechargeRoll(deltaTime);
        ReadBufferedInput(deltaTime, blocked);
        UpdateAim();
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
            TickCharging(moveInput, deltaTime);
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
        if (toPointer.sqrMagnitude > 0.0001f)
        {
            aimDirection = toPointer.normalized;
        }
#if UNITY_EDITOR
        if (editorAimOverride.sqrMagnitude > 0.0001f)
        {
            aimDirection = editorAimOverride.normalized;
        }
#endif
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

    private static byte EncodeAim(Vector2 aim)
    {
        float degrees = Mathf.Repeat(Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, 360f);
        return (byte)(Mathf.RoundToInt(degrees / 360f * 256f) & 255);
    }

    private static Vector2 DecodeAim(byte value)
    {
        float radians = value * (360f / 256f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void TickNormal(Vector2 moveInput, bool blocked)
    {
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        bool wantAttack = bufferedAction == BufferedAction.Attack || (input.AttackHeld && attackAllowed);
        bool wantBlock = !blocked && inventory.ShieldActive && input.OffhandHeld;
        bool castStarted = false;
        // 스킬 트리가 열려 있으면 Q, E, R은 장착에 쓴다(SkillUi가 처리).
        if (!blocked && !hub.SkillUi.IsTreeOpen)
        {
            int skillKey = input.SkillKeyPressed;
            if (skillKey >= 0)
            {
                castStarted = TryCast(skillKey);
            }
        }

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

    // 스킬(Q, E, R). 쓸 수 있으면 시전 상태로 들어간다. 판정은 PlayerSkills가 서버에서 한다.
    private bool TryCast(int skillIndex)
    {
        SkillDefinition skill = skills.TryUse(skillIndex, aimDirection);
        bool started = skill != null;
        if (started)
        {
            state = PlayerState.Casting;
            stateTimer = 0f;
            castSkill = skill;
            castDirection = aimDirection;
            PlaySkillVisual(skill, aimDirection);
        }
        return started;
    }

    // 시전 중에는 멈춰 있고, 돌진 스킬이면 시전 시간 동안 조준 방향으로 정해진 거리를 이동한다.
    private void TickCasting(float deltaTime)
    {
        stateTimer += deltaTime;
        if (castSkill.Kind == SkillKind.Dash)
        {
            desiredVelocity = castDirection * (castSkill.Range / castSkill.CastTime);
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
        if (!isLocalPlayer)
        {
            remoteActionTimer = skill.CastTime;
        }
        visual.SetFacing(aim.x < 0f);
        visual.PlayAction(skill.Anim, skill.AnimStartTime, skill.AnimSpeed, false);
    }

    private void TickAttacking(Vector2 moveInput, float deltaTime)
    {
        stateTimer += deltaTime;
        desiredVelocity = moveInput * (MoveSpeed * weapon.MoveFactor);

        bool canCancel = stateTimer >= AttackHitDelay;
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        if (canCancel && wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (stateTimer >= AttackDuration)
        {
            EnterNormal();
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
        desiredVelocity = moveInput * (MoveSpeed * weapon.MoveFactor);
        visual.SetFacing(aimDirection.x < 0f);

        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        if (wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (!(input.AttackHeld && attackAllowed))
        {
            StartAttack(GetCharge01());
        }
    }

    private float GetCharge01()
    {
        float charge = 1f;
        if (state == PlayerState.Charging)
        {
            charge = Mathf.Clamp01(stateTimer / weapon.ChargeTime);
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

    private void StartAttack(float charge)
    {
        state = PlayerState.Attacking;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        PlayAttackVisual(aimDirection);
        CmdAttack(aimDirection, charge);
    }

    private void StartCharge()
    {
        state = PlayerState.Charging;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        PlayChargeVisual(aimDirection);
        CmdCharge(aimDirection);
    }

    private void StartBlock()
    {
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
        visual.SetFacing(aim.x < 0f);
        // 패키지 공격 동작은 손을 머리 위로 올려 조준 회전과 어긋나므로 쓰지 않고, 무기 연출(휘두르기·찌르기·반동)만 쓴다.
        // 휘두르는 방향은 공격한 순간의 조준으로 맞춘다(다른 사람 화면도 같은 방향).
        visual.SetAim(aim, false);
        visual.SetAimEnabled(true);
        visual.PlayWeaponMotion(weapon.Motion, AttackDuration);
        hub.Sound.PlayAt(weapon.AttackSound, transform.position);

        if (!weapon.IsRanged)
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

    // 공격 클립의 시작 자세에서 멈춰 시위를 당긴 모습으로 쓴다.
    private void PlayChargeVisual(Vector2 aim)
    {
        visual.SetFacing(aim.x < 0f);
        visual.SetAim(aim, false);
        hub.Sound.PlayAt(SoundId.BowCharge, transform.position);
    }

    private void PlayRollVisual(Vector2 direction)
    {
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
                ServerStopBlocking();
                serverAttackLock = AttackDuration * 0.8f;
                pendingAim = aim.normalized;
                pendingCharge = Mathf.Clamp01(charge);
                pendingHitTimer = AttackHitDelay;
                hasPendingHit = true;
                RpcAttack(pendingAim);
            }
        }
    }

    [ClientRpc(includeOwner = false)]
    private void RpcAttack(Vector2 aim)
    {
        remoteActionTimer = AttackDuration;
        PlayAttackVisual(aim);
    }

    [Command]
    private void CmdCharge(Vector2 aim)
    {
        if (!dead && weapon.UsesCharge)
        {
            ServerStopBlocking();
            RpcCharge(aim.normalized);
        }
    }

    // 차지 자세는 다음 동작(발사, 구르기, 사망)이 올 때까지 유지한다. 오래 들고 있으면 평소 자세로 돌아간다.
    [ClientRpc(includeOwner = false)]
    private void RpcCharge(Vector2 aim)
    {
        remoteActionTimer = 10f;
        PlayChargeVisual(aim);
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
        if (!dead && kind != StatusKind.None)
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
            ServerRefreshStatusMask();
        }
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

    private void ServerUpdate(float deltaTime)
    {
        ServerTickStatus(deltaTime);

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

    // 원거리 발사. 로비에는 투사체 풀이 없으므로 동작만 한다.
    private void ServerFire()
    {
        if (hub.ProjectilePool != null)
        {
            Vector2 origin = (Vector2)transform.position + pendingAim * muzzleDistance;
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
            shot.hitsMonsters = true;
            shot.authoritative = authoritative;
            if (authoritative)
            {
                shot.hit = inventory.RollWeaponHit(firedWeapon, factor);
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
        bool started = !dead && mana >= manaCost && serverAttackLock <= 0.15f;
        if (started)
        {
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
