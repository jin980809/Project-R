using UnityEngine;

// 강화한 장비의 빛. 무기 그림 둘레에 단계 색 1픽셀 외곽선을 두르고,
// 무기(무기 강화)와 몸(투구·갑옷·방패 강화) 둘레에 작은 반짝이를 띄운다.
// 단계 색은 ItemBalance.GetUpgradeTierColor(+1·2 흰색, +3·4 파랑, +5 금색). 단계가 높을수록 반짝이가 잦다.
// CharacterVisual이 처음 강화 장비를 입을 때 붙인다. 조준·휘두르기가 끝난 뒤(CharacterVisual.LateUpdate 다음) 무기를 따라간다.
[DefaultExecutionOrder(100)]
public class UpgradeGlow : MonoBehaviour
{
    private const string ShaderName = "Tower/SpriteSilhouette";
    private const int OutlineCount = 4;
    private const int SparkleCount = 10;
    // 화면 1픽셀(PixelView가 1유닛을 32픽셀로 그린다)
    private const float PixelSize = 1f / 32f;
    private const float SparkleLife = 0.5f;
    private const int SparkleTextureSize = 5;

    // 단계(1~3)별 반짝이 간격. 몸 반짝이는 gearIntervalScale배 느리다.
    private readonly float[] sparkleIntervals = { 0f, 0.6f, 0.34f, 0.18f };
    private readonly float gearIntervalScale = 1.7f;
    // 몸 반짝이가 생기는 범위(캐릭터 발밑 기준)
    private readonly Vector2 bodyMin = new Vector2(-0.38f, 0.15f);
    private readonly Vector2 bodyMax = new Vector2(0.38f, 1.15f);
    private readonly Vector2[] outlineDirections = { new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f) };

    private readonly SpriteRenderer[] outlines = new SpriteRenderer[OutlineCount];
    private readonly SpriteRenderer[] sparkles = new SpriteRenderer[SparkleCount];
    private readonly float[] sparkleTimers = new float[SparkleCount];
    private Material material;
    private Texture2D sparkleTexture;
    private Sprite sparkleSprite;
    private SpriteRenderer weapon;
    private Transform outlineParent;
    private int weaponTier;
    private int gearTier;
    private Color weaponColor;
    private Color gearColor;
    private float weaponSpawnTimer;
    private float gearSpawnTimer;
    // 강화 평타 차지(0~1). 차지하는 동안은 강화 빛 대신 금빛 테두리가 점점 진해진다(반짝이는 띄우지 않는다).
    private float chargeAmount;
    private readonly Color chargeColor = new Color(1f, 0.9f, 0.5f, 1f);

    private void Awake()
    {
        material = new Material(Shader.Find(ShaderName));
        BuildSparkleSprite();
        for (int i = 0; i < OutlineCount; i++)
        {
            GameObject outlineObject = new GameObject("UpgradeOutline" + i);
            SpriteRenderer outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = material;
            outline.enabled = false;
            outlines[i] = outline;
        }
        for (int i = 0; i < SparkleCount; i++)
        {
            GameObject sparkleObject = new GameObject("UpgradeSparkle" + i);
            sparkleObject.transform.SetParent(transform, false);
            SpriteRenderer sparkle = sparkleObject.AddComponent<SpriteRenderer>();
            sparkle.sharedMaterial = material;
            sparkle.sprite = sparkleSprite;
            sparkle.enabled = false;
            sparkles[i] = sparkle;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < OutlineCount; i++)
        {
            if (outlines[i] != null)
            {
                Destroy(outlines[i].gameObject);
            }
        }
        Destroy(material);
        Destroy(sparkleSprite);
        Destroy(sparkleTexture);
    }

    // 5x5 십자 반짝이(가운데가 가장 밝다). 색은 SpriteRenderer.color로 입힌다.
    private void BuildSparkleSprite()
    {
        sparkleTexture = new Texture2D(SparkleTextureSize, SparkleTextureSize, TextureFormat.RGBA32, false);
        sparkleTexture.filterMode = FilterMode.Point;
        sparkleTexture.wrapMode = TextureWrapMode.Clamp;
        int center = SparkleTextureSize / 2;
        for (int y = 0; y < SparkleTextureSize; y++)
        {
            for (int x = 0; x < SparkleTextureSize; x++)
            {
                float alpha = 0f;
                if (x == center || y == center)
                {
                    alpha = 1f;
                }
                sparkleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        sparkleTexture.Apply();
        sparkleSprite = Sprite.Create(sparkleTexture, new Rect(0f, 0f, SparkleTextureSize, SparkleTextureSize), new Vector2(0.5f, 0.5f), 32f);
    }

    // 무기 그림과 강화 단계를 정한다. 레벨 0이면 그쪽 빛은 끈다.
    public void Set(SpriteRenderer weaponRenderer, int weaponLevel, Color weaponTierColor, int gearLevel, Color gearTierColor)
    {
        weapon = weaponRenderer;
        weaponTier = GetTier(weaponLevel);
        gearTier = GetTier(gearLevel);
        weaponColor = weaponTierColor;
        gearColor = gearTierColor;
        Transform parent = null;
        if (weapon != null)
        {
            parent = weapon.transform;
        }
        if (parent != outlineParent)
        {
            outlineParent = parent;
            for (int i = 0; i < OutlineCount; i++)
            {
                outlines[i].transform.SetParent(parent, false);
                outlines[i].enabled = false;
            }
        }
    }

    public void SetCharge(float amount)
    {
        chargeAmount = amount;
    }

    private int GetTier(int level)
    {
        int tier = 0;
        if (level >= 5)
        {
            tier = 3;
        }
        else if (level >= 3)
        {
            tier = 2;
        }
        else if (level >= 1)
        {
            tier = 1;
        }
        return tier;
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        bool weaponVisible = weapon != null && weapon.enabled && weapon.sprite != null && weapon.gameObject.activeInHierarchy && weapon.color.a > 0.05f;
        bool charging = chargeAmount > 0f;
        UpdateOutlines(weaponVisible && (weaponTier > 0 || charging));


        if (weaponVisible && weaponTier > 0)
        {
            weaponSpawnTimer -= deltaTime;
            if (weaponSpawnTimer <= 0f)
            {
                weaponSpawnTimer = sparkleIntervals[weaponTier] * Random.Range(0.7f, 1.3f);
                Bounds bounds = weapon.bounds;
                Vector3 position = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), 0f);
                Spawn(position, weaponColor);
            }
        }
        if (gearTier > 0)
        {
            gearSpawnTimer -= deltaTime;
            if (gearSpawnTimer <= 0f)
            {
                gearSpawnTimer = sparkleIntervals[gearTier] * gearIntervalScale * Random.Range(0.7f, 1.3f);
                Vector3 local = new Vector3(Random.Range(bodyMin.x, bodyMax.x), Random.Range(bodyMin.y, bodyMax.y), 0f);
                Spawn(transform.position + local, gearColor);
            }
        }
        UpdateSparkles(deltaTime);
    }

    // 무기 그림을 상하좌우 1픽셀씩 밀어 단계 색 실루엣 4장을 무기 바로 뒤에 둔다. 빛은 천천히 숨 쉬듯 밝아졌다 어두워진다.
    private void UpdateOutlines(bool visible)
    {
        Color color = weaponColor;
        if (visible)
        {
            color.a = 0.7f + 0.25f * Mathf.Sin(Time.time * 4f);
            if (chargeAmount > 0f)
            {
                // 차지: 금빛이 점점 진해지고, 다 모으면 빠르게 깜박인다.
                color = chargeColor;
                color.a = 0.25f + 0.6f * chargeAmount;
                if (chargeAmount >= 1f)
                {
                    color.a = 0.75f + 0.25f * Mathf.Sin(Time.time * 30f);
                }
            }
        }
        Vector3 scale = Vector3.one;
        if (visible)
        {
            scale = weapon.transform.lossyScale;
        }
        for (int i = 0; i < OutlineCount; i++)
        {
            SpriteRenderer outline = outlines[i];
            if (visible)
            {
                outline.sprite = weapon.sprite;
                outline.flipX = weapon.flipX;
                outline.flipY = weapon.flipY;
                outline.sortingLayerID = weapon.sortingLayerID;
                outline.sortingOrder = weapon.sortingOrder - 1;
                outline.color = color;
                Vector2 direction = outlineDirections[i];
                outline.transform.localPosition = new Vector3(direction.x * PixelSize / Mathf.Abs(scale.x), direction.y * PixelSize / Mathf.Abs(scale.y), 0f);
                if (!outline.enabled)
                {
                    outline.enabled = true;
                }
            }
            else if (outline.enabled)
            {
                outline.enabled = false;
            }
        }
    }

    private void Spawn(Vector3 position, Color color)
    {
        int slot = -1;
        for (int i = 0; i < SparkleCount && slot < 0; i++)
        {
            if (sparkleTimers[i] <= 0f)
            {
                slot = i;
            }
        }
        if (slot >= 0)
        {
            SpriteRenderer sparkle = sparkles[slot];
            sparkleTimers[slot] = SparkleLife;
            sparkle.transform.position = position;
            sparkle.color = color;
            int order = 100;
            if (weapon != null)
            {
                sparkle.sortingLayerID = weapon.sortingLayerID;
                order = weapon.sortingOrder + 10;
            }
            sparkle.sortingOrder = order;
            sparkle.transform.localScale = Vector3.zero;
            sparkle.enabled = true;
        }
    }

    // 반짝이는 커졌다가 작아지며 사라진다.
    private void UpdateSparkles(float deltaTime)
    {
        for (int i = 0; i < SparkleCount; i++)
        {
            if (sparkleTimers[i] > 0f)
            {
                sparkleTimers[i] -= deltaTime;
                if (sparkleTimers[i] <= 0f)
                {
                    sparkles[i].enabled = false;
                }
                else
                {
                    float progress = 1f - sparkleTimers[i] / SparkleLife;
                    float size = Mathf.Sin(progress * Mathf.PI);
                    sparkles[i].transform.localScale = new Vector3(size, size, 1f);
                }
            }
        }
    }
}
