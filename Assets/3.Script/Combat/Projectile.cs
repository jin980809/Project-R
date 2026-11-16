using UnityEngine;

// 풀에서 재사용하는 투사체. ProjectilePool이 Tick을 호출한다.
public class Projectile : MonoBehaviour
{
    private Vector2 position;
    private Vector2 velocity;
    private float lifetime;
    private float radiusSqr;
    private int damage;
    private bool authoritative;

    public void Launch(Vector2 origin, Vector2 direction, MonsterDefinition definition, bool isAuthoritative)
    {
        position = origin;
        velocity = direction * definition.ProjectileSpeed;
        lifetime = definition.ProjectileLifetime;
        radiusSqr = definition.ProjectileRadius * definition.ProjectileRadius;
        damage = definition.Damage;
        authoritative = isAuthoritative;
        transform.position = origin;
    }

    // 수명이 끝나면 true를 반환한다.
    public bool Tick(float deltaTime, GameHub hub)
    {
        bool finished = false;
        position += velocity * deltaTime;
        transform.position = position;
        lifetime -= deltaTime;

        if (lifetime <= 0f)
        {
            finished = true;
        }
        else if (!hub.IsInsideArena(position, 0f))
        {
            finished = true;
        }
        else
        {
            int count = hub.PlayerCount;
            for (int i = 0; i < count && !finished; i++)
            {
                PlayerController player = hub.GetPlayer(i);
                if (!player.IsDead)
                {
                    Vector2 toPlayer = (Vector2)player.transform.position - position;
                    if (toPlayer.sqrMagnitude <= radiusSqr)
                    {
                        finished = true;
                        if (authoritative)
                        {
                            player.ServerTakeDamage(damage, velocity.normalized, 0);
                        }
                    }
                }
            }
        }
        return finished;
    }
}
