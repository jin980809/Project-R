using Mirror;
using UnityEngine;

// 키 큰 건물이나 나무 뒤로 내 캐릭터가 들어가면 그 그림을 반투명하게 해서 캐릭터가 보이게 한다.
// 발밑(피벗)보다 위쪽, 그림이 덮는 사각형 안에 내 캐릭터가 있으면 "뒤에 있다"고 본다.
public class OcclusionFade : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] renderers;
    [Tooltip("피벗 기준으로 캐릭터가 이 사각형 안이면 가려진 것으로 본다(유닛)")]
    [SerializeField] private Vector2 areaMin = new Vector2(-1f, 0.05f);
    [SerializeField] private Vector2 areaMax = new Vector2(1f, 3f);
    [SerializeField] private float fadedAlpha = 0.45f;
    [SerializeField] private float fadeSpeed = 4f;

    private float alpha = 1f;

    private void LateUpdate()
    {
        bool behind = false;
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local != null)
        {
            Vector2 offset = local.transform.position - transform.position;
            behind = offset.x > areaMin.x && offset.x < areaMax.x && offset.y > areaMin.y && offset.y < areaMax.y;
        }
        float target = 1f;
        if (behind)
        {
            target = fadedAlpha;
        }
        if (alpha != target)
        {
            alpha = Mathf.MoveTowards(alpha, target, fadeSpeed * Time.deltaTime);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }
        }
    }
}
