using UnityEngine;

public enum SkillKind : byte
{
    // 자기 주변 원형 범위 공격
    AreaAround,
    // 조준 방향으로 돌진하며 지나간 길의 적을 공격(피해 배율이 0이면 이동만). 착지 반경이 있으면 끝에서 범위 공격.
    Dash,
    // 투사체를 부채꼴로 여러 발 발사
    Volley,
    // 일정 시간 능력치 강화(반경이 있으면 반경 안의 아군 모두)
    Buff,
    // 반경 안의 아군(자신 포함) 회복
    Heal,
    // 반경 안의 쓰러진 아군을 일으킨다
    Revive,
    // 조준한 곳(사거리까지)에 장판을 깐다. 0.5초마다 적에게 피해, 회복 비율이 있으면 아군 회복.
    Zone
}

// 스킬 이펙트 종류. SkillFxPlayer가 같은 이름의 프레임 묶음을 재생한다.
public enum SkillFx : byte
{
    None,
    // 원형 베기 고리
    Slash,
    // 바닥을 내려찍는 충격파
    Shockwave,
    // 화염 폭발
    Fire,
    // 보라 마력 폭발
    Arcane,
    // 위에서 내리는 빛기둥
    Holy,
    // 초록 회복 반짝임
    Heal,
    // 몸을 감싸는 강화 기운
    Aura,
    // 독 구름
    Poison,
    // 번개
    Lightning,
    // 돌진 바람 자국
    Wind,
    // 냉기 결정
    Frost
}

// 직업 스킬 하나의 수치. 피해는 "들고 있는 무기 피해 x DamageFactor"로 계산한다.
[CreateAssetMenu(menuName = "Tower/Skill Definition", fileName = "Skill")]
public class SkillDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "스킬";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private SkillKind kind;
    [SerializeField] private int manaCost = 10;
    [SerializeField] private float cooldown = 5f;
    [Tooltip("시전 동작 시간. 이 동안 다른 행동을 할 수 없다(돌진은 이동 시간).")]
    [SerializeField] private float castTime = 0.35f;
    [SerializeField] private float damageFactor = 1.5f;

    [Header("범위와 거리")]
    [Tooltip("AreaAround·Heal·Revive·Zone: 반경, Dash: 길의 반폭, Volley: 투사체 반경, Buff: 아군 반경(0이면 자신만)")]
    [SerializeField] private float radius = 2f;
    [Tooltip("Dash: 돌진 거리, Zone: 설치 사거리")]
    [SerializeField] private float range = 4f;
    [Tooltip("Dash: 돌진 중 무적")]
    [SerializeField] private bool invulnerable;

    [Header("투사체(Volley)·착지(Dash)")]
    [SerializeField] private int count = 1;
    [SerializeField] private float spreadDegrees;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileLifetime = 1f;
    [Tooltip("Volley: 폭발 반경, Dash: 끝에서 내려찍는 반경")]
    [SerializeField] private float explosionRadius;
    [SerializeField] private ProjectileLook projectileLook = ProjectileLook.Arrow;

    [Header("강화(Buff)·장판(Zone) 지속")]
    [SerializeField] private JobNodeEffect buffEffect = JobNodeEffect.DamagePercent;
    [SerializeField] private float buffValue = 30f;
    [SerializeField] private float buffDuration = 6f;

    [Header("회복")]
    [Tooltip("Heal: 최대 체력의 몇 %, Zone: 0.5초마다 최대 체력의 몇 %, Revive: 일어날 때 체력 %")]
    [SerializeField] private float healPercent;

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
    [SerializeField] private SkillFx fx;
    [SerializeField] private SoundId sound = SoundId.SkillCast;

    public string DisplayName => displayName;
    public string Description => description;
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
    public float HealPercent => healPercent;
    public StatusKind StatusKind => statusKind;
    public float StatusDuration => statusDuration;
    public float StatusPower => statusPower;
    public CharacterAnim Anim => anim;
    public float AnimStartTime => animStartTime;
    public float AnimSpeed => animSpeed;
    public Color FxColor => fxColor;
    public SkillFx Fx => fx;
    public SoundId Sound => sound;
}
