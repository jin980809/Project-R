using UnityEngine;

// 바닥에 떨어진 장비의 표시. 네트워크 오브젝트가 아니며 주인 화면에만 있다(ItemDropPool이 관리).
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private float iconSize = 0.6f;
    [SerializeField] private float bobHeight = 0.08f;
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private float iconBaseHeight = 0.3f;

    private int id;
    private ItemData item;
    private float time;

    public int Id => id;
    public ItemData Item => item;

    public void Show(int dropId, in ItemData dropItem, Vector2 position, Sprite icon, Color rarityColor)
    {
        id = dropId;
        item = dropItem;
        time = 0f;
        transform.position = position;
        iconRenderer.sprite = icon;
        float scale = 1f;
        if (icon != null)
        {
            Vector2 size = icon.bounds.size;
            float longest = Mathf.Max(size.x, size.y);
            if (longest > 0.001f)
            {
                scale = iconSize / longest;
            }
        }
        iconRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        rarityColor.a = 0.55f;
        glowRenderer.color = rarityColor;
    }

    public void Tick(float deltaTime)
    {
        time += deltaTime;
        Vector3 position = iconRenderer.transform.localPosition;
        position.y = iconBaseHeight + Mathf.Sin(time * bobSpeed) * bobHeight;
        iconRenderer.transform.localPosition = position;
    }
}
