using Mirror;
using UnityEngine;

public enum MonsterState : byte
{
    Idle,
    Chase,
    Windup,
    Strike,
    Recover,
    Stagger,
    Dead
}

public enum MonsterRank : byte
{
    Normal,
    Elite,
    Boss
}

// 엘리트가 하나씩 갖는 특성
public enum EliteModifier : byte
{
    None,
    // 신속: 이동 속도 증가
    Swift,
    // 강인: 체력 증가
    Tough,
    // 흉포: 공격력 증가
    Fierce
}

// 보스의 공격 패턴. 체력이 절반 아래로 내려가면(2페이즈) 더 자주 쓰고 내려찍기가 추가된다.
public enum BossPattern : byte
{
    // 그 몬스터 종류의 기본 공격
    Basic,
    // 사방으로 투사체 발사
    Burst,
    // 먼 거리 돌진
    Charge,
    // 부하 소환
    Summon,
    // 자기 주변 넓은 범위 내려찍기(2페이즈)
    Slam
}

// 몬스터 한 마리. AI와 피해 판정은 서버(호스트)가 처리하고, 클라이언트는 상태 값에 맞춰 프레임 애니메이션만 재생한다.
// Update를 직접 돌리지 않고 MonsterDirector가 한꺼번에 Tick을 호출한다. 오브젝트는 풀에서 재사용한다.
public class MonsterController : NetworkBehaviour, IDamageable
{
    private const byte BurnBit = 1;
    private const byte BleedBit = 2;
    private const byte SlowBit = 4;
    private const byte WeakenBit = 8;

    [Header("참조")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CircleCollider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform shadow;
    [SerializeField] private SpriteRenderer telegraphRenderer;
    [SerializeField] private SpriteRenderer auraRenderer;
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Transform hpBarFill;
    // 타락한 모험가일 때만 켜는 인간형 외형(플레이어와 같은 캐릭터)
    [SerializeField] private GameObject humanoidRoot;
    [SerializeField] private CharacterVisual humanoid;
    [SerializeField] private ItemBalance balance;

    [Header("연출")]
    [SerializeField] private float staggerTime = 0.2f;
    [SerializeField] private float deadTime = 0.75f;
    [SerializeField] private float flashTime = 0.1f;
    [SerializeField] private Color flashColor = new Color(1f, 0.45f, 0.45f);
    [SerializeField] private Color telegraphColor = new Color(1f, 0.2f, 0.15f, 0.35f);
    [SerializeField] private float hpBarWidth = 0.6f;
    [SerializeField] private Color critTextColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private float critTextScale = 1.5f;

    [Header("등급(일반, 엘리트, 보스 순서)")]
    [SerializeField] private float[] rankScales = { 1f, 1.4f, 2.2f };
    [SerializeField] private float[] rankHpFactors = { 1f, 3f, 10f };
    [SerializeField] private float[] rankDamageFactors = { 1f, 1.3f, 1.6f };
    [SerializeField] private float[] rankAttackSizeFactors = { 1f, 1.2f, 1.8f };
    [Tooltip("엘리트 특성 색(없음, 신속, 강인, 흉포 순서). 발밑 원의 색으로 보인다.")]
    [SerializeField] private Color[] modifierColors =
    {
        new Color(1f, 1f, 1f, 0f), new Color(1f, 0.9f, 0.2f, 0.6f), new Color(0.3f, 0.6f, 1f, 0.6f), new Color(1f, 0.25f, 0.2f, 0.6f)
    };
    [SerializeField] private Color bossAuraColor = new Color(0.7f, 0.2f, 0.9f, 0.45f);
    [SerializeField] private float swiftSpeedFactor = 1.4f;
    [SerializeField] private float toughHpFactor = 1.6f;
    [SerializeField] private float fierceDamageFactor = 1.5f;

    [Header("타락한 모험가(장비를 걸친 인간형 엘리트·보스)")]
    [SerializeField] private string fallenName = "타락한 모험가";
    [Tooltip("일반, 엘리트, 보스 순서")]
    [SerializeField] private float[] fallenScales = { 1f, 1.15f, 1.5f };
    [SerializeField] private float fallenMoveSpeed = 2.3f;
    [SerializeField] private float fallenHpFactor = 1.5f;
    [SerializeField] private float fallenAttackStartTime = 0.6f;
    [SerializeField] private float fallenAttackSpeed = 1.6f;

    [Header("플레이어에게 거는 상태이상")]
    [SerializeField] private float playerStatusDuration = 3f;
    [SerializeField] private float playerDotFactor = 0.1f;
    [SerializeField] private float slamStunTime = 0.8f;

    [Header("상태이상")]
    [SerializeField] private float statusTickInterval = 0.5f;
    [SerializeField] private float slowSpeedFactor = 0.6f;
    [SerializeField] private float weakenDamageFactor = 0.7f;
    [SerializeField] private Color burnColor = new Color(1f, 0.7f, 0.4f);
    [SerializeField] private Color bleedColor = new Color(0.85f, 0.45f, 0.5f);
    [SerializeField] private Color slowColor = new Color(0.6f, 0.8f, 1f);
    [SerializeField] private Color weakenColor = new Color(0.8f, 0.65f, 1f);

    [Header("보스")]
    [SerializeField] private Color enragedColor = new Color(1f, 0.7f, 0.7f);
    [SerializeField] private float bossPatternWindup = 0.8f;
    [SerializeField] private int bossBurstCount = 9;
    [SerializeField] private int bossBurstCountEnraged = 14;
    [SerializeField] private float bossChargeSpeed = 9f;
    [SerializeField] private float bossChargeDuration = 0.55f;
    [SerializeField] private int bossSummonCount = 2;
    [SerializeField] private int bossSummonLimit = 5;
    [SerializeField] private float bossSlamRadius = 3f;
    [SerializeField] private float bossSlamDamageFactor = 1.5f;
    [SerializeField] private float bossEnragedCooldownFactor = 0.65f;
    [SerializeField] private float bossEnragedSpeedFactor = 1.25f;

    [SyncVar] private int definitionIndex;
    [SyncVar] private MonsterRank rank;
    [SyncVar] private EliteModifier modifier;
    [SyncVar] private int maxHp;
    // 타락한 모험가 여부와 걸친 장비. 쓰러뜨리면 걸친 장비를 떨어뜨린다.
    [SyncVar] private bool fallen;
    [SyncVar] private ItemData wornWeapon;
    [SyncVar] private ItemData wornHelmet;
    [SyncVar] private ItemData wornArmor;
    // 치명타로 맞을 때마다 1씩 오른다. hp보다 먼저 전달되므로 hp 훅에서 치명타 여부를 알 수 있다.
    [SyncVar] private byte critCounter;
    [SyncVar(hook = nameof(OnHpChanged))] private int hp;
    [SyncVar] private Vector2 attackPoint;
    // 공격 예고 원의 반경과 시간. state보다 먼저 전달되어야 예고를 제대로 그린다. 반경이 0이면 예고가 없다.
    [SyncVar] private float telegraphRadius;
    [SyncVar] private float telegraphTime;
    [SyncVar(hook = nameof(OnStateChanged))] private MonsterState state;
    [SyncVar(hook = nameof(OnFacingChanged))] private bool faceLeft;
    [SyncVar(hook = nameof(OnStatusMaskChanged))] private byte statusMask;
    [SyncVar(hook = nameof(OnEnragedChanged))] private bool enraged;

    private MonsterDirector director;
    private GameHub hub;
    private MonsterDefinition definition;
    private bool tracked;
    private bool inPool;
    private bool visualReady;
    private Vector3 hpBarFillScale;
    private byte seenCritCounter;
    private Color baseColor = Color.white;

    // 서버 AI
    private PlayerController target;
    private float stateTimer;
    private float retargetTimer;
    private float attackCooldown;
    private Vector2 aimDirection;
    private bool lungeHit;
    private float attackDamage;
    private float moveSpeed;
    private float attackRadius;
    private float attackReach;
    private float strikeSpeed;
    private float strikeRadius;
    private BossPattern bossPattern;

    // 서버 상태이상
    private float burnTime;
    private int burnDamage;
    private float bleedTime;
    private int bleedDamage;
    private float slowTime;
    private float weakenTime;
    private float statusTickTimer;

    // 프레임 애니메이션
    private Sprite[] frames;
    private float frameDuration;
    private float frameTimer;
    private int frameIndex;
    private bool frameLoop;
    private float flashTimer;
    private float telegraphTimer;
    private float telegraphDuration;
    private float telegraphScale;

    public MonsterState State => state;
    public int Hp => hp;
    public int MaxHp => maxHp;
    public MonsterRank Rank => rank;
    public EliteModifier Modifier => modifier;
    public bool Enraged => enraged;
    public byte StatusMask => statusMask;
    public bool Fallen => fallen;
    public int DefinitionIndex => definitionIndex;

    public string DisplayName
    {
        get
        {
            string displayName = fallenName;
            if (!fallen)
            {
                displayName = definition.DisplayName;
            }
            return displayName;
        }
    }

    // 풀에 들어 있는 동안 true. MonsterDirector만 바꾼다(같은 몬스터가 풀에 두 번 들어가는 것을 막는다).
    public bool InPool => inPool;

    public void SetInPool(bool value)
    {
        inPool = value;
    }

    // 풀에서 처음 만들 때 한 번 호출
    public void Setup(MonsterDirector ownerDirector, GameHub ownerHub)
    {
        director = ownerDirector;
        hub = ownerHub;
        hpBarFillScale = hpBarFill.localScale;
    }

    // 서버에서 스폰 직전에 호출
    public void ServerInit(int newDefinitionIndex, float hpScale, float damageScale, MonsterRank newRank, EliteModifier newModifier, bool newFallen)
    {
        definitionIndex = newDefinitionIndex;
        definition = director.GetDefinition(newDefinitionIndex);
        rank = newRank;
        modifier = newModifier;
        fallen = newFallen;

        float hpFactor = hpScale * rankHpFactors[(int)newRank];
        float damageFactor = damageScale * rankDamageFactors[(int)newRank];
        moveSpeed = definition.MoveSpeed;
        if (newFallen)
        {
            // 걸친 장비는 엘리트면 희귀 이상, 보스면 영웅 이상이다.
            ItemRarity rarity = balance.RollRarity(1.5f);
            ItemRarity minRarity = ItemRarity.Rare;
            if (newRank == MonsterRank.Boss)
            {
                minRarity = ItemRarity.Epic;
            }
            if (rarity < minRarity)
            {
                rarity = minRarity;
            }
            wornWeapon = balance.CreateWeapon((WeaponKind)Random.Range(0, WeaponDefinition.KindCount), rarity);
            wornHelmet = balance.CreateGear(ItemSlot.Helmet, rarity);
            wornArmor = balance.CreateGear(ItemSlot.Armor, rarity);
            moveSpeed = fallenMoveSpeed;
            hpFactor *= fallenHpFactor;
        }
        if (newModifier == EliteModifier.Swift)
        {
            moveSpeed *= swiftSpeedFactor;
        }
        else if (newModifier == EliteModifier.Tough)
        {
            hpFactor *= toughHpFactor;
        }
        else if (newModifier == EliteModifier.Fierce)
        {
            damageFactor *= fierceDamageFactor;
        }
        attackDamage = definition.Damage * damageFactor;
        attackRadius = definition.AttackRadius * rankAttackSizeFactors[(int)newRank];
        attackReach = definition.AttackReach * rankAttackSizeFactors[(int)newRank];

        maxHp = Mathf.RoundToInt(definition.MaxHp * hpFactor);
        hp = maxHp;
        faceLeft = false;
        enraged = false;
        statusMask = 0;
        telegraphRadius = 0f;
        state = MonsterState.Chase;
        target = null;
        stateTimer = 0f;
        retargetTimer = 0f;
        attackCooldown = Random.Range(0.3f, 0.9f);
        lungeHit = false;
        burnTime = 0f;
        bleedTime = 0f;
        slowTime = 0f;
        weakenTime = 0f;
        statusTickTimer = statusTickInterval;
        bossPattern = BossPattern.Basic;
        bodyCollider.enabled = true;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = Vector2.zero;
        ApplyRankScale();
    }

    private void ApplyRankScale()
    {
        float scale = rankScales[(int)rank];
        if (fallen)
        {
            scale = fallenScales[(int)rank];
        }
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public override void OnStartServer()
    {
        Track();
    }

    public override void OnStartClient()
    {
        definition = director.GetDefinition(definitionIndex);
        if (!isServer)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
        }
        ApplyRankScale();
        bodyCollider.radius = definition.BodyRadius;
        bodyCollider.offset = new Vector2(0f, definition.BodyRadius * 0.7f);
        shadow.localScale = new Vector3(definition.ShadowScale, definition.ShadowScale, 1f);
        spriteRenderer.flipX = faceLeft;
        spriteRenderer.enabled = !fallen;
        humanoidRoot.SetActive(fallen);
        if (fallen)
        {
            humanoid.Init();
            humanoid.EquipWeapon(balance.GetPartsType(wornWeapon), wornWeapon.partsIndex, false, 0);
            humanoid.EquipGear(true, wornHelmet.partsIndex, true, wornArmor.partsIndex, 0);
            humanoid.SetFacing(faceLeft);
        }
        telegraphRenderer.enabled = false;
        hpBarRoot.SetActive(false);
        flashTimer = 0f;
        seenCritCounter = critCounter;

        // 엘리트와 보스는 발밑에 색 있는 원이 깔린다.
        if (rank == MonsterRank.Normal)
        {
            auraRenderer.enabled = false;
        }
        else
        {
            if (rank == MonsterRank.Boss)
            {
                auraRenderer.color = bossAuraColor;
            }
            else
            {
                auraRenderer.color = modifierColors[(int)modifier];
            }
            // Circle 스프라이트는 지름 0.64 유닛. 몸 반경의 1.7배 크기로 깐다.
            float auraScale = definition.BodyRadius * 1.7f * 2f / 0.64f;
            auraRenderer.transform.localScale = new Vector3(auraScale, auraScale * 0.6f, 1f);
            auraRenderer.enabled = true;
        }

        visualReady = true;
        RefreshBaseColor();
        ApplyStateVisual(state);
        Track();
    }

    public override void OnStopClient()
    {
        visualReady = false;
        Untrack();
    }

    public override void OnStopServer()
    {
        Untrack();
    }

    private void Track()
    {
        if (!tracked)
        {
            tracked = true;
            director.AddActive(this);
        }
    }

    private void Untrack()
    {
        if (tracked)
        {
            tracked = false;
            director.RemoveActive(this);
        }
    }

    // ------------------------------------------------------------ 서버 AI

    public void ServerTick(float deltaTime)
    {
        if (state != MonsterState.Dead)
        {
            ServerTickStatus(deltaTime);
        }

        if (state == MonsterState.Dead)
        {
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                director.ServerDespawn(this);
            }
        }
        else if (state == MonsterState.Stagger)
        {
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                state = MonsterState.Chase;
            }
        }
        else if (state == MonsterState.Windup)
        {
            body.linearVelocity = Vector2.zero;
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                BeginStrike();
            }
        }
        else if (state == MonsterState.Strike)
        {
            TickLunge(deltaTime);
        }
        else if (state == MonsterState.Recover)
        {
            body.linearVelocity = Vector2.zero;
            stateTimer -= deltaTime;
            if (stateTimer <= 0f)
            {
                state = MonsterState.Chase;
            }
        }
        else
        {
            TickChase(deltaTime);
        }
    }

    private float CurrentMoveSpeed
    {
        get
        {
            float speed = moveSpeed;
            if (slowTime > 0f)
            {
                speed *= slowSpeedFactor;
            }
            if (enraged)
            {
                speed *= bossEnragedSpeedFactor;
            }
            return speed;
        }
    }

    private int CurrentDamage
    {
        get
        {
            float damage = attackDamage;
            if (weakenTime > 0f)
            {
                damage *= weakenDamageFactor;
            }
            return Mathf.Max(1, Mathf.RoundToInt(damage));
        }
    }

    private void TickChase(float deltaTime)
    {
        if (attackCooldown > 0f)
        {
            attackCooldown -= deltaTime;
        }

        retargetTimer -= deltaTime;
        if (retargetTimer <= 0f)
        {
            retargetTimer = 0.3f;
            target = hub.FindNearestAlivePlayer(body.position);
        }

        bool hasTarget = false;
        if (target != null)
        {
            hasTarget = !target.IsDead;
        }

        if (hasTarget)
        {
            Vector2 toTarget = (Vector2)target.transform.position - body.position;
            float distance = toTarget.magnitude;
            Vector2 direction = Vector2.right;
            if (distance > 0.001f)
            {
                direction = toTarget / distance;
            }

            if (Mathf.Abs(direction.x) > 0.1f)
            {
                bool left = direction.x < 0f;
                if (left != faceLeft)
                {
                    faceLeft = left;
                }
            }

            bool inRange = distance <= definition.AttackRange * rankAttackSizeFactors[(int)rank];
            if (rank == MonsterRank.Boss && attackCooldown <= 0f)
            {
                BeginBossPattern(direction, distance, inRange);
            }
            else if (inRange && attackCooldown <= 0f)
            {
                BeginWindup(direction, target.transform.position);
            }
            else
            {
                Vector2 velocity = GetChaseVelocity(direction, distance);
                body.linearVelocity = velocity;
                if (velocity.sqrMagnitude > 0.01f)
                {
                    SetStateIfChanged(MonsterState.Chase);
                }
                else
                {
                    SetStateIfChanged(MonsterState.Idle);
                }
            }
        }
        else
        {
            body.linearVelocity = Vector2.zero;
            SetStateIfChanged(MonsterState.Idle);
        }
    }

    private Vector2 GetChaseVelocity(Vector2 direction, float distance)
    {
        Vector2 velocity = Vector2.zero;
        float speed = CurrentMoveSpeed;
        if (definition.AttackKind == MonsterAttackKind.Ranged)
        {
            if (distance < definition.PreferredDistance * 0.6f)
            {
                velocity = -direction * speed;
            }
            else if (distance > definition.AttackRange * 0.9f)
            {
                velocity = direction * speed;
            }
        }
        else if (distance > attackReach * 0.6f)
        {
            velocity = direction * speed;
        }
        return velocity;
    }

    private void SetStateIfChanged(MonsterState newState)
    {
        if (state != newState)
        {
            state = newState;
        }
    }

    private void BeginWindup(Vector2 direction, Vector2 targetPosition)
    {
        bossPattern = BossPattern.Basic;
        aimDirection = direction;
        body.linearVelocity = Vector2.zero;
        if (definition.AttackKind == MonsterAttackKind.Melee)
        {
            attackPoint = body.position + direction * attackReach;
            telegraphRadius = attackRadius;
        }
        else if (definition.AttackKind == MonsterAttackKind.Lunge)
        {
            attackPoint = body.position + direction * (definition.LungeSpeed * definition.LungeDuration);
            telegraphRadius = attackRadius;
        }
        else
        {
            attackPoint = targetPosition;
            telegraphRadius = 0f;
        }
        telegraphTime = definition.AttackWindup;
        stateTimer = definition.AttackWindup;
        state = MonsterState.Windup;
    }

    // 보스: 공격할 차례가 되면 패턴을 하나 고른다. 가까이 있으면 기본 공격과 내려찍기를, 멀면 돌진과 발사를 더 쓴다.
    private void BeginBossPattern(Vector2 direction, float distance, bool inRange)
    {
        BossPattern pattern = BossPattern.Burst;
        int roll = Random.Range(0, 4);
        if (roll == 0)
        {
            pattern = BossPattern.Burst;
        }
        else if (roll == 1)
        {
            pattern = BossPattern.Charge;
        }
        else if (roll == 2)
        {
            if (director.AliveCount < bossSummonLimit)
            {
                pattern = BossPattern.Summon;
            }
        }
        else if (enraged && distance <= bossSlamRadius)
        {
            pattern = BossPattern.Slam;
        }
        else if (inRange)
        {
            pattern = BossPattern.Basic;
        }
        else
        {
            pattern = BossPattern.Charge;
        }

        if (pattern == BossPattern.Basic)
        {
            BeginWindup(direction, target.transform.position);
        }
        else
        {
            bossPattern = pattern;
            aimDirection = direction;
            body.linearVelocity = Vector2.zero;
            if (pattern == BossPattern.Charge)
            {
                attackPoint = body.position + direction * (bossChargeSpeed * bossChargeDuration);
                telegraphRadius = attackRadius;
            }
            else if (pattern == BossPattern.Slam)
            {
                attackPoint = body.position;
                telegraphRadius = bossSlamRadius;
            }
            else
            {
                attackPoint = body.position;
                telegraphRadius = 0f;
            }
            telegraphTime = bossPatternWindup;
            stateTimer = bossPatternWindup;
            state = MonsterState.Windup;
        }
    }

    private void BeginStrike()
    {
        attackCooldown = definition.AttackCooldown;
        if (enraged)
        {
            attackCooldown *= bossEnragedCooldownFactor;
        }

        if (bossPattern == BossPattern.Burst)
        {
            int count = bossBurstCount;
            if (enraged)
            {
                count = bossBurstCountEnraged;
            }
            float startAngle = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + Mathf.PI * 2f * i / count;
                director.ServerFireProjectile(body.position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), definitionIndex, CurrentDamage, StatusKind.Burn);
            }
            EnterRecover();
        }
        else if (bossPattern == BossPattern.Charge)
        {
            lungeHit = false;
            strikeSpeed = bossChargeSpeed;
            strikeRadius = attackRadius;
            stateTimer = bossChargeDuration;
            state = MonsterState.Strike;
        }
        else if (bossPattern == BossPattern.Summon)
        {
            director.ServerSpawnMinions(bossSummonCount);
            EnterRecover();
        }
        else if (bossPattern == BossPattern.Slam)
        {
            DamagePlayersInCircle(body.position, bossSlamRadius, Mathf.RoundToInt(CurrentDamage * bossSlamDamageFactor), StatusKind.Stun);
            EnterRecover();
        }
        else if (definition.AttackKind == MonsterAttackKind.Melee)
        {
            DamagePlayersInCircle(attackPoint, attackRadius, CurrentDamage, MeleeStatus());
            EnterRecover();
        }
        else if (definition.AttackKind == MonsterAttackKind.Lunge)
        {
            lungeHit = false;
            strikeSpeed = definition.LungeSpeed;
            strikeRadius = attackRadius;
            stateTimer = definition.LungeDuration;
            state = MonsterState.Strike;
        }
        else
        {
            // 독버섯의 포자는 약화를 건다.
            director.ServerFireProjectile(body.position, aimDirection, definitionIndex, CurrentDamage, StatusKind.Weaken);
            EnterRecover();
        }
        bossPattern = BossPattern.Basic;
    }

    private void TickLunge(float deltaTime)
    {
        body.linearVelocity = aimDirection * strikeSpeed;
        if (!lungeHit)
        {
            lungeHit = DamagePlayersInCircle(body.position, strikeRadius, CurrentDamage, MeleeStatus());
        }
        stateTimer -= deltaTime;
        if (stateTimer <= 0f)
        {
            EnterRecover();
        }
    }

    private void EnterRecover()
    {
        body.linearVelocity = Vector2.zero;
        stateTimer = Mathf.Clamp(definition.AttackRecoverTime, 0.2f, 1f);
        state = MonsterState.Recover;
    }

    // 플레이어는 최대 4명이라 물리 쿼리 대신 거리만 비교한다.
    // 근접 공격이 거는 상태이상: 흉포 엘리트는 출혈, 신속 엘리트는 둔화, 타락한 모험가는 약화.
    private StatusKind MeleeStatus()
    {
        StatusKind status = StatusKind.None;
        if (modifier == EliteModifier.Fierce)
        {
            status = StatusKind.Bleed;
        }
        else if (modifier == EliteModifier.Swift)
        {
            status = StatusKind.Slow;
        }
        else if (fallen)
        {
            status = StatusKind.Weaken;
        }
        return status;
    }

    private bool DamagePlayersInCircle(Vector2 center, float radius, int damage, StatusKind status)
    {
        bool anyHit = false;
        float radiusSqr = radius * radius;
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            if (!player.IsDead)
            {
                Vector2 toPlayer = (Vector2)player.transform.position - center;
                if (toPlayer.sqrMagnitude <= radiusSqr)
                {
                    int hpBefore = player.Hp;
                    player.ServerTakeDamage(damage, aimDirection, netId);
                    // 실제로 피해가 들어갔을 때만(무적, 패링이 아닐 때) 상태이상이 걸린다.
                    if (status != StatusKind.None && player.Hp < hpBefore)
                    {
                        float duration = playerStatusDuration;
                        if (status == StatusKind.Stun)
                        {
                            duration = slamStunTime;
                        }
                        player.ServerApplyStatus(status, duration, Mathf.Max(1, Mathf.RoundToInt(damage * playerDotFactor)));
                    }
                    anyHit = true;
                }
            }
        }
        return anyHit;
    }

    // 플레이어의 타격. 치명타 표시와 상태이상까지 처리한다.
    public void ServerTakeHit(in HitInfo hit, Vector2 hitDirection, uint attackerNetId)
    {
        if (state != MonsterState.Dead)
        {
            if (hit.crit)
            {
                critCounter++;
            }
            ServerTakeDamage(hit.damage, hitDirection, attackerNetId);
            if (state != MonsterState.Dead && hit.status != StatusKind.None)
            {
                ServerApplyStatus(hit.status, hit.statusDuration, hit.statusPower, hit.damage);
            }
        }
    }

    public void ServerTakeDamage(int amount, Vector2 hitDirection, uint attackerNetId)
    {
        if (state != MonsterState.Dead)
        {
            hp = Mathf.Max(0, hp - amount);
            if (hp == 0)
            {
                ServerDie();
            }
            else if (rank == MonsterRank.Boss)
            {
                // 보스는 맞아도 밀리거나 끊기지 않는다. 체력이 절반 아래로 내려가면 2페이즈가 된다.
                if (!enraged && hp * 2 <= maxHp)
                {
                    enraged = true;
                    attackCooldown = Mathf.Min(attackCooldown, 0.4f);
                }
            }
            else if (state != MonsterState.Strike)
            {
                body.linearVelocity = hitDirection * definition.KnockbackSpeed;
                stateTimer = staggerTime;
                state = MonsterState.Stagger;
            }
        }
    }

    private void ServerDie()
    {
        body.linearVelocity = Vector2.zero;
        bodyCollider.enabled = false;
        stateTimer = deadTime;
        statusMask = 0;
        state = MonsterState.Dead;
        director.ServerOnMonsterKilled(this);
        if (fallen)
        {
            ServerDropWornGear();
        }
    }

    // 타락한 모험가는 걸친 장비를 떨어뜨린다(플레이어마다 따로). 엘리트는 한 점, 보스는 세 점 모두.
    private void ServerDropWornGear()
    {
        Vector2 position = body.position;
        int single = Random.Range(0, 3);
        int count = hub.PlayerCount;
        for (int i = 0; i < count; i++)
        {
            PlayerInventory inventory = hub.GetPlayer(i).Inventory;
            if (rank == MonsterRank.Boss || single == 0)
            {
                inventory.ServerOfferDrop(wornWeapon, position + new Vector2(-0.6f, 0f));
            }
            if (rank == MonsterRank.Boss || single == 1)
            {
                inventory.ServerOfferDrop(wornHelmet, position + new Vector2(0f, 0.4f));
            }
            if (rank == MonsterRank.Boss || single == 2)
            {
                inventory.ServerOfferDrop(wornArmor, position + new Vector2(0.6f, 0f));
            }
        }
    }

    // 패링당했을 때: 피해 없이 지정한 시간만큼 경직된다. 돌진 중이어도 끊긴다.
    public void ServerStagger(float duration, Vector2 pushDirection)
    {
        if (state != MonsterState.Dead && rank != MonsterRank.Boss)
        {
            body.linearVelocity = pushDirection * definition.KnockbackSpeed;
            stateTimer = duration;
            state = MonsterState.Stagger;
        }
    }

    // ------------------------------------------------------------ 서버 상태이상

    public void ServerApplyStatus(StatusKind kind, float duration, float power, int hitDamage)
    {
        if (state != MonsterState.Dead)
        {
            if (kind == StatusKind.Burn)
            {
                burnTime = duration;
                burnDamage = Mathf.Max(burnDamage, Mathf.Max(1, Mathf.RoundToInt(hitDamage * power)));
            }
            else if (kind == StatusKind.Bleed)
            {
                bleedTime = duration;
                bleedDamage = Mathf.Max(bleedDamage, Mathf.Max(1, Mathf.RoundToInt(hitDamage * power)));
            }
            else if (kind == StatusKind.Slow)
            {
                slowTime = duration;
            }
            else if (kind == StatusKind.Stun)
            {
                ServerStagger(duration, Vector2.zero);
            }
            else if (kind == StatusKind.Weaken)
            {
                weakenTime = duration;
            }
            ServerRefreshStatusMask();
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
                if (damage > 0)
                {
                    // 지속 피해는 밀치거나 끊지 않는다.
                    hp = Mathf.Max(0, hp - damage);
                    if (hp == 0)
                    {
                        ServerDie();
                    }
                }
            }
            if (state != MonsterState.Dead)
            {
                ServerRefreshStatusMask();
            }
        }
    }

    private void ServerRefreshStatusMask()
    {
        byte mask = 0;
        if (burnTime > 0f)
        {
            mask |= BurnBit;
        }
        else
        {
            burnDamage = 0;
        }
        if (bleedTime > 0f)
        {
            mask |= BleedBit;
        }
        else
        {
            bleedDamage = 0;
        }
        if (slowTime > 0f)
        {
            mask |= SlowBit;
        }
        if (weakenTime > 0f)
        {
            mask |= WeakenBit;
        }
        if (mask != statusMask)
        {
            statusMask = mask;
        }
    }

    // ------------------------------------------------------------ 클라이언트 연출

    public void VisualTick(float deltaTime)
    {
        if (visualReady)
        {
            TickFrames(deltaTime);
            if (fallen)
            {
                humanoid.Tick(deltaTime);
            }

            if (flashTimer > 0f)
            {
                flashTimer -= deltaTime;
                if (flashTimer <= 0f)
                {
                    spriteRenderer.color = baseColor;
                }
            }

            if (telegraphTimer > 0f)
            {
                telegraphTimer -= deltaTime;
                float progress = 1f - Mathf.Clamp01(telegraphTimer / telegraphDuration);
                float scale = telegraphScale * Mathf.Lerp(0.35f, 1f, progress);
                telegraphRenderer.transform.localScale = new Vector3(scale, scale * 0.6f, 1f);
            }
        }
    }

    // 평소 색: 상태이상이 걸려 있으면 그 색, 2페이즈 보스는 붉은 색.
    private void RefreshBaseColor()
    {
        Color color = Color.white;
        if ((statusMask & BurnBit) != 0)
        {
            color = burnColor;
        }
        else if ((statusMask & BleedBit) != 0)
        {
            color = bleedColor;
        }
        else if ((statusMask & SlowBit) != 0)
        {
            color = slowColor;
        }
        else if ((statusMask & WeakenBit) != 0)
        {
            color = weakenColor;
        }
        else if (enraged)
        {
            color = enragedColor;
        }
        baseColor = color;
        if (flashTimer <= 0f)
        {
            spriteRenderer.color = color;
        }
    }

    private void TickFrames(float deltaTime)
    {
        if (frames != null)
        {
            frameTimer += deltaTime;
            if (frameTimer >= frameDuration)
            {
                int steps = (int)(frameTimer / frameDuration);
                frameTimer -= steps * frameDuration;
                int next = frameIndex + steps;
                int last = frames.Length - 1;
                if (frameLoop)
                {
                    next %= frames.Length;
                }
                else if (next > last)
                {
                    next = last;
                }
                if (next != frameIndex)
                {
                    frameIndex = next;
                    spriteRenderer.sprite = frames[frameIndex];
                }
            }
        }
    }

    private void PlayFrames(Sprite[] newFrames, float duration, bool loop)
    {
        frames = newFrames;
        frameLoop = loop;
        frameIndex = 0;
        frameTimer = 0f;
        if (newFrames != null && newFrames.Length > 0)
        {
            frameDuration = duration / newFrames.Length;
            spriteRenderer.sprite = newFrames[0];
        }
        else
        {
            frames = null;
        }
    }

    private float LoopDuration(Sprite[] clip)
    {
        float duration = 1f;
        if (clip != null)
        {
            duration = clip.Length / definition.FramesPerSecond;
        }
        return duration;
    }

    private void ApplyStateVisual(MonsterState newState)
    {
        if (newState == MonsterState.Idle)
        {
            PlayFrames(definition.IdleFrames, LoopDuration(definition.IdleFrames), true);
        }
        else if (newState == MonsterState.Chase)
        {
            PlayFrames(definition.WalkFrames, LoopDuration(definition.WalkFrames), true);
        }
        else if (newState == MonsterState.Windup)
        {
            PlayFrames(definition.AttackFrames, definition.AttackClipDuration, false);
            ShowTelegraph();
        }
        else if (newState == MonsterState.Strike)
        {
            hub.Sound.PlayAt(SoundId.MonsterAttack, transform.position, 0.7f);
        }
        else if (newState == MonsterState.Stagger)
        {
            PlayFrames(definition.HitFrames, LoopDuration(definition.HitFrames), false);
        }
        else if (newState == MonsterState.Dead)
        {
            PlayFrames(definition.DeadFrames, deadTime, false);
            hpBarRoot.SetActive(false);
            auraRenderer.enabled = false;
        }

        if (fallen)
        {
            ApplyHumanoidVisual(newState);
        }

        // Strike와 Recover는 공격 클립이 이어서 재생되므로 프레임을 바꾸지 않는다.
        if (newState != MonsterState.Windup)
        {
            telegraphRenderer.enabled = false;
            telegraphTimer = 0f;
        }
    }

    // 타락한 모험가는 프레임 그림 대신 캐릭터 애니메이션을 쓴다.
    private void ApplyHumanoidVisual(MonsterState newState)
    {
        if (newState == MonsterState.Idle)
        {
            humanoid.PlayLoop(CharacterAnim.Idle);
        }
        else if (newState == MonsterState.Chase)
        {
            humanoid.PlayLoop(CharacterAnim.Walk);
        }
        else if (newState == MonsterState.Windup)
        {
            humanoid.PlayAction(CharacterAnim.Attack, fallenAttackStartTime, fallenAttackSpeed, false);
        }
        else if (newState == MonsterState.Stagger)
        {
            humanoid.PlayAction(CharacterAnim.Stun, 0f, 1f, false);
        }
        else if (newState == MonsterState.Dead)
        {
            humanoid.PlayAction(CharacterAnim.Dead, 0f, 1.4f, true);
        }
    }

    private void ShowTelegraph()
    {
        if (telegraphRadius > 0f)
        {
            telegraphDuration = telegraphTime;
            telegraphTimer = telegraphDuration;
            // Circle 스프라이트는 지름 0.64 유닛. 예고 원은 몬스터의 자식이라 몬스터 크기만큼 나눠 준다.
            telegraphScale = telegraphRadius * 2f / 0.64f / transform.localScale.x;
            telegraphRenderer.color = telegraphColor;
            telegraphRenderer.transform.position = attackPoint;
            telegraphRenderer.transform.localScale = new Vector3(telegraphScale * 0.35f, telegraphScale * 0.21f, 1f);
            telegraphRenderer.enabled = true;
        }
    }

    private void OnStateChanged(MonsterState oldValue, MonsterState newValue)
    {
        if (visualReady)
        {
            ApplyStateVisual(newValue);
            // 쓰러뜨린 몬스터는 각자의 도감에 기록된다.
            if (newValue == MonsterState.Dead && oldValue != MonsterState.Dead)
            {
                hub.Sound.PlayAt(SoundId.MonsterDie, transform.position);
                int codexIndex = definitionIndex;
                if (fallen)
                {
                    codexIndex = director.DefinitionCount;
                }
                hub.Meta.RecordMonsterKill(codexIndex);
            }
        }
    }

    private void OnFacingChanged(bool oldValue, bool newValue)
    {
        if (visualReady)
        {
            spriteRenderer.flipX = newValue;
            if (fallen)
            {
                humanoid.SetFacing(newValue);
            }
        }
    }

    private void OnStatusMaskChanged(byte oldValue, byte newValue)
    {
        if (visualReady)
        {
            RefreshBaseColor();
        }
    }

    private void OnEnragedChanged(bool oldValue, bool newValue)
    {
        if (visualReady)
        {
            RefreshBaseColor();
        }
    }

    private void OnHpChanged(int oldValue, int newValue)
    {
        if (visualReady)
        {
            if (newValue < oldValue)
            {
                flashTimer = flashTime;
                spriteRenderer.color = flashColor;
                Vector2 textPosition = (Vector2)transform.position + new Vector2(0f, 0.9f * transform.localScale.y);
                if (critCounter != seenCritCounter)
                {
                    seenCritCounter = critCounter;
                    hub.DamageTextPool.ShowScaled(textPosition, oldValue - newValue, critTextColor, critTextScale);
                    hub.Sound.PlayAt(SoundId.CriticalHit, transform.position);
                }
                else
                {
                    hub.DamageTextPool.Show(textPosition, oldValue - newValue, Color.white);
                    hub.Sound.PlayAt(SoundId.MonsterHit, transform.position);
                }

                if (newValue > 0)
                {
                    float ratio = (float)newValue / maxHp;
                    hpBarRoot.SetActive(true);
                    hpBarFill.localScale = new Vector3(hpBarFillScale.x * ratio, hpBarFillScale.y, 1f);
                    hpBarFill.localPosition = new Vector3(-hpBarWidth * 0.5f * (1f - ratio), 0f, 0f);
                }
            }
        }
    }
}
