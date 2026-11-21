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
    // 치명타 피해 배율에 더하는 값(저격수 각성)
    public float critDamageBonus;
}

// 장비 수치 규칙을 한곳에 모은 데이터. 프리팹과 씬 오브젝트가 이 에셋을 참조해 쓴다.
// 수치는 Tower > 프로젝트 초기 구성(TowerBootstrap)에서 정한다.
[CreateAssetMenu(menuName = "Tower/Item Balance", fileName = "ItemBalance")]
public class ItemBalance : ScriptableObject
{
    // 투구·갑옷 계열(helmetClasses, armorClasses 값). 계열이 없으면(무기·방패) -1.
    public const int GearWarrior = 0;
    public const int GearShooter = 1;
    public const int GearMage = 2;
    // 공용: 누구나 입는 무난한 장비. 어느 세트에도 들지 않는다.
    public const int GearCommon = 3;
    public const int GearClassCount = 4;

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
    [SerializeField] private int maxUpgradeLevel = 5;
    [SerializeField] private int rerollBaseCost = 30;

    [Header("포션(PotionKind 순서, None 포함)")]
    [SerializeField] private string[] potionNames = { "", "체력 포션(소)", "체력 포션(대)", "마나 포션", "정화 포션", "힘의 물약", "신속의 물약" };
    [SerializeField] private int[] potionPrices = { 0, 25, 55, 30, 35, 45, 40 };
    [Tooltip("몬스터가 포션을 떨어뜨릴 때 종류별 가중치")]
    [SerializeField] private float[] potionDropWeights = { 0f, 60f, 8f, 18f, 7f, 4f, 3f };
    [Tooltip("가방 아이콘(TowerBootstrap이 채운다)")]
    [SerializeField] private Sprite[] potionIcons;
    [Tooltip("가방 한 칸에 쌓이는 최대 개수")]
    [SerializeField] private int potionStackSize = 5;
    [Tooltip("판을 시작할 때 받는 체력 포션(소). 영구 해금 단계마다 하나씩 더 받는다.")]
    [SerializeField] private int startPotions = 3;
    [SerializeField] private float potionDropChance = 0.07f;
    [SerializeField] private float smallHealFraction = 0.35f;
    [SerializeField] private float largeHealFraction = 0.7f;
    [SerializeField] private float manaRestoreFraction = 0.8f;
    [Tooltip("정화 포션: 상태이상에 걸리지 않는 시간")]
    [SerializeField] private float cleanseImmuneTime = 4f;
    [Tooltip("힘·신속의 물약 지속 시간")]
    [SerializeField] private float tonicDuration = 20f;
    [SerializeField] private float powerDamageBonus = 0.25f;
    [SerializeField] private float swiftSpeedBonus = 0.25f;
    [Tooltip("포션을 연달아 마실 수 없는 시간")]
    [SerializeField] private float potionCooldown = 0.6f;

    [Header("세트 효과(투구와 갑옷이 같은 계열일 때, 아래 수치는 전설 기준)")]
    [Tooltip("계열 이름(GearWarrior, GearShooter, GearMage, GearCommon 순서)")]
    [SerializeField] private string[] gearClassNames = { "전사", "사수", "마법사", "공용" };
    [Tooltip("두 장비 가운데 낮은 등급(일반, 희귀, 영웅, 전설)에 따라 세트 수치에 곱하는 값")]
    [SerializeField] private float[] setTierScale = { 0.4f, 0.6f, 0.8f, 1f };
    [SerializeField] private int warriorSetHp = 30;
    [SerializeField] private int warriorSetDefensePercent = 10;
    [Tooltip("근접 무기(검, 창, 도끼, 둔기)를 들었을 때 피해")]
    [SerializeField] private int warriorSetMeleeDamagePercent = 15;
    [SerializeField] private int shooterSetAttackSpeedPercent = 12;
    [SerializeField] private int shooterSetMoveSpeedPercent = 8;
    [SerializeField] private int shooterSetCritPercent = 10;
    [SerializeField] private int mageSetMana = 30;
    [SerializeField] private int mageSetCooldownPercent = 12;
    [SerializeField] private int mageSetSkillDamagePercent = 15;
    [Header("투구·갑옷 계열(파츠 번호 순서, TowerBootstrap이 그림 이름으로 채운다)")]
    [SerializeField] private byte[] helmetClasses = new byte[0];
    [SerializeField] private byte[] armorClasses = new byte[0];

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
        item.partsIndex = PickPart(ItemSlot.Weapon, (int)kind, rarity);
        item.rarity = GetFixedRarity(ItemSlot.Weapon, (int)kind, item.partsIndex);
        RollOptions(ref item);
        return item;
    }

    public ItemData CreateGear(ItemSlot slot, ItemRarity rarity)
    {
        ItemData item = new ItemData();
        item.exists = 1;
        item.slot = slot;
        item.partsIndex = PickPart(slot, 0, rarity);
        item.rarity = GetFixedRarity(slot, 0, item.partsIndex);
        RollOptions(ref item);
        return item;
    }

    // ------------------------------------------------------------ 고정 등급
    // 장비 등급은 무작위로 굴리지 않고 외형(부위·무기 종류·파츠 번호)마다 정해져 있다. 같은 장비는 언제나 같은 등급이다.
    // 파츠 목록을 고정된 순서로 섞은 뒤 앞에서부터 아래 비율대로 일반·희귀·영웅·전설을 나눈다.
    // 드랍은 등급을 먼저 뽑고(rarityWeights) 그 등급인 파츠 가운데 하나를 고른다.
    private readonly float[] rarityPartShare = { 0.5f, 0.28f, 0.15f, 0.07f };

    private int GetPartsCount(ItemSlot slot, int kind)
    {
        int count = 1;
        if (slot == ItemSlot.Weapon)
        {
            count = weaponPartsCounts[Mathf.Clamp(kind, 0, weaponPartsCounts.Length - 1)];
        }
        else if (slot == ItemSlot.Offhand)
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
        return Mathf.Max(1, count);
    }

    public ItemRarity GetFixedRarity(ItemSlot slot, int kind, int partsIndex)
    {
        int count = GetPartsCount(slot, kind);
        // 투구·갑옷은 계열(전사·사수·마법사)마다 따로 나눠, 어느 계열이든 일반부터 전설까지 고루 있게 한다.
        int gearClass = GetGearClass(slot, partsIndex);
        if (gearClass >= 0)
        {
            byte[] classes = GetClassTable(slot);
            int groupCount = 0;
            int groupIndex = 0;
            for (int i = 0; i < classes.Length; i++)
            {
                if (classes[i] == gearClass)
                {
                    if (i < partsIndex)
                    {
                        groupIndex++;
                    }
                    groupCount++;
                }
            }
            count = groupCount;
            partsIndex = groupIndex;
            kind = gearClass;
        }
        ItemRarity rarity = ItemRarity.Common;
        if (count > 1)
        {
            // count와 서로소인 보폭으로 건너뛰며 섞는다(부위·종류마다 시작점이 다르다).
            int step = count / 2 + 1;
            while (Gcd(step, count) != 1)
            {
                step++;
            }
            int offset = ((int)slot * 37 + kind * 11) % count;
            int rank = (int)(((long)partsIndex * step + offset) % count);
            float position = (rank + 0.5f) / count;
            float cumulative = 0f;
            bool found = false;
            for (int i = 0; i < rarityPartShare.Length && !found; i++)
            {
                cumulative += rarityPartShare[i];
                if (position < cumulative || i == rarityPartShare.Length - 1)
                {
                    rarity = (ItemRarity)i;
                    found = true;
                }
            }
        }
        return rarity;
    }

    // 시작 장비용: 그 등급인 파츠 가운데 번호가 가장 작은 것(언제나 같은 외형). 없으면 0.
    public ushort GetFirstPart(ItemSlot slot, int kind, ItemRarity rarity)
    {
        int count = GetPartsCount(slot, kind);
        int picked = -1;
        for (int i = 0; i < count && picked < 0; i++)
        {
            if (GetFixedRarity(slot, kind, i) == rarity)
            {
                picked = i;
            }
        }
        if (picked < 0)
        {
            picked = 0;
        }
        return (ushort)picked;
    }

    // 원하는 등급인 파츠를 무작위로 하나 고른다. 그 등급이 없으면 가장 가까운 낮은 등급, 그래도 없으면 아무 파츠.
    private ushort PickPart(ItemSlot slot, int kind, ItemRarity rarity)
    {
        int count = GetPartsCount(slot, kind);
        int start = Random.Range(0, count);
        int picked = -1;
        for (int want = (int)rarity; want >= 0 && picked < 0; want--)
        {
            for (int i = 0; i < count && picked < 0; i++)
            {
                int index = (start + i) % count;
                if ((int)GetFixedRarity(slot, kind, index) == want)
                {
                    picked = index;
                }
            }
        }
        if (picked < 0)
        {
            picked = start;
        }
        return (ushort)picked;
    }

    private int Gcd(int a, int b)
    {
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }
        return a;
    }

    // 부위를 무작위로 정하고, 뽑은 등급에 해당하는 파츠를 고른 장비 한 점(등급은 파츠마다 고정). rarityBonus가 클수록 높은 등급이 잘 나온다(엘리트, 보물방).
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
        // 계열 세트: 투구와 갑옷이 같은 계열이면 그 계열 효과를 받는다(직업과 상관없음). 크기는 둘 중 낮은 등급을 따른다.
        int setClass = GetSetClass(helmet, armor);
        if (setClass >= 0)
        {
            float scale = GetSetScale(helmet, armor);
            if (setClass == GearWarrior)
            {
                stats.maxHpBonus += Mathf.RoundToInt(warriorSetHp * scale);
                stats.damageTakenMultiplier *= 1f - warriorSetDefensePercent * scale * 0.01f;
                if (weapon.Exists && IsMeleeKind(weapon.kind))
                {
                    stats.damageMultiplier *= 1f + warriorSetMeleeDamagePercent * scale * 0.01f;
                }
            }
            else if (setClass == GearShooter)
            {
                stats.attackSpeedMultiplier *= 1f + shooterSetAttackSpeedPercent * scale * 0.01f;
                stats.moveSpeedMultiplier *= 1f + shooterSetMoveSpeedPercent * scale * 0.01f;
                stats.critChance += shooterSetCritPercent * scale * 0.01f;
            }
            else
            {
                stats.maxManaBonus += Mathf.RoundToInt(mageSetMana * scale);
                stats.cooldownMultiplier *= 1f - mageSetCooldownPercent * scale * 0.01f;
                stats.skillDamageMultiplier *= 1f + mageSetSkillDamagePercent * scale * 0.01f;
            }
        }
        stats.damageTakenMultiplier = Mathf.Max(0.2f, stats.damageTakenMultiplier);
        return stats;
    }

    public bool HasSet(in ItemData helmet, in ItemData armor)
    {
        return GetSetClass(helmet, armor) >= 0;
    }

    // 투구와 갑옷이 같은 계열이면 그 계열, 아니면 -1
    public int GetSetClass(in ItemData helmet, in ItemData armor)
    {
        int setClass = -1;
        if (helmet.Exists && armor.Exists)
        {
            int helmetClass = GetGearClass(helmet);
            if (helmetClass >= 0 && helmetClass != GearCommon && helmetClass == GetGearClass(armor))
            {
                setClass = helmetClass;
            }
        }
        return setClass;
    }

    // 세트 수치 배율: 두 장비 가운데 낮은 등급 기준
    public float GetSetScale(in ItemData helmet, in ItemData armor)
    {
        int tier = Mathf.Min((int)helmet.rarity, (int)armor.rarity);
        return setTierScale[Mathf.Clamp(tier, 0, setTierScale.Length - 1)];
    }

    // 투구·갑옷의 계열(GearWarrior 등). 무기·방패·포션은 -1.
    public int GetGearClass(in ItemData item)
    {
        int gearClass = -1;
        if (item.Exists)
        {
            gearClass = GetGearClass(item.slot, item.partsIndex);
        }
        return gearClass;
    }

    private int GetGearClass(ItemSlot slot, int partsIndex)
    {
        int gearClass = -1;
        byte[] classes = GetClassTable(slot);
        if (classes != null && partsIndex >= 0 && partsIndex < classes.Length)
        {
            gearClass = classes[partsIndex];
        }
        return gearClass;
    }

    private byte[] GetClassTable(ItemSlot slot)
    {
        byte[] classes = null;
        if (slot == ItemSlot.Helmet)
        {
            classes = helmetClasses;
        }
        else if (slot == ItemSlot.Armor)
        {
            classes = armorClasses;
        }
        return classes;
    }

    public string GetGearClassName(int gearClass)
    {
        string name = string.Empty;
        if (gearClass >= 0 && gearClass < gearClassNames.Length)
        {
            name = gearClassNames[gearClass];
        }
        return name;
    }

    // 근접 무기(검, 창, 도끼, 둔기)
    private bool IsMeleeKind(byte kind)
    {
        WeaponKind weaponKind = (WeaponKind)kind;
        return weaponKind == WeaponKind.Sword || weaponKind == WeaponKind.Spear || weaponKind == WeaponKind.Axe || weaponKind == WeaponKind.Blunt;
    }

    // 투구·갑옷 설명 끝에 붙는 세트 효과 안내
    private void AppendSetText(StringBuilder builder, in ItemData item)
    {
        int gearClass = GetGearClass(item);
        if (gearClass >= 0 && gearClass != GearCommon)
        {
            float scale = setTierScale[Mathf.Clamp((int)item.rarity, 0, setTierScale.Length - 1)];
            builder.Append("세트(").Append(GetGearClassName(gearClass)).Append(" 투구+갑옷, 낮은 등급 기준): ");
            if (gearClass == GearWarrior)
            {
                builder.Append("최대 체력 +").Append(Mathf.RoundToInt(warriorSetHp * scale));
                builder.Append(", 받는 피해 -").Append(Mathf.RoundToInt(warriorSetDefensePercent * scale)).Append('%');
                builder.Append(", 근접 무기 피해 +").Append(Mathf.RoundToInt(warriorSetMeleeDamagePercent * scale)).Append('%');
            }
            else if (gearClass == GearShooter)
            {
                builder.Append("공격 속도 +").Append(Mathf.RoundToInt(shooterSetAttackSpeedPercent * scale)).Append('%');
                builder.Append(", 이동 속도 +").Append(Mathf.RoundToInt(shooterSetMoveSpeedPercent * scale)).Append('%');
                builder.Append(", 치명타 확률 +").Append(Mathf.RoundToInt(shooterSetCritPercent * scale)).Append('%');
            }
            else
            {
                builder.Append("최대 마나 +").Append(Mathf.RoundToInt(mageSetMana * scale));
                builder.Append(", 스킬 재사용 대기 -").Append(Mathf.RoundToInt(mageSetCooldownPercent * scale)).Append('%');
                builder.Append(", 스킬 피해 +").Append(Mathf.RoundToInt(mageSetSkillDamagePercent * scale)).Append('%');
            }
            builder.Append('\n');
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

    public int ShopStockCount => shopStockCount;
    public int PotionKindCount => potionNames.Length;
    public int PotionStackSize => potionStackSize;
    public int StartPotions => startPotions;
    public float PotionDropChance => potionDropChance;
    public float SmallHealFraction => smallHealFraction;
    public float LargeHealFraction => largeHealFraction;
    public float ManaRestoreFraction => manaRestoreFraction;
    public float CleanseImmuneTime => cleanseImmuneTime;
    public float TonicDuration => tonicDuration;
    public float PowerDamageBonus => powerDamageBonus;
    public float SwiftSpeedBonus => swiftSpeedBonus;
    public float PotionCooldown => potionCooldown;
    public int MaxUpgradeLevel => maxUpgradeLevel;

    public ItemData CreatePotion(PotionKind kind, int count)
    {
        ItemData item = new ItemData();
        item.exists = 1;
        item.slot = ItemSlot.Potion;
        item.kind = (byte)kind;
        item.count = (byte)Mathf.Clamp(count, 1, potionStackSize);
        return item;
    }

    public PotionKind RollPotionKind()
    {
        float total = 0f;
        for (int i = 1; i < potionDropWeights.Length; i++)
        {
            total += potionDropWeights[i];
        }
        float roll = Random.value * total;
        PotionKind kind = PotionKind.HealthSmall;
        bool found = false;
        for (int i = 1; i < potionDropWeights.Length && !found; i++)
        {
            roll -= potionDropWeights[i];
            if (roll <= 0f)
            {
                kind = (PotionKind)i;
                found = true;
            }
        }
        return kind;
    }

    public int GetPotionPrice(PotionKind kind)
    {
        return potionPrices[(int)kind];
    }

    public string GetPotionName(PotionKind kind)
    {
        return potionNames[(int)kind];
    }

    public Sprite GetPotionIcon(PotionKind kind)
    {
        Sprite icon = null;
        if (potionIcons != null && (int)kind < potionIcons.Length)
        {
            icon = potionIcons[(int)kind];
        }
        return icon;
    }

    // 포션은 한 개 값으로 산다.
    public int GetBuyPrice(in ItemData item)
    {
        int price;
        if (item.slot == ItemSlot.Potion)
        {
            price = potionPrices[item.kind];
        }
        else
        {
            price = Mathf.RoundToInt(GetValue(item) * buyMultiplier);
        }
        return price;
    }

    // 포션은 쌓인 개수만큼 판다.
    public int GetSellPrice(in ItemData item)
    {
        int price;
        if (item.slot == ItemSlot.Potion)
        {
            price = Mathf.Max(1, Mathf.RoundToInt(potionPrices[item.kind] * sellMultiplier) * item.count);
        }
        else
        {
            price = Mathf.Max(1, Mathf.RoundToInt(GetValue(item) * sellMultiplier));
        }
        return price;
    }

    // 아이템 칸 테두리 색(강화 단계): 0 무쇠, +1 어두운 청동, +2 청동, +3 파랑, +4 밝은 하늘, +5 이상 금색.
    // 칸 바탕은 등급 색이고, 캐릭터 무기 빛(GetUpgradeTierColor)과는 따로 정한다.
    public Color GetUpgradeBorderColor(int level)
    {
        Color color = new Color(0.32f, 0.32f, 0.36f, 1f);
        if (level >= 5)
        {
            color = new Color(1f, 0.82f, 0.28f, 1f);
        }
        else if (level == 4)
        {
            color = new Color(0.55f, 0.88f, 1f, 1f);
        }
        else if (level == 3)
        {
            color = new Color(0.36f, 0.6f, 0.95f, 1f);
        }
        else if (level == 2)
        {
            color = new Color(0.88f, 0.6f, 0.32f, 1f);
        }
        else if (level == 1)
        {
            color = new Color(0.62f, 0.42f, 0.26f, 1f);
        }
        return color;
    }

    // 강화 단계 색: +1·2 흰색, +3·4 파랑, +5 금색. 0이면 투명(빛 없음).
    public Color GetUpgradeTierColor(int level)
    {
        Color color = new Color(1f, 1f, 1f, 0f);
        if (level >= 5)
        {
            color = new Color(1f, 0.8f, 0.25f, 1f);
        }
        else if (level >= 3)
        {
            color = new Color(0.4f, 0.7f, 1f, 1f);
        }
        else if (level >= 1)
        {
            color = new Color(0.95f, 0.95f, 0.95f, 1f);
        }
        return color;
    }

    public bool CanUpgrade(in ItemData item)
    {
        return item.Exists && item.slot != ItemSlot.Potion && item.level < maxUpgradeLevel;
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
    private readonly PartsType[] weaponPartsTypes =
    {
        PartsType.Sword, PartsType.Spear, PartsType.Bow, PartsType.Staff, PartsType.Axe,
        PartsType.Blunt, PartsType.Crossbow, PartsType.Gun, PartsType.Wand
    };

    public PartsType GetWeaponPartsType(WeaponKind kind)
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
        if (item.slot == ItemSlot.Potion)
        {
            builder.Append(potionNames[item.kind]);
            if (item.count > 1)
            {
                builder.Append(" x").Append(item.count);
            }
        }
        else
        {
            AppendGearName(builder, item);
        }
    }

    private void AppendGearName(StringBuilder builder, in ItemData item)
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
            AppendGearClassName(builder, item);
            builder.Append(helmetName);
        }
        else
        {
            AppendGearClassName(builder, item);
            builder.Append(armorName);
        }
        if (item.level > 0)
        {
            builder.Append(" +").Append(item.level);
        }
    }

    // 투구·갑옷 이름 앞의 계열("전사 투구")
    private void AppendGearClassName(StringBuilder builder, in ItemData item)
    {
        int gearClass = GetGearClass(item);
        if (gearClass >= 0)
        {
            builder.Append(GetGearClassName(gearClass)).Append(' ');
        }
    }

    // 이름, 기본 수치, 옵션을 여러 줄로 적는다. weapons는 무기 계열별 정의(피해 표시용).
    public void AppendDescription(StringBuilder builder, in ItemData item, WeaponDefinition[] weapons)
    {
        if (item.Exists && item.slot == ItemSlot.Potion)
        {
            AppendName(builder, item);
            builder.Append('\n');
            AppendPotionEffect(builder, (PotionKind)item.kind);
        }
        else if (item.Exists)
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
                AppendSetText(builder, item);
            }
        }
        else
        {
            builder.Append("(없음)\n");
        }
    }

    // 포션 효과 한 줄
    public void AppendPotionEffect(StringBuilder builder, PotionKind kind)
    {
        if (kind == PotionKind.HealthSmall)
        {
            builder.Append("체력을 ").Append(Mathf.RoundToInt(smallHealFraction * 100f)).Append("% 회복합니다.\n");
        }
        else if (kind == PotionKind.HealthLarge)
        {
            builder.Append("체력을 ").Append(Mathf.RoundToInt(largeHealFraction * 100f)).Append("% 회복합니다.\n");
        }
        else if (kind == PotionKind.Mana)
        {
            builder.Append("마나를 ").Append(Mathf.RoundToInt(manaRestoreFraction * 100f)).Append("% 회복합니다.\n");
        }
        else if (kind == PotionKind.Cleanse)
        {
            builder.Append("상태이상을 모두 풀고 ").Append(cleanseImmuneTime).Append("초 동안 걸리지 않습니다.\n");
        }
        else if (kind == PotionKind.Power)
        {
            builder.Append(tonicDuration).Append("초 동안 주는 피해 +").Append(Mathf.RoundToInt(powerDamageBonus * 100f)).Append("%\n");
        }
        else if (kind == PotionKind.Swift)
        {
            builder.Append(tonicDuration).Append("초 동안 이동 속도 +").Append(Mathf.RoundToInt(swiftSpeedBonus * 100f)).Append("%\n");
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
