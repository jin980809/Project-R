using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 방 시설(전직 제단, 상점, 대장간, 낚시터, 광맥, 마구간)의 그림과 애니메이션, 불빛.
// 방 종류마다 대기 반복 프레임, 사용할 때 한 번 재생하는 프레임, 다 쓴 뒤 그림(광맥)을 갖는다.
public class FacilityView : MonoBehaviour
{
    [System.Serializable]
    public class FacilityArt
    {
        public Sprite[] idle;
        public Sprite[] action;
        public Sprite[] empty;
        public float idleFps = 8f;
        public float actionFps = 14f;
        public Color lightColor = Color.white;
        public float lightIntensity;
        public float lightRadius = 3f;
        public Vector2 lightOffset;
        // 바닥에 까는 시설(낚시터 연못)은 캐릭터보다 뒤에 그린다.
        public bool onFloor;
        public float labelHeight = 2f;
        // 그림을 시설 위치(상호작용 기준점)에서 옮긴다(낚시터 연못은 잔교 끝이 기준점에 오도록 내린다).
        public Vector2 artOffset;
        // 지나갈 수 없는 자리(시설 기준점 로컬). x, y = 가운데, z, w = 가로·세로 크기. 최대 blockerColliders 개수만큼.
        public Vector4[] blockers;
    }

    [SerializeField] private FrameAnimator artAnimator;
    [Tooltip("시설 그림 전체(탈것 포함)를 한 덩어리로 Y 정렬한다")]
    [SerializeField] private SortingGroup sortingGroup;
    [Tooltip("RoomType 순서. 시설이 없는 방은 비워 둔다.")]
    [SerializeField] private FacilityArt[] arts;
    [SerializeField] private Transform label;
    [SerializeField] private Light2D facilityLight;
    [SerializeField] private LightFlicker lightFlicker;
    [Tooltip("시설 몸통 충돌(Wall 레이어). 종류마다 FacilityArt.blockers대로 켜고 맞춘다.")]
    [SerializeField] private BoxCollider2D[] blockerColliders;

    [Header("낚시 물결(낚시하는 사람 위치에 그린다)")]
    [SerializeField] private Sprite[] bobberFrames;
    [SerializeField] private Sprite[] biteFrames;
    [SerializeField] private Sprite[] splashFrames;

    [Header("마구간: 지금 고를 수 있는 탈것")]
    [SerializeField] private SpriteRenderer mountRenderer;
    [Tooltip("말, 멧돼지, 물범 순서")]
    [SerializeField] private Sprite[] mountSprites;
    [SerializeField] private Vector2 mountOffset = new Vector2(0.1f, 0.55f);

    private RoomType kind;
    private bool hasKind;
    private bool empty;
    private int shownMount = -1;

    public Sprite[] BobberFrames => bobberFrames;
    public Sprite[] BiteFrames => biteFrames;
    public Sprite[] SplashFrames => splashFrames;

    public void SetKind(RoomType type)
    {
        bool changed = !hasKind || type != kind;
        hasKind = true;
        kind = type;
        if (changed)
        {
            empty = false;
            ShowMount(-1);
        }
        FacilityArt art = GetArt();
        ApplyBlockers(art);
        if (art == null)
        {
            return;
        }
        sortingGroup.sortingOrder = 0;
        if (art.onFloor)
        {
            sortingGroup.sortingOrder = -850;
        }
        artAnimator.transform.localPosition = art.artOffset;
        if (label != null)
        {
            label.localPosition = new Vector3(0f, art.labelHeight, 0f);
        }
        Sprite[] loopFrames = art.idle;
        if (empty && art.empty != null && art.empty.Length > 0)
        {
            loopFrames = art.empty;
        }
        artAnimator.PlayLoop(loopFrames, art.idleFps);
        if (facilityLight != null)
        {
            facilityLight.enabled = art.lightIntensity > 0f;
            facilityLight.color = art.lightColor;
            facilityLight.intensity = art.lightIntensity;
            facilityLight.pointLightOuterRadius = art.lightRadius;
            facilityLight.transform.localPosition = art.lightOffset;
            if (lightFlicker != null)
            {
                lightFlicker.SetBase(art.lightIntensity, art.lightRadius);
            }
        }
    }

    // 사용(광석 캐기, 강화, 구매, 전직)할 때 한 번 재생한다.
    public void PlayAction()
    {
        FacilityArt art = GetArt();
        if (art != null && art.action != null && art.action.Length > 0 && !empty)
        {
            artAnimator.PlayOnce(art.action, art.actionFps);
        }
    }

    // 다 쓴 시설(광맥)은 빛을 잃은 그림으로 바꾼다. 본인 기준으로 정한다.
    public void SetEmpty(bool value)
    {
        FacilityArt art = GetArt();
        if (art == null || art.empty == null || art.empty.Length == 0 || value == empty)
        {
            return;
        }
        empty = value;
        if (empty)
        {
            artAnimator.PlayLoop(art.empty, art.idleFps);
        }
        else
        {
            artAnimator.PlayLoop(art.idle, art.idleFps);
        }
        if (facilityLight != null)
        {
            facilityLight.intensity = art.lightIntensity;
            if (empty)
            {
                facilityLight.intensity = art.lightIntensity * 0.25f;
            }
            if (lightFlicker != null)
            {
                lightFlicker.SetBase(facilityLight.intensity, art.lightRadius);
            }
        }
    }

    // 마구간 우리 안에 고를 수 있는 탈것을 세워 둔다(-1이면 비운다). index는 MountKind - 1.
    public void ShowMount(int index)
    {
        if (mountRenderer == null || index == shownMount)
        {
            return;
        }
        shownMount = index;
        bool show = index >= 0 && index < mountSprites.Length;
        mountRenderer.gameObject.SetActive(show);
        if (show)
        {
            mountRenderer.sprite = mountSprites[index];
        }
    }

    private void Update()
    {
        if (mountRenderer != null && mountRenderer.gameObject.activeSelf)
        {
            // 숨 쉬듯 위아래로 살짝 움직인다.
            float breath = Mathf.Sin(Time.time * 2.4f);
            mountRenderer.transform.localPosition = mountOffset + new Vector2(0f, breath * 0.02f);
            mountRenderer.transform.localScale = new Vector3(1f, 1f + breath * 0.025f, 1f);
        }
    }

    private void ApplyBlockers(FacilityArt art)
    {
        int count = 0;
        if (art != null && art.blockers != null)
        {
            count = Mathf.Min(art.blockers.Length, blockerColliders.Length);
        }
        for (int i = 0; i < blockerColliders.Length; i++)
        {
            BoxCollider2D box = blockerColliders[i];
            if (i < count)
            {
                Vector4 blocker = art.blockers[i];
                box.offset = new Vector2(blocker.x, blocker.y);
                box.size = new Vector2(blocker.z, blocker.w);
                box.enabled = true;
            }
            else
            {
                box.enabled = false;
            }
        }
    }

    private FacilityArt GetArt()
    {
        int index = (int)kind;
        if (!hasKind || arts == null || index >= arts.Length || arts[index] == null || arts[index].idle == null || arts[index].idle.Length == 0)
        {
            return null;
        }
        return arts[index];
    }
}
