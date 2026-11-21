using UnityEngine;

// 스프라이트 프레임 재생기(반복 재생, 한 번 재생 후 다른 반복으로 넘어가기). 시설·보물 상자·낚시 물결에 쓴다.
public class FrameAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Sprite[] loopFrames;
    private float loopFps = 8f;
    private Sprite[] onceFrames;
    private float onceFps = 12f;
    private float timer;
    private bool playingOnce;
    private int shownFrame = -1;
    private Sprite[] shownSet;

    public SpriteRenderer Renderer => spriteRenderer;
    public bool IsPlayingOnce => playingOnce;

    public void Init(SpriteRenderer target)
    {
        spriteRenderer = target;
    }

    // 반복 재생. 같은 묶음이면 처음부터 다시 시작하지 않는다.
    public void PlayLoop(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }
        if (frames != loopFrames)
        {
            loopFrames = frames;
            if (!playingOnce)
            {
                timer = Random.value * frames.Length / Mathf.Max(1f, fps);
            }
        }
        loopFps = fps;
        if (!playingOnce)
        {
            Show(loopFrames, timer, loopFps, true);
        }
    }

    // 한 번 재생한 뒤 지금 반복 묶음으로 돌아간다.
    public void PlayOnce(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }
        onceFrames = frames;
        onceFps = fps;
        playingOnce = true;
        timer = 0f;
        Show(onceFrames, 0f, onceFps, false);
    }

    public void Stop()
    {
        playingOnce = false;
        loopFrames = null;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (playingOnce)
        {
            if (timer * onceFps >= onceFrames.Length)
            {
                playingOnce = false;
                timer = 0f;
            }
            else
            {
                Show(onceFrames, timer, onceFps, false);
                return;
            }
        }
        if (loopFrames != null)
        {
            Show(loopFrames, timer, loopFps, true);
        }
    }

    private void Show(Sprite[] frames, float time, float fps, bool loop)
    {
        int frame = Mathf.FloorToInt(time * fps);
        if (loop)
        {
            frame %= frames.Length;
        }
        else
        {
            frame = Mathf.Min(frame, frames.Length - 1);
        }
        if (frame != shownFrame || frames != shownSet)
        {
            shownFrame = frame;
            shownSet = frames;
            spriteRenderer.sprite = frames[frame];
        }
    }
}
