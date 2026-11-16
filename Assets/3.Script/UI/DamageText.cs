using TMPro;
using UnityEngine;

// 떠오르며 사라지는 피해 숫자 하나. DamageTextPool이 Tick을 호출한다.
public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;

    private float timer;
    private float duration;
    private float riseSpeed;
    private Color color;

    public void Show(Vector2 position, int amount, Color textColor, float showDuration, float showRiseSpeed)
    {
        transform.position = position;
        duration = showDuration;
        timer = showDuration;
        riseSpeed = showRiseSpeed;
        color = textColor;
        label.color = textColor;
        // 숫자 서식 SetText는 문자열을 새로 만들지 않는다.
        label.SetText("{0}", amount);
    }

    // 표시가 끝나면 true를 반환한다.
    public bool Tick(float deltaTime)
    {
        timer -= deltaTime;
        bool finished = timer <= 0f;
        if (!finished)
        {
            Vector3 position = transform.position;
            position.y += riseSpeed * deltaTime;
            transform.position = position;

            float ratio = timer / duration;
            color.a = Mathf.Clamp01(ratio * 2f);
            label.color = color;
        }
        return finished;
    }
}
