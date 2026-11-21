using LayerLab.ArtMakerUnity;
using UnityEngine;

public enum WeaponKind : byte
{
    Sword,
    Spear,
    Bow,
    Staff,
    Axe,
    Blunt,
    Crossbow,
    Gun,
    Wand
}

// 강화 평타(좌클릭을 누른 채 평타 뒤 차지했다가 놓기, 활은 시위를 다 당기기)의 종류
public enum PowerAttack : byte
{
    None,
    // 몸 둘레를 한 바퀴 돌려 벤다(검)
    Spin,
    // 조준 방향으로 내달리며 길게 찌른다(창)
    Lunge,
    // 높이 들어 앞쪽 땅을 내려찍고 기절시킨다(도끼, 둔기)
    Smash,
    // 몬스터를 꿰뚫는 강한 화살·탄(활, 석궁, 총)
    PierceShot,
    // 크게 터지는 마법탄(지팡이, 완드)
    BigShot,
    // 휘두른 자리에서 몬스터를 꿰뚫는 검기를 날린다(전사 무기: 검, 창, 도끼, 둔기)
    Wave
}

// 무기 한 자루의 능력치와 공격 방식. 무기 계열(Kind)이 조작감을 정한다.
// - 검: 부채꼴 근접, 방패 사용 가능
// - 창: 긴 직선 찌르기
// - 활: 누르는 동안 차지, 떼면 발사
// - 지팡이: 마법 투사체, 닿거나 사거리 끝에서 광역 폭발
// - 도끼: 넓고 느린 부채꼴 근접(출혈)
// - 둔기: 느린 근접, 기절
// - 석궁: 차지 없이 빠르고 강한 화살
// - 총: 짧은 사거리의 빠른 연사
// - 완드: 가볍고 빠른 마법 탄
[CreateAssetMenu(menuName = "Tower/Weapon Definition", fileName = "WeaponDefinition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("기본")]
    [SerializeField] private string displayName;
    [SerializeField] private WeaponKind kind;
    [SerializeField] private int damage = 12;
    [SerializeField] private int manaOnHit = 3;
    [Tooltip("공격 시작부터 판정(또는 발사)까지 걸리는 시간")]
    [SerializeField] private float hitDelay = 0.11f;
    [Tooltip("공격 한 번의 전체 길이. 이 시간이 지나야 다음 행동을 한다.")]
    [SerializeField] private float duration = 0.34f;
    [Tooltip("공격(차지) 중 이동 속도 배율")]
    [SerializeField] private float moveFactor = 0.25f;
    [SerializeField] private bool allowsShield;

    [Header("외형(패키지 파츠)")]
    [SerializeField] private PartsType partsType = PartsType.Sword;
    [SerializeField] private int partsIndex;

    [Header("애니메이션")]
    [SerializeField] private CharacterAnim anim = CharacterAnim.Attack;
    [SerializeField] private float animStartTime = 0.75f;
    [SerializeField] private float animSpeed = 2.2f;
    [SerializeField] private SoundId attackSound = SoundId.SwordSwing;

    [Header("근접")]
    [SerializeField] private float range = 1.5f;
    [Tooltip("부채꼴 각도. 직선 폭이 0보다 크면 쓰지 않는다.")]
    [SerializeField] private float arcDegrees = 120f;
    [Tooltip("0보다 크면 부채꼴 대신 이 반폭의 직선으로 판정한다(창).")]
    [SerializeField] private float lineHalfWidth;
    [SerializeField] private Vector2 slashScale = Vector2.one;

    [Header("원거리")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileLifetime = 1.2f;
    [SerializeField] private float projectileRadius = 0.22f;
    [SerializeField] private float explosionRadius;

    [Header("상태이상(맞은 몬스터에게 건다)")]
    [SerializeField] private StatusKind statusKind;
    [SerializeField] private float statusDuration = 3f;
    [Tooltip("화상·출혈: 0.5초마다 들어가는 피해가 타격 피해의 몇 배인지")]
    [SerializeField] private float statusPower = 0.12f;
    [SerializeField] private ProjectileLook projectileLook = ProjectileLook.Arrow;
    [SerializeField] private Color projectileColor = Color.white;
    [Tooltip("기본 공격 픽셀 그림(Assets/4.Sprite/Fx/SkillVfx/Weapon_<무기>/). cast는 근접 휘두르기, projectile·muzzle·impact는 원거리 탄")]
    [SerializeField] private SkillVfx vfx = new SkillVfx();

    [Header("차지(활)")]
    [Tooltip("최대 차지까지 걸리는 시간. 0이면 차지하지 않는다.")]
    [SerializeField] private float chargeTime;
    [Tooltip("차지 없이 쐈을 때의 피해·속도 배율")]
    [SerializeField] private float minChargeFactor = 0.35f;

    [Header("강화 평타(좌클릭을 누른 채 차지했다가 놓기)")]
    [SerializeField] private PowerAttack powerAttack;
    [Tooltip("다 모으는 데 걸리는 시간(활은 위의 차지 시간을 쓴다)")]
    [SerializeField] private float powerChargeTime = 0.8f;
    [Tooltip("평타 피해의 몇 배인지(활은 다 모은 화살 피해에 곱한다)")]
    [SerializeField] private float powerDamageFactor = 2.5f;
    [Tooltip("회전 베기·내려찍기 반지름, 큰 마법탄 폭발 반지름")]
    [SerializeField] private float powerRadius = 2f;
    [Tooltip("창 돌진 거리, 내려찍기 자리(몸에서 조준 방향으로 떨어진 거리)")]
    [SerializeField] private float powerReach = 1f;
    [SerializeField] private float powerDuration = 0.5f;
    [SerializeField] private float powerHitDelay = 0.2f;
    [Tooltip("꿰뚫는 화살·탄이 지나갈 수 있는 몬스터 수")]
    [SerializeField] private int powerPierce;
    [Tooltip("내려찍기 기절 시간")]
    [SerializeField] private float powerStunTime = 1.2f;
    [Tooltip("검기가 날아가는 속도(날아가는 거리는 powerReach, 판정 반지름은 powerRadius)")]
    [SerializeField] private float powerShotSpeed = 13f;
    [Tooltip("강화 평타 그림(비슷한 스킬의 이펙트 프레임을 빌려 쓴다). 비면 평타 그림")]
    [SerializeField] private SkillVfx powerVfx = new SkillVfx();

    public string DisplayName => displayName;
    public WeaponKind Kind => kind;
    public int Damage => damage;
    public int ManaOnHit => manaOnHit;
    public float HitDelay => hitDelay;
    public float Duration => duration;
    public float MoveFactor => moveFactor;
    public bool AllowsShield => allowsShield;
    public PartsType PartsType => partsType;
    public int PartsIndex => partsIndex;
    public CharacterAnim Anim => anim;
    public float AnimStartTime => animStartTime;
    public float AnimSpeed => animSpeed;
    public SoundId AttackSound => attackSound;
    public float Range => range;
    public float ArcDegrees => arcDegrees;
    public float LineHalfWidth => lineHalfWidth;
    public Vector2 SlashScale => slashScale;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float ProjectileRadius => projectileRadius;
    public float ExplosionRadius => explosionRadius;
    public StatusKind StatusKind => statusKind;
    public float StatusDuration => statusDuration;
    public float StatusPower => statusPower;
    public ProjectileLook ProjectileLook => projectileLook;
    public Color ProjectileColor => projectileColor;
    public SkillVfx Vfx => vfx;
    public float ChargeTime => chargeTime;
    public float MinChargeFactor => minChargeFactor;
    public PowerAttack Power => powerAttack;
    public float PowerChargeTime => powerChargeTime;
    public float PowerDamageFactor => powerDamageFactor;
    public float PowerRadius => powerRadius;
    public float PowerReach => powerReach;
    public float PowerDuration => powerDuration;
    public float PowerHitDelay => powerHitDelay;
    public int PowerPierce => powerPierce;
    public float PowerStunTime => powerStunTime;
    public float PowerShotSpeed => powerShotSpeed;
    public SkillVfx PowerVfx => powerVfx;

    public bool IsRanged => kind == WeaponKind.Bow || kind == WeaponKind.Staff || kind == WeaponKind.Crossbow || kind == WeaponKind.Gun || kind == WeaponKind.Wand;
    public const int KindCount = 9;

    // 공격할 때 무기를 움직이는 방식(CharacterVisual이 조준 각도 위에 더한다)
    public WeaponMotion Motion
    {
        get
        {
            WeaponMotion result = WeaponMotion.Swing;
            if (kind == WeaponKind.Spear)
            {
                result = WeaponMotion.Thrust;
            }
            else if (IsRanged)
            {
                result = WeaponMotion.Recoil;
            }
            return result;
        }
    }
    public bool UsesCharge => chargeTime > 0f;
}
