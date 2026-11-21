using UnityEngine;
using UnityEngine.Rendering.Universal;

// 방 가장자리의 문. 잠기면 창살(북쪽)이나 문짝(동·서·남)이 내려오고/닫히고, 열리면 반대로 움직인다.
// frames는 닫힘(0)부터 열림(끝)까지 순서다. 문 너머가 엘리트 방이면 보라 룬과 쇠사슬, 보스 방이면 뿔 해골과 붉은 빛이 도는 문으로 바뀐다.
public class DoorView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private Sprite closedSprite;
    [Tooltip("닫힘 -> 열림 순서의 전환 프레임")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 14f;
    [Header("문 너머 방 종류별 그림(엘리트, 보스)")]
    [SerializeField] private Sprite[] eliteFrames;
    [SerializeField] private Sprite[] bossFrames;
    [Tooltip("엘리트·보스 문에서 새어 나오는 빛")]
    [SerializeField] private Light2D styleLight;
    [SerializeField] private Color eliteLightColor = new Color(0.7f, 0.35f, 1f);
    [SerializeField] private Color bossLightColor = new Color(1f, 0.2f, 0.15f);

    public const int StyleNormal = 0;
    public const int StyleElite = 1;
    public const int StyleBoss = 2;

    private Sprite[] normalFrames;
    private int style = -1;

    private float openness = 1f;
    private float target = 1f;
    private bool initialized;

    public void Hide()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void Show(bool locked)
    {
        Show(locked, StyleNormal);
    }

    public void Show(bool locked, int doorStyle)
    {
        SetStyle(doorStyle);
        bool wasHidden = !gameObject.activeSelf;
        if (wasHidden)
        {
            gameObject.SetActive(true);
        }
        target = 1f;
        if (locked)
        {
            target = 0f;
        }
        // 방에 처음 들어왔거나 숨어 있던 문은 움직이지 않고 바로 그 상태로 보인다.
        if (!initialized || wasHidden)
        {
            initialized = true;
            openness = target;
        }
        Refresh();
    }

    private void SetStyle(int doorStyle)
    {
        if (normalFrames == null)
        {
            normalFrames = frames;
        }
        if (doorStyle == style)
        {
            return;
        }
        style = doorStyle;
        Sprite[] chosen = normalFrames;
        if (doorStyle == StyleElite && eliteFrames != null && eliteFrames.Length > 1)
        {
            chosen = eliteFrames;
        }
        else if (doorStyle == StyleBoss && bossFrames != null && bossFrames.Length > 1)
        {
            chosen = bossFrames;
        }
        frames = chosen;
        if (styleLight != null)
        {
            styleLight.gameObject.SetActive(doorStyle != StyleNormal);
            if (doorStyle == StyleElite)
            {
                styleLight.color = eliteLightColor;
            }
            else if (doorStyle == StyleBoss)
            {
                styleLight.color = bossLightColor;
            }
        }
        Refresh();
    }

    // 방을 옮길 때 문 상태를 바로 맞춘다(전환 연출 없이).
    public void Snap()
    {
        openness = target;
        Refresh();
    }

    private void Update()
    {
        if (!Mathf.Approximately(openness, target))
        {
            float step = framesPerSecond / Mathf.Max(1, FrameCount - 1) * Time.deltaTime;
            openness = Mathf.MoveTowards(openness, target, step);
            Refresh();
        }
    }

    private int FrameCount
    {
        get
        {
            int count = 2;
            if (frames != null && frames.Length > 1)
            {
                count = frames.Length;
            }
            return count;
        }
    }

    private void Refresh()
    {
        if (frames != null && frames.Length > 1)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(openness * (frames.Length - 1)), 0, frames.Length - 1);
            doorRenderer.sprite = frames[index];
        }
        else
        {
            if (openness >= 0.5f)
            {
                doorRenderer.sprite = openSprite;
            }
            else
            {
                doorRenderer.sprite = closedSprite;
            }
        }
    }
}
