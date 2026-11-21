using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 유물 고르기 화면. 보스·엘리트 방을 깨면 세 개 중 하나를 고른다(PlayerFacility가 연다).
// 씬에 미리 만들어 두지 않고 처음 열 때 HUD 캔버스 아래에 코드로 만든다.
public class RelicUi : MonoBehaviour
{
    private const int ChoiceCount = 3;

    private PlayerFacility facility;
    private GameObject root;
    private readonly TMP_Text[] nameLabels = new TMP_Text[ChoiceCount];
    private readonly TMP_Text[] descLabels = new TMP_Text[ChoiceCount];
    private readonly Image[] cardFrames = new Image[ChoiceCount];
    private TMP_Text ownedLabel;

    public bool IsOpen => root != null && root.activeSelf;

    // 캔버스 아래 RectTransform 오브젝트에 붙인 직후 한 번 부른다(PlayerFacility가 만든다).
    public void Setup(TMP_FontAsset font, PlayerFacility owner)
    {
        facility = owner;
        Build(font);
    }

    private void Build(TMP_FontAsset font)
    {
        RectTransform holderRect = (RectTransform)transform;
        Stretch(holderRect);

        root = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        Stretch((RectTransform)root.transform);
        root.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.72f);

        TMP_Text title = MakeText(root.transform, font, "유물 선택", 46f, new Color(1f, 0.86f, 0.45f), FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(800f, 70f));
        TMP_Text hint = MakeText(root.transform, font, "하나를 골라 이번 판 동안 효과를 얻습니다", 24f, new Color(0.85f, 0.85f, 0.9f), FontStyles.Normal);
        Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(900f, 40f));

        for (int i = 0; i < ChoiceCount; i++)
        {
            GameObject card = new GameObject("Card" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            card.transform.SetParent(root.transform, false);
            Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 330f, -10f), new Vector2(300f, 330f));
            Image frame = card.GetComponent<Image>();
            frame.color = new Color(0.16f, 0.13f, 0.22f, 0.96f);
            cardFrames[i] = frame;

            GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(card.transform, false);
            RectTransform innerRect = (RectTransform)inner.transform;
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(6f, 6f);
            innerRect.offsetMax = new Vector2(-6f, -6f);
            Image innerImage = inner.GetComponent<Image>();
            innerImage.color = new Color(0.09f, 0.08f, 0.13f, 1f);
            innerImage.raycastTarget = false;

            nameLabels[i] = MakeText(card.transform, font, "", 32f, Color.white, FontStyles.Bold);
            Place(nameLabels[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(270f, 80f));
            descLabels[i] = MakeText(card.transform, font, "", 25f, new Color(0.88f, 0.9f, 0.95f), FontStyles.Normal);
            Place(descLabels[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(260f, 160f));

            Button button = card.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            int choice = i;
            button.onClick.AddListener(() => facility.RequestPickRelic(choice));
        }

        ownedLabel = MakeText(root.transform, font, "", 22f, new Color(0.8f, 0.8f, 0.85f), FontStyles.Normal);
        Place(ownedLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1100f, 60f));
        root.SetActive(false);
    }

    public void Open(int[] choices, string owned)
    {
        for (int i = 0; i < ChoiceCount; i++)
        {
            int id = choices[i];
            nameLabels[i].text = facility.RelicName(id);
            nameLabels[i].color = facility.RelicColor(id);
            descLabels[i].text = facility.RelicDescription(id);
            cardFrames[i].color = facility.RelicColor(id) * 0.8f;
        }
        ownedLabel.text = owned;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
    }

    public void Close()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private TMP_Text MakeText(Transform parent, TMP_FontAsset font, string text, float size, Color color, FontStyles style)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }
}
