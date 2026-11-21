using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private GameHub hub;
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private int prewarmCount = 16;

    private readonly Stack<Projectile> freeProjectiles = new Stack<Projectile>(32);
    private readonly List<Projectile> activeProjectiles = new List<Projectile>(32);

    private void Awake()
    {
        for (int i = 0; i < prewarmCount; i++)
        {
            freeProjectiles.Push(CreateInstance());
        }
    }

    private Projectile CreateInstance()
    {
        Projectile projectile = Instantiate(projectilePrefab, transform);
        projectile.gameObject.SetActive(false);
        return projectile;
    }

    public void Launch(in ProjectileShot shot)
    {
        Projectile projectile;
        if (freeProjectiles.Count > 0)
        {
            projectile = freeProjectiles.Pop();
        }
        else
        {
            projectile = CreateInstance();
        }
        projectile.Launch(shot);
        projectile.gameObject.SetActive(true);
        activeProjectiles.Add(projectile);
    }

    // 방을 옮기면 FloorDirector가 부른다. 날아가던 투사체를 모두 거둔다.
    public void ClearAll()
    {
        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = activeProjectiles[i];
            projectile.gameObject.SetActive(false);
            freeProjectiles.Push(projectile);
        }
        activeProjectiles.Clear();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = activeProjectiles[i];
            if (projectile.Tick(deltaTime, hub))
            {
                int last = activeProjectiles.Count - 1;
                activeProjectiles[i] = activeProjectiles[last];
                activeProjectiles.RemoveAt(last);
                projectile.gameObject.SetActive(false);
                freeProjectiles.Push(projectile);
            }
        }
    }
}
