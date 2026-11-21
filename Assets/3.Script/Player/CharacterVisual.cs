using LayerLab.ArtMakerUnity;
using UnityEngine;

public enum CharacterAnim : byte
{
    Idle,
    Walk,
    Run,
    Roll,
    Attack,
    DoubleAttack,
    JumpAttack,
    Skill,
    Stun,
    Dead,
    Victory
}

// 무기를 휘두르는 연출 종류. 조준 각도 위에 더해진다.
public enum WeaponMotion : byte
{
    // 조준 방향을 가로질러 크게 벤다(검, 도끼, 둔기)
    Swing,
    // 조준 방향으로 찔렀다가 돌아온다(창)
    Thrust,
    // 쏘는 순간 위로 살짝 튄다(활, 석궁, 총, 지팡이, 완드)
    Recoil,
    // 강화 평타: 젖힌 자리에서 한 바퀴 돌아 벤다(검)
    Spin,
    // 강화 평타: 높이 든 무기를 앞쪽 땅으로 내려찍는다(도끼, 둔기)
    Smash,
    // 강화 평타: 멀리 찔렀다가 돌아온다(창)
    Lunge,
    // 스킬(대지 가르기, 내려찍기): 뛰어오르며 무기를 머리 위로 들었다가 내려찍는 순간 땅을 친다
    LeapSmash
}

// 패키지 캐릭터(PartsManager + Animator)를 게임에서 쓰기 위한 래퍼.
// 패키지 Animator는 전이가 없고 모든 클립이 반복이라, 재생 시작 지점·속도·정지를 여기서 직접 제어한다.
// 조준이 켜져 있으면 Animator가 손을 움직인 뒤(LateUpdate) 오른손 뼈대를 돌려 무기가 조준 방향을 가리키게 한다.
public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private PartsManager partsManager;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform flipRoot;
    [Tooltip("무기 파츠가 붙어 있는 오른손 뼈대")]
    [SerializeField] private Transform handRight;

    [Header("커스터마이징 팔레트(번호로 저장·동기화)")]
    [SerializeField] private Color[] skinColors =
    {
        new Color(1f, 1f, 1f), new Color(1f, 0.9f, 0.8f), new Color(0.98f, 0.8f, 0.66f), new Color(0.88f, 0.68f, 0.52f),
        new Color(0.72f, 0.52f, 0.38f), new Color(0.54f, 0.38f, 0.28f), new Color(0.75f, 0.9f, 0.75f), new Color(0.75f, 0.8f, 1f)
    };
    [SerializeField] private Color[] hairColors =
    {
        new Color(0.95f, 0.95f, 0.95f), new Color(0.22f, 0.22f, 0.25f), new Color(0.45f, 0.3f, 0.2f), new Color(0.95f, 0.8f, 0.4f),
        new Color(0.82f, 0.32f, 0.22f), new Color(0.95f, 0.56f, 0.2f), new Color(0.95f, 0.56f, 0.72f), new Color(0.62f, 0.42f, 0.88f),
        new Color(0.36f, 0.56f, 0.95f), new Color(0.42f, 0.76f, 0.42f), new Color(0.6f, 0.6f, 0.64f), new Color(0.75f, 0.88f, 0.98f)
    };

    [Header("휘두르기 연출")]
    [SerializeField] private float swingArcDegrees = 150f;
    [Tooltip("휘두르기 전체 시간 중 베는 구간의 비율. 나머지는 제자리로 돌아온다.")]
    [SerializeField] private float swingCutRatio = 0.45f;
    [Tooltip("벤 뒤 남은 시간 동안 끝 각도를 얼마나 유지할지(1이면 끝에 멈춤, 0이면 조준 방향까지 돌아옴). 이어서 벨 때 이 자리에서 바로 반대로 벤다.")]
    [SerializeField] private float swingEndKeep = 0.6f;
    [Tooltip("휘두르기가 끝난 뒤 남은 각도가 조준 방향으로 돌아오는 시간(초)")]
    [SerializeField] private float swingSettleTime = 0.12f;
    [SerializeField] private float thrustDistance = 0.45f;
    [SerializeField] private float recoilDegrees = 14f;

    [Header("강화 평타 연출")]
    [Tooltip("차지할 때 휘두르는 무기를 뒤로 젖히는 각도(돌려 베기·내려찍기도 이 자리에서 시작한다)")]
    [SerializeField] private float chargeWindupDegrees = 110f;
    [Tooltip("차지할 때 찌르는 무기를 당기는 거리")]
    [SerializeField] private float chargePullback = 0.3f;
    [Tooltip("다 모았을 때 무기가 떨리는 각도")]
    [SerializeField] private float chargeShakeDegrees = 3f;
    [Tooltip("한 바퀴 베기·내려찍기에서 베는 구간의 비율(나머지는 끝 자리에 머문다)")]
    [SerializeField] private float spinCutRatio = 0.6f;
    [SerializeField] private float smashCutRatio = 0.35f;
    [Tooltip("내려찍은 무기 끝 각도(조준 방향보다 아래로)")]
    [SerializeField] private float smashEndDegrees = 70f;
    [Tooltip("창 돌진 찌르기 거리 = thrustDistance x 이 값")]
    [SerializeField] private float lungeReachScale = 2.2f;
    [Tooltip("다른 사람의 조준 각도를 따라가는 속도(도/초)")]
    [SerializeField] private float remoteAimTurnSpeed = 900f;

    [Header("무기 들기(엔터 더 건전 방식: 몸 중심을 도는 원 위에서 손잡이를 잡는다)")]
    [Tooltip("발밑에서 무기를 도는 중심까지의 높이(유닛)")]
    [SerializeField] private float holdHeight = 0.08f;
    [Tooltip("중심에서 손(손잡이)까지의 거리(유닛)")]
    [SerializeField] private float holdRadius = 0.4f;

    [Header("원거리 무기 들기")]
    [Tooltip("조준 기준 높이(발밑에서). PlayerController.centerOffset과 같게 둔다.")]
    [SerializeField] private float aimCenterHeight = 0.35f;
    [Tooltip("활: 조준 기준점에서 활 가운데까지의 거리. 활 가운데가 조준선 위에 오므로 화살이 그 자리에서 마우스 쪽으로 나간다.")]
    [SerializeField] private float bowHoldRadius = 0.5f;
    [Tooltip("석궁: 조준 기준점에서 석궁 그림 가운데까지의 거리. 석궁 몸체가 조준선 위에 놓여 볼트가 마우스 쪽으로 곧게 나간다.")]
    [SerializeField] private float crossbowHoldRadius = 0.62f;
    [Tooltip("지팡이: 조준 쪽으로 돌 때 중심이 되는 자리(그림 아래 끝 0 ~ 위 끝 1). 이 자리가 몸 중심(무기를 쥐는 높이)에 온다.")]
    [SerializeField] private float staffPivotRatio = 0.2f;

    private bool aimEnabled;
    private PartsType weaponType = PartsType.Sword;
    private SpriteRenderer weaponRenderer;
    private UpgradeGlow upgradeGlow;
    private SpriteRenderer[] handRenderers;
    private float aimAngle;
    private float targetAimAngle;
    private bool smoothAim;
    private float weaponForward;
    private WeaponMotion motion;
    private float motionTimer;
    private float motionDuration;
    private float swingSign = 1f;
    // 지금 휘두르기의 시작 각도(swingSign 기준, 양수 = 시작하는 쪽)와, 마지막으로 무기에 더한 휘두르기 각도
    private float swingFrom;
    private float swingRest;
    // 강화 평타 차지(0~1)와 그때의 자세 종류(무기 평타 연출)
    private float chargeAmount;
    private WeaponMotion chargeMotion;
    // 뛰어올라 내려찍기: 전체 시간 가운데 무기가 땅에 닿는 순간의 비율
    private float leapImpactRatio = 0.6f;
    // 탈것을 타면 패키지(MountSeat)가 들어 올리는 사람 부분. 무기를 쥐는 기준점도 같이 올린다.
    private Transform rider;
    private Vector3 riderRestLocal;
    // 몸통. 달리기·대기 애니메이션이 위아래로 들썩이게 하므로 무기도 같이 움직여 몸과 따로 놀지 않게 한다.
    private Transform body;
    private Vector3 bodyRestLocal;
    // 오른손 바로 아래 무기 묶음들(검, 활, 지팡이...)과 대기 자세에서의 위치
    private Transform[] handParts = new Transform[0];
    private Vector3[] handPartRest = new Vector3[0];
    private Quaternion[] handPartRestRotation = new Quaternion[0];
    // 마법 무기(지팡이·완드): 무기를 쥐는 중심에서 무기 끝까지의 거리(조준 방향으로). LateUpdate가 잰다.
    private float magicTipDistance;
    private bool hasMagicTip;

    private readonly int[] stateHashes = new int[11];
    private readonly float[] clipLengths =
    {
        1.8333f, 0.6667f, 0.3333f, 0.5833f, 1.8333f, 1.8333f, 1.65f, 1.1667f, 2f, 1f, 1.3333f
    };

    private bool initialized;
    // 부위 그림들과 처음 투명도. 사망 동작이 부위를 투명하게 만들므로, 다시 꺼내 쓸 때 되돌린다.
    private SpriteRenderer[] partRenderers = new SpriteRenderer[0];
    private float[] partAlphas = new float[0];
    private bool hasLoop;
    private CharacterAnim loopAnim;
    private bool actionPlaying;
    private float holdTimer;
    private bool faceLeft;

    // 시설 동작(낚시, 채굴, 망치질). 재생하는 동안 조준을 멈추고, 이동·공격을 시작하면 끝난다.
    private bool posing;
    // 시설 동작(낚시 등) 중에 들어온 방향. 동작 중에는 돌지 않고, 끝나면 이 방향으로 돌아선다.
    private bool wantFaceLeft;
    private float poseTimer;
    // 끌어올리기(Fishing_Catch) 클립은 반복 클립이라 길이(0.67초)를 넘기면 처음부터 다시 나온다.
    private const float FishCatchTime = 0.64f;
    private int poseNextHash;
    private float poseNextDuration;
    private FishingEquip fishingEquip;
    private Transform fishingPoint;
    private readonly int fishThrowHash = Animator.StringToHash("Fishing_Throwing");
    private readonly int fishWaitHash = Animator.StringToHash("Fishing_Idle");
    private readonly int fishReelHash = Animator.StringToHash("Fishing_Real");
    private readonly int fishCatchHash = Animator.StringToHash("Fishing_Catch");

    public PartsManager PartsManager => partsManager;
    public bool IsPosing => posing;
    // 지금(마지막) 휘두르기가 아래에서 위로 올려 베는 차례인지(PlayWeaponMotion이 번갈아 바꾼다)
    public bool SwingUpward => swingSign < 0f;

    // 낚싯줄 끝(찌) 위치. 낚시 물결을 이 자리에 그린다.
    public Vector3 FishingPointPosition
    {
        get
        {
            if (fishingPoint == null)
            {
                fishingPoint = FindDeep(animator.transform, "Tool_Fishing_Point");
            }
            Vector3 point = transform.position;
            if (fishingPoint != null)
            {
                point = fishingPoint.position;
            }
            return point;
        }
    }

    public void Init()
    {
        if (!initialized)
        {
            initialized = true;
            stateHashes[(int)CharacterAnim.Idle] = Animator.StringToHash("Idle");
            stateHashes[(int)CharacterAnim.Walk] = Animator.StringToHash("Walk");
            stateHashes[(int)CharacterAnim.Run] = Animator.StringToHash("Run");
            stateHashes[(int)CharacterAnim.Roll] = Animator.StringToHash("Roll");
            stateHashes[(int)CharacterAnim.Attack] = Animator.StringToHash("Attack");
            stateHashes[(int)CharacterAnim.DoubleAttack] = Animator.StringToHash("Double Attack");
            stateHashes[(int)CharacterAnim.JumpAttack] = Animator.StringToHash("Jump Attack");
            stateHashes[(int)CharacterAnim.Skill] = Animator.StringToHash("Skill");
            stateHashes[(int)CharacterAnim.Stun] = Animator.StringToHash("Stun");
            stateHashes[(int)CharacterAnim.Dead] = Animator.StringToHash("Dead1");
            stateHashes[(int)CharacterAnim.Victory] = Animator.StringToHash("Victory");

            partsManager.Init();
            if (SceneLook.Current != null)
            {
                SceneLook.Current.Apply(this);
            }
            partRenderers = animator.GetComponentsInChildren<SpriteRenderer>(true);
            partAlphas = new float[partRenderers.Length];
            for (int i = 0; i < partRenderers.Length; i++)
            {
                partAlphas[i] = partRenderers[i].color.a;
            }
            rider = FindDeep(transform, "Rider");
            if (rider != null)
            {
                riderRestLocal = rider.localPosition;
            }
            body = animator.transform.Find("Body");
            if (body != null)
            {
                bodyRestLocal = body.localPosition;
            }
            handParts = new Transform[handRight.childCount];
            handPartRest = new Vector3[handRight.childCount];
            handPartRestRotation = new Quaternion[handRight.childCount];
            for (int i = 0; i < handParts.Length; i++)
            {
                handParts[i] = handRight.GetChild(i);
                handPartRest[i] = handParts[i].localPosition;
                handPartRestRotation[i] = handParts[i].localRotation;
            }
            PlayLoop(CharacterAnim.Idle);
        }
    }

    // 몬스터처럼 풀에서 다시 꺼내 쓰는 캐릭터. 사망 동작이 투명하게 만든 부위를 처음 투명도로 되돌리고 서 있는 자세로 시작한다.
    // 애니메이터는 꺼졌다 켜질 때 그 순간 값을 기본값으로 다시 잡으므로(투명한 채로 잡혀 투구와 무기만 보였다),
    // 꺼져도 상태와 기본값을 그대로 두게 한다.
    public void ResetForReuse()
    {
        animator.keepAnimatorStateOnDisable = true;
        for (int i = 0; i < partRenderers.Length; i++)
        {
            Color color = partRenderers[i].color;
            color.a = partAlphas[i];
            partRenderers[i].color = color;
        }
        animator.speed = 1f;
        posing = false;
        poseTimer = 0f;
        poseNextHash = 0;
        hasLoop = false;
        actionPlaying = false;
        holdTimer = 0f;
        PlayLoop(CharacterAnim.Idle);
    }

    // 무기를 쥐는 기준점(발밑). 탈것에 타 있으면 앉은 높이만큼 올라가고, 몸통이 들썩이는 만큼 같이 움직인다.
    private Vector3 GetSeatPosition()
    {
        Vector3 seat = transform.position;
        if (rider != null && rider.parent != null)
        {
            seat += rider.position - rider.parent.TransformPoint(riderRestLocal);
        }
        if (body != null && body.parent != null)
        {
            seat += body.position - body.parent.TransformPoint(bodyRestLocal);
        }
        return seat;
    }

    // 반복 애니메이션(대기, 달리기). 이미 같은 것이 재생 중이면 다시 시작하지 않는다.
    public void PlayLoop(CharacterAnim anim)
    {
        if (posing)
        {
            // 서 있는 상태로 돌아가는 것은 시설 동작을 끊지 않는다(움직이거나 공격하면 끊는다).
            if (anim == CharacterAnim.Idle)
            {
                hasLoop = true;
                loopAnim = anim;
                return;
            }
            EndPose(false);
        }
        bool same = hasLoop && loopAnim == anim && !actionPlaying;
        if (!same)
        {
            hasLoop = true;
            loopAnim = anim;
            actionPlaying = false;
            holdTimer = 0f;
            animator.speed = 1f;
            animator.Play(stateHashes[(int)anim], 0, 0f);
        }
    }

    // 한 번 재생하는 동작(공격, 구르기, 사망). startTime초 지점부터 speed 배속으로 재생한다.
    // holdAtEnd가 true면 클립 끝에서 멈춘다(사망).
    public void PlayAction(CharacterAnim anim, float startTime, float speed, bool holdAtEnd)
    {
        if (posing)
        {
            EndPose(false);
        }
        float length = clipLengths[(int)anim];
        actionPlaying = true;
        animator.speed = speed;
        animator.Play(stateHashes[(int)anim], 0, startTime / length);
        if (holdAtEnd)
        {
            holdTimer = (length - startTime) / speed - 0.03f;
        }
        else
        {
            holdTimer = 0f;
        }
    }

    // 오른손 무기를 바꾼다. 패키지가 다른 오른손 무기는 알아서 벗기고, 양손 무기면 방패도 벗긴다.
    public void EquipWeapon(PartsType type, int index, bool withShield, int shieldIndex)
    {
        partsManager.EquipParts(type, index);
        weaponForward = GetWeaponForward(type);
        weaponType = type;
        weaponRenderer = FindWeaponRenderer(type);
        hasMagicTip = false;
        if (withShield)
        {
            partsManager.EquipParts(PartsType.Shield, shieldIndex);
        }
        else
        {
            partsManager.UnequipParts(PartsType.Shield);
        }
    }

    // 강화 빛(무기 외곽선과 반짝이). 단계가 0이면 그쪽 빛은 끈다. 처음 강화 장비를 입을 때 UpgradeGlow를 붙인다.
    public void SetUpgradeGlow(int weaponLevel, Color weaponColor, int gearLevel, Color gearColor)
    {
        if (upgradeGlow == null && (weaponLevel > 0 || gearLevel > 0))
        {
            upgradeGlow = gameObject.AddComponent<UpgradeGlow>();
        }
        if (upgradeGlow != null)
        {
            upgradeGlow.Set(weaponRenderer, weaponLevel, weaponColor, gearLevel, gearColor);
        }
    }

    // 강화 평타 차지(0이면 끈다): 무기를 젖히거나 당긴 자세와 금빛(UpgradeGlow).
    public void SetCharge(float amount, WeaponMotion holdMotion)
    {
        chargeAmount = amount;
        chargeMotion = holdMotion;
        if (upgradeGlow == null && amount > 0f)
        {
            upgradeGlow = gameObject.AddComponent<UpgradeGlow>();
            upgradeGlow.Set(weaponRenderer, 0, Color.white, 0, Color.white);
        }
        if (upgradeGlow != null)
        {
            upgradeGlow.SetCharge(amount);
        }
    }

    public int SkinColorCount => skinColors.Length;
    public int HairColorCount => hairColors.Length;

    public Color GetSkinColor(int index)
    {
        return skinColors[index % skinColors.Length];
    }

    public Color GetHairColor(int index)
    {
        return hairColors[index % hairColors.Length];
    }

    public int GetPartsCount(PartsType type)
    {
        return partsManager.GetPartsCount(type);
    }

    // 커스터마이징한 외형을 입힌다. valid가 0이면 패키지 기본 모습 그대로 둔다.
    public void ApplyLook(in CharacterLook look)
    {
        if (look.valid != 0)
        {
            partsManager.EquipParts(PartsType.Hair, look.hair);
            partsManager.EquipParts(PartsType.Eye, look.eye);
            partsManager.EquipParts(PartsType.Skin, look.skin);
            if (look.beard == CharacterLook.NoBeard)
            {
                partsManager.UnequipParts(PartsType.Beard);
            }
            else
            {
                partsManager.EquipParts(PartsType.Beard, look.beard);
            }
            partsManager.SetColor(ColorTargetType.Hair, hairColors[look.hairColor % hairColors.Length]);
            partsManager.SetColor(ColorTargetType.Beard, hairColors[look.beardColor % hairColors.Length]);
            partsManager.SetColor(ColorTargetType.Skin, skinColors[look.skinColor % skinColors.Length]);
        }
    }

    // 투구와 갑옷. 투구가 없으면 머리카락이 보이고, 갑옷이 없으면 기본 옷을 입는다.
    public void EquipGear(bool hasHelmet, int helmetIndex, bool hasArmor, int armorIndex, int defaultChestIndex)
    {
        if (hasHelmet)
        {
            partsManager.EquipParts(PartsType.Helmet, helmetIndex);
        }
        else
        {
            partsManager.UnequipParts(PartsType.Helmet);
        }
        if (hasArmor)
        {
            partsManager.EquipParts(PartsType.Chest, armorIndex);
        }
        else
        {
            partsManager.EquipParts(PartsType.Chest, defaultChestIndex);
        }
    }

    public void Tick(float deltaTime)
    {
        if (posing && poseTimer > 0f)
        {
            poseTimer -= deltaTime;
            if (poseTimer <= 0f)
            {
                if (poseNextHash != 0)
                {
                    int next = poseNextHash;
                    poseNextHash = 0;
                    animator.speed = 1f;
                    animator.Play(next, 0, 0f);
                    poseTimer = poseNextDuration;
                }
                else
                {
                    EndPose(true);
                }
            }
        }
        if (holdTimer > 0f)
        {
            holdTimer -= deltaTime;
            if (holdTimer <= 0f)
            {
                animator.speed = 0f;
            }
        }
    }

    // ------------------------------------------------------------ 조준

    // 오른손 뼈대 회전이 0일 때 무기 파츠가 가리키는 각도(패키지 프리팹의 파츠 배치에서 잰 값)
    private float GetWeaponForward(PartsType type)
    {
        float forward = -15f;
        if (type == PartsType.Wand)
        {
            forward = -30f;
        }
        else if (type == PartsType.Gun)
        {
            forward = -24.3f;
        }
        else if (type == PartsType.Crossbow)
        {
            // 석궁 그림은 몸체(개머리판과 활 부분)가 볼트보다 아래로 기울어 그려져 있어,
            // 화면에서 잰 몸체 방향(-19도)을 기준으로 해야 마우스 쪽을 똑바로 겨눈다.
            forward = -19f;
        }
        else if (type == PartsType.Staff)
        {
            forward = 90f;
        }
        else if (type == PartsType.Bow)
        {
            forward = -90f;
        }
        return forward;
    }

    // 조준을 켜면 무기가 조준 방향을 가리킨다. 구르기, 사망, 시설 도구를 쓸 때는 끈다.
    public void SetAimEnabled(bool enabled)
    {
        if (aimEnabled && !enabled)
        {
            // 손 회전을 애니메이션이 다시 정하도록 기본 자세로 돌려놓는다.
            motionTimer = 0f;
            swingRest = 0f;
            handRight.localRotation = Quaternion.identity;
        }
        aimEnabled = enabled;
    }

    // 본인은 매 프레임 바로 적용하고(smooth = false), 다른 사람은 동기화된 값을 부드럽게 따라간다.
    public void SetAim(Vector2 aim, bool smooth)
    {
        // 캐릭터는 좌우 반전만 하므로 바라보는 쪽 기준(-90 ~ 90도)으로 바꾼다.
        targetAimAngle = Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg;
        smoothAim = smooth;
        if (!smooth)
        {
            aimAngle = targetAimAngle;
        }
    }

    // 공격 연출 시작. duration 동안 휘두르기·찌르기·반동을 조준 각도 위에 더한다.
    // 뛰어올라 내려찍기: duration 동안, impactTime 순간에 무기가 땅에 닿는다.
    public void PlayLeapSmash(float duration, float impactTime)
    {
        PlayWeaponMotion(WeaponMotion.LeapSmash, duration);
        leapImpactRatio = Mathf.Clamp(impactTime / Mathf.Max(0.05f, duration), 0.2f, 0.95f);
    }

    public void PlayWeaponMotion(WeaponMotion newMotion, float duration)
    {
        motion = newMotion;
        motionDuration = Mathf.Max(0.05f, duration);
        motionTimer = motionDuration;
        if (newMotion == WeaponMotion.Swing)
        {
            // 한 번은 위에서 아래로, 다음은 아래에서 위로 벤다.
            swingSign = -swingSign;
            // 이어서 벨 때는 앞 휘두르기가 멈춘 자리(새로 벨 쪽)에서 바로 시작한다. 조준 방향으로 돌아갔다가
            // 반대쪽 끝으로 한 프레임에 튀지 않게 한다.
            swingFrom = swingArcDegrees * 0.5f;
            float restOnStartSide = swingRest * swingSign;
            if (restOnStartSide > 0f && restOnStartSide < swingFrom)
            {
                swingFrom = restOnStartSide;
            }
        }
        else
        {
            swingRest = 0f;
        }
    }

    private void LateUpdate()
    {
        if (aimEnabled && !posing)
        {
            float deltaTime = Time.deltaTime;
            if (smoothAim)
            {
                aimAngle = Mathf.MoveTowardsAngle(aimAngle, targetAimAngle, remoteAimTurnSpeed * deltaTime);
            }

            float angleOffset = 0f;
            float reach = 0f;
            if (motionTimer > 0f)
            {
                motionTimer -= deltaTime;
                float progress = 1f - Mathf.Clamp01(motionTimer / motionDuration);
                if (motion == WeaponMotion.Swing)
                {
                    angleOffset = GetSwingOffset(progress) * swingSign;
                    swingRest = angleOffset;
                }
                else if (motion == WeaponMotion.Thrust)
                {
                    reach = Mathf.Sin(progress * Mathf.PI) * thrustDistance;
                }
                else if (motion == WeaponMotion.Spin)
                {
                    // 젖힌 자리에서 한 바퀴(360도 + 젖힌 각도) 돌아 조준 방향에서 멈춘다.
                    float cut = Mathf.Clamp01(progress / spinCutRatio);
                    float eased = cut * cut * (3f - 2f * cut);
                    angleOffset = Mathf.Lerp(chargeWindupDegrees, -360f, eased);
                    swingRest = Mathf.DeltaAngle(0f, angleOffset);
                }
                else if (motion == WeaponMotion.Smash)
                {
                    // 높이 든 자리에서 점점 빨라지며 내려찍고, 남은 시간은 땅에 댄 채 머문다.
                    float cut = Mathf.Clamp01(progress / smashCutRatio);
                    angleOffset = Mathf.Lerp(chargeWindupDegrees, -smashEndDegrees, cut * cut);
                    swingRest = angleOffset;
                }
                else if (motion == WeaponMotion.Lunge)
                {
                    reach = Mathf.Sin(progress * Mathf.PI) * thrustDistance * lungeReachScale;
                }
                else if (motion == WeaponMotion.LeapSmash)
                {
                    // 앞 75%: 뛰어오르며 머리 위로 천천히 든다. 나머지: 점점 빨라지며 내려찍는다. 땅에 닿은 뒤에는 그대로 머문다.
                    float raiseEnd = leapImpactRatio * 0.75f;
                    if (progress < raiseEnd)
                    {
                        float k = progress / raiseEnd;
                        angleOffset = chargeWindupDegrees * (1f - (1f - k) * (1f - k));
                    }
                    else if (progress < leapImpactRatio)
                    {
                        float k = (progress - raiseEnd) / (leapImpactRatio - raiseEnd);
                        angleOffset = Mathf.Lerp(chargeWindupDegrees, -smashEndDegrees, k * k);
                    }
                    else
                    {
                        angleOffset = -smashEndDegrees;
                    }
                    swingRest = angleOffset;
                }
                else
                {
                    angleOffset = recoilDegrees * (1f - progress) * (1f - progress);
                }
            }
            else if (chargeAmount > 0f)
            {
                // 강화 평타 차지 자세: 휘두르는 무기는 뒤로 젖히고, 찌르는 무기는 당긴다. 다 모으면 살짝 떤다.
                float eased = 1f - (1f - chargeAmount) * (1f - chargeAmount);
                float shake = 0f;
                if (chargeAmount >= 1f)
                {
                    shake = Mathf.Sin(Time.time * 70f) * chargeShakeDegrees;
                }
                angleOffset = shake;
                if (chargeMotion == WeaponMotion.Swing)
                {
                    angleOffset = chargeWindupDegrees * eased + shake;
                    swingRest = angleOffset;
                }
                else if (chargeMotion == WeaponMotion.Thrust)
                {
                    reach = -chargePullback * eased;
                }
            }
            else if (swingRest != 0f)
            {
                // 휘두르기가 끝나고 더 베지 않으면 남은 각도만큼 조준 방향으로 돌아온다.
                swingRest = Mathf.MoveTowards(swingRest, 0f, swingArcDegrees * 0.5f / Mathf.Max(0.01f, swingSettleTime) * deltaTime);
                angleOffset = swingRest;
            }

            // 달리기 애니메이션이 무기 묶음(오른손 아래 무기 부모)을 10~20도 흔든다. 조준 중에는 대기 자세(회전 0)로 고정해
            // 활을 당긴 채 걸어도 무기가 흔들리지 않게 한다.
            if (weaponRenderer != null)
            {
                // 지팡이처럼 무기 그림이 손 바로 아래에 붙어 있으면 달리기 애니메이션이 그림 자체를 약 15도 돌린다.
                // 그림도 대기 자세 회전·위치로 되돌린다(달리며 쏠 때 지팡이 끝이 마우스에서 어긋나던 문제).
                Transform weaponTransform = weaponRenderer.transform;
                for (int i = 0; i < handParts.Length; i++)
                {
                    if (handParts[i] == weaponTransform)
                    {
                        weaponTransform.localRotation = handPartRestRotation[i];
                        weaponTransform.localPosition = handPartRest[i];
                    }
                }
                Transform part = weaponRenderer.transform.parent;
                while (part != null && part != handRight)
                {
                    part.localRotation = Quaternion.identity;
                    // 달리기 애니메이션은 지팡이 묶음의 위치도 옮긴다(대기 자세에서 옆으로 약 5픽셀). 대기 자세 위치로 고정해
                    // 서서 쏘다가 움직이기 시작할 때 지팡이가 튀지 않게 한다.
                    for (int i = 0; i < handPartRest.Length; i++)
                    {
                        if (handParts[i] == part)
                        {
                            part.localPosition = handPartRest[i];
                        }
                    }
                    part = part.parent;
                }
            }

            // 손의 부모부터 캐릭터 뿌리까지 쌓인 회전(달리기·차지 애니메이션이 팔과 몸을 돌려도 무기 각도가 흔들리지 않게 모두 뺀다).
            float parentAngle = 0f;
            Transform bone = handRight.parent;
            while (bone != null && bone != transform)
            {
                parentAngle += bone.localEulerAngles.z;
                bone = bone.parent;
            }
            float side = 1f;
            if (faceLeft)
            {
                side = -1f;
            }
            Vector3 seat = GetSeatPosition();

            float handAngle = aimAngle + angleOffset;
            handRight.localRotation = Quaternion.Euler(0f, 0f, handAngle - weaponForward - parentAngle);

            // 손 위치: 애니메이션이 정한 자리 대신 몸 중심을 도는 원 위(휘두르기도 이 원을 따라 돈다).
            float radians = handAngle * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(radians) * side, Mathf.Sin(radians), 0f);
            bool centerOnAim = weaponType == PartsType.Bow || weaponType == PartsType.Crossbow;
            if (centerOnAim && weaponRenderer != null && weaponRenderer.sprite != null)
            {
                // 활·석궁: 무기 그림 가운데를 조준 기준점에서 조준 방향으로 일정 거리에 둔다(쥔 손이 아니라 무기 가운데 기준).
                // 조준 기준점이 마우스를 향한 선의 시작이므로 무기가 그 선 위에 놓인다.
                float radius = crossbowHoldRadius;
                if (weaponType == PartsType.Bow)
                {
                    radius = bowHoldRadius;
                }
                Vector3 target = seat + new Vector3(0f, aimCenterHeight, 0f) + direction * (radius + reach);
                handRight.position = target;
                Vector3 bowCenter = WeaponPoint(0.5f, 0.5f);
                if (weaponType == PartsType.Crossbow)
                {
                    // 석궁: 활대 축(양 끝 가운데 → 활대 꼭대기)이 조준 방향과 같아지게 손을 더 돌리고, 활대 꼭대기를 조준선 위에 둔다.
                    Vector2 axis = WeaponPixel(prodApex) - WeaponPixel(prodChordMid);
                    float error = Vector2.SignedAngle(axis, direction);
                    Vector3 parentScale = handRight.parent.lossyScale;
                    float mirror = Mathf.Sign(parentScale.x * parentScale.y);
                    handRight.localRotation = Quaternion.Euler(0f, 0f, handRight.localEulerAngles.z + error * mirror);
                    bowCenter = WeaponPixel(prodApex);
                }
                handRight.position = target + (target - bowCenter);
            }
            else
            {
                float distance = holdRadius + reach;
                Vector3 center = seat + new Vector3(0f, holdHeight, 0f);
                Vector3 hand = center + direction * distance;
                handRight.position = hand;
                if (weaponType == PartsType.Staff && weaponRenderer != null && weaponRenderer.sprite != null)
                {
                    // 지팡이: 완드처럼 끝이 조준 쪽을 향하게 들고, 아래에서 5분의 1 지점을 몸 중심에 두어 그 자리를 축으로 돈다.
                    Vector3 pivot = WeaponPoint(0.5f, staffPivotRatio);
                    handRight.position = hand + (center + direction * reach - pivot);
                }
                if (IsMagicWeapon())
                {
                    Vector2 tipNow;
                    if (TryGetMuzzle(direction, out tipNow))
                    {
                        magicTipDistance = Vector2.Dot(tipNow - (Vector2)center, direction);
                        hasMagicTip = true;
                    }
                }
            }
        }
    }

    // 휘두르기: 앞쪽 비율 동안 호의 한쪽 끝에서 반대쪽 끝까지 빠르게 베고, 남은 시간 동안 조준 방향으로 돌아온다.
    private float GetSwingOffset(float progress)
    {
        float half = swingArcDegrees * 0.5f;
        float offset;
        if (progress < swingCutRatio)
        {
            float cut = progress / swingCutRatio;
            // 처음은 느리고 끝으로 갈수록 빨라졌다가 멈춘다.
            float eased = cut * cut * (3f - 2f * cut);
            offset = Mathf.Lerp(swingFrom, -half, eased);
        }
        else
        {
            // 끝 각도의 일부(swingEndKeep)는 남겨 둔다. 나머지는 휘두르기가 끝난 뒤 LateUpdate가 되돌린다.
            float back = (progress - swingCutRatio) / (1f - swingCutRatio);
            offset = Mathf.Lerp(-half, -half * swingEndKeep, back * back * (3f - 2f * back));
        }
        return offset;
    }

    // 오른손 아래에서 무기 그림 렌더러(패키지 파츠 이름 = 부위 이름)를 찾는다. 장착할 때만 찾는다.
    private SpriteRenderer FindWeaponRenderer(PartsType type)
    {
        if (handRenderers == null)
        {
            handRenderers = handRight.GetComponentsInChildren<SpriteRenderer>(true);
        }
        string partName = type.ToString();
        SpriteRenderer found = null;
        for (int i = 0; i < handRenderers.Length && found == null; i++)
        {
            if (handRenderers[i].name == partName && handRenderers[i].sprite != null)
            {
                found = handRenderers[i];
            }
        }
        return found;
    }

    // 무기 그림의 로컬 사각형 안 한 점(0~1 비율)을 월드 좌표로 바꾼다.
    private Vector2 WeaponPoint(float u, float v)
    {
        Bounds local = weaponRenderer.sprite.bounds;
        Vector3 point = new Vector3(Mathf.Lerp(local.min.x, local.max.x, u), Mathf.Lerp(local.min.y, local.max.y, v), 0f);
        return weaponRenderer.transform.TransformPoint(point);
    }

    // 석궁 활대 그림(FA_WP_Main_Crossbow) 안의 픽셀 위치(왼쪽 위 기준): 활대 꼭대기와 양 끝의 가운데.
    private readonly Vector2 prodApex = new Vector2(59f, 36f);
    private readonly Vector2 prodChordMid = new Vector2(31.5f, 36f);

    // 무기 그림의 픽셀 위치(왼쪽 위 기준)를 월드 좌표로 바꾼다.
    private Vector2 WeaponPixel(Vector2 pixel)
    {
        Sprite sprite = weaponRenderer.sprite;
        Rect rect = sprite.rect;
        Vector3 point = new Vector3((pixel.x - sprite.pivot.x) / sprite.pixelsPerUnit, (rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
        return weaponRenderer.transform.TransformPoint(point);
    }

    // 투사체가 나가는 자리(월드, 그림 높이 기준).
    // 활은 활 가운데, 지팡이는 머리(맨 위), 총·석궁·완드는 그림에서 조준 방향으로 가장 먼 쪽 끝.
    private bool IsMagicWeapon()
    {
        return weaponType == PartsType.Staff || weaponType == PartsType.Wand;
    }

    // 마법 무기를 aim 쪽으로 겨눴을 때의 무기 끝(지팡이 머리, 완드 끝). 마법 기본 공격과 스킬이 여기서 나간다.
    // 지금 그려진 무기 각도가 아니라 쥐는 중심 + 조준 방향 × 끝까지 거리로 구하므로, 서버 판정에서도 같은 자리가 나온다.
    public bool TryGetMagicTip(Vector2 aim, out Vector2 tip)
    {
        tip = Vector2.zero;
        bool found = hasMagicTip && IsMagicWeapon() && weaponRenderer != null && weaponRenderer.enabled && aim.sqrMagnitude > 0.0001f;
        if (found)
        {
            Vector3 center = GetSeatPosition() + new Vector3(0f, holdHeight, 0f);
            tip = (Vector2)center + aim.normalized * magicTipDistance;
        }
        return found;
    }

    public bool TryGetMuzzle(Vector2 aim, out Vector2 muzzle)
    {
        muzzle = Vector2.zero;
        bool found = aimEnabled && weaponRenderer != null && weaponRenderer.sprite != null && weaponRenderer.enabled;
        if (found)
        {
            if (weaponType == PartsType.Bow)
            {
                muzzle = WeaponPoint(0.5f, 0.5f);
            }
            else if (weaponType == PartsType.Crossbow)
            {
                muzzle = WeaponPixel(prodApex);
            }
            else if (weaponType == PartsType.Staff)
            {
                muzzle = WeaponPoint(0.5f, 0.93f);
            }
            else
            {
                Vector2 center = WeaponPoint(0.5f, 0.5f);
                float best = 0f;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 point = WeaponPoint(corner & 1, corner >> 1);
                    best = Mathf.Max(best, Vector2.Dot(point - center, aim));
                }
                muzzle = center + aim * (best * 0.85f);
            }
        }
        return found;
    }

    // ------------------------------------------------------------ 시설 동작

    // 낚시: 던지기 -> 기다리기(반복). 입질이 오면 FishBite, 끝나면 FishCatch 또는 EndPose.
    public void PoseFishCast()
    {
        BeginPose();
        animator.Play(fishThrowHash, 0, 0f);
        poseNextHash = fishWaitHash;
        poseNextDuration = 0f;
        poseTimer = 0.75f;
    }

    public void PoseFishBite()
    {
        BeginPose();
        animator.Play(fishReelHash, 0, 0f);
        poseNextHash = 0;
        poseTimer = 0f;
    }

    public void PoseFishCatch()
    {
        BeginPose();
        animator.Play(fishCatchHash, 0, 0f);
        poseNextHash = 0;
        poseTimer = FishCatchTime;
    }

    // 곡괭이(mining) 또는 손에 든 무기로 한 번 내려친다.
    public void PoseStrike(bool mining)
    {
        BeginPose();
        if (fishingEquip != null)
        {
            fishingEquip.SetMining(mining);
        }
        animator.Play(stateHashes[(int)CharacterAnim.Attack], 0, 0.15f / clipLengths[(int)CharacterAnim.Attack]);
        animator.speed = 1.6f;
        poseNextHash = 0;
        poseTimer = 0.6f;
    }

    public void EndPose(bool returnToLoop)
    {
        if (!posing)
        {
            return;
        }
        posing = false;
        poseTimer = 0f;
        poseNextHash = 0;
        ApplyFacing(wantFaceLeft);
        if (fishingEquip != null)
        {
            fishingEquip.SetMining(false);
        }
        if (returnToLoop && hasLoop)
        {
            actionPlaying = false;
            animator.speed = 1f;
            animator.Play(stateHashes[(int)loopAnim], 0, 0f);
        }
    }

    private void BeginPose()
    {
        if (fishingEquip == null)
        {
            fishingEquip = partsManager.GetComponent<FishingEquip>();
        }
        if (!posing)
        {
            // 손 회전을 애니메이션에 맡긴다.
            handRight.localRotation = Quaternion.identity;
            motionTimer = 0f;
        }
        posing = true;
        actionPlaying = true;
        holdTimer = 0f;
        animator.speed = 1f;
    }

    // 탈것(-1이면 내림). 패키지의 탈것 파츠 번호(0 검정, 1 갈색, 2 파랑).
    public void SetMount(int partsIndex)
    {
        if (partsIndex < 0)
        {
            partsManager.UnequipParts(PartsType.Mount);
        }
        else
        {
            partsManager.EquipParts(PartsType.Mount, partsIndex);
        }
    }

    private Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    public void SetFacing(bool left)
    {
        wantFaceLeft = left;
        // 낚싯대를 던진 뒤처럼 시설 동작 중에는 마우스를 움직여도 몸을 돌리지 않는다.
        if (!posing)
        {
            ApplyFacing(left);
        }
    }

    private void ApplyFacing(bool left)
    {
        if (left != faceLeft)
        {
            faceLeft = left;
            Vector3 scale = flipRoot.localScale;
            float size = Mathf.Abs(scale.x);
            if (left)
            {
                scale.x = -size;
            }
            else
            {
                scale.x = size;
            }
            flipRoot.localScale = scale;
        }
    }

    // 씬의 그림 방식(SceneLook)에 맞춘다: 캐릭터 기본 머티리얼(Sprites/Default)을 쓰는 파츠만 material로 바꾸고,
    // 이름이 Shadow인 발밑 그림자는 shadow 그림으로 바꾼다. 강화 빛 같은 다른 머티리얼은 그대로 둔다.
    public void ApplySceneLook(Material material, Sprite shadow)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer target = renderers[i];
            if (shadow != null && target.name == "Shadow")
            {
                target.sprite = shadow;
                continue;
            }
            Material current = target.sharedMaterial;
            if (material != null && current != null && current.shader != null && current.shader.name == "Sprites/Default")
            {
                target.sharedMaterial = material;
            }
        }
    }

}
