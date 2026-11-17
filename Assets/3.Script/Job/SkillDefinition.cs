using UnityEngine;

public enum SkillKind : byte
{
    // 자기 주변 원형 범위 공격
    AreaAround,
    // 조준 방향으로 돌진하며 지나간 길의 적을 공격(피해 배율이 0이면 이동만)
    Dash,
    // 투사체를 부채꼴로 여러 발 발사
    Volley,
    // 일정 시간 능력치 강화
    Buff
}

// 직업 스킬 하나의 수치. 피해는 "들고 있는 무기 피해 x DamageFactor"로 계산한다.
[CreateAssetMenu(menuName = "Tower/Skill Definition", fileName = "Skill")]
public class SkillDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "스킬";
    [SerializeField] private SkillKind kind;
    [SerializeField] private int manaCost = 10;
    [SerializeField] private float cooldown = 5f;
    [Tooltip("시전 동작 시간. 이 동안 다른 행동을 할 수 없다(돌진은 이동 시간).")]
    [SerializeField] private float castTime = 0.35f;
    [SerializeField] private float damageFactor = 1.5f;

    [Header("범위와 거리")]
    [Tooltip("AreaAround: 반경, Dash: 길의 반폭, Volley: 투사체 반경")]
    [SerializeField] private float radius = 2f;
    [Tooltip("Dash: 돌진 거리")]
    [SerializeField] private float range = 4f;
    [Tooltip("Dash: 돌진 중 무적")]
    [SerializeField] private bool invulnerable;

    [Header("투사체(Volley)")]
    [SerializeField] private int count = 1;
    [SerializeField] private float spreadDegrees;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileLifetime = 1f;
    [SerializeField] private float explosionRadius;
    [SerializeField] private ProjectileLook projectileLook = ProjectileLook.Arrow;

    [Header("강화(Buff)")]
    [SerializeField] private JobNodeEffect buffEffect = JobNodeEffect.DamagePercent;
    [SerializeField] private float buffValue = 30f;
    [SerializeField] private float buffDuration = 6f;

    [Header("상태이상(맞은 몬스터에게 건다)")]
    [SerializeField] private StatusKind statusKind;
    [SerializeField] private float statusDuration = 3f;
    [Tooltip("화상·출혈: 0.5초마다 들어가는 피해가 타격 피해의 몇 배인지")]
    [SerializeField] private float statusPower = 0.12f;

    [Header("연출")]
    [SerializeField] private CharacterAnim anim = CharacterAnim.Skill;
    [SerializeField] private float animStartTime = 0.3f;
    [SerializeField] private float animSpeed = 1.6f;
    [SerializeField] private Color fxColor = Color.white;

    public string DisplayName => displayName;
    public SkillKind Kind => kind;
    public int ManaCost => manaCost;
    public float Cooldown => cooldown;
    public float CastTime => castTime;
    public float DamageFactor => damageFactor;
    public float Radius => radius;
    public float Range => range;
    public bool Invulnerable => invulnerable;
    public int Count => count;
    public float SpreadDegrees => spreadDegrees;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float ExplosionRadius => explosionRadius;
    public ProjectileLook ProjectileLook => projectileLook;
    public JobNodeEffect BuffEffect => buffEffect;
    public float BuffValue => buffValue;
    public float BuffDuration => buffDuration;
    public StatusKind StatusKind => statusKind;
    public float StatusDuration => statusDuration;
    public float StatusPower => statusPower;
    public CharacterAnim Anim => anim;
    public float AnimStartTime => animStartTime;
    public float AnimSpeed => animSpeed;
    public Color FxColor => fxColor;
}
