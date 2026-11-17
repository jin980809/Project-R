using UnityEngine;

// 로컬 플레이어를 따라가는 카메라. 맵 밖이 보이지 않게 제한하고 흔들림을 지원한다.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private Vector2 mapHalfSize = new Vector2(9.6f, 5.4f);
    [SerializeField] private Vector2 lookOffset = new Vector2(0f, 0.4f);

    private Transform target;
    private Vector2 velocity;
    private Vector2 basePosition;
    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeTimer;
    private bool shakeEnabled = true;

    private void Awake()
    {
        basePosition = transform.position;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            basePosition = ClampToMap((Vector2)target.position + lookOffset);
        }
    }

    public void SetShakeEnabled(bool value)
    {
        shakeEnabled = value;
    }

    public void Shake(float amplitude, float duration)
    {
        if (shakeEnabled && (amplitude >= shakeAmplitude || shakeTimer <= 0f))
        {
            shakeAmplitude = amplitude;
            shakeDuration = duration;
            shakeTimer = duration;
        }
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            Vector2 goal = ClampToMap((Vector2)target.position + lookOffset);
            basePosition = Vector2.SmoothDamp(basePosition, goal, ref velocity, smoothTime);
        }

        Vector2 offset = Vector2.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float strength = shakeAmplitude * Mathf.Clamp01(shakeTimer / shakeDuration);
            offset = Random.insideUnitCircle * strength;
        }

        transform.position = new Vector3(basePosition.x + offset.x, basePosition.y + offset.y, transform.position.z);
    }

    private Vector2 ClampToMap(Vector2 point)
    {
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;
        float limitX = Mathf.Max(0f, mapHalfSize.x - halfWidth);
        float limitY = Mathf.Max(0f, mapHalfSize.y - halfHeight);
        point.x = Mathf.Clamp(point.x, -limitX, limitX);
        point.y = Mathf.Clamp(point.y, -limitY, limitY);
        return point;
    }
}
