using UnityEngine;

// 화면 전체를 배경과 같은 픽셀 격자(1유닛 = 32픽셀)로 보이게 한다.
// 월드 카메라(worldCamera)가 작은 렌더 텍스처에 장면을 그리고, 이 카메라(viewCamera)는 그 그림을 판(screenQuad)에 붙여 화면 크기로 보여 준 뒤
// 글자(WorldText 레이어)만 매끈하게 덧그린다. 월드 카메라는 픽셀 격자에 맞춰 서고 판도 같은 자리에 놓으므로, 화면은 부드럽게 움직이고 글자와 그림이 어긋나지 않는다.
// 카메라 이동·흔들림·확대(꾸미기 화면)는 지금처럼 viewCamera에 하면 된다.
[DefaultExecutionOrder(1000)]
public class PixelView : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private MeshRenderer screenQuad;
    [SerializeField] private int pixelsPerUnit = 32;
    [Tooltip("격자에 맞추느라 생기는 화면 가장자리 틈을 덮는 여유(픽셀, 한쪽)")]
    [SerializeField] private int borderPixels = 2;
    [Tooltip("판이 카메라 앞 얼마나 떨어진 곳에 놓이는지")]
    [SerializeField] private float quadDistance = 1f;
    [Header("공통 팔레트(판 머티리얼이 PixelPalette 셰이더일 때)")]
    [Tooltip("화면의 모든 색을 이 팔레트로 맞춘다(Endesga 32). 비우면 팔레트를 쓰지 않는다.")]
    [SerializeField] private Color[] palette;
    [Range(0f, 1f)]
    [SerializeField] private float paletteStrength = 1f;

    private RenderTexture target;
    private Material quadMaterial;
    private int targetWidth;
    private int targetHeight;

    private static readonly int PaletteLinearId = Shader.PropertyToID("_PaletteLinear");
    private static readonly int PaletteLabId = Shader.PropertyToID("_PaletteLab");
    private static readonly int PaletteCountId = Shader.PropertyToID("_PaletteCount");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");

    private void Awake()
    {
        quadMaterial = screenQuad.material;
        ApplyPalette();
    }

    // 팔레트 색을 선형 RGB와 Oklab으로 바꿔 셰이더에 넣는다(최대 64색).
    private void ApplyPalette()
    {
        if (palette == null || palette.Length == 0 || !quadMaterial.HasProperty(StrengthId))
        {
            return;
        }
        int count = Mathf.Min(64, palette.Length);
        Vector4[] linear = new Vector4[64];
        Vector4[] lab = new Vector4[64];
        for (int i = 0; i < count; i++)
        {
            Color c = palette[i].linear;
            linear[i] = new Vector4(c.r, c.g, c.b, 1f);
            lab[i] = LinearToOklab(c);
        }
        quadMaterial.SetVectorArray(PaletteLinearId, linear);
        quadMaterial.SetVectorArray(PaletteLabId, lab);
        quadMaterial.SetFloat(PaletteCountId, count);
        quadMaterial.SetFloat(StrengthId, paletteStrength);
    }

    private static Vector4 LinearToOklab(Color c)
    {
        float l = 0.4122214708f * c.r + 0.5363325363f * c.g + 0.0514459929f * c.b;
        float m = 0.2119034982f * c.r + 0.6806995451f * c.g + 0.1073969566f * c.b;
        float s = 0.0883024619f * c.r + 0.2817188376f * c.g + 0.6299787005f * c.b;
        l = Mathf.Pow(Mathf.Max(l, 0f), 1f / 3f);
        m = Mathf.Pow(Mathf.Max(m, 0f), 1f / 3f);
        s = Mathf.Pow(Mathf.Max(s, 0f), 1f / 3f);
        return new Vector4(
            0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
            1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
            0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s,
            0f);
    }

    private void LateUpdate()
    {
        float unitsPerPixel = 1f / pixelsPerUnit;
        float halfHeight = viewCamera.orthographicSize;
        float halfWidth = halfHeight * viewCamera.aspect;
        int width = 2 * (Mathf.CeilToInt(halfWidth * pixelsPerUnit) + borderPixels);
        int height = 2 * (Mathf.CeilToInt(halfHeight * pixelsPerUnit) + borderPixels);
        if (target == null || width != targetWidth || height != targetHeight)
        {
            Rebuild(width, height);
        }

        Vector3 viewPosition = viewCamera.transform.position;
        float snappedX = Mathf.Round(viewPosition.x * pixelsPerUnit) * unitsPerPixel;
        float snappedY = Mathf.Round(viewPosition.y * pixelsPerUnit) * unitsPerPixel;
        worldCamera.transform.position = new Vector3(snappedX, snappedY, viewPosition.z);
        worldCamera.orthographicSize = height * 0.5f * unitsPerPixel;

        Transform quad = screenQuad.transform;
        quad.position = new Vector3(snappedX, snappedY, viewPosition.z + quadDistance);
        quad.localScale = new Vector3(width * unitsPerPixel, height * unitsPerPixel, 1f);
    }

    private void Rebuild(int width, int height)
    {
        ReleaseTarget();
        target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        target.filterMode = FilterMode.Point;
        target.wrapMode = TextureWrapMode.Clamp;
        target.Create();
        worldCamera.targetTexture = target;
        quadMaterial.mainTexture = target;
        targetWidth = width;
        targetHeight = height;
    }

    private void ReleaseTarget()
    {
        if (target != null)
        {
            worldCamera.targetTexture = null;
            target.Release();
            Destroy(target);
            target = null;
        }
    }

    private void OnDestroy()
    {
        ReleaseTarget();
        if (quadMaterial != null)
        {
            Destroy(quadMaterial);
        }
    }
}
