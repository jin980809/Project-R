using UnityEngine;

public enum ProjectileLook : byte
{
    Spore,
    Arrow,
    Orb,
    // 총알: 짧고 굵은 막대
    Bullet
}

// 투사체 한 발의 설정. 값 형식이라 넘길 때 할당이 없다.
public struct ProjectileShot
{
    public Vector2 origin;
    public Vector2 direction;
    public float speed;
    public float lifetime;
    public float radius;
    public int damage;
    public float explosionRadius;
    public ProjectileLook look;
    public Color color;
    // true면 몬스터를, false면 플레이어를 맞힌다.
    public bool hitsMonsters;
    // 서버(호스트)의 판정용 투사체만 true. 클라이언트의 표시용은 피해를 주지 않는다.
    public bool authoritative;
    // 쏜 플레이어(서버 판정용에서만 사용, 적중 시 마나 회복). 몬스터가 쐈으면 null.
    public PlayerController owner;
    // 플레이어가 쏜 판정용 투사체의 타격 정보(치명타, 상태이상). 몬스터를 맞힐 때는 damage 대신 이것을 쓴다.
    public HitInfo hit;
    // 몬스터가 쏜 투사체가 플레이어에게 거는 상태이상(없으면 None)과 지속 시간, 0.5초마다의 피해
    public StatusKind playerStatus;
    public float playerStatusDuration;
    public int playerStatusDot;
    // 그림만 이만큼 옮긴 자리(무기 끝)에서 시작해 잠깐 사이 제 높이로 돌아온다. 판정 위치와는 상관없다.
    public Vector2 visualOffset;
    // 스킬 투사체의 전용 그림(날아가는 프레임, 맞은 자리 프레임). 없으면 look으로 그린다.
    public SkillVfx vfx;
    // 몬스터를 꿰뚫고 지나가는 횟수(0이면 처음 맞은 몬스터에서 멈춘다). 강화 평타 화살·탄.
    public int pierce;
    // 그림 크기 배율(0이면 1). 강화 평타 화살·탄을 크게 그린다.
    public float visualScale;
}

// 풀에서 재사용하는 투사체. ProjectilePool이 Tick을 호출한다.
// 위치는 바닥(그림자) 기준이고, 그림은 그보다 조금 위에 떠 있다.
// 마법 구슬과 총알은 빛무리·꼬리·발사 섬광·맞은 자리 파동으로 꾸민다(할당 없이 렌더러 값만 바꾼다).
public class Projectile : MonoBehaviour
{
    // 그림이 바닥 위로 떠 있는 높이(유닛). 프리팹의 Sprite 자식 위치와 같다.
    public const float SpriteHeight = 0.35f;
    // Pixel 스프라이트는 한 변 0.04 유닛, Circle 스프라이트는 지름 0.64 유닛
    private const float PixelSize = 0.04f;
    private const float CircleSize = 0.64f;
    // 효과 그림 크기(유닛): 빛무리·섬광·파동 0.64, 구슬 심 0.48, 총알 줄기 0.64 x 0.16
    private const float GlowSize = 0.64f;
    private const float CoreSize = 0.48f;
    private const float TracerLength = 0.64f;
    private const float TracerWidth = 0.16f;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private Sprite sporeSprite;
    [SerializeField] private Sprite pixelSprite;
    [SerializeField] private Sprite circleSprite;
    [SerializeField] private float explosionTime = 0.18f;
    [SerializeField] private float monsterHitHeight = 0.2f;

    [Header("꾸밈")]
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private SpriteRenderer flashRenderer;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private Sprite coreSprite;
    [SerializeField] private Sprite tracerSprite;
    [SerializeField] private Sprite flashSprite;
    [SerializeField] private Sprite ringSprite;
    [Tooltip("무기 끝에서 제 높이로 돌아오는 시간")]
    [SerializeField] private float offsetReturnTime = 0.12f;
    [SerializeField] private float flashTime = 0.08f;
    [Tooltip("폭발하지 않는 구슬·총알이 맞은 자리에 남기는 파동 시간과 반지름")]
    [SerializeField] private float burstTime = 0.14f;
    [SerializeField] private float burstRadius = 0.32f;

    private ProjectileShot shot;
    private Vector2 position;
    private Vector2 velocity;
    private float lifetime;
    private float radiusSqr;
    private bool exploding;
    private float explosionTimer;
    private float explosionDuration;
    private float visualRadius;
    private bool burstOnly;
    private float age;
    private float offsetTimer;
    private float flashTimer;
    private Vector3 flashPosition;
    private float glowScale;
    private Color glowColor;
    // 전용 그림 투사체: 프레임을 돌리고, 맞으면 SkillFxPlayer로 맞은 자리 그림을 띄운다.
    private bool animated;
    private bool expired;
    private float spriteAngle;
    private float spriteFlip;
    // 꿰뚫고 지나간 몬스터(같은 몬스터를 두 번 맞히지 않게)
    private const int MaxPierce = 8;
    private readonly MonsterController[] pierced = new MonsterController[MaxPierce];
    private int piercedCount;

    public void Launch(in ProjectileShot newShot)
    {
        shot = newShot;
        position = shot.origin;
        velocity = shot.direction * shot.speed;
        lifetime = shot.lifetime;
        radiusSqr = shot.radius * shot.radius;
        exploding = false;
        expired = false;
        piercedCount = 0;
        burstOnly = false;
        age = 0f;
        transform.position = position;

        Transform spriteTransform = spriteRenderer.transform;
        float directionAngle = Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg;
        spriteRenderer.color = shot.color;
        spriteRenderer.enabled = true;
        shadowRenderer.enabled = true;
        glowRenderer.enabled = false;
        flashRenderer.enabled = false;
        flashTimer = 0f;
        float trailWidth = 0f;
        float trailTime = 0f;
        float sizeScale = 1f;
        if (shot.visualScale > 0f)
        {
            sizeScale = shot.visualScale;
        }
        animated = shot.vfx != null && (shot.vfx.Has(shot.vfx.projectile) || shot.vfx.projectileHidden);
        if (animated && shot.vfx.projectileHidden)
        {
            // 빛줄기 투사체(레일건): 시전할 때 길 그림을 따로 깔므로 날아가는 그림과 그림자는 숨긴다.
            spriteRenderer.enabled = false;
            shadowRenderer.enabled = false;
        }
        else if (animated)
        {
            // 오른쪽을 보고 그린 그림을 날아가는 방향으로 돌린다(왼쪽이면 위아래를 뒤집어 거꾸로 서지 않게).
            Sprite first = shot.vfx.projectile[0];
            float scale = shot.vfx.projectileSize * sizeScale / Mathf.Max(0.01f, first.bounds.size.x);
            spriteRenderer.sprite = first;
            spriteRenderer.color = Color.white;
            spriteAngle = directionAngle;
            spriteFlip = 1f;
            if (shot.vfx.projectileSpin == 0f && shot.direction.x < 0f)
            {
                spriteFlip = -1f;
            }
            spriteTransform.localScale = new Vector3(scale, scale * spriteFlip, 1f);
            spriteTransform.localRotation = Quaternion.Euler(0f, 0f, spriteAngle);
            // 픽셀 그림에 꼬리·빛 번짐이 이미 그려져 있어 매끈한 빛무리와 TrailRenderer는 쓰지 않는다.
        }
        else if (shot.look == ProjectileLook.Arrow)
        {
            spriteRenderer.sprite = pixelSprite;
            spriteTransform.localScale = new Vector3(0.56f * sizeScale / PixelSize, 0.07f * sizeScale / PixelSize, 1f);
            spriteTransform.localRotation = Quaternion.Euler(0f, 0f, directionAngle);
            trailWidth = 0.05f * sizeScale;
            trailTime = 0.08f * sizeScale;
        }
        else if (shot.look == ProjectileLook.Bullet)
        {
            // 밝은 줄기 + 작은 빛무리 + 짧은 꼬리, 총구 섬광
            spriteRenderer.sprite = tracerSprite;
            spriteRenderer.color = Color.Lerp(shot.color, Color.white, 0.45f);
            spriteTransform.localScale = new Vector3(0.85f * sizeScale / TracerLength, 0.3f * sizeScale / TracerWidth, 1f);
            spriteTransform.localRotation = Quaternion.Euler(0f, 0f, directionAngle);
            ShowGlow(0.8f * sizeScale, 0.6f);
            trailWidth = 0.1f * sizeScale;
            trailTime = 0.08f * sizeScale;
            StartFlash(0.75f, Color.Lerp(shot.color, Color.white, 0.3f));
        }
        else if (shot.look == ProjectileLook.Orb)
        {
            // 하얀 심 + 색 빛무리(맥동) + 색 꼬리, 무기 끝 섬광
            float scale = shot.radius * 2.2f / CoreSize;
            spriteRenderer.sprite = coreSprite;
            spriteRenderer.color = Color.Lerp(shot.color, Color.white, 0.55f);
            spriteTransform.localScale = new Vector3(scale, scale, 1f);
            spriteTransform.localRotation = Quaternion.identity;
            ShowGlow(shot.radius * 5.2f, 0.75f);
            trailWidth = shot.radius * 1.5f;
            trailTime = 0.2f;
            StartFlash(shot.radius * 4f, shot.color);
        }
        else
        {
            spriteRenderer.sprite = sporeSprite;
            spriteTransform.localScale = Vector3.one;
            spriteTransform.localRotation = Quaternion.identity;
        }

        offsetTimer = 0f;
        if (shot.visualOffset.sqrMagnitude > 0.0001f)
        {
            offsetTimer = offsetReturnTime;
        }
        ApplyVisualOffset();

        trail.Clear();
        trail.emitting = trailTime > 0f;
        if (trailTime > 0f)
        {
            trail.time = trailTime;
            trail.widthMultiplier = trailWidth;
            Color head = shot.color;
            head.a = 0.75f;
            Color tail = shot.color;
            tail.a = 0f;
            trail.startColor = head;
            trail.endColor = tail;
        }
    }

    private void ShowGlow(float size, float alpha)
    {
        glowRenderer.enabled = true;
        glowRenderer.sprite = glowSprite;
        glowScale = size / GlowSize;
        glowRenderer.transform.localScale = new Vector3(glowScale, glowScale, 1f);
        glowColor = shot.color;
        glowColor.a = alpha;
        glowRenderer.color = glowColor;
    }

    // 무기 끝(그림이 시작하는 자리)에 짧은 섬광. 투사체가 날아가도 그 자리에 남는다.
    private void StartFlash(float size, Color color)
    {
        flashTimer = flashTime;
        flashRenderer.enabled = true;
        flashRenderer.sprite = flashSprite;
        flashRenderer.color = color;
        float scale = size / GlowSize;
        flashRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        flashRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        flashPosition = (Vector3)(shot.origin + shot.visualOffset) + new Vector3(0f, SpriteHeight, 0f);
        flashRenderer.transform.position = flashPosition;
    }

    // 그림(심·빛무리)을 무기 끝에서 제 높이로 옮긴다.
    private void ApplyVisualOffset()
    {
        Vector3 local = new Vector3(0f, SpriteHeight, 0f);
        if (offsetTimer > 0f)
        {
            float t = offsetTimer / offsetReturnTime;
            local += (Vector3)(shot.visualOffset * (t * t));
        }
        spriteRenderer.transform.localPosition = local;
        glowRenderer.transform.localPosition = local;
    }

    private void TickDecor(float deltaTime)
    {
        age += deltaTime;
        if (offsetTimer > 0f)
        {
            offsetTimer = Mathf.Max(0f, offsetTimer - deltaTime);
            ApplyVisualOffset();
        }
        if (flashTimer > 0f)
        {
            flashTimer -= deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashTime);
            flashRenderer.transform.position = flashPosition;
            Color color = flashRenderer.color;
            color.a = t;
            flashRenderer.color = color;
            if (flashTimer <= 0f)
            {
                flashRenderer.enabled = false;
            }
        }
        if (animated)
        {
            TickAnimatedSprite();
        }
        else if (shot.look == ProjectileLook.Orb && glowRenderer.enabled)
        {
            // 빛무리가 맥동하고 심이 돈다.
            float pulse = glowScale * (1f + 0.14f * Mathf.Sin(age * 28f));
            glowRenderer.transform.localScale = new Vector3(pulse, pulse, 1f);
            spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, age * 540f);
        }
    }

    // 전용 그림 투사체의 프레임을 돌린다(숨긴 투사체는 그리지 않는다).
    private void TickAnimatedSprite()
    {
        if (!shot.vfx.projectileHidden)
        {
            Sprite[] frames = shot.vfx.projectile;
            int frame = (int)(age * shot.vfx.projectileFps) % frames.Length;
            spriteRenderer.sprite = frames[frame];
            if (shot.vfx.projectileSpin != 0f)
            {
                spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, spriteAngle - age * shot.vfx.projectileSpin);
            }
        }
    }

    // 수명이 끝나면 true를 반환한다.
    public bool Tick(float deltaTime, GameHub hub)
    {
        bool finished = false;
        if (exploding)
        {
            finished = TickExplosion(deltaTime);
        }
        else
        {
            TickDecor(deltaTime);
            position += velocity * deltaTime;
            transform.position = position;
            lifetime -= deltaTime;

            if (lifetime <= 0f)
            {
                expired = true;
                finished = Impact(hub, null, null);
            }
            else if (!hub.IsInsideArena(position, 0f))
            {
                finished = Impact(hub, null, null);
            }
            else if (Physics2D.OverlapPoint(position, GameLayers.WallMask) != null)
            {
                // 방 장애물에 막힌다.
                finished = Impact(hub, null, null);
            }
            else if (shot.hitsMonsters)
            {
                MonsterController monster = FindMonsterHit(hub);
                if (monster != null && piercedCount < shot.pierce && piercedCount < MaxPierce && shot.explosionRadius <= 0f)
                {
                    PierceThrough(hub, monster);
                }
                else if (monster != null)
                {
                    finished = Impact(hub, monster, null);
                }
            }
            else
            {
                PlayerController player = FindPlayerHit(hub);
                if (player != null)
                {
                    finished = Impact(hub, null, player);
                }
            }
        }
        return finished;
    }

    private MonsterController FindMonsterHit(GameHub hub)
    {
        MonsterController found = null;
        Vector2 center = position + new Vector2(0f, monsterHitHeight);
        int count = hub.OverlapMonsters(center, shot.radius);
        Collider2D[] buffer = hub.HitBuffer;
        for (int i = 0; i < count && found == null; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster) && !WasPierced(monster))
            {
                found = monster;
            }
        }
        return found;
    }

    private bool WasPierced(MonsterController monster)
    {
        bool found = false;
        for (int i = 0; i < piercedCount && !found; i++)
        {
            found = pierced[i] == monster;
        }
        return found;
    }

    // 꿰뚫기: 맞은 몬스터에게 피해를 주고(판정용만) 그대로 날아간다.
    private void PierceThrough(GameHub hub, MonsterController monster)
    {
        pierced[piercedCount] = monster;
        piercedCount++;
        if (animated && shot.vfx.Has(shot.vfx.impact))
        {
            PlayImpactFrames(hub, shot.vfx.impactSize);
        }
        if (shot.authoritative)
        {
            monster.ServerTakeHit(shot.hit, shot.direction, 0);
            if (shot.owner != null)
            {
                shot.owner.ServerOnAttackHit();
            }
        }
    }

    private PlayerController FindPlayerHit(GameHub hub)
    {
        PlayerController found = null;
        int count = hub.PlayerCount;
        for (int i = 0; i < count && found == null; i++)
        {
            PlayerController player = hub.GetPlayer(i);
            if (!player.IsDead)
            {
                Vector2 toPlayer = (Vector2)player.transform.position - position;
                if (toPlayer.sqrMagnitude <= radiusSqr)
                {
                    found = player;
                }
            }
        }
        return found;
    }

    // 맞았거나 수명·사거리가 끝났을 때. 폭발형이면 폭발 연출로 넘어가고(false), 아니면 바로 끝난다(true).
    private bool Impact(GameHub hub, MonsterController monster, PlayerController player)
    {
        bool finished = true;
        if (shot.explosionRadius > 0f)
        {
            finished = false;
            if (shot.authoritative && shot.hitsMonsters)
            {
                DamageMonstersInExplosion(hub);
            }
            hub.Sound.PlayAt(SoundId.Explosion, position);
            if (animated && shot.vfx.Has(shot.vfx.impact))
            {
                finished = true;
                PlayImpactFrames(hub, shot.explosionRadius * shot.vfx.impactScale);
            }
            else
            {
                StartExplosion(shot.explosionRadius, explosionTime, false);
            }
        }
        else if (animated)
        {
            // 전용 그림: 무언가에 맞았을 때만 맞은 자리 그림(사거리 끝에서 사라질 때는 없음)
            if (!expired && shot.vfx.Has(shot.vfx.impact))
            {
                PlayImpactFrames(hub, shot.vfx.impactSize);
            }
        }
        else if (shot.look == ProjectileLook.Orb || shot.look == ProjectileLook.Bullet)
        {
            // 폭발하지 않아도 맞은 자리에 작은 파동을 남긴다(피해는 아래에서 준다).
            finished = false;
            StartExplosion(burstRadius, burstTime, true);
        }
        if (shot.explosionRadius <= 0f && shot.authoritative)
        {
            if (monster != null)
            {
                monster.ServerTakeHit(shot.hit, shot.direction, 0);
                if (shot.owner != null)
                {
                    shot.owner.ServerOnAttackHit();
                }
            }
            else if (player != null)
            {
                int hpBefore = player.Hp;
                player.ServerTakeDamage(shot.damage, shot.direction, 0);
                if (shot.playerStatus != StatusKind.None && player.Hp < hpBefore)
                {
                    player.ServerApplyStatus(shot.playerStatus, shot.playerStatusDuration, shot.playerStatusDot);
                }
            }
        }
        return finished;
    }

    private void PlayImpactFrames(GameHub hub, float radius)
    {
        if (hub.SkillFx != null)
        {
            SkillVfx vfx = shot.vfx;
            Vector2 at = position;
            if (!vfx.impactGround)
            {
                at += new Vector2(0f, SpriteHeight);
            }
            float angle = Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg;
            hub.SkillFx.PlayFrames(vfx.impact, vfx.impactFps, vfx.impactGround, at, radius, 0f, null, angle, vfx.impactDirectional);
        }
    }

    private void DamageMonstersInExplosion(GameHub hub)
    {
        Vector2 center = position + new Vector2(0f, monsterHitHeight);
        int count = hub.OverlapMonsters(center, shot.explosionRadius);
        Collider2D[] buffer = hub.HitBuffer;
        int hitCount = 0;
        for (int i = 0; i < count; i++)
        {
            MonsterController monster;
            if (buffer[i].TryGetComponent(out monster))
            {
                Vector2 away = (Vector2)buffer[i].bounds.center - center;
                Vector2 direction = shot.direction;
                if (away.sqrMagnitude > 0.0001f)
                {
                    direction = away.normalized;
                }
                monster.ServerTakeHit(shot.hit, direction, 0);
                hitCount++;
            }
        }
        if (hitCount > 0 && shot.owner != null)
        {
            shot.owner.ServerOnAttackHit();
        }
    }

    // 폭발(또는 작은 파동) 연출. 원이 커지며 사라지고, 바깥에 고리가 퍼진다.
    private void StartExplosion(float radius, float duration, bool ringOnly)
    {
        exploding = true;
        burstOnly = ringOnly;
        visualRadius = radius;
        explosionDuration = duration;
        explosionTimer = duration;
        shadowRenderer.enabled = false;
        trail.emitting = false;
        spriteRenderer.enabled = !ringOnly;
        spriteRenderer.sprite = circleSprite;
        spriteRenderer.transform.localRotation = Quaternion.identity;
        glowRenderer.enabled = true;
        glowRenderer.sprite = ringSprite;
        offsetTimer = 0f;
        ApplyVisualOffset();
        ApplyExplosionVisual(0f);
    }

    private bool TickExplosion(float deltaTime)
    {
        explosionTimer -= deltaTime;
        if (flashTimer > 0f)
        {
            TickDecor(deltaTime);
        }
        float progress = 1f - Mathf.Clamp01(explosionTimer / explosionDuration);
        ApplyExplosionVisual(progress);
        bool done = explosionTimer <= 0f;
        if (done)
        {
            spriteRenderer.enabled = true;
            flashRenderer.enabled = false;
        }
        return done;
    }

    private void ApplyExplosionVisual(float progress)
    {
        float scale = visualRadius * 2f / CircleSize * Mathf.Lerp(0.4f, 1f, progress);
        spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        Color color = shot.color;
        color.a = Mathf.Lerp(0.7f, 0f, progress);
        spriteRenderer.color = color;

        float ringScale = visualRadius * 2f / GlowSize * Mathf.Lerp(0.5f, 1.25f, progress);
        glowRenderer.transform.localScale = new Vector3(ringScale, ringScale, 1f);
        Color ring = Color.Lerp(shot.color, Color.white, 0.35f);
        float startAlpha = 0.85f;
        if (burstOnly)
        {
            startAlpha = 1f;
        }
        ring.a = Mathf.Lerp(startAlpha, 0f, progress * progress);
        glowRenderer.color = ring;
    }
}
