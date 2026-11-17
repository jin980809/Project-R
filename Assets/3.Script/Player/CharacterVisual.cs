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

// 패키지 캐릭터(PartsManager + Animator)를 게임에서 쓰기 위한 래퍼.
// 패키지 Animator는 전이가 없고 모든 클립이 반복이라, 재생 시작 지점·속도·정지를 여기서 직접 제어한다.
public class CharacterVisual : MonoBehaviour
{
    [SerializeField] private PartsManager partsManager;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform flipRoot;

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
        if (withShield)
        {
            partsManager.EquipParts(PartsType.Shield, shieldIndex);
        }
        else
        {
            partsManager.UnequipParts(PartsType.Shield);
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
