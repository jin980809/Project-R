using UnityEngine;

// 판이 끝나도 남는 진행 상황: 영혼석, 영구 해금, 도감, 설정. 기기에 저장한다(PlayerPrefs).
// GameHub가 씬마다 하나 만들어 Load하고, 씬이 끝날 때 Save한다.
public class MetaProgress
{
    public const int UnlockCount = 3;
    public const int MaxUnlockLevel = 3;
    public const int MonsterSlots = 8;
    public const int ItemSlotKinds = 4;
    public const int RarityKinds = 4;

    private const string SoulstoneKey = "ProjectR.Soulstone";
    private const string UnlockKey = "ProjectR.Unlock.";
    private const string MonsterKey = "ProjectR.Codex.Monster.";
    private const string ItemKey = "ProjectR.Codex.Item.";
    private const string ShakeKey = "ProjectR.Setting.Shake";
    private const string DamageTextKey = "ProjectR.Setting.DamageText";

    private int soulstones;
    private readonly int[] unlockLevels = new int[UnlockCount];
    private readonly int[] monsterKills = new int[MonsterSlots];
    private readonly bool[] itemFound = new bool[ItemSlotKinds * RarityKinds];
    private bool shakeEnabled = true;
    private bool damageTextEnabled = true;
    private bool dirty;

    public int Soulstones => soulstones;
    public bool ShakeEnabled => shakeEnabled;
    public bool DamageTextEnabled => damageTextEnabled;

    public void Load()
    {
        soulstones = PlayerPrefs.GetInt(SoulstoneKey, 0);
        for (int i = 0; i < UnlockCount; i++)
        {
            unlockLevels[i] = Mathf.Clamp(PlayerPrefs.GetInt(UnlockKey + i, 0), 0, MaxUnlockLevel);
        }
        for (int i = 0; i < MonsterSlots; i++)
        {
            monsterKills[i] = PlayerPrefs.GetInt(MonsterKey + i, 0);
        }
        for (int i = 0; i < itemFound.Length; i++)
        {
            itemFound[i] = PlayerPrefs.GetInt(ItemKey + i, 0) != 0;
        }
        shakeEnabled = PlayerPrefs.GetInt(ShakeKey, 1) != 0;
        damageTextEnabled = PlayerPrefs.GetInt(DamageTextKey, 1) != 0;
        dirty = false;
    }

    // 바뀐 것이 있을 때만 기기에 쓴다.
    public void Save()
    {
        if (dirty)
        {
            dirty = false;
            PlayerPrefs.SetInt(SoulstoneKey, soulstones);
            for (int i = 0; i < UnlockCount; i++)
            {
                PlayerPrefs.SetInt(UnlockKey + i, unlockLevels[i]);
            }
            for (int i = 0; i < MonsterSlots; i++)
            {
                PlayerPrefs.SetInt(MonsterKey + i, monsterKills[i]);
            }
            for (int i = 0; i < itemFound.Length; i++)
            {
                int value = 0;
                if (itemFound[i])
                {
                    value = 1;
                }
                PlayerPrefs.SetInt(ItemKey + i, value);
            }
            int shake = 0;
            if (shakeEnabled)
            {
                shake = 1;
            }
            int damageText = 0;
            if (damageTextEnabled)
            {
                damageText = 1;
            }
            PlayerPrefs.SetInt(ShakeKey, shake);
            PlayerPrefs.SetInt(DamageTextKey, damageText);
            PlayerPrefs.Save();
        }
    }

    public void AddSoulstones(int amount)
    {
        soulstones = Mathf.Max(0, soulstones + amount);
        dirty = true;
    }

    public int GetUnlockLevel(int index)
    {
        return unlockLevels[index];
    }

    // 영혼석이 충분하고 최대 단계가 아니면 한 단계 올린다.
    public bool TryBuyUnlock(int index, int cost)
    {
        bool bought = unlockLevels[index] < MaxUnlockLevel && soulstones >= cost;
        if (bought)
        {
            soulstones -= cost;
            unlockLevels[index]++;
            dirty = true;
        }
        return bought;
    }

    public int GetMonsterKills(int definitionIndex)
    {
        int kills = 0;
        if (definitionIndex >= 0 && definitionIndex < MonsterSlots)
        {
            kills = monsterKills[definitionIndex];
        }
        return kills;
    }

    public void RecordMonsterKill(int definitionIndex)
    {
        if (definitionIndex >= 0 && definitionIndex < MonsterSlots)
        {
            monsterKills[definitionIndex]++;
            dirty = true;
        }
    }

    public bool IsItemFound(int slot, int rarity)
    {
        return itemFound[slot * RarityKinds + rarity];
    }

    public void RecordItem(int slot, int rarity)
    {
        int index = slot * RarityKinds + rarity;
        if (!itemFound[index])
        {
            itemFound[index] = true;
            dirty = true;
        }
    }

    public void SetShakeEnabled(bool value)
    {
        shakeEnabled = value;
        dirty = true;
    }

    public void SetDamageTextEnabled(bool value)
    {
        damageTextEnabled = value;
        dirty = true;
    }
}
