using UnityEngine;

// 피해를 받을 수 있는 대상. 피해 판정은 항상 서버(호스트)에서만 호출한다.
public interface IDamageable
{
    void ServerTakeDamage(int amount, Vector2 hitDirection, uint attackerNetId);
}
