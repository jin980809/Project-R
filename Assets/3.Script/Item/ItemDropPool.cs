using System.Collections.Generic;
using UnityEngine;

// 바닥 아이템 표시를 재사용한다. 씬마다 하나 있고, 본인에게 온 드랍만 보여 준다.
public class ItemDropPool : MonoBehaviour
{
    [SerializeField] private ItemPickup pickupPrefab;
    [SerializeField] private int prewarmCount = 8;

    private readonly Stack<ItemPickup> freePickups = new Stack<ItemPickup>(16);
    private readonly List<ItemPickup> activePickups = new List<ItemPickup>(16);

    public int ActiveCount => activePickups.Count;

    private void Awake()
    {
        for (int i = 0; i < prewarmCount; i++)
        {
            freePickups.Push(CreateInstance());
        }
    }

    private ItemPickup CreateInstance()
    {
        ItemPickup pickup = Instantiate(pickupPrefab, transform);
        pickup.gameObject.SetActive(false);
        return pickup;
    }

    public ItemPickup GetActive(int index)
    {
        return activePickups[index];
    }

    public void Show(int dropId, in ItemData item, Vector2 position, Sprite icon, Color rarityColor)
    {
        ItemPickup pickup;
        if (freePickups.Count > 0)
        {
            pickup = freePickups.Pop();
        }
        else
        {
            pickup = CreateInstance();
        }
        pickup.Show(dropId, item, position, icon, rarityColor);
        pickup.gameObject.SetActive(true);
        activePickups.Add(pickup);
    }

    public void Hide(int dropId)
    {
        bool removed = false;
        for (int i = activePickups.Count - 1; i >= 0 && !removed; i--)
        {
            ItemPickup pickup = activePickups[i];
            if (pickup.Id == dropId)
            {
                int last = activePickups.Count - 1;
                activePickups[i] = activePickups[last];
                activePickups.RemoveAt(last);
                pickup.gameObject.SetActive(false);
                freePickups.Push(pickup);
                removed = true;
            }
        }
    }

    public void HideAll()
    {
        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            ItemPickup pickup = activePickups[i];
            pickup.gameObject.SetActive(false);
            freePickups.Push(pickup);
        }
        activePickups.Clear();
    }

    // range 안에서 가장 가까운 바닥 아이템. 없으면 null.
    public ItemPickup FindNearest(Vector2 from, float range)
    {
        ItemPickup nearest = null;
        float nearestSqr = range * range;
        for (int i = 0; i < activePickups.Count; i++)
        {
            ItemPickup pickup = activePickups[i];
            float sqr = ((Vector2)pickup.transform.position - from).sqrMagnitude;
            if (sqr <= nearestSqr)
            {
                nearestSqr = sqr;
                nearest = pickup;
            }
        }
        return nearest;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = 0; i < activePickups.Count; i++)
        {
            activePickups[i].Tick(deltaTime);
        }
    }
}
