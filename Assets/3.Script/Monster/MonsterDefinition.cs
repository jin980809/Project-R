using UnityEngine;

public enum MonsterAttackKind : byte
{
    Melee,
    Lunge,
    Ranged,
    // 자폭: 다가와 넓은 범위를 예고한 뒤 터지며 스스로 쓰러진다.
    Explode
}

// 몬스터 한 종류의 능력치와 애니메이션 프레임.
// 그림을 다른 에셋으로 바꿀 때는 프레임 배열만 교체하면 된다(대기, 이동, 공격, 피격, 사망 5개 상태).
[CreateAssetMenu(menuName = "Tower/Monster Definition", fileName = "MonsterDefinition")]
public class MonsterDefinition : ScriptableObject
{
    [Header("기본")]
    [SerializeField] private string displayName;
    [SerializeField] private int maxHp = 30;
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float bodyRadius = 0.28f;
    [SerializeField] private float shadowScale = 1f;
    [SerializeField] private float knockbackSpeed = 3f;
    [Tooltip("그림에 곱하는 색(같은 그림을 다른 몬스터로 쓸 때)")]
    [SerializeField] private Color tint = Color.white;
    [Tooltip("이 몬스터의 공격이 플레이어에게 거는 상태이상(엘리트 특성이 없을 때)")]
    [SerializeField] private StatusKind hitStatus = StatusKind.None;
    [Tooltip("원거리: 한 번에 쏘는 투사체 수와 퍼지는 각도")]
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float projectileSpread = 0f;

    [Header("공격")]
    [SerializeField] private MonsterAttackKind attackKind;
    [SerializeField] private int damage = 8;
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float attackReach = 0.7f;
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private float attackWindup = 0.5f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float lungeSpeed = 7f;
    [SerializeField] private float lungeDuration = 0.25f;
    [SerializeField] private float preferredDistance = 4f;
    [SerializeField] private float projectileSpeed = 5f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private float projectileRadius = 0.3f;

    [Header("애니메이션 프레임")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite[] hitFrames;
    [SerializeField] private Sprite[] deadFrames;
    [SerializeField] private float framesPerSecond = 20f;
    [Tooltip("공격 클립에서 타격이 일어나는 지점(0~1). 공격 준비 시간에 맞춰 재생 속도를 정한다.")]
    [SerializeField] private float attackHitNormalized = 0.5f;

    public string DisplayName => displayName;
    public int MaxHp => maxHp;
    public float MoveSpeed => moveSpeed;
    public float BodyRadius => bodyRadius;
    public float ShadowScale => shadowScale;
    public float KnockbackSpeed => knockbackSpeed;
    public Color Tint => tint;
    public StatusKind HitStatus => hitStatus;
    public int ProjectileCount => Mathf.Max(1, projectileCount);
    public float ProjectileSpread => projectileSpread;
    public MonsterAttackKind AttackKind => attackKind;
    public int Damage => damage;
    public float AttackRange => attackRange;
    public float AttackReach => attackReach;
    public float AttackRadius => attackRadius;
    public float AttackWindup => attackWindup;
    public float AttackCooldown => attackCooldown;
    public float LungeSpeed => lungeSpeed;
    public float LungeDuration => lungeDuration;
    public float PreferredDistance => preferredDistance;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float ProjectileRadius => projectileRadius;
    public Sprite[] IdleFrames => idleFrames;
    public Sprite[] WalkFrames => walkFrames;
    public Sprite[] AttackFrames => attackFrames;
    public Sprite[] HitFrames => hitFrames;
    public Sprite[] DeadFrames => deadFrames;
    public float FramesPerSecond => framesPerSecond;
    public float AttackHitNormalized => attackHitNormalized;

    // 공격 클립 전체 길이(초). 타격 지점이 공격 준비 시간과 일치하도록 계산한다.
    public float AttackClipDuration => attackWindup / attackHitNormalized;
    public float AttackRecoverTime => AttackClipDuration - attackWindup;
}
