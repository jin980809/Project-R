using UnityEngine;

// 방마다 장애물(돌기둥, 바위, 상자, 나무통)을 놓는다. 장애물은 Wall 레이어 충돌체라 사람·몬스터·투사체를 막는다.
// 배치는 층 번호와 방 번호로 정해지므로 서버와 모든 클라이언트가 따로 계산해도 같다(네트워크로 보내지 않음).
// 가운데(출발 위치, 상자, 계단, 시설)와 문에서 들어오는 길은 비워 둔다.
public class RoomDecor : MonoBehaviour
{
    public const int KindPillar = 0;
    public const int KindRock = 1;
    public const int KindCrate = 2;
    public const int KindBarrel = 3;
    public const int KindStatue = 4;
    public const int KindUrn = 5;
    public const int KindRuin = 6;
    // 화로: 불꽃 애니메이션과 불빛이 켜진다.
    public const int KindBrazier = 7;
    private const int KindCount = 8;

    [Tooltip("미리 만들어 둔 장애물. 배치에 쓰이지 않는 것은 꺼 둔다.")]
    [SerializeField] private SpriteRenderer[] slots;
    [SerializeField] private BoxCollider2D[] slotColliders;
    [Tooltip("종류 순서(돌기둥, 바위, 상자, 나무통, 석상, 항아리, 부서진 기둥, 화로)")]
    [SerializeField] private Sprite[] kindSprites;
    [Tooltip("종류별 발밑 충돌 크기")]
    [SerializeField] private Vector2[] kindColliderSizes =
    {
        new Vector2(0.75f, 0.4f), new Vector2(1.05f, 0.5f), new Vector2(0.95f, 0.45f), new Vector2(0.75f, 0.4f),
        new Vector2(0.9f, 0.45f), new Vector2(0.55f, 0.3f), new Vector2(0.8f, 0.4f), new Vector2(0.5f, 0.3f)
    };
    [Header("화로")]
    [SerializeField] private Sprite[] brazierFrames;
    [Tooltip("칸마다 화로 불빛(칸 순서)")]
    [SerializeField] private GameObject[] slotLights;
    [SerializeField] private FrameAnimator[] slotAnimators;

    // 배치 하나 = (x, y, 종류) 묶음. 종류 -1은 끝.
    private readonly float[][] combatLayouts =
    {
        // 기둥 네 개
        new float[] { -5f, 2.8f, KindPillar, 5f, 2.8f, KindPillar, -5f, -2.8f, KindPillar, 5f, -2.8f, KindPillar },
        // 바위 무더기
        new float[] { -6.2f, 3f, KindRock, 6f, -3.1f, KindRock, -3.6f, -3.3f, KindRock, 3.8f, 3.2f, KindRock, -7.2f, -2.4f, KindRock },
        // 창고
        new float[] { -6.6f, -3f, KindCrate, -5.6f, -3.5f, KindBarrel, -7.3f, -2.4f, KindBarrel, 6.6f, 3f, KindCrate, 5.6f, 3.4f, KindBarrel, 7.3f, 2.4f, KindBarrel, -3.8f, 3.3f, KindCrate, 3.8f, -3.3f, KindCrate },
        // 섞임
        new float[] { -4f, 3f, KindPillar, 4f, -3f, KindPillar, -6.5f, -3.2f, KindRock, 6.5f, 3.2f, KindRock, -3.2f, -3.5f, KindBarrel, 3.2f, 3.5f, KindBarrel },
        // 무너진 신전: 석상과 부서진 기둥, 화로
        new float[] { -5.5f, 3f, KindStatue, 5.5f, 3f, KindStatue, -3.8f, -3f, KindRuin, 3.8f, -3f, KindRuin, -7.6f, 0f, KindBrazier, 7.6f, 0f, KindBrazier, -6.4f, -3.6f, KindUrn, 6.4f, -3.6f, KindUrn },
        // 화로가 밝힌 홀
        new float[] { -4.6f, 2.6f, KindPillar, 4.6f, 2.6f, KindPillar, -4.6f, -2.6f, KindPillar, 4.6f, -2.6f, KindPillar, -2.6f, 2.8f, KindBrazier, 2.6f, -2.8f, KindBrazier, -7.8f, -3.6f, KindRuin, 7.8f, 3.6f, KindRock }
    };
    private readonly float[] bossLayout = { -6f, 3.2f, KindPillar, 6f, 3.2f, KindPillar, -6f, -3.2f, KindPillar, 6f, -3.2f, KindPillar, -7.8f, 0f, KindBrazier, 7.8f, 0f, KindBrazier, -3f, 4f, KindStatue, 3f, 4f, KindStatue };
    // 전투가 없는 방은 구석에만 소품을 둔다.
    private readonly float[] calmLayout = { -8.3f, 4.1f, KindBarrel, -7.4f, 4.2f, KindCrate, 8.3f, 4.1f, KindUrn, 8.3f, -4.2f, KindBarrel, -8.3f, -4.2f, KindBrazier, 7.4f, 4.2f, KindBrazier };

    private int appliedKey = int.MinValue;

    public int SlotCount => slots.Length;

    // 같은 방이면 다시 놓지 않는다. 놓은 직후 물리 위치를 맞춰 바로 겹침 검사에 쓸 수 있게 한다.
    public void Apply(int floorNumber, int roomIndex, RoomType type)
    {
        int key = floorNumber * 1000 + roomIndex;
        if (key != appliedKey)
        {
            appliedKey = key;
            float[] layout = PickLayout(floorNumber, roomIndex, type);
            int used = 0;
            for (int i = 0; i + 2 < layout.Length && used < slots.Length; i += 3)
            {
                int kind = (int)layout[i + 2];
                // 좌우를 뒤집어 같은 배치도 달라 보이게 한다.
                float x = layout[i];
                if (((floorNumber + roomIndex) & 1) == 1)
                {
                    x = -x;
                }
                Place(used, kind, new Vector2(x, layout[i + 1]));
                used++;
            }
            for (int i = used; i < slots.Length; i++)
            {
                slots[i].gameObject.SetActive(false);
            }
            Physics2D.SyncTransforms();
        }
    }

    private float[] PickLayout(int floorNumber, int roomIndex, RoomType type)
    {
        float[] layout = calmLayout;
        if (type == RoomType.Combat || type == RoomType.Elite)
        {
            int index = (floorNumber * 7 + roomIndex * 3) % combatLayouts.Length;
            layout = combatLayouts[index];
        }
        else if (type == RoomType.Boss)
        {
            layout = bossLayout;
        }
        return layout;
    }

    private void Place(int slot, int kind, Vector2 position)
    {
        if (kind >= 0 && kind < KindCount)
        {
            SpriteRenderer renderer = slots[slot];
            renderer.sprite = kindSprites[kind];
            bool brazier = kind == KindBrazier && brazierFrames != null && brazierFrames.Length > 0;
            if (slotAnimators != null && slot < slotAnimators.Length && slotAnimators[slot] != null)
            {
                FrameAnimator animator = slotAnimators[slot];
                animator.enabled = brazier;
                if (brazier)
                {
                    animator.PlayLoop(brazierFrames, 10f);
                }
                else
                {
                    animator.Stop();
                }
            }
            if (slotLights != null && slot < slotLights.Length && slotLights[slot] != null)
            {
                slotLights[slot].SetActive(brazier);
            }
            renderer.transform.position = position;
            BoxCollider2D box = slotColliders[slot];
            box.size = kindColliderSizes[kind];
            box.offset = new Vector2(0f, kindColliderSizes[kind].y * 0.3f);
            renderer.gameObject.SetActive(true);
        }
    }
}
