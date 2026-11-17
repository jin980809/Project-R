using UnityEngine;
using UnityEngine.UI;

// 미니맵. 가 본 방은 종류 색으로, 가 본 방과 문으로 이어진 방은 흐리게 보여 준다.
// 방 사이 길목(문으로 이어진 곳)은 칸 사이의 짧은 선으로 그린다.
public class MinimapUi : MonoBehaviour
{
    [SerializeField] private FloorDirector floor;
    [Tooltip("격자 칸. 아래 줄부터 왼쪽에서 오른쪽 순서(y * GridSize + x)")]
    [SerializeField] private Image[] cells;
    [Tooltip("가로 길목: (x,y)와 (x+1,y) 사이. 순서 y * (GridSize - 1) + x")]
    [SerializeField] private Image[] horizontalLinks;
    [Tooltip("세로 길목: (x,y)와 (x,y+1) 사이. 순서 y * GridSize + x")]
    [SerializeField] private Image[] verticalLinks;
    [SerializeField] private RectTransform currentMarker;
    [Tooltip("RoomType 순서(시작, 전투, 엘리트, 보물, 상점, 대장간, 보스, 낚시터, 광맥, 마구간)")]
    [SerializeField] private Color[] typeColors =
    {
        new Color(0.8f, 0.8f, 0.8f), new Color(0.55f, 0.6f, 0.65f), new Color(0.9f, 0.45f, 0.3f), new Color(1f, 0.85f, 0.3f),
        new Color(0.35f, 0.75f, 1f), new Color(1f, 0.6f, 0.25f), new Color(0.75f, 0.35f, 0.9f),
        new Color(0.3f, 0.9f, 0.9f), new Color(0.7f, 0.7f, 0.95f), new Color(0.85f, 0.7f, 0.45f)
    };
    [SerializeField] private Color linkColor = new Color(0.92f, 0.88f, 0.78f);
    [SerializeField] private float unvisitedAlpha = 0.35f;

    private int lastRevision = -1;
    // 격자 칸마다 방 번호(-1이면 없음)와 보이는지
    private readonly int[] roomAt = new int[FloorDirector.GridSize * FloorDirector.GridSize];
    private readonly bool[] known = new bool[FloorDirector.GridSize * FloorDirector.GridSize];

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
            roomAt[i] = -1;
            known[i] = false;
        }
        for (int i = 0; i < horizontalLinks.Length; i++)
        {
            horizontalLinks[i].enabled = false;
        }
        for (int i = 0; i < verticalLinks.Length; i++)
        {
            verticalLinks[i].enabled = false;
        }

        int count = floor.RoomCount;
        for (int i = 0; i < count; i++)
        {
            RoomInfo room = floor.GetRoom(i);
            int cellIndex = room.y * FloorDirector.GridSize + room.x;
            roomAt[cellIndex] = i;
            bool isKnown = room.visited != 0;
            // 가 본 방과 문으로 이어져 있으면 흐리게 보인다.
            for (int direction = 0; direction < 4 && !isKnown; direction++)
            {
                int neighbor = floor.FindNeighbor(i, direction);
                if (neighbor >= 0 && floor.GetRoom(neighbor).visited != 0)
                {
                    isKnown = true;
                }
            }
            known[cellIndex] = isKnown;
            if (isKnown)
            {
                Image cell = cells[cellIndex];
                Color color = typeColors[(int)room.type];
                if (room.visited == 0)
                {
                    color.a = unvisitedAlpha;
                }
                cell.color = color;
                cell.enabled = true;
            }
        }

        // 길목: 북(0)과 동(1)만 보면 모든 연결을 한 번씩 센다. 양쪽 방이 다 보이면 그린다.
        for (int i = 0; i < count; i++)
        {
            RoomInfo room = floor.GetRoom(i);
            int cellIndex = room.y * FloorDirector.GridSize + room.x;
            if (known[cellIndex])
            {
                ShowLink(i, room, 0, cellIndex + FloorDirector.GridSize, verticalLinks, room.y * FloorDirector.GridSize + room.x);
                ShowLink(i, room, 1, cellIndex + 1, horizontalLinks, room.y * (FloorDirector.GridSize - 1) + room.x);
            }
        }

        RoomInfo current = floor.GetRoom(floor.CurrentRoom);
        currentMarker.anchoredPosition = cells[current.y * FloorDirector.GridSize + current.x].rectTransform.anchoredPosition;
    }

    private void ShowLink(int roomIndex, RoomInfo room, int direction, int neighborCell, Image[] links, int linkIndex)
    {
        int neighbor = floor.FindNeighbor(roomIndex, direction);
        if (neighbor >= 0 && known[neighborCell])
        {
            RoomInfo other = floor.GetRoom(neighbor);
            Color color = linkColor;
            // 둘 다 가 본 길이면 진하게, 한쪽만 가 봤으면 흐리게
            if (room.visited == 0 || other.visited == 0)
            {
                color.a = unvisitedAlpha + 0.15f;
            }
            Image link = links[linkIndex];
            link.color = color;
            link.enabled = true;
        }
    }
}
