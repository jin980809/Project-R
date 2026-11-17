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
    Recoil
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
    [SerializeField] private float thrustDistance = 0.32f;
    [SerializeField] private float recoilDegrees = 14f;
    [Tooltip("다른 사람의 조준 각도를 따라가는 속도(도/초)")]
    [SerializeField] private float remoteAimTurnSpeed = 900f;

    [Header("무기 들기(엔터 더 건전 방식: 몸 중심을 도는 원 위에서 손잡이를 잡는다)")]
    [Tooltip("발밑에서 무기를 도는 중심까지의 높이(유닛)")]
    [SerializeField] private float holdHeight = 0.08f;
    [Tooltip("중심에서 손(손잡이)까지의 거리(유닛)")]
    [SerializeField] private float holdRadius = 0.4f;
    [Tooltip("이 각도(바라보는 쪽 기준)보다 위를 조준하면 무기를 몸 뒤에 그린다")]
    [SerializeField] private float behindAngle = 35f;
    [SerializeField] private int behindOrderOffset = -40;

    private SpriteRenderer[] weaponRenderers;
    private int[] weaponBaseOrders;
    private bool weaponBehind;

    private bool aimEnabled;
    private float aimAngle;
    private float targetAimAngle;
    private bool smoothAim;
    private float weaponForward;
    private WeaponMotion motion;
    private float motionTimer;
    private float motionDuration;
    private float swingSign = 1f;

    private readonly int[] stateHashes = new int[11];
    private readonly float[] clipLengths =
    {
        1.8333f, 0.6667f, 0.3333f, 0.5833f, 1.8333f, 1.8333f, 1.65f, 1.1667f, 2f, 1f, 1.3333f
    };

    private bool initialized;
    private bool hasLoop;
    private CharacterAnim loopAnim;
    private bool actionPlaying;
    private float holdTimer;
    private bool faceLeft;

    public PartsManager PartsManager => partsManager;

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
            PlayLoop(CharacterAnim.Idle);

            // 오른손 아래 무기 파츠는 모두 미리 있고 켜고 끄기만 하므로 한 번만 모아 둔다.
            weaponRenderers = handRight.GetComponentsInChildren<SpriteRenderer>(true);
            weaponBaseOrders = new int[weaponRenderers.Length];
            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                weaponBaseOrders[i] = weaponRenderers[i].sortingOrder;
            }
        }
    }

    // 반복 애니메이션(대기, 달리기). 이미 같은 것이 재생 중이면 다시 시작하지 않는다.
    public void PlayLoop(CharacterAnim anim)
    {
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
        if (withShield)
        {
            partsManager.EquipParts(PartsType.Shield, shieldIndex);
        }
        else
        {
            partsManager.UnequipParts(PartsType.Shield);
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
    private static float GetWeaponForward(PartsType type)
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
            forward = -4.3f;
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
            handRight.localRotation = Quaternion.identity;
            SetWeaponBehind(false);
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
    public void PlayWeaponMotion(WeaponMotion newMotion, float duration)
    {
        motion = newMotion;
        motionDuration = Mathf.Max(0.05f, duration);
        motionTimer = motionDuration;
        if (newMotion == WeaponMotion.Swing)
        {
            // 한 번은 위에서 아래로, 다음은 아래에서 위로 벤다.
            swingSign = -swingSign;
        }
    }

    private void LateUpdate()
    {
        if (aimEnabled)
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
                }
                else if (motion == WeaponMotion.Thrust)
                {
                    reach = Mathf.Sin(progress * Mathf.PI) * thrustDistance;
                }
                else
                {
                    angleOffset = recoilDegrees * (1f - progress) * (1f - progress);
                }
            }

            float parentAngle = handRight.parent.localEulerAngles.z;
            float handAngle = aimAngle + angleOffset;
            handRight.localRotation = Quaternion.Euler(0f, 0f, handAngle - weaponForward - parentAngle);

            // 손 위치: 애니메이션이 정한 자리 대신 몸 중심을 도는 원 위(휘두르기도 이 원을 따라 돈다).
            float radians = handAngle * Mathf.Deg2Rad;
            float side = 1f;
            if (faceLeft)
            {
                side = -1f;
            }
            float distance = holdRadius + reach;
            Vector3 center = transform.position + new Vector3(0f, holdHeight, 0f);
            handRight.position = center + new Vector3(Mathf.Cos(radians) * distance * side, Mathf.Sin(radians) * distance, 0f);

            SetWeaponBehind(handAngle > behindAngle);
        }
    }

    // 위쪽을 조준하면 무기를 몸 뒤에 그린다(같은 SortingGroup 안의 순서만 바꾼다).
    private void SetWeaponBehind(bool behind)
    {
        if (behind != weaponBehind && weaponRenderers != null)
        {
            weaponBehind = behind;
            int offset = 0;
            if (behind)
            {
                offset = behindOrderOffset;
            }
            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                weaponRenderers[i].sortingOrder = weaponBaseOrders[i] + offset;
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
            offset = Mathf.Lerp(half, -half, eased);
        }
        else
        {
            float back = (progress - swingCutRatio) / (1f - swingCutRatio);
            offset = Mathf.Lerp(-half, 0f, back * back * (3f - 2f * back));
        }
        return offset;
    }

    public void SetFacing(bool left)
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
}
