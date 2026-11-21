// 상태이상 5종. 몬스터에게 걸린다.
public enum StatusKind : byte
{
    None,
    // 화상: 지속 피해
    Burn,
    // 출혈: 지속 피해(화상과 따로 쌓인다)
    Bleed,
    // 둔화: 이동 속도 감소
    Slow,
    // 기절: 잠시 행동 불가(보스는 면역)
    Stun,
    // 약화: 주는 피해 감소
    Weaken,
    // 중독: 지속 피해(화상·출혈과 따로 쌓인다. 독 구름·독 폭탄)
    Poison
}

// 플레이어가 몬스터에게 주는 한 번의 타격. 값 형식이라 넘길 때 할당이 없다.
public struct HitInfo
{
    public int damage;
    public bool crit;
    public StatusKind status;
    // 상태이상 지속 시간(초)
    public float statusDuration;
    // 화상·출혈: 0.5초마다 들어가는 피해가 타격 피해의 몇 배인지. 나머지는 쓰지 않는다.
    public float statusPower;
}
