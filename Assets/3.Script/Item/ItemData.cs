public enum ItemSlot : byte
{
    Weapon,
    Offhand,
    Helmet,
    Armor,
    // 가방에만 들어가는 소모품(장착하지 않는다). kind = PotionKind, count = 개수
    Potion
}

public enum PotionKind : byte
{
    None,
    // 체력 포션(소)
    HealthSmall,
    // 체력 포션(대)
    HealthLarge,
    Mana,
    // 정화: 상태이상을 풀고 잠깐 걸리지 않는다
    Cleanse,
    // 힘의 물약: 잠시 주는 피해 증가
    Power,
    // 신속의 물약: 잠시 이동 속도 증가
    Swift
}

public enum ItemRarity : byte
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum ItemOptionType : byte
{
    None,
    DamagePercent,
    AttackSpeedPercent,
    MoveSpeedPercent,
    CritChancePercent,
    MaxHp,
    DefensePercent,
    MaxMana,
    ManaOnHit
}

// 장비 한 점. 네트워크로 그대로 보내는 값 형식이다(기본 형식 필드만 사용).
// 기본 수치는 종류와 등급에서 계산하고(ItemBalance), 여기에는 "무엇인가"만 담는다.
public struct ItemData
{
    // 0이면 빈 칸
    public byte exists;
    public ItemSlot slot;
    // 무기일 때 WeaponKind 번호. 다른 부위는 0.
    public byte kind;
    // 패키지 파츠 번호(외형)
    public ushort partsIndex;
    public ItemRarity rarity;
    // 대장간 강화 단계
    public byte level;
    public ItemOptionType option1;
    public short value1;
    public ItemOptionType option2;
    public short value2;
    // 옵션 재설정 횟수(비용 증가용)
    public byte rerollCount;
    // 포션 개수(한 칸에 쌓인 수). 장비는 0.
    public byte count;

    public bool Exists => exists != 0;
}
