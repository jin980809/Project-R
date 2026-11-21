using LayerLab.ArtMakerUnity;
using Mirror;
using TMPro;
using UnityEngine;

// 마을 주민. 플레이어와 같은 패키지 캐릭터(CharacterVisual)에 역할별 의상(직접 그린 상의·모자)과 손 소품을 입힌다.
// 정해 둔 지점을 차례로 걸어 다니거나(지점이 없으면) 제자리에서 대기 동작을 반복하고,
// 걸을 때는 물리(Rigidbody2D)로 움직여 벽·건물·소품을 뚫지 못한다. 막혀서 한동안 못 가면 다음 지점으로 넘어간다.
// 가끔, 또는 내 캐릭터가 가까이 오면 머리 위에 말풍선을 띄운다.
// 네트워크로 맞추지 않는 장식이라 각자 화면에서 따로 움직인다. 매 프레임 할당이 없다.
public class VillageNpc : MonoBehaviour
{
    // 손에 든 것을 모두 내려놓을 때 벗기는 부위
    private readonly PartsType[] handParts =
    {
        PartsType.Sword, PartsType.Axe, PartsType.Bow, PartsType.Shield, PartsType.Wand, PartsType.Staff, PartsType.Spear,
        PartsType.Blunt, PartsType.Crossbow, PartsType.Gun, PartsType.Fishing, PartsType.Pickaxe, PartsType.SubItem, PartsType.Arrow, PartsType.Mount
    };
    // 공격 클립 길이(초). CharacterVisual의 clipLengths와 같다.
    private const float WorkClipLength = 1.8333f;

    [Header("모습")]
    [SerializeField] private CharacterVisual visual;
    [Tooltip("PartsManager 상의 목록에 덧붙인 의상 번호")]
    [SerializeField] private int chestIndex;
    [Tooltip("모자 번호. 음수면 모자 없이 머리카락을 보인다.")]
    [SerializeField] private int helmetIndex = -1;
    [SerializeField] private bool hasItem;
    [SerializeField] private PartsType itemType = PartsType.SubItem;
    [SerializeField] private int itemIndex;
    [SerializeField] private int hair;
    [SerializeField] private int eye;
    [Tooltip("수염 번호. 음수면 수염 없음")]
    [SerializeField] private int beard = -1;
    [SerializeField] private int hairColor;
    [SerializeField] private int skinColor;
    [SerializeField] private bool startFacingLeft;
    [Tooltip("켜면 서 있는 동안 가끔 망치질(공격 동작)을 한다.")]
    [SerializeField] private bool work;
    [SerializeField] private Vector2 workIntervalRange = new Vector2(0.6f, 2.2f);

    [Header("걷기(지점이 없으면 제자리)")]
    [Tooltip("걷는 주민만 있다. 벽에 막히도록 속도로 움직인다.")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Vector2[] waypoints;
    [Tooltip("이 시간 동안 거의 못 움직이면 막힌 것으로 보고 다음 지점으로 간다.")]
    [SerializeField] private float stuckTime = 1.2f;
    [SerializeField] private float walkSpeed = 1.1f;
    [SerializeField] private Vector2 pauseRange = new Vector2(1.2f, 3.5f);

    [Header("말풍선")]
    [SerializeField] private string[] lines;
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private SpriteRenderer bubbleSprite;
    [SerializeField] private TextMeshPro bubbleText;
    [SerializeField] private Vector2 bubbleIntervalRange = new Vector2(7f, 16f);
    [SerializeField] private float bubbleDuration = 3.5f;
    [SerializeField] private float greetDistance = 2.2f;
    [SerializeField] private float greetCooldown = 12f;
    [Tooltip("말풍선 그림 크기 = 글자 크기 + 이 여백(유닛)")]
    [SerializeField] private Vector2 bubblePadding = new Vector2(0.5f, 0.62f);
    [Tooltip("말풍선 아래 꼬리 부분 높이(유닛). 글자는 그만큼 위에 둔다.")]
    [SerializeField] private float bubbleTailHeight = 0.2f;

    private int waypointIndex;
    private float pauseTimer;
    private bool walking;
    private Vector2 moveDirection;
    private float stuckTimer;
    private Vector2 stuckCheckPosition;
    private float workTimer;
    private float workPlaying;

    private float bubbleTimer;
    private float bubbleShowTimer;
    private float greetTimer;
    private int lastLine = -1;
    private Vector3 bubbleBase;

    private void Start()
    {
        visual.Init();
        DressUp();
        visual.SetFacing(startFacingLeft);
        visual.PlayLoop(CharacterAnim.Idle);

        pauseTimer = Random.Range(0f, pauseRange.y);
        workTimer = Random.Range(workIntervalRange.x, workIntervalRange.y);
        bubbleTimer = Random.Range(bubbleIntervalRange.x * 0.3f, bubbleIntervalRange.y);
        if (bubbleRoot != null)
        {
            bubbleBase = bubbleRoot.transform.localPosition;
            bubbleRoot.SetActive(false);
        }
        if (waypoints != null && waypoints.Length > 0)
        {
            transform.position = waypoints[0];
            if (body != null)
            {
                body.position = waypoints[0];
            }
            waypointIndex = 1 % waypoints.Length;
        }
    }

    // 외형(머리·눈·수염·색) → 손에 든 것 → 의상 순서로 입힌다. 머리카락을 먼저 정해야 모자용 머리카락이 맞춰진다.
    private void DressUp()
    {
        PartsManager parts = visual.PartsManager;
        CharacterLook look = new CharacterLook();
        look.valid = 1;
        look.hair = (byte)hair;
        look.eye = (byte)eye;
        look.beard = CharacterLook.NoBeard;
        if (beard >= 0)
        {
            look.beard = (byte)beard;
        }
        look.hairColor = (byte)hairColor;
        look.beardColor = (byte)hairColor;
        look.skinColor = (byte)skinColor;
        visual.ApplyLook(look);

        for (int i = 0; i < handParts.Length; i++)
        {
            parts.UnequipParts(handParts[i]);
        }
        if (hasItem)
        {
            parts.EquipParts(itemType, itemIndex);
        }

        parts.EquipParts(PartsType.Chest, chestIndex);
        if (helmetIndex >= 0)
        {
            parts.EquipParts(PartsType.Helmet, helmetIndex);
        }
        else
        {
            parts.UnequipParts(PartsType.Helmet);
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        UpdateMovement(deltaTime);
        UpdateWork(deltaTime);
        UpdateBubble(deltaTime);
    }

    // 다음 지점으로 갈 방향을 정한다(실제 이동은 FixedUpdate에서 속도로).
    private void UpdateMovement(float deltaTime)
    {
        bool wasWalking = walking;
        walking = false;
        if (body != null && waypoints != null && waypoints.Length > 1)
        {
            if (pauseTimer > 0f)
            {
                pauseTimer -= deltaTime;
                stuckTimer = 0f;
                stuckCheckPosition = body.position;
            }
            else
            {
                Vector2 position = body.position;
                Vector2 toTarget = waypoints[waypointIndex] - position;
                float arriveDistance = Mathf.Max(0.06f, walkSpeed * deltaTime);
                if (toTarget.sqrMagnitude <= arriveDistance * arriveDistance)
                {
                    NextWaypoint();
                }
                else
                {
                    walking = true;
                    moveDirection = toTarget.normalized;
                    if (Mathf.Abs(toTarget.x) > 0.02f)
                    {
                        visual.SetFacing(toTarget.x < 0f);
                    }
                    // 막힘 검사: 일정 시간마다 움직인 거리를 본다.
                    stuckTimer += deltaTime;
                    if (stuckTimer >= stuckTime)
                    {
                        float moved = (position - stuckCheckPosition).sqrMagnitude;
                        stuckTimer = 0f;
                        stuckCheckPosition = position;
                        if (moved < walkSpeed * stuckTime * 0.15f * walkSpeed * stuckTime * 0.15f)
                        {
                            NextWaypoint();
                            walking = false;
                        }
                    }
                }
            }
        }
        if (walking != wasWalking)
        {
            if (walking)
            {
                visual.PlayLoop(CharacterAnim.Walk);
            }
            else
            {
                visual.PlayLoop(CharacterAnim.Idle);
            }
        }
    }

    private void NextWaypoint()
    {
        waypointIndex = (waypointIndex + 1) % waypoints.Length;
        pauseTimer = Random.Range(pauseRange.x, pauseRange.y);
        stuckTimer = 0f;
    }

    private void FixedUpdate()
    {
        if (body != null)
        {
            Vector2 velocity = Vector2.zero;
            if (walking)
            {
                velocity = moveDirection * walkSpeed;
            }
            body.linearVelocity = velocity;
        }
    }

    // 대장장이: 공격 동작을 한 번 재생하고, 끝나면 대기 동작으로 돌아왔다가 잠시 뒤 다시 친다.
    private void UpdateWork(float deltaTime)
    {
        if (!work || walking)
        {
            return;
        }
        if (workPlaying > 0f)
        {
            workPlaying -= deltaTime;
            if (workPlaying <= 0f)
            {
                visual.PlayLoop(CharacterAnim.Idle);
                workTimer = Random.Range(workIntervalRange.x, workIntervalRange.y);
            }
            return;
        }
        workTimer -= deltaTime;
        if (workTimer <= 0f)
        {
            visual.PlayAction(CharacterAnim.Attack, 0f, 1f, false);
            workPlaying = WorkClipLength;
        }
    }

    private void UpdateBubble(float deltaTime)
    {
        if (bubbleRoot == null || lines == null || lines.Length == 0)
        {
            return;
        }
        if (greetTimer > 0f)
        {
            greetTimer -= deltaTime;
        }
        if (bubbleShowTimer > 0f)
        {
            bubbleShowTimer -= deltaTime;
            if (bubbleShowTimer <= 0f)
            {
                bubbleRoot.SetActive(false);
            }
            return;
        }

        bubbleTimer -= deltaTime;
        bool greet = false;
        if (greetTimer <= 0f && NetworkClient.localPlayer != null)
        {
            Vector2 offset = NetworkClient.localPlayer.transform.position - transform.position;
            greet = offset.sqrMagnitude <= greetDistance * greetDistance;
        }
        if (bubbleTimer <= 0f || greet)
        {
            if (greet)
            {
                greetTimer = greetCooldown;
            }
            bubbleTimer = Random.Range(bubbleIntervalRange.x, bubbleIntervalRange.y);
            ShowLine();
        }
    }

    // 직전과 다른 대사를 골라 말풍선 크기를 글자에 맞춘다(띄울 때만 계산한다).
    private void ShowLine()
    {
        int index = Random.Range(0, lines.Length);
        if (lines.Length > 1 && index == lastLine)
        {
            index = (index + 1) % lines.Length;
        }
        lastLine = index;
        bubbleText.text = lines[index];
        Vector2 textSize = bubbleText.GetPreferredValues(lines[index]);
        Vector2 size = textSize + bubblePadding;
        bubbleSprite.size = size;
        // 말풍선 아래쪽 끝을 머리 위 같은 높이에 맞춘다.
        bubbleRoot.transform.localPosition = bubbleBase + new Vector3(size.x * 0.5f - 0.3f, size.y * 0.5f, 0f);
        bubbleText.transform.localPosition = new Vector3(0f, bubbleTailHeight * 0.5f, 0f);
        bubbleShowTimer = bubbleDuration;
        bubbleRoot.SetActive(true);
    }
}
