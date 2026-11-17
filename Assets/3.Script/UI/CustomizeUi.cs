using System.Text;
using LayerLab.ArtMakerUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 꾸미기 화면. 처음 실행할 때(취소 불가)와 로비 미용실에서 연다.
// 고르는 동안은 내 화면의 캐릭터에만 보이고, 완료하면 기기에 저장하고 모두에게 보낸다.
public class CustomizeUi : MonoBehaviour
{
    private const int RowHair = 0;
    private const int RowHairColor = 1;
    private const int RowEye = 2;
    private const int RowBeard = 3;
    private const int RowBeardColor = 4;
    private const int RowSkin = 5;
    private const int RowSkinColor = 6;
    public const int RowCount = 7;

    [SerializeField] private GameHub hub;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text[] rowValues;
    [Tooltip("색 줄에서 숫자 대신 보여 주는 색 견본")]
    [SerializeField] private Image[] rowSwatches;
    [SerializeField] private Button[] rowPrevButtons;
    [SerializeField] private Button[] rowNextButtons;
    [SerializeField] private Button randomButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button doneButton;
    [SerializeField] private TMP_Text hintLabel;
    [Tooltip("꾸미는 동안 카메라를 캐릭터에 가까이 댄다(정사영 크기)")]
    [SerializeField] private float previewZoom = 2.6f;

    private readonly StringBuilder builder = new StringBuilder(16);
    private PlayerController player;
    private CharacterLook editing;
    private CharacterLook original;
    private bool canCancel;
    private float savedZoom;

    public bool IsOpen => panel.activeSelf;
    public bool CanCancel => canCancel;

    private void Awake()
    {
        for (int i = 0; i < RowCount; i++)
        {
            int row = i;
            rowPrevButtons[i].onClick.AddListener(() => Step(row, -1));
            rowNextButtons[i].onClick.AddListener(() => Step(row, 1));
        }
        randomButton.onClick.AddListener(Randomize);
        cancelButton.onClick.AddListener(Cancel);
        doneButton.onClick.AddListener(Done);
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < RowCount; i++)
        {
            rowPrevButtons[i].onClick.RemoveAllListeners();
            rowNextButtons[i].onClick.RemoveAllListeners();
        }
        randomButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();
        doneButton.onClick.RemoveAllListeners();
        if (hub != null)
        {
            hub.SetCustomizeBlocked(false);
        }
    }

    // firstTime이면 취소할 수 없고, 무작위 외형에서 시작한다.
    public void Open(PlayerController owner, bool firstTime)
    {
        player = owner;
        canCancel = !firstTime;
        original = owner.CurrentLook;
        if (firstTime || original.valid == 0)
        {
            editing = CreateRandom();
        }
        else
        {
            editing = original;
        }
        cancelButton.gameObject.SetActive(canCancel);
        if (firstTime)
        {
            hintLabel.text = "모험을 떠나기 전에 모습을 정해 주세요.\n나중에 마을 미용실에서 바꿀 수 있습니다.";
        }
        else
        {
            hintLabel.text = "바꾼 모습은 함께하는 동료에게도 보입니다.";
        }
        panel.SetActive(true);
        hub.SetCustomizeBlocked(true);
        Camera camera = hub.MainCamera;
        savedZoom = camera.orthographicSize;
        camera.orthographicSize = previewZoom;
        hub.Sound.Play(SoundId.UiOpen);
        Apply();
    }

    public void Cancel()
    {
        if (canCancel)
        {
            player.PreviewLook(original);
            Hide();
        }
    }

    private void Done()
    {
        hub.Meta.SetLook(editing);
        hub.Meta.Save();
        player.SubmitLook(hub.Meta.Look);
        Hide();
    }

    private void Hide()
    {
        panel.SetActive(false);
        hub.SetCustomizeBlocked(false);
        hub.MainCamera.orthographicSize = savedZoom;
        hub.Sound.Play(SoundId.UiClose);
    }

    private CharacterLook CreateRandom()
    {
        CharacterVisual visual = player.Visual;
        CharacterLook look = new CharacterLook();
        look.hair = (byte)Random.Range(0, visual.GetPartsCount(PartsType.Hair));
        look.eye = (byte)Random.Range(0, visual.GetPartsCount(PartsType.Eye));
        look.beard = CharacterLook.NoBeard;
        if (Random.value < 0.4f)
        {
            look.beard = (byte)Random.Range(0, visual.GetPartsCount(PartsType.Beard));
        }
        look.skin = (byte)Random.Range(0, visual.GetPartsCount(PartsType.Skin));
        look.hairColor = (byte)Random.Range(0, visual.HairColorCount);
        look.beardColor = look.hairColor;
        look.skinColor = (byte)Random.Range(0, visual.SkinColorCount);
        look.valid = 1;
        return look;
    }

    private void Randomize()
    {
        editing = CreateRandom();
        Apply();
    }

    private void Step(int row, int delta)
    {
        CharacterVisual visual = player.Visual;
        if (row == RowHair)
        {
            editing.hair = (byte)Wrap(editing.hair + delta, visual.GetPartsCount(PartsType.Hair));
        }
        else if (row == RowHairColor)
        {
            // 수염 색이 머리 색과 같았다면 함께 바꾼다.
            bool linked = editing.beardColor == editing.hairColor;
            editing.hairColor = (byte)Wrap(editing.hairColor + delta, visual.HairColorCount);
            if (linked)
            {
                editing.beardColor = editing.hairColor;
            }
        }
        else if (row == RowEye)
        {
            editing.eye = (byte)Wrap(editing.eye + delta, visual.GetPartsCount(PartsType.Eye));
        }
        else if (row == RowBeard)
        {
            // 0번은 수염 없음, 1번부터 수염 0번
            int count = visual.GetPartsCount(PartsType.Beard) + 1;
            int current = 0;
            if (editing.beard != CharacterLook.NoBeard)
            {
                current = editing.beard + 1;
            }
            int next = Wrap(current + delta, count);
            if (next == 0)
            {
                editing.beard = CharacterLook.NoBeard;
            }
            else
            {
                editing.beard = (byte)(next - 1);
            }
        }
        else if (row == RowBeardColor)
        {
            editing.beardColor = (byte)Wrap(editing.beardColor + delta, visual.HairColorCount);
        }
        else if (row == RowSkin)
        {
            editing.skin = (byte)Wrap(editing.skin + delta, visual.GetPartsCount(PartsType.Skin));
        }
        else
        {
            editing.skinColor = (byte)Wrap(editing.skinColor + delta, visual.SkinColorCount);
        }
        editing.valid = 1;
        Apply();
    }

    private static int Wrap(int value, int count)
    {
        int result = 0;
        if (count > 0)
        {
            result = ((value % count) + count) % count;
        }
        return result;
    }

    private void Apply()
    {
        player.PreviewLook(editing);
        CharacterVisual visual = player.Visual;
        SetNumber(RowHair, editing.hair, visual.GetPartsCount(PartsType.Hair));
        SetSwatch(RowHairColor, visual.GetHairColor(editing.hairColor));
        SetNumber(RowEye, editing.eye, visual.GetPartsCount(PartsType.Eye));
        if (editing.beard == CharacterLook.NoBeard)
        {
            rowValues[RowBeard].text = "없음";
        }
        else
        {
            SetNumber(RowBeard, editing.beard, visual.GetPartsCount(PartsType.Beard));
        }
        SetSwatch(RowBeardColor, visual.GetHairColor(editing.beardColor));
        SetNumber(RowSkin, editing.skin, visual.GetPartsCount(PartsType.Skin));
        SetSwatch(RowSkinColor, visual.GetSkinColor(editing.skinColor));
    }

    private void SetNumber(int row, int index, int count)
    {
        builder.Clear();
        builder.Append(index + 1).Append(" / ").Append(count);
        rowValues[row].SetText(builder);
        rowSwatches[row].enabled = false;
    }

    private void SetSwatch(int row, Color color)
    {
        rowValues[row].text = string.Empty;
        rowSwatches[row].color = color;
        rowSwatches[row].enabled = true;
    }
}
