using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미니맵. 가 본 방은 종류 색으로, 가 본 방과 문으로 이어진 방은 흐리게 보여 준다.
// 방 사이 길목(문으로 이어진 곳)은 칸 사이의 짧은 선으로 그린다.
// 가 본 방에는 방 종류 아이콘을 얹는다(끝낸 방은 아이콘을 어둡게).
// 지도 키(M)를 누르면 화면 가운데에 크게 펼치고, 가 본 특수방(상점, 대장간, 낚시터, 광맥, 마구간, 보물)을 누르면
// 그 방으로 순간이동한다. 전투 중(방이 잠겨 있을 때)에는 순간이동할 수 없다.
public class MinimapUi : MonoBehaviour
{
    [SerializeField] private GameHub hub;
    [SerializeField] private FloorDirector floor;
    [Tooltip("격자 칸. 아래 줄부터 왼쪽에서 오른쪽 순서(y * GridSize + x)")]
    [SerializeField] private Image[] cells;
    [Tooltip("칸마다 위에 얹는 방 종류 아이콘. cells와 같은 순서")]
    [SerializeField] private Image[] icons;
    [Tooltip("RoomType 순서의 방 종류 아이콘")]
    [SerializeField] private Sprite[] roomIcons;
    [Tooltip("가로 길목: (x,y)와 (x+1,y) 사이. 순서 y * (GridSize - 1) + x")]
    [SerializeField] private Image[] horizontalLinks;
    [Tooltip("세로 길목: (x,y)와 (x,y+1) 사이. 순서 y * GridSize + x")]
    [SerializeField] private Image[] verticalLinks;
    [SerializeField] private RectTransform currentMarker;
    [Tooltip("칸마다 위에 얹는 글자(안 가 본 방 ?, 끝낸 방 체크). cells와 같은 순서")]
    [SerializeField] private TMP_Text[] marks;
    [Tooltip("큰 지도일 때만 보이는 안내 글(지도 아래)")]
    [SerializeField] private TMP_Text hintLabel;
    [Tooltip("아직 안 가 본 방(종류를 숨긴다)")]
    [SerializeField] private Color unknownColor = new Color(0.32f, 0.32f, 0.36f, 0.95f);
    [SerializeField] private Color unknownMarkColor = new Color(0.85f, 0.85f, 0.9f);
    [SerializeField] private Color clearedMarkColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);
    [SerializeField] private string unknownMark = "?";
    [SerializeField] private string clearedMark = "✓";
    [Tooltip("끝낸 방(지금 방 제외)의 아이콘 색. 아이콘이 없는 방은 체크 글자로 표시한다")]
    [SerializeField] private Color clearedIconColor = new Color(0.55f, 0.55f, 0.58f, 0.9f);
    [Tooltip("방 종류 색에 곱해 칸을 어둡게 한다(아이콘이 잘 보이게)")]
    [SerializeField] private float cellShade = 0.6f;
    [Tooltip("RoomType 순서(시작, 전투, 엘리트, 보물, 상점, 대장간, 보스, 낚시터, 광맥, 마구간)")]
    [SerializeField] private Color[] typeColors =
    {
        new Color(0.8f, 0.8f, 0.8f), new Color(0.55f, 0.6f, 0.65f), new Color(0.9f, 0.45f, 0.3f), new Color(1f, 0.85f, 0.3f),
        new Color(0.35f, 0.75f, 1f), new Color(1f, 0.6f, 0.25f), new Color(0.75f, 0.35f, 0.9f),
        new Color(0.3f, 0.9f, 0.9f), new Color(0.7f, 0.7f, 0.95f), new Color(0.85f, 0.7f, 0.45f)
    };
    [SerializeField] private Color linkColor = new Color(0.92f, 0.88f, 0.78f);
    [SerializeField] private float unvisitedAlpha = 0.35f;

    [Header("큰 지도")]
    [SerializeField] private float bigScale = 3f;
    [Tooltip("큰 지도일 때 바탕 색(뒤의 방이 덜 비치게 진하게)")]
    [SerializeField] private Color bigBackColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("순간이동할 수 있는 방에 마우스를 올렸을 때 칸 색")]
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color hintColor = new Color(1f, 0.92f, 0.6f);
    [SerializeField] private Color hintBlockedColor = new Color(1f, 0.5f, 0.45f);

    private int lastRevision = -1;
    // 격자 칸마다 방 번호(-1이면 없음)와 보이는지
    private readonly int[] roomAt = new int[FloorDirector.GridSize * FloorDirector.GridSize];
    private readonly bool[] known = new bool[FloorDirector.GridSize * FloorDirector.GridSize];
    private RectTransform panel;
    private Image panelImage;
    private Vector2 smallAnchor;
    private Vector2 smallPivot;
    private Vector2 smallPosition;
    private Color smallBackColor;
    private bool big;
    // 마우스를 올린 칸(-1이면 없음)과 그때의 안내 상태(바뀔 때만 글을 다시 쓴다)
    private int hoveredCell = -1;
    private int hintState = -1;
    private bool lastLocked;

    public bool IsBig => big;

    private void Awake()
    {
        panel = (RectTransform)transform;
        panelImage = GetComponent<Image>();
        smallAnchor = panel.anchorMin;
        smallPivot = panel.pivot;
        smallPosition = panel.anchoredPosition;
        smallBackColor = panelImage.color;
        hintLabel.enabled = false;
    }

    public void ToggleBig()
    {
        SetBig(!big);
    }

    public void SetBig(bool value)
    {
        big = value;
        if (big)
        {
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, 50f);
            panel.localScale = new Vector3(bigScale, bigScale, 1f);
            panelImage.color = bigBackColor;
            transform.SetAsLastSibling();
        }
        else
        {
            panel.anchorMin = smallAnchor;
            panel.anchorMax = smallAnchor;
            panel.pivot = smallPivot;
            panel.anchoredPosition = smallPosition;
            panel.localScale = Vector3.one;
            panelImage.color = smallBackColor;
        }
        // 큰 지도일 때는 바탕이 마우스를 받아 지도를 누를 때 공격이 나가지 않게 한다.
        panelImage.raycastTarget = big;
        hintLabel.enabled = big;
        hoveredCell = -1;
        hintState = -1;
        lastRevision = -1;
    }

    private void Update()
    {
        if (floor.Revision != lastRevision && floor.CurrentRoom >= 0 && floor.CurrentRoom < floor.RoomCount)
        {
            lastRevision = floor.Revision;
            Refresh();
        }
        if (big)
        {
            UpdateBigMap();
        }
    }

    // 큰 지도: 마우스를 올린 칸을 찾아 안내를 바꾸고, 누르면 순간이동을 부탁한다.
    private void UpdateBigMap()
    {
        GameInput input = hub.LocalInput;
        if (input != null && floor.CurrentRoom >= 0)
        {
            Vector2 pointer = input.PointerPosition;
            int found = -1;
            for (int i = 0; i < cells.Length && found < 0; i++)
            {
                if (cells[i].enabled && roomAt[i] >= 0 && RectTransformUtility.RectangleContainsScreenPoint(cells[i].rectTransform, pointer, null))
                {
                    found = i;
                }
            }
            bool locked = floor.RoomLocked;
            if (found != hoveredCell || locked != lastLocked)
            {
                if (hoveredCell >= 0)
                {
                    RestoreCellColor(hoveredCell);
                }
                hoveredCell = found;
                lastLocked = locked;
                if (GetHintState(found, locked) == 2)
                {
                    cells[hoveredCell].color = hoverColor;
                }
            }

            int state = GetHintState(found, locked);
            if (state != hintState)
            {
                hintState = state;
                ShowHint(state, found);
            }

            if (state == 2 && input.AttackPressed && !hub.InputBlocked && NetworkClient.localPlayer != null)
            {
                PlayerFacility facility;
                if (NetworkClient.localPlayer.TryGetComponent(out facility))
                {
                    facility.RequestTeleport(roomAt[found]);
                    SetBig(false);
                }
            }
        }
    }

    // 0: 아무 칸도 아님, 1: 순간이동할 수 없는 칸, 2: 순간이동할 수 있는 칸, 3: 갈 수 있는 방이지만 전투 중
    private int GetHintState(int cell, bool locked)
    {
        int state = 0;
        if (cell >= 0)
        {
            int room = roomAt[cell];
            state = 1;
            if (room != floor.CurrentRoom && floor.IsTeleportRoom(room))
            {
                if (locked)
                {
                    state = 3;
                }
                else
                {
                    state = 2;
                }
            }
        }
        return state;
    }

    private void ShowHint(int state, int cell)
    {
        if (state == 2)
        {
            hintLabel.text = "클릭: " + floor.GetRoomName(floor.GetRoom(roomAt[cell]).type) + "(으)로 순간이동";
            hintLabel.color = hintColor;
        }
        else if (state == 3)
        {
            hintLabel.text = "전투 중에는 순간이동할 수 없습니다";
            hintLabel.color = hintBlockedColor;
        }
        else
        {
            hintLabel.text = "가 본 특수방을 누르면 순간이동합니다   (" + hub.GetKeyName(GameInput.KeyMap, "M") + " 닫기)";
            hintLabel.color = Color.white;
        }
    }

    private void RestoreCellColor(int cellIndex)
    {
        int room = roomAt[cellIndex];
        if (room >= 0 && known[cellIndex])
        {
            RoomInfo info = floor.GetRoom(room);
            if (info.visited == 0)
            {
                cells[cellIndex].color = unknownColor;
            }
            else
            {
                Color color = typeColors[(int)info.type] * cellShade;
                color.a = 1f;
                cells[cellIndex].color = color;
            }
        }
    }

    private void Refresh()
    {
        hoveredCell = -1;
        hintState = -1;
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].enabled = false;
            marks[i].enabled = false;
            icons[i].enabled = false;
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
                TMP_Text mark = marks[cellIndex];
                if (room.visited == 0)
                {
                    mark.text = unknownMark;
                    mark.color = unknownMarkColor;
                    mark.enabled = true;
                }
                else
                {
                    bool cleared = room.cleared != 0 && i != floor.CurrentRoom;
                    Image icon = icons[cellIndex];
                    int typeIndex = (int)room.type;
                    if (roomIcons != null && typeIndex < roomIcons.Length && roomIcons[typeIndex] != null)
                    {
                        icon.sprite = roomIcons[typeIndex];
                        icon.color = Color.white;
                        if (cleared)
                        {
                            icon.color = clearedIconColor;
                        }
                        icon.enabled = true;
                    }
                    else if (cleared)
                    {
                        mark.text = clearedMark;
                        mark.color = clearedMarkColor;
                        mark.enabled = true;
                    }
                }
                cell.enabled = true;
                RestoreCellColor(cellIndex);
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
