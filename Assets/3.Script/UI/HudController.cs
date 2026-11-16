using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 체력, 마나, 구르기 충전, 웨이브 표시. 값이 바뀔 때만 UI를 갱신한다.
public class HudController : MonoBehaviour
{
    [SerializeField] private MonsterDirector monsterDirector;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image manaFill;
    [SerializeField] private Image[] rollPips;
    [SerializeField] private TMP_Text waveLabel;
    [SerializeField] private Color pipReadyColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color pipEmptyColor = new Color(1f, 1f, 1f, 0.2f);

    private PlayerController player;
    private int lastHp = -1;
    private int lastMana = -1;
    private int lastRollCharges = -1;
    private int lastWave = -1;

    public void Bind(PlayerController localPlayer)
    {
        player = localPlayer;
        lastHp = -1;
        lastMana = -1;
        lastRollCharges = -1;
    }

    private void Update()
    {
        if (player != null)
        {
            if (player.Hp != lastHp)
            {
                lastHp = player.Hp;
                hpFill.fillAmount = (float)lastHp / player.MaxHp;
            }

            if (player.Mana != lastMana)
            {
                lastMana = player.Mana;
                manaFill.fillAmount = (float)lastMana / player.MaxMana;
            }

            if (player.RollCharges != lastRollCharges)
            {
                lastRollCharges = player.RollCharges;
                for (int i = 0; i < rollPips.Length; i++)
                {
                    if (i < lastRollCharges)
                    {
                        rollPips[i].color = pipReadyColor;
                    }
                    else
                    {
                        rollPips[i].color = pipEmptyColor;
                    }
                }
            }
        }

        // 로비에는 몬스터 관리자가 없다.
        if (monsterDirector != null)
        {
            int wave = monsterDirector.WaveNumber;
            if (wave != lastWave)
            {
                lastWave = wave;
                waveLabel.SetText("WAVE {0}", wave);
            }
        }
    }
}
