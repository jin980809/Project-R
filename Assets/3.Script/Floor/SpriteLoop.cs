using UnityEngine;

// 프레임을 계속 돌리는 단순 애니메이션(횃불 등). 개체마다 시작 위치를 달리해 똑같이 움직이지 않게 한다.
public class SpriteLoop : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 8f;
    [SerializeField] private float timeOffset;

    private int shownFrame = -1;

    private void Update()
    {
        int frame = Mathf.FloorToInt((Time.time + timeOffset) * framesPerSecond) % frames.Length;
        if (frame != shownFrame)
        {
            shownFrame = frame;
            spriteRenderer.sprite = frames[frame];
        }
    }
}
