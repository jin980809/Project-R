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
        text.Show(position, amount, color, duration, riseSpeed);
        activeTexts.Add(text);
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
