using System.Text;
using LayerLab.ArtMakerUnity;
using UnityEngine;

// 장비가 주는 능력치 합계. 서버와 본인 클라이언트가 각자 같은 장비 목록에서 계산한다.
public struct EquipStats
{
    public float damageMultiplier;
    public float attackSpeedMultiplier;
    public float moveSpeedMultiplier;
    public float critChance;
    public int maxHpBonus;
    public float damageTakenMultiplier;
    public int maxManaBonus;
    public int manaOnHitBonus;
    public float blockDamageFactor;
    // 아래 둘은 직업 노드와 강화 스킬이 바꾼다(PlayerSkills.ModifyStats).
    public float cooldownMultiplier;
    public float skillDamageMultiplier;
}

// 장비 수치 규칙을 한곳에 모은 데이터. 프리팹과 씬 오브젝트가 이 에셋을 참조해 쓴다.
// 수치는 Tower > 프로젝트 초기 구성(TowerBootstrap)에서 정한다.
[CreateAssetMenu(menuName = "Tower/Item Balance", fileName = "ItemBalance")]
public class ItemBalance : ScriptableObject
{
    [Header("등급(일반, 희귀, 영웅, 전설 순)")]
    [SerializeField] private string[] rarityNames = { "일반", "희귀", "영웅", "전설" };
    [SerializeField] private Color[] rarityColors =
    {
        new Color(0.85f, 0.85f, 0.85f), new Color(0.35f, 0.65f, 1f), new Color(0.75f, 0.45f, 1f), new Color(1f, 0.7f, 0.2f)
    };
    [Tooltip("드랍 시 등급 확률 가중치")]
    [SerializeField] private float[] rarityWeights = { 60f, 27f, 10f, 3f };
    [SerializeField] private float[] weaponDamageMultipliers = { 1f, 1.25f, 1.55f, 1.9f };
    [SerializeField] private int[] helmetHp = { 10, 16, 24, 34 };
    [SerializeField] private int[] armorHp = { 6, 10, 14, 20 };
    [SerializeField] private float[] armorDefensePercent = { 6f, 10f, 14f, 18f };
    [SerializeField] private float[] shieldBlockFactors = { 0.3f, 0.25f, 0.2f, 0.15f };
    [Tooltip("등급별 옵션 개수")]
    [SerializeField] private int[] optionCounts = { 0, 1, 2, 2 };
    [Tooltip("강화 1단계당 기본 수치 증가율")]
    [SerializeField] private float upgradeStep = 0.1f;
    [SerializeField] private float critDamageMultiplier = 1.5f;
    [SerializeField] private float baseBlockDamageFactor = 0.3f;

    [Header("상점, 대장간, 포션")]
    [SerializeField] private float buyMultiplier = 1.5f;
    [SerializeField] private float sellMultiplier = 0.3f;
    [SerializeField] private int shopStockCount = 4;
    [SerializeField] private int potionPrice = 25;
    [SerializeField] private int maxPotions = 3;
    [Tooltip("포션 하나가 회복하는 최대 체력 비율")]
    [SerializeField] private float potionHealFraction = 0.4f;
    [SerializeField] private int maxUpgradeLevel = 5;
    [SerializeField] private int rerollBaseCost = 30;

    [Header("세트 효과(투구와 갑옷이 같은 등급일 때)")]
    [SerializeField] private int setRareHp = 15;
    [SerializeField] private int setEpicDefensePercent = 8;
    [SerializeField] private int setLegendaryDamagePercent = 12;

    [Header("옵션 수치 범위(ItemOptionType 순서, None 포함)")]
    [SerializeField] private string[] optionNames =
    {
        "", "공격력 +{0}%", "공격 속도 +{0}%", "이동 속도 +{0}%", "치명타 확률 +{0}%", "최대 체력 +{0}", "받는 피해 -{0}%", "최대 마나 +{0}", "적중 시 마나 +{0}"
    };
    [SerializeField] private int[] optionMin = { 0, 4, 4, 3, 3, 6, 2, 5, 1 };
    [SerializeField] private int[] optionMax = { 0, 10, 10, 8, 8, 16, 6, 12, 2 };
    [Tooltip("등급이 오를 때 옵션 수치에 곱하는 값(희귀 기준 1)")]
    [SerializeField] private float[] optionRarityScale = { 1f, 1f, 1.4f, 1.8f };

    [Header("이름")]
    [SerializeField] private string[] weaponKindNames = { "검", "창", "활", "지팡이", "도끼", "둔기", "석궁", "총", "완드" };
    [SerializeField] private string offhandName = "방패";
    [SerializeField] private string helmetName = "투구";
    [SerializeField] private string armorName = "갑옷";

    [Header("외형 파츠 수(TowerBootstrap이 프리팹에서 읽어 채운다)")]
    [SerializeField] private int[] weaponPartsCounts = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };
    [SerializeField] private int shieldPartsCount = 1;
    [SerializeField] private int helmetPartsCount = 1;
    [SerializeField] private int armorPartsCount = 1;

    public float CritDamageMultiplier => critDamageMultiplier;

    public Color GetRarityColor(ItemRarity rarity)
    {
        return rarityColors[(int)rarity];
    }

    // ------------------------------------------------------------ 생성

    public ItemData CreateWeapon(WeaponKind kind, ItemRarity rarity)
    {
        ItemData item = new ItemData();
        item.exists = 1;
        item.slot = ItemSlot.Weapon;
        item.kind = (byte)kind;
        item.rarity = rarity;
        item.partsIndex = (ushort)Random.Range(0, Mathf.Max(1, weaponPartsCounts[(int)kind]));
        RollOptions(ref item);
        return item;
    }

    public ItemData CreateGear(ItemSlot slot, ItemRarity rarity)
    {
        ItemData item = new ItemData();
        item.exists = 1;
        item.slot = slot;
        item.rarity = rarity;
        int count = 1;
        if (slot == ItemSlot.Offhand)
        {
            count = shieldPartsCount;
        }
        else if (slot == ItemSlot.Helmet)
        {
            count = helmetPartsCount;
        }
        else if (slot == ItemSlot.Armor)
        {
            count = armorPartsCount;
        }
        item.partsIndex = (ushort)Random.Range(0, Mathf.Max(1, count));
        RollOptions(ref item);
        return item;
    }

    // 부위와 등급을 무작위로 정한 장비 한 점. rarityBonus가 클수록 높은 등급이 잘 나온다(엘리트, 보물방).
    public ItemData CreateRandom(float rarityBonus)
    {
        ItemRarity rarity = RollRarity(rarityBonus);
        ItemData item;
        int roll = Random.Range(0, 100);
        if (roll < 45)
        {
            item = CreateWeapon((WeaponKind)Random.Range(0, weaponKindNames.Length), rarity);
        }
        else if (roll < 60)
        {
            item = CreateGear(ItemSlot.Offhand, rarity);
        }
        else if (roll < 80)
        {
            item = CreateGear(ItemSlot.Helmet, rarity);
        }
        else
        {
            item = CreateGear(ItemSlot.Armor, rarity);
        }
        return item;
    }

    public ItemRarity RollRarity(float rarityBonus)
    {
        float total = 0f;
        for (int i = 0; i < rarityWeights.Length; i++)
        {
            total += GetRarityWeight(i, rarityBonus);
        }
        float roll = Random.Range(0f, total);
        int picked = 0;
        bool found = false;
        for (int i = 0; i < rarityWeights.Length && !found; i++)
        {
            float weight = GetRarityWeight(i, rarityBonus);
            if (roll < weight)
            {
                picked = i;
                found = true;
            }
            else
            {
                roll -= weight;
            }
        }
        return (ItemRarity)picked;
    }

    // 보너스는 일반 등급의 가중치를 줄이고 나머지를 늘린다.
    private float GetRarityWeight(int index, float rarityBonus)
    {
        float weight = rarityWeights[index];
        if (index == 0)
        {
            weight /= 1f + rarityBonus;
        }
        else
        {
            weight *= 1f + rarityBonus;
        }
        return weight;
    }

    public void RollOptions(ref ItemData item)
    {
        int count = optionCounts[(int)item.rarity];
        item.option1 = ItemOptionType.None;
        item.value1 = 0;
        item.option2 = ItemOptionType.None;
        item.value2 = 0;
        if (count >= 1)
        {
            item.option1 = RollOptionType(ItemOptionType.None);
            item.value1 = RollOptionValue(item.option1, item.rarity);
        }
        if (count >= 2)
        {
            item.option2 = RollOptionType(item.option1);
            item.value2 = RollOptionValue(item.option2, item.rarity);
        }
    }

    // 옵션 하나만 다시 굴린다(대장간 재설정). index는 0 또는 1.
    public void RerollOption(ref ItemData item, int index)
    {
        if (index == 0)
        {
            item.option1 = RollOptionType(item.option2);
            item.value1 = RollOptionValue(item.option1, item.rarity);
        }
        else
        {
            item.option2 = RollOptionType(item.option1);
            item.value2 = RollOptionValue(item.option2, item.rarity);
        }
    }

    private ItemOptionType RollOptionType(ItemOptionType exclude)
    {
        int last = optionNames.Length - 1;
        ItemOptionType picked = (ItemOptionType)Random.Range(1, last + 1);
        if (picked == exclude)
        {
            int next = (int)picked + 1;
            if (next > last)
            {
                next = 1;
            }
            picked = (ItemOptionType)next;
        }
        return picked;
    }

    private short RollOptionValue(ItemOptionType type, ItemRarity rarity)
    {
        int index = (int)type;
        float value = Random.Range(optionMin[index], optionMax[index] + 1) * optionRarityScale[(int)rarity];
        return (short)Mathf.Max(1, Mathf.RoundToInt(value));
    }

    // ------------------------------------------------------------ 수치

    public float GetUpgradeMultiplier(in ItemData item)
    {
        return 1f + upgradeStep * item.level;
    }

    public int GetWeaponDamage(in ItemData item, WeaponDefinition weapon)
    {
        float damage = weapon.Damage * weaponDamageMultipliers[(int)item.rarity] * GetUpgradeMultiplier(item);
        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    // 장착한 장비 4점에서 능력치 합계를 계산한다.
    public EquipStats ComputeStats(in ItemData weapon, in ItemData offhand, in ItemData helmet, in ItemData armor)
    {
        EquipStats stats = new EquipStats();
        stats.damageMultiplier = 1f;
        stats.attackSpeedMultiplier = 1f;
        stats.moveSpeedMultiplier = 1f;
        stats.damageTakenMultiplier = 1f;
        stats.blockDamageFactor = baseBlockDamageFactor;
        stats.cooldownMultiplier = 1f;
        stats.skillDamageMultiplier = 1f;

        if (weapon.Exists)
        {
            stats.damageMultiplier = weaponDamageMultipliers[(int)weapon.rarity] * GetUpgradeMultiplier(weapon);
            AddOptions(ref stats, weapon);
        }
        if (offhand.Exists)
        {
            stats.blockDamageFactor = shieldBlockFactors[(int)offhand.rarity] / GetUpgradeMultiplier(offhand);
            AddOptions(ref stats, offhand);
        }
        if (helmet.Exists)
        {
            stats.maxHpBonus += Mathf.RoundToInt(helmetHp[(int)helmet.rarity] * GetUpgradeMultiplier(helmet));
            AddOptions(ref stats, helmet);
        }
        if (armor.Exists)
        {
            stats.maxHpBonus += Mathf.RoundToInt(armorHp[(int)armor.rarity] * GetUpgradeMultiplier(armor));
            float defense = armorDefensePercent[(int)armor.rarity] * GetUpgradeMultiplier(armor);
            stats.damageTakenMultiplier *= 1f - defense * 0.01f;
            AddOptions(ref stats, armor);
        }
        // 세트 효과: 투구와 갑옷이 같은 등급(희귀 이상)이면 등급에 따라 추가 효과를 받는다.
        if (HasSet(helmet, armor))
        {
            if (helmet.rarity == ItemRarity.Rare)
            {
                stats.maxHpBonus += setRareHp;
            }
            else if (helmet.rarity == ItemRarity.Epic)
            {
                stats.damageTakenMultiplier *= 1f - setEpicDefensePercent * 0.01f;
            }
            else
            {
                stats.damageMultiplier *= 1f + setLegendaryDamagePercent * 0.01f;
            }
        }
        stats.damageTakenMultiplier = Mathf.Max(0.2f, stats.damageTakenMultiplier);
        return stats;
    }

    public bool HasSet(in ItemData helmet, in ItemData armor)
    {
        return helmet.Exists && armor.Exists && helmet.rarity == armor.rarity && helmet.rarity >= ItemRarity.Rare;
    }

    // 투구·갑옷 설명 끝에 붙는 세트 효과 안내
    private void AppendSetText(StringBuilder builder, ItemRarity rarity)
    {
        if (rarity == ItemRarity.Rare)
        {
            builder.Append("세트(같은 등급 투구+갑옷): 최대 체력 +").Append(setRareHp).Append('\n');
        }
        else if (rarity == ItemRarity.Epic)
        {
            builder.Append("세트(같은 등급 투구+갑옷): 받는 피해 -").Append(setEpicDefensePercent).Append("%\n");
        }
        else if (rarity == ItemRarity.Legendary)
        {
            builder.Append("세트(같은 등급 투구+갑옷): 피해 +").Append(setLegendaryDamagePercent).Append("%\n");
        }
    }

    private void AddOptions(ref EquipStats stats, in ItemData item)
    {
        AddOption(ref stats, item.option1, item.value1);
        AddOption(ref stats, item.option2, item.value2);
    }

    private void AddOption(ref EquipStats stats, ItemOptionType type, int value)
    {
        if (type == ItemOptionType.DamagePercent)
        {
            stats.damageMultiplier *= 1f + value * 0.01f;
        }
        else if (type == ItemOptionType.AttackSpeedPercent)
        {
            stats.attackSpeedMultiplier *= 1f + value * 0.01f;
        }
        else if (type == ItemOptionType.MoveSpeedPercent)
        {
            stats.moveSpeedMultiplier *= 1f + value * 0.01f;
        }
        else if (type == ItemOptionType.CritChancePercent)
        {
            stats.critChance += value * 0.01f;
        }
        else if (type == ItemOptionType.MaxHp)
        {
            stats.maxHpBonus += value;
        }
        else if (type == ItemOptionType.DefensePercent)
        {
            stats.damageTakenMultiplier *= 1f - value * 0.01f;
        }
        else if (type == ItemOptionType.MaxMana)
        {
            stats.maxManaBonus += value;
        }
        else if (type == ItemOptionType.ManaOnHit)
        {
            stats.manaOnHitBonus += value;
        }
    }

    // ------------------------------------------------------------ 상점, 대장간, 포션

    public int MaxPotions => maxPotions;
    public float PotionHealFraction => potionHealFraction;
    public int PotionPrice => potionPrice;
    public int ShopStockCount => shopStockCount;

    public int GetBuyPrice(in ItemData item)
    {
        return Mathf.RoundToInt(GetValue(item) * buyMultiplier);
    }

    public int GetSellPrice(in ItemData item)
    {
        return Mathf.Max(1, Mathf.RoundToInt(GetValue(item) * sellMultiplier));
    }

    public bool CanUpgrade(in ItemData item)
    {
        return item.Exists && item.level < maxUpgradeLevel;
    }

    // 강화에 드는 광석. 단계가 오를수록 하나씩 더 든다.
    public int GetUpgradeOreCost(in ItemData item)
    {
        return item.level + 1;
    }

    // 옵션 재설정에 드는 골드. 같은 장비를 다시 굴릴수록 비싸진다.
    public int GetRerollCost(in ItemData item)
    {
        return rerollBaseCost * (item.rerollCount + 1);
    }

    public bool HasOption(in ItemData item, int optionIndex)
    {
        bool has = false;
        if (item.Exists)
        {
            if (optionIndex == 0)
            {
                has = item.option1 != ItemOptionType.None;
            }
            else
            {
                has = item.option2 != ItemOptionType.None;
            }
        }
        return has;
    }

    // 상점 판매가와 구매가의 기준 값
    public int GetValue(in ItemData item)
    {
        int value = 0;
        if (item.Exists)
        {
            value = 20 + 30 * (int)item.rarity * ((int)item.rarity + 1) / 2 + 10 * item.level;
        }
        return value;
    }

    // ------------------------------------------------------------ 외형과 설명

    // 무기 계열의 패키지 파츠 종류(WeaponKind 순서와 같다)
    private static readonly PartsType[] weaponPartsTypes =
    {
        PartsType.Sword, PartsType.Spear, PartsType.Bow, PartsType.Staff, PartsType.Axe,
        PartsType.Blunt, PartsType.Crossbow, PartsType.Gun, PartsType.Wand
    };

    public static PartsType GetWeaponPartsType(WeaponKind kind)
    {
        return weaponPartsTypes[(int)kind];
    }

    public PartsType GetPartsType(in ItemData item)
    {
        PartsType type = PartsType.Sword;
        if (item.slot == ItemSlot.Weapon)
        {
            type = GetWeaponPartsType((WeaponKind)item.kind);
        }
        else if (item.slot == ItemSlot.Offhand)
        {
            type = PartsType.Shield;
        }
        else if (item.slot == ItemSlot.Helmet)
        {
            type = PartsType.Helmet;
        }
        else
        {
            type = PartsType.Chest;
        }
        return type;
    }

    public void AppendName(StringBuilder builder, in ItemData item)
    {
        builder.Append(rarityNames[(int)item.rarity]).Append(' ');
        if (item.slot == ItemSlot.Weapon)
        {
            builder.Append(weaponKindNames[item.kind]);
        }
        else if (item.slot == ItemSlot.Offhand)
        {
            builder.Append(offhandName);
        }
        else if (item.slot == ItemSlot.Helmet)
        {
            builder.Append(helmetName);
        }
        else
        {
            builder.Append(armorName);
        }
        if (item.level > 0)
        {
            builder.Append(" +").Append(item.level);
        }
    }

    // 이름, 기본 수치, 옵션을 여러 줄로 적는다. weapons는 무기 계열별 정의(피해 표시용).
    public void AppendDescription(StringBuilder builder, in ItemData item, WeaponDefinition[] weapons)
    {
        if (item.Exists)
        {
            AppendName(builder, item);
            builder.Append('\n');
            if (item.slot == ItemSlot.Weapon)
            {
                builder.Append("피해 ").Append(GetWeaponDamage(item, weapons[item.kind])).Append('\n');
            }
            else if (item.slot == ItemSlot.Offhand)
            {
                float factor = shieldBlockFactors[(int)item.rarity] / GetUpgradeMultiplier(item);
                builder.Append("막기 시 피해 ").Append(Mathf.RoundToInt(factor * 100f)).Append("%\n");
            }
            else if (item.slot == ItemSlot.Helmet)
            {
                builder.Append("최대 체력 +").Append(Mathf.RoundToInt(helmetHp[(int)item.rarity] * GetUpgradeMultiplier(item))).Append('\n');
            }
            else
            {
                builder.Append("최대 체력 +").Append(Mathf.RoundToInt(armorHp[(int)item.rarity] * GetUpgradeMultiplier(item))).Append('\n');
                builder.Append("받는 피해 -").Append(Mathf.RoundToInt(armorDefensePercent[(int)item.rarity] * GetUpgradeMultiplier(item))).Append("%\n");
            }
            AppendOption(builder, item.option1, item.value1);
            AppendOption(builder, item.option2, item.value2);
            if (item.slot == ItemSlot.Helmet || item.slot == ItemSlot.Armor)
            {
                AppendSetText(builder, item.rarity);
            }
        }
        else
        {
            builder.Append("(없음)\n");
        }
    }

    private void AppendOption(StringBuilder builder, ItemOptionType type, int value)
    {
        if (type != ItemOptionType.None)
        {
            builder.AppendFormat(optionNames[(int)type], value).Append('\n');
        }
    }
}
