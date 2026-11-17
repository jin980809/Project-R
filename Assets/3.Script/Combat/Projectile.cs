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
}

// 풀에서 재사용하는 투사체. ProjectilePool이 Tick을 호출한다.
// 위치는 바닥(그림자) 기준이고, 그림은 그보다 조금 위에 떠 있다.
public class Projectile : MonoBehaviour
{
    // Pixel 스프라이트는 한 변 0.04 유닛, Circle 스프라이트는 지름 0.64 유닛
    private const float PixelSize = 0.04f;
    private const float CircleSize = 0.64f;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private Sprite sporeSprite;
    [SerializeField] private Sprite pixelSprite;
    [SerializeField] private Sprite circleSprite;
    [SerializeField] private float explosionTime = 0.18f;
    [SerializeField] private float monsterHitHeight = 0.2f;

    private ProjectileShot shot;
    private Vector2 position;
    private Vector2 velocity;
    private float lifetime;
    private float radiusSqr;
    private bool exploding;
    private float explosionTimer;

    public void Launch(in ProjectileShot newShot)
    {
        shot = newShot;
        position = shot.origin;
        velocity = shot.direction * shot.speed;
        lifetime = shot.lifetime;
        radiusSqr = shot.radius * shot.radius;
        exploding = false;
        transform.position = position;

        Transform spriteTransform = spriteRenderer.transform;
        spriteRenderer.color = shot.color;
        shadowRenderer.enabled = true;
        if (shot.look == ProjectileLook.Arrow)
        {
            spriteRenderer.sprite = pixelSprite;
            spriteTransform.localScale = new Vector3(0.56f / PixelSize, 0.07f / PixelSize, 1f);
            spriteTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg);
        }
        else if (shot.look == ProjectileLook.Bullet)
        {
            spriteRenderer.sprite = pixelSprite;
            spriteTransform.localScale = new Vector3(0.24f / PixelSize, 0.1f / PixelSize, 1f);
            spriteTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg);
        }
        else if (shot.look == ProjectileLook.Orb)
        {
            float scale = shot.radius * 2f / CircleSize;
            spriteRenderer.sprite = circleSprite;
            spriteTransform.localScale = new Vector3(scale, scale, 1f);
            spriteTransform.localRotation = Quaternion.identity;
        }
        else
        {
            spriteRenderer.sprite = sporeSprite;
            spriteTransform.localScale = Vector3.one;
            spriteTransform.localRotation = Quaternion.identity;
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
            position += velocity * deltaTime;
            transform.position = position;
            lifetime -= deltaTime;

            if (lifetime <= 0f)
            {
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
                if (monster != null)
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
            if (buffer[i].TryGetComponent(out monster))
            {
                found = monster;
            }
        }
        return found;
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
            StartExplosion();
        }
        else if (shot.authoritative)
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

    private void StartExplosion()
    {
        exploding = true;
        explosionTimer = explosionTime;
        shadowRenderer.enabled = false;
        spriteRenderer.sprite = circleSprite;
        spriteRenderer.transform.localRotation = Quaternion.identity;
        ApplyExplosionVisual(0f);
    }

    private bool TickExplosion(float deltaTime)
    {
        explosionTimer -= deltaTime;
        float progress = 1f - Mathf.Clamp01(explosionTimer / explosionTime);
        ApplyExplosionVisual(progress);
        return explosionTimer <= 0f;
    }

    private void ApplyExplosionVisual(float progress)
    {
        float scale = shot.explosionRadius * 2f / CircleSize * Mathf.Lerp(0.4f, 1f, progress);
        spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        Color color = shot.color;
        color.a = Mathf.Lerp(0.7f, 0f, progress);
        spriteRenderer.color = color;
    }
}
