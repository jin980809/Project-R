using UnityEngine;
using UnityEngine.UI;

// 미니맵. 가 본 방은 종류 색으로, 가 본 방과 문으로 이어진 방은 흐리게 보여 준다.
public class MinimapUi : MonoBehaviour
{
    [SerializeField] private FloorDirector floor;
    [Tooltip("격자 칸. 아래 줄부터 왼쪽에서 오른쪽 순서(y * GridSize + x)")]
    [SerializeField] private Image[] cells;
    [SerializeField] private RectTransform currentMarker;
    [Tooltip("RoomType 순서(시작, 전투, 엘리트, 보물, 상점, 대장간, 보스)")]
    [SerializeField] private Color[] typeColors =
    {
        new Color(0.8f, 0.8f, 0.8f), new Color(0.55f, 0.6f, 0.65f), new Color(0.9f, 0.45f, 0.3f), new Color(1f, 0.85f, 0.3f),
        new Color(0.35f, 0.75f, 1f), new Color(1f, 0.6f, 0.25f), new Color(0.75f, 0.35f, 0.9f),
        new Color(0.3f, 0.9f, 0.9f), new Color(0.7f, 0.7f, 0.95f), new Color(0.85f, 0.7f, 0.45f)
    };
    [SerializeField] private float unvisitedAlpha = 0.35f;

    private int lastRevision = -1;

    private void Update()
    {
        if (floor.Revision != lastRevision && floor.CurrentRoom >= 0 && floor.CurrentRoom < floor.RoomCount)
        {
            lastRevision = floor.Revision;
            Refresh();
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].enabled = false;
        }

        int count = floor.RoomCount;
        for (int i = 0; i < count; i++)
        {
            RoomInfo room = floor.GetRoom(i);
            bool known = room.visited != 0;
            // 가 본 방과 문으로 이어져 있으면 흐리게 보인다.
            for (int direction = 0; direction < 4 && !known; direction++)
            {
                int neighbor = floor.FindNeighbor(i, direction);
                if (neighbor >= 0 && floor.GetRoom(neighbor).visited != 0)
                {
                    known = true;
                }
            }
            if (known)
            {
                Image cell = cells[room.y * FloorDirector.GridSize + room.x];
                Color color = typeColors[(int)room.type];
                if (room.visited == 0)
                {
                    color.a = unvisitedAlpha;
                }
                cell.color = color;
                cell.enabled = true;
            }
        }

        RoomInfo current = floor.GetRoom(floor.CurrentRoom);
        currentMarker.anchoredPosition = cells[current.y * FloorDirector.GridSize + current.x].rectTransform.anchoredPosition;
    }
}
