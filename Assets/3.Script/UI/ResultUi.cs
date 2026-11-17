using System.Text;
using TMPro;
using UnityEngine;

// 판이 끝났을 때(정복 또는 전멸) 잠깐 보여 주는 결과 화면. 로비로 돌아가기 전까지 떠 있다.
public class ResultUi : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Color victoryColor = new Color(1f, 0.9f, 0.4f);
    [SerializeField] private Color defeatColor = new Color(1f, 0.5f, 0.45f);

    private readonly StringBuilder builder = new StringBuilder(256);

    public void Show(bool victory, int floor, int kills, int seconds, int level, int gold, int soulGain, int soulTotal)
    {
        if (victory)
        {
            titleLabel.text = "탑 정복";
            titleLabel.color = victoryColor;
        }
        else
        {
            titleLabel.text = "전멸";
            titleLabel.color = defeatColor;
        }

        builder.Clear();
        builder.Append("도달한 층    ").Append(floor).Append("층\n");
        builder.Append("처치한 몬스터    ").Append(kills).Append("마리\n");
        builder.Append("걸린 시간    ").Append(seconds / 60).Append("분 ").Append(seconds % 60).Append("초\n");
        builder.Append("레벨    ").Append(level).Append('\n');
        builder.Append("모은 골드    ").Append(gold).Append(" G\n\n");
        builder.Append("영혼석  +").Append(soulGain).Append("    (보유 ").Append(soulTotal).Append(")\n\n");
        builder.Append("잠시 후 로비로 돌아갑니다.");
        bodyLabel.SetText(builder);
        panel.SetActive(true);
    }
}
