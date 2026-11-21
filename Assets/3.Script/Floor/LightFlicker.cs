using UnityEngine;
using UnityEngine.Rendering.Universal;

// 불빛(횃불, 화로, 등불)이 살짝 흔들리게 세기와 반경을 노이즈로 바꾼다.
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private Light2D target;
    [SerializeField] private float amount = 0.18f;
    [SerializeField] private float speed = 7f;

    private float baseIntensity;
    private float baseRadius;
    private float seed;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponent<Light2D>();
        }
        baseIntensity = target.intensity;
        baseRadius = target.pointLightOuterRadius;
        seed = Random.value * 100f;
    }

    // 시설이 불빛 세기를 바꿀 때 기준값도 함께 바꾼다.
    public void SetBase(float intensity, float radius)
    {
        baseIntensity = intensity;
        baseRadius = radius;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        target.intensity = baseIntensity * (1f + noise * 2f * amount);
        target.pointLightOuterRadius = baseRadius * (1f + noise * amount * 0.5f);
    }
}
