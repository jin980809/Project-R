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

// 몬스터 한 마리. AI와 피해 판정은 서버(호스트)가 처리하고, 클라이언트는 상태 값에 맞춰 프레임 애니메이션만 재생한다.
// Update를 직접 돌리지 않고 MonsterDirector가 한꺼번에 Tick을 호출한다. 오브젝트는 풀에서 재사용한다.
public class MonsterController : NetworkBehaviour, IDamageable
{
    [Header("참조")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CircleCollider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform shadow;
    [SerializeField] private SpriteRenderer telegraphRenderer;
    [SerializeField] private GameObject hpBarRoot;
    [SerializeField] private Transform hpBarFill;

    [Header("연출")]
    [SerializeField] private float staggerTime = 0.2f;
    [SerializeField] private float deadTime = 0.75f;
    [SerializeField] private float flashTime = 0.1f;
    [SerializeField] private Color flashColor = new Color(1f, 0.45f, 0.45f);
    [SerializeField] private Color telegraphColor = new Color(1f, 0.2f, 0.15f, 0.35f);
    [SerializeField] private float hpBarWidth = 0.6f;

    [SyncVar] private int definitionIndex;
    [SyncVar] private int maxHp;
    [SyncVar(hook = nameof(OnHpChanged))] private int hp;
    [SyncVar(hook = nameof(OnStateChanged))] private MonsterState state;
    [SyncVar(hook = nameof(OnFacingChanged))] private bool faceLeft;
    [SyncVar] private Vector2 attackPoint;

    private MonsterDirector director;
    private GameHub hub;
    private MonsterDefinition definition;
    private bool tracked;
    private bool visualReady;
    private Vector3 hpBarFillScale;

    // 서버 AI
    private PlayerController target;
    private float stateTimer;
    private float retargetTimer;
    private float attackCooldown;
    private Vector2 aimDirection;
    private bool lungeHit;

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

    // 풀에서 처음 만들 때 한 번 호출
    public void Setup(MonsterDirector ownerDirector, GameHub ownerHub)
    {
        director = ownerDirector;
        hub = ownerHub;
        hpBarFillScale = hpBarFill.localScale;
    }

    // 서버에서 스폰 직전에 호출
    public void ServerInit(int newDefinitionIndex, float hpScale)
    {
        definitionIndex = newDefinitionIndex;
        definition = director.GetDefinition(newDefinitionIndex);
        maxHp = Mathf.RoundToInt(definition.MaxHp * hpScale);
        hp = maxHp;
        faceLeft = false;
        state = MonsterState.Chase;
        target = null;
        stateTimer = 0f;
        retargetTimer = 0f;
        attackCooldown = Random.Range(0.3f, 0.9f);
        lungeHit = false;
        bodyCollider.enabled = true;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.linearVelocity = Vector2.zero;
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
        bodyCollider.radius = definition.BodyRadius;
        bodyCollider.offset = new Vector2(0f, definition.BodyRadius * 0.7f);
        shadow.localScale = new Vector3(definition.ShadowScale, definition.ShadowScale, 1f);
        spriteRenderer.color = Color.white;
        spriteRenderer.flipX = faceLeft;
        telegraphRenderer.enabled = false;
        hpBarRoot.SetActive(false);
        flashTimer = 0f;
        visualReady = true;
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

            if (distance <= definition.AttackRange && attackCooldown <= 0f)
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
        if (definition.AttackKind == MonsterAttackKind.Ranged)
        {
            if (distance < definition.PreferredDistance * 0.6f)
            {
                velocity = -direction * definition.MoveSpeed;
            }
            else if (distance > definition.AttackRange * 0.9f)
            {
                velocity = direction * definition.MoveSpeed;
            }
        }
        else if (distance > definition.AttackReach * 0.6f)
        {
            velocity = direction * definition.MoveSpeed;
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
        aimDirection = direction;
        body.linearVelocity = Vector2.zero;
        if (definition.AttackKind == MonsterAttackKind.Melee)
        {
            attackPoint = body.position + direction * definition.AttackReach;
        }
        else if (definition.AttackKind == MonsterAttackKind.Lunge)
        {
            attackPoint = body.position + direction * (definition.LungeSpeed * definition.LungeDuration);
        }
        else
        {
            attackPoint = targetPosition;
        }
        stateTimer = definition.AttackWindup;
        state = MonsterState.Windup;
    }

    private void BeginStrike()
    {
        attackCooldown = definition.AttackCooldown;
        if (definition.AttackKind == MonsterAttackKind.Melee)
        {
            DamagePlayersInCircle(attackPoint, definition.AttackRadius);
            EnterRecover();
        }
        else if (definition.AttackKind == MonsterAttackKind.Lunge)
        {
            lungeHit = false;
            stateTimer = definition.LungeDuration;
            state = MonsterState.Strike;
        }
        else
        {
            director.ServerFireProjectile(body.position, aimDirection, definitionIndex);
            EnterRecover();
        }
    }

    private void TickLunge(float deltaTime)
    {
        body.linearVelocity = aimDirection * definition.LungeSpeed;
        if (!lungeHit)
        {
            lungeHit = DamagePlayersInCircle(body.position, definition.AttackRadius);
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
    private bool DamagePlayersInCircle(Vector2 center, float radius)
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
                    player.ServerTakeDamage(definition.Damage, aimDirection, netId);
                    anyHit = true;
                }
            }
        }
        return anyHit;
    }

    public void ServerTakeDamage(int amount, Vector2 hitDirection, uint attackerNetId)
    {
        if (state != MonsterState.Dead)
        {
            hp = Mathf.Max(0, hp - amount);
            if (hp == 0)
            {
                body.linearVelocity = Vector2.zero;
                bodyCollider.enabled = false;
                stateTimer = deadTime;
                state = MonsterState.Dead;
                director.ServerOnMonsterKilled(this);
            }
            else if (state != MonsterState.Strike)
            {
                body.linearVelocity = hitDirection * definition.KnockbackSpeed;
                stateTimer = staggerTime;
                state = MonsterState.Stagger;
            }
        }
    }

    // ------------------------------------------------------------ 클라이언트 연출

    public void VisualTick(float deltaTime)
    {
        if (visualReady)
        {
            TickFrames(deltaTime);

            if (flashTimer > 0f)
            {
                flashTimer -= deltaTime;
                if (flashTimer <= 0f)
                {
                    spriteRenderer.color = Color.white;
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
        else if (newState == MonsterState.Stagger)
        {
            PlayFrames(definition.HitFrames, LoopDuration(definition.HitFrames), false);
        }
        else if (newState == MonsterState.Dead)
        {
            PlayFrames(definition.DeadFrames, deadTime, false);
            hpBarRoot.SetActive(false);
        }

        // Strike와 Recover는 공격 클립이 이어서 재생되므로 프레임을 바꾸지 않는다.
        if (newState != MonsterState.Windup)
        {
            telegraphRenderer.enabled = false;
            telegraphTimer = 0f;
        }
    }

    private void ShowTelegraph()
    {
        if (definition.AttackKind != MonsterAttackKind.Ranged)
        {
            telegraphDuration = definition.AttackWindup;
            telegraphTimer = telegraphDuration;
            // Circle 스프라이트는 지름 0.64 유닛
            telegraphScale = definition.AttackRadius * 2f / 0.64f;
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
        }
    }

    private void OnFacingChanged(bool oldValue, bool newValue)
    {
        if (visualReady)
        {
            spriteRenderer.flipX = newValue;
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
                hub.DamageTextPool.Show((Vector2)transform.position + new Vector2(0f, 0.9f), oldValue - newValue, Color.white);

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
