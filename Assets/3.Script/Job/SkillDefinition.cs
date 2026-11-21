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
    Frost,
    // 십자로 두 번 긋는 베기(조준 방향으로 돌린다)
    CrossSlash,
    // 몸 주위를 도는 회전 칼날
    Whirl,
    // 앞쪽을 크게 휩쓰는 반원(조준 방향)
    Sweep,
    // 길게 찌르는 창끝(돌진 길에 깐다)
    Pierce,
    // 위에서 아래로 내려치는 큰 일격(조준 방향)
    Cleave,
    // 땅이 갈라지며 바위가 솟는다
    Quake,
    // 얼음 파편이 사방으로 터진다
    IceNova,
    // 화살비(장판, 반복)
    ArrowRain,
    // 저주 문양과 해골
    Hex,
    // 강화 시전: 공격(불기둥과 화살표), 방어(방패 막), 속도(바람 소용돌이), 집중(조준선)
    BuffPower,
    BuffGuard,
    BuffSpeed,
    BuffFocus
}

// 스킬 하나만의 이펙트 그림(Tools/ArtGen/vfx가 스킬마다 따로 그린 프레임). 비어 있는 부분은 쓰지 않는다.
// - cast: 시전 이펙트(범위·회복·강화는 시전 자리, 장판은 반복, 돌진은 출발 자리)
// - trail: 돌진 길(가로로 늘려 돌진 방향으로 돌린다)
// - projectile: 투사체 그림(오른쪽을 보고 그렸다), impact: 투사체가 맞은 자리·돌진 끝·착지 자리
// - muzzle: 투사체를 쏠 때 무기 앞
[System.Serializable]
public class SkillVfx
{
    public Sprite[] cast;
    public float castFps = 20f;
    public bool castGround;
    public bool castDirectional;
    [Tooltip("옆에서 본 바닥 그림(망치 찍기, 휩쓸기): 조준 각도로 돌리지 않고 좌우만 뒤집는다")]
    public bool castMirror;
    [Tooltip("스킬 반경에 곱하는 그림 크기 배율")]
    public float castScale = 1f;
    [Tooltip("돌진 출발 자리 그림 반지름(유닛)")]
    public float castSize = 0.9f;
    [Tooltip("장판: 반복 그림(cast) 앞에 한 번 재생하는 나타나는 그림. cast와 칸 크기가 같다.")]
    public Sprite[] castIntro;
    public float castIntroFps = 20f;
    [Tooltip("장판: 지속 시간 끝에 한 번 재생하는 사라지는 그림. cast와 칸 크기가 같다.")]
    public Sprite[] castOutro;
    public float castOutroFps = 20f;

    public Sprite[] trail;
    public float trailFps = 20f;
    [Tooltip("돌진 길 그림의 세로 폭(유닛)")]
    public float trailWidth = 1.4f;

    public Sprite[] projectile;
    public float projectileFps = 18f;
    [Tooltip("투사체 그림 가로 길이(유닛)")]
    public float projectileSize = 1f;
    [Tooltip("투사체 그림 회전 속도(도/초). 0이면 날아가는 방향을 본다.")]
    public float projectileSpin;
    [Tooltip("날아가는 그림을 숨긴다. 시전할 때 trail 그림을 사거리만큼 깔아 빛줄기로 보여 주는 투사체(레일건).")]
    public bool projectileHidden;

    public Sprite[] impact;
    public float impactFps = 20f;
    [Tooltip("폭발 반경이 없을 때 그림 반지름(유닛)")]
    public float impactSize = 0.8f;
    [Tooltip("폭발·착지 반경에 곱하는 그림 크기 배율")]
    public float impactScale = 1f;
    public bool impactGround;
    public bool impactDirectional;
    [Tooltip("옆에서 본 바닥 그림(내려찍기): 조준 각도로 돌리지 않고 좌우만 뒤집는다")]
    public bool impactMirror;

    public Sprite[] muzzle;
    public float muzzleFps = 22f;
    public float muzzleSize = 0.7f;
    public bool muzzleDirectional;

    public bool Has(Sprite[] frames)
    {
        return frames != null && frames.Length > 0;
    }

    public bool HasAny => Has(cast) || Has(trail) || Has(projectile) || Has(impact) || Has(muzzle);
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
    [Tooltip("주변 범위 공격을 조준 방향 부채꼴로만 칠 때의 각도(0이면 몸 둘레 전체)")]
    [SerializeField] private float arcDegrees;
    [Tooltip("주변 범위 공격이 시전을 시작하고 몇 초 뒤에 맞는지(뛰어올라 무기를 내려찍는 순간). 0이면 바로 맞는다.")]
    [SerializeField] private float impactDelay;
    [Header("차징(누르고 있는 동안 모으고 떼면 나간다. 모을 시간이 0이면 바로 시전)")]
    [Tooltip("다 모으는 데 걸리는 시간(초)")]
    [SerializeField] private float chargeTime;
    [Tooltip("하나도 모으지 않고 뗐을 때의 피해 배율(다 모으면 1)")]
    [SerializeField] private float chargeMinDamage = 0.35f;
    [Tooltip("하나도 모으지 않고 뗐을 때의 크기 배율(범위 반경, 돌진 거리, 투사체 크기·폭발 반경. 다 모으면 1)")]
    [SerializeField] private float chargeMinSize = 0.55f;
    [Tooltip("Volley: 투사체가 꿰뚫고 지나가는 적 수(0이면 첫 적에서 멈춘다)")]
    [SerializeField] private int pierce;
    [Tooltip("Dash: 돌진 거리, Zone: 설치 사거리")]
    [SerializeField] private float range = 4f;
    [Tooltip("Dash: 돌진 중 무적")]
    [SerializeField] private bool invulnerable;
    [Tooltip("커서 자리에 놓는 스킬(장판, 마법형 범위). 키를 누르고 있으면 사거리(range)가 보이고, 떼면 커서 자리(사거리 밖이면 사거리 끝)에 놓는다.")]
    [SerializeField] private bool groundTarget;
    [Tooltip("Zone: 초당 장판 가운데로 끌어당기는 거리(유닛). 0이면 끌지 않는다(블랙홀).")]
    [SerializeField] private float pullSpeed;

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
    [Tooltip("스킬 창·스킬 줄 아이콘(스킬마다 따로 그린 그림, Tools/ArtGen/SkillIcons). 비면 이펙트 종류 아이콘을 쓴다")]
    [SerializeField] private Sprite icon;
    [SerializeField] private SoundId sound = SoundId.SkillCast;
    [SerializeField] private SkillVfx vfx = new SkillVfx();

    public string DisplayName => displayName;
    public string Description => description;
    public SkillKind Kind => kind;
    public int ManaCost => manaCost;
    public float Cooldown => cooldown;
    public float CastTime => castTime;
    public float DamageFactor => damageFactor;
    public float Radius => radius;
    public float ArcDegrees => arcDegrees;
    public float ImpactDelay => impactDelay;
    public bool IsCharge => chargeTime > 0f;
    public float ChargeTime => chargeTime;
    public int Pierce => pierce;

    // 모은 정도(0~1)에 따른 피해 배율. 차징 스킬이 아니면 1.
    public float GetChargeDamage(float charge01)
    {
        float factor = 1f;
        if (chargeTime > 0f)
        {
            factor = Mathf.Lerp(chargeMinDamage, 1f, Mathf.Clamp01(charge01));
        }
        return factor;
    }

    // 모은 정도(0~1)에 따른 크기 배율. 차징 스킬이 아니면 1.
    public float GetChargeSize(float charge01)
    {
        float factor = 1f;
        if (chargeTime > 0f)
        {
            factor = Mathf.Lerp(chargeMinSize, 1f, Mathf.Clamp01(charge01));
        }
        return factor;
    }
    public float Range => range;
    public bool Invulnerable => invulnerable;
    public bool GroundTarget => groundTarget;
    public float PullSpeed => pullSpeed;
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
    public Sprite Icon => icon;
    public SoundId Sound => sound;
    public SkillVfx Vfx => vfx;
}
