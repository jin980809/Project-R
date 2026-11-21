using UnityEngine;

// 씬마다 캐릭터를 어떻게 그릴지 정한다. 전투 씬은 캐릭터도 조명을 받는 머티리얼로 바꿔 횃불·광원에 함께 밝아지고 어두워지게 하고,
// 발밑 그림자도 맵 소품과 같은 픽셀 그림자로 바꾼다(맵과 따로 놀지 않게). 이 컴포넌트가 없는 씬(로비·마을)은 원래대로 그린다.
public class SceneLook : MonoBehaviour
{
    [Tooltip("캐릭터 파츠에 쓸 머티리얼(Sprite-Lit-Default). 비우면 바꾸지 않는다.")]
    [SerializeField] private Material characterMaterial;
    [Tooltip("캐릭터 발밑 그림자 그림. 비우면 바꾸지 않는다.")]
    [SerializeField] private Sprite shadowSprite;

    public static SceneLook Current { get; private set; }

    private void Awake()
    {
        Current = this;
    }

    private void Start()
    {
        CharacterVisual[] visuals = FindObjectsByType<CharacterVisual>(FindObjectsSortMode.None);
        for (int i = 0; i < visuals.Length; i++)
        {
            Apply(visuals[i]);
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public void Apply(CharacterVisual visual)
    {
        visual.ApplySceneLook(characterMaterial, shadowSprite);
    }
}
