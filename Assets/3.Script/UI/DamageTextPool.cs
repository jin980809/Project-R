using System.Collections.Generic;
using UnityEngine;

public class DamageTextPool : MonoBehaviour
{
    [SerializeField] private DamageText textPrefab;
    [SerializeField] private int prewarmCount = 24;
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float riseSpeed = 1.2f;
    [SerializeField] private float spread = 0.2f;

    private readonly Stack<DamageText> freeTexts = new Stack<DamageText>(32);
    private readonly List<DamageText> activeTexts = new List<DamageText>(32);
    private bool showEnabled = true;

    private void Awake()
    {
        for (int i = 0; i < prewarmCount; i++)
        {
            freeTexts.Push(CreateInstance());
        }
    }

    private DamageText CreateInstance()
    {
        DamageText text = Instantiate(textPrefab, transform);
        text.gameObject.SetActive(false);
        return text;
    }

    public void Show(Vector2 position, int amount, Color color)
    {
        ShowScaled(position, amount, color, 1f);
    }

    // 치명타처럼 크게 보여 줄 때 쓴다.
    public void ShowScaled(Vector2 position, int amount, Color color, float scale)
    {
        if (showEnabled)
        {
            DamageText text;
            if (freeTexts.Count > 0)
            {
                text = freeTexts.Pop();
            }
            else
            {
                text = CreateInstance();
            }
            position.x += Random.Range(-spread, spread);
            text.gameObject.SetActive(true);
            text.transform.localScale = new Vector3(scale, scale, 1f);
            text.Show(position, amount, color, duration, riseSpeed);
            activeTexts.Add(text);
        }
    }

    // 연계 이름 같은 글자. 피해 숫자 설정과 상관없이 보여 준다.
    public void ShowLabel(Vector2 position, string label, Color color, float scale)
    {
        DamageText text;
        if (freeTexts.Count > 0)
        {
            text = freeTexts.Pop();
        }
        else
        {
            text = CreateInstance();
        }
        text.gameObject.SetActive(true);
        text.transform.localScale = new Vector3(scale, scale, 1f);
        text.ShowText(position, label, color, duration * 1.6f, riseSpeed * 0.7f);
        activeTexts.Add(text);
    }

    // 설정에서 피해 숫자를 끄면 더 이상 띄우지 않는다.
    public void SetShowEnabled(bool value)
    {
        showEnabled = value;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = activeTexts.Count - 1; i >= 0; i--)
        {
            DamageText text = activeTexts[i];
            if (text.Tick(deltaTime))
            {
                int last = activeTexts.Count - 1;
                activeTexts[i] = activeTexts[last];
                activeTexts.RemoveAt(last);
                text.gameObject.SetActive(false);
                freeTexts.Push(text);
            }
        }
    }
}
