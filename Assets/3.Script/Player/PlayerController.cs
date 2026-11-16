using Mirror;
using UnityEngine;

public enum PlayerState : byte
{
    Normal,
    Attacking,
    Rolling,
    Dead
}

public enum BufferedAction : byte
{
    None,
    Attack,
    Roll
}

// 플레이어 조작과 전투.
// 이동과 연출은 본인 클라이언트가 즉시 처리하고, 피해 판정과 체력·마나는 서버(호스트)가 확정한다.
public class PlayerController : NetworkBehaviour, IDamageable
{
    [Header("참조")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CharacterVisual visual;
    [SerializeField] private SpriteRenderer slashRenderer;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 4.2f;
    [SerializeField] private float rollSpeed = 9f;
    [SerializeField] private float rollDuration = 0.38f;
    [SerializeField] private float rollInvulnTime = 0.32f;
    [SerializeField] private int maxRollCharges = 2;
    [SerializeField] private float rollRechargeTime = 1.1f;
    [SerializeField] private float inputBufferTime = 0.2f;

    [Header("기본 공격(검)")]
    [SerializeField] private int attackDamage = 12;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackArcDegrees = 120f;
    [SerializeField] private float attackHitDelay = 0.11f;
    [SerializeField] private float attackDuration = 0.34f;
    [SerializeField] private float attackMoveFactor = 0.25f;
    [SerializeField] private float attackAnimStartTime = 0.75f;
    [SerializeField] private float attackAnimSpeed = 2.2f;
    [SerializeField] private float slashDuration = 0.14f;
    [SerializeField] private Vector2 centerOffset = new Vector2(0f, 0.35f);

    [Header("능력치")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int maxMana = 50;
    [SerializeField] private float manaRegenPerSecond = 2f;
    [SerializeField] private int manaOnHit = 3;
    [SerializeField] private float hitInvulnTime = 0.5f;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private Color damageTextColor = new Color(1f, 0.35f, 0.3f);

    [SyncVar(hook = nameof(OnHpChanged))] private int hp;
    [SyncVar] private int mana;
    [SyncVar(hook = nameof(OnDeadChanged))] private bool dead;
    [SyncVar(hook = nameof(OnLocomotionChanged))] private bool moving;
    [SyncVar(hook = nameof(OnFacingChanged))] private bool faceLeft;
    [SyncVar] private int slot;
    [SyncVar] private bool hostPlayer;

    private GameNetworkManager manager;
    private GameHub hub;
    private GameInput input;

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
    private bool slashFlip;
    private float slashTimer;
    private float remoteActionTimer;
    private bool attackAllowed;

    // 서버 상태
    private bool hasPendingHit;
    private float pendingHitTimer;
    private Vector2 pendingAim;
    private float invulnerableUntil;
    private float manaAccumulator;
    private float respawnTimer;
    private float serverAttackLock;

    public int Hp => hp;
    public int MaxHp => maxHp;
    public int Mana => mana;
    public int MaxMana => maxMana;
    public bool IsDead => dead;
    public int RollCharges => rollCharges;
    public int MaxRollCharges => maxRollCharges;
    public int Slot => slot;
    public bool IsHostPlayer => hostPlayer;

    // 서버에서 스폰 직전에 호출. 자리 번호(0~3)와 호스트 여부를 정한다.
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
        hp = maxHp;
        mana = maxMana;
        dead = false;
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
        slashRenderer.enabled = false;
        body.bodyType = RigidbodyType2D.Kinematic;
        rollCharges = maxRollCharges;
        ApplyRemoteLocomotion();
        visual.SetFacing(faceLeft);
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
        // 주소 입력 중에는 조작을 막고, 마우스가 UI 위에 있으면 공격하지 않는다.
        bool blocked = hub.InputBlocked;
        attackAllowed = !blocked && !hub.IsPointerOverUi();

        RechargeRoll(deltaTime);
        ReadBufferedInput(deltaTime, blocked);
        UpdateAim();
        Vector2 moveInput = Vector2.zero;
        if (!blocked)
        {
            moveInput = input.Move;
            ReadSceneInput();
        }

        if (state == PlayerState.Normal)
        {
            TickNormal(moveInput);
        }
        else if (state == PlayerState.Attacking)
        {
            TickAttacking(moveInput, deltaTime);
        }
        else if (state == PlayerState.Rolling)
        {
            TickRolling(deltaTime);
        }
        else
        {
            desiredVelocity = Vector2.zero;
        }
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

    // 공격·구르기 도중에 누른 입력을 잠시 기억했다가 이어서 실행한다.
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
        else if (input.MenuPressed)
        {
            manager.RequestExit();
        }
    }

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
    }

    private void TickNormal(Vector2 moveInput)
    {
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        bool wantAttack = bufferedAction == BufferedAction.Attack || (input.AttackHeld && attackAllowed);

        if (wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (wantAttack)
        {
            StartAttack();
        }
        else
        {
            desiredVelocity = moveInput * moveSpeed;
            SetLocalLocomotion(moveInput.sqrMagnitude > 0.01f, aimDirection.x < 0f);
        }
    }

    private void TickAttacking(Vector2 moveInput, float deltaTime)
    {
        stateTimer += deltaTime;
        desiredVelocity = moveInput * (moveSpeed * attackMoveFactor);

        bool canCancel = stateTimer >= attackHitDelay;
        bool wantRoll = bufferedAction == BufferedAction.Roll && rollCharges > 0;
        if (canCancel && wantRoll)
        {
            StartRoll(moveInput);
        }
        else if (stateTimer >= attackDuration)
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

    private void EnterNormal()
    {
        state = PlayerState.Normal;
        locomotionSent = false;
        desiredVelocity = Vector2.zero;
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

    private void StartAttack()
    {
        state = PlayerState.Attacking;
        stateTimer = 0f;
        bufferedAction = BufferedAction.None;
        PlayAttackVisual(aimDirection);
        CmdAttack(aimDirection);
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

    // ------------------------------------------------------------ 연출(모든 클라이언트 공통)

    private void PlayAttackVisual(Vector2 aim)
    {
        visual.SetFacing(aim.x < 0f);
        visual.PlayAction(CharacterAnim.Attack, attackAnimStartTime, attackAnimSpeed, false);

        slashFlip = !slashFlip;
        slashTimer = slashDuration;
        slashRenderer.enabled = true;
        slashRenderer.flipY = slashFlip;
        Transform slash = slashRenderer.transform;
        slash.localPosition = centerOffset + aim * (attackRange * 0.5f);
        slash.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        slashRenderer.color = Color.white;
    }

    private void PlayRollVisual(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > 0.05f)
        {
            visual.SetFacing(direction.x < 0f);
        }
        visual.PlayAction(CharacterAnim.Roll, 0f, 0.5833f / rollDuration, false);
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

    private void ApplyRemoteLocomotion()
    {
        if (!isLocalPlayer)
        {
            visual.SetFacing(faceLeft);
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

    [Command]
    private void CmdSetLocomotion(bool isMoving, bool isFaceLeft)
    {
        moving = isMoving;
        faceLeft = isFaceLeft;
    }

    [Command]
    private void CmdAttack(Vector2 aim)
    {
        if (!dead)
        {
            if (serverAttackLock <= 0f)
            {
                serverAttackLock = attackDuration * 0.8f;
                pendingAim = aim.normalized;
                pendingHitTimer = attackHitDelay;
                hasPendingHit = true;
                RpcAttack(pendingAim);
            }
        }
    }

    [ClientRpc(includeOwner = false)]
    private void RpcAttack(Vector2 aim)
    {
        remoteActionTimer = attackDuration;
        PlayAttackVisual(aim);
    }

    [Command]
    private void CmdRoll(Vector2 direction)
    {
        if (!dead)
        {
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

    [TargetRpc]
    private void TargetTeleport(Vector2 position)
    {
        transform.position = position;
        body.position = position;
        hub.CameraFollow.SetTarget(transform);
    }

    // ------------------------------------------------------------ 서버 판정

    private void ServerUpdate(float deltaTime)
    {
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
                ResolveMeleeHit();
            }
        }

        if (dead)
        {
            respawnTimer -= deltaTime;
            if (respawnTimer <= 0f)
            {
                ServerRespawn();
            }
        }
        else if (mana < maxMana)
        {
            manaAccumulator += manaRegenPerSecond * deltaTime;
            if (manaAccumulator >= 1f)
            {
                int gain = (int)manaAccumulator;
                manaAccumulator -= gain;
                mana = Mathf.Min(maxMana, mana + gain);
            }
        }
    }

    private void ResolveMeleeHit()
    {
        Vector2 center = (Vector2)transform.position + centerOffset;
        int count = hub.OverlapMonsters(center, attackRange);
        Collider2D[] buffer = hub.HitBuffer;
        float minDot = Mathf.Cos(attackArcDegrees * 0.5f * Mathf.Deg2Rad);
        int hitCount = 0;

        for (int i = 0; i < count; i++)
        {
            Collider2D target = buffer[i];
            MonsterController monster;
            if (target.TryGetComponent(out monster))
            {
                Vector2 toTarget = (Vector2)target.bounds.center - center;
                float distance = toTarget.magnitude;
                bool inArc = distance < 0.45f;
                if (!inArc)
                {
                    inArc = Vector2.Dot(toTarget / distance, pendingAim) >= minDot;
                }
                if (inArc)
                {
                    monster.ServerTakeDamage(attackDamage, pendingAim, netId);
                    hitCount++;
                }
            }
        }

        if (hitCount > 0)
        {
            mana = Mathf.Min(maxMana, mana + manaOnHit);
        }
    }

    public void ServerTakeDamage(int amount, Vector2 hitDirection, uint attackerNetId)
    {
        if (!dead)
        {
            if (Time.time >= invulnerableUntil)
            {
                invulnerableUntil = Time.time + hitInvulnTime;
                hp = Mathf.Max(0, hp - amount);
                if (hp == 0)
                {
                    dead = true;
                    hasPendingHit = false;
                    respawnTimer = respawnDelay;
                }
            }
        }
    }

    // 1단계 임시 규칙: 3초 뒤 제자리 근처에서 부활. 런 실패 규칙은 탑 구조를 만들 때 교체한다.
    private void ServerRespawn()
    {
        hp = maxHp;
        mana = maxMana;
        invulnerableUntil = Time.time + 1.5f;
        dead = false;
        TargetTeleport(hub.GetSpawnPoint(slot));
    }

    // ------------------------------------------------------------ SyncVar 훅(클라이언트 연출)

    private void OnHpChanged(int oldValue, int newValue)
    {
        if (newValue < oldValue)
        {
            if (hub != null)
            {
                hub.DamageTextPool.Show((Vector2)transform.position + new Vector2(0f, 1.1f), oldValue - newValue, damageTextColor);
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
            if (newValue)
            {
                visual.PlayAction(CharacterAnim.Dead, 0f, 1f, true);
                if (isLocalPlayer)
                {
                    state = PlayerState.Dead;
                    desiredVelocity = Vector2.zero;
                }
            }
            else
            {
                visual.PlayLoop(CharacterAnim.Idle);
                if (isLocalPlayer)
                {
                    EnterNormal();
                }
            }
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
