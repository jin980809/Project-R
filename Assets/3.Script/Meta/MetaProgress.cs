using UnityEngine;

// 판이 끝나도 남는 진행 상황: 영혼석, 영구 해금, 도감, 설정. 기기에 저장한다(PlayerPrefs).
// GameHub가 씬마다 하나 만들어 Load하고, 씬이 끝날 때 Save한다.
public class MetaProgress
{
    // 영구 능력치판(로비 해금 상점). 앞의 셋(최대 체력, 포션, 시작 골드)은 예전 해금과 같은 번호라 저장값을 그대로 이어 쓴다.
    public const int UnlockHp = 0;
    public const int UnlockPotion = 1;
    public const int UnlockGold = 2;
    public const int UnlockDamage = 3;
    public const int UnlockDefense = 4;
    public const int UnlockMove = 5;
    public const int UnlockCrit = 6;
    public const int UnlockMana = 7;
    public const int UnlockCooldown = 8;
    public const int UnlockCount = 9;
    public const int MaxUnlockLevel = 5;
    // 단계 하나에 3비트씩(PackUnlockLevels)
    public const int UnlockBits = 3;
    // 초기화할 때 돌려받는 몫(쓴 영혼석의 %)
    public const int ResetRefundPercent = 80;
    public const int MonsterSlots = 8;
    public const int ItemSlotKinds = 4;
    public const int RarityKinds = 4;

    private const string SoulstoneKey = "ProjectR.Soulstone";
    private const string UnlockKey = "ProjectR.Unlock.";
    private const string MonsterKey = "ProjectR.Codex.Monster.";
    private const string ItemKey = "ProjectR.Codex.Item.";
    private const string ShakeKey = "ProjectR.Setting.Shake";
    private const string DamageTextKey = "ProjectR.Setting.DamageText";
    private const string VolumeKey = "ProjectR.Setting.Volume.";
    private const string LookPartsKey = "ProjectR.Look.Parts";
    private const string LookColorsKey = "ProjectR.Look.Colors";
    private const string KeyBindingKey = "ProjectR.Setting.Key.";
    private const string StartClassKey = "ProjectR.StartClass";
    // 음량은 0~10단계(전체, 효과음, 배경음 순서)
    public const int VolumeChannels = 3;
    public const int MaxVolumeStep = 10;

    private int soulstones;
    private readonly int[] unlockLevels = new int[UnlockCount];
    // 단계를 올리는 비용(영혼석). n단계에서 n+1단계로 갈 때 unlockCosts[n]
    private readonly int[] unlockCosts = { 15, 30, 50, 75, 105 };
    // 로비 전직 교관에서 고른 시작 계열(다음 판에도 기억한다)
    private int startClass;
    private readonly int[] monsterKills = new int[MonsterSlots];
    private readonly bool[] itemFound = new bool[ItemSlotKinds * RarityKinds];
    private bool shakeEnabled = true;
    private bool damageTextEnabled = true;
    private readonly int[] volumeSteps = { 8, 8, 6 };
    private bool dirty;
    private CharacterLook look;
    // 바꾼 키(GameInput 키 순서). 빈 문자열이면 기본 키.
    private readonly string[] keyBindings = new string[GameInput.RebindCount];

    public int Soulstones => soulstones;
    public int StartClass => startClass;

    public void SetStartClass(int value)
    {
        if (startClass != value)
        {
            startClass = value;
            dirty = true;
        }
    }
    // 처음 실행이면 false(커스터마이징 화면을 먼저 띄운다)
    public bool HasLook => look.valid != 0;
    public CharacterLook Look => look;

    public void SetLook(in CharacterLook value)
    {
        look = value;
        look.valid = 1;
        dirty = true;
    }
    public bool ShakeEnabled => shakeEnabled;
    public bool DamageTextEnabled => damageTextEnabled;

    public void Load()
    {
        soulstones = PlayerPrefs.GetInt(SoulstoneKey, 0);
        startClass = PlayerPrefs.GetInt(StartClassKey, 0);
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
        for (int i = 0; i < VolumeChannels; i++)
        {
            volumeSteps[i] = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey + i, volumeSteps[i]), 0, MaxVolumeStep);
        }
        look = new CharacterLook(PlayerPrefs.GetInt(LookPartsKey, 0), PlayerPrefs.GetInt(LookColorsKey, 0));
        for (int i = 0; i < keyBindings.Length; i++)
        {
            keyBindings[i] = PlayerPrefs.GetString(KeyBindingKey + i, string.Empty);
        }
        dirty = false;
    }

    // 바뀐 것이 있을 때만 기기에 쓴다.
    public void Save()
    {
        if (dirty)
        {
            dirty = false;
            PlayerPrefs.SetInt(SoulstoneKey, soulstones);
            PlayerPrefs.SetInt(StartClassKey, startClass);
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
            for (int i = 0; i < VolumeChannels; i++)
            {
                PlayerPrefs.SetInt(VolumeKey + i, volumeSteps[i]);
            }
            PlayerPrefs.SetInt(LookPartsKey, look.PackParts());
            PlayerPrefs.SetInt(LookColorsKey, look.PackColors());
            for (int i = 0; i < keyBindings.Length; i++)
            {
                PlayerPrefs.SetString(KeyBindingKey + i, keyBindings[i]);
            }
            PlayerPrefs.Save();
        }
    }

    public string GetKeyBinding(int index)
    {
        return keyBindings[index];
    }

    public void SetKeyBinding(int index, string path)
    {
        if (keyBindings[index] != path)
        {
            keyBindings[index] = path;
            dirty = true;
        }
    }

    // 저장해 둔 키를 입력에 되살린다(본인 플레이어가 생길 때).
    public void ApplyKeyBindings(GameInput input)
    {
        for (int i = 0; i < keyBindings.Length; i++)
        {
            input.ApplyOverridePath(i, keyBindings[i]);
        }
    }

    // 입력의 지금 키를 저장 목록에 옮긴다(설정 화면에서 바꾼 뒤).
    public void StoreKeyBindings(GameInput input)
    {
        for (int i = 0; i < keyBindings.Length; i++)
        {
            SetKeyBinding(i, input.GetOverridePath(i));
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

    // 지금 단계에서 한 단계 올리는 비용. 최대 단계면 0.
    public int GetUnlockCost(int index)
    {
        int cost = 0;
        int level = unlockLevels[index];
        if (level < MaxUnlockLevel)
        {
            cost = unlockCosts[Mathf.Min(level, unlockCosts.Length - 1)];
        }
        return cost;
    }

    // 능력치판에 지금까지 쓴 영혼석
    public int GetSpentSoulstones()
    {
        int spent = 0;
        for (int i = 0; i < UnlockCount; i++)
        {
            for (int level = 0; level < unlockLevels[i]; level++)
            {
                spent += unlockCosts[Mathf.Min(level, unlockCosts.Length - 1)];
            }
        }
        return spent;
    }

    public int GetResetRefund()
    {
        return GetSpentSoulstones() * ResetRefundPercent / 100;
    }

    // 능력치판을 모두 0단계로 되돌리고 쓴 영혼석의 일부를 돌려준다. 돌려준 양을 돌려준다.
    public int ResetUnlocks()
    {
        int refund = GetResetRefund();
        for (int i = 0; i < UnlockCount; i++)
        {
            unlockLevels[i] = 0;
        }
        soulstones += refund;
        dirty = true;
        return refund;
    }

    // 서버에 보낼 단계 묶음(능력치마다 UnlockBits비트)
    public int PackUnlockLevels()
    {
        int packed = 0;
        for (int i = 0; i < UnlockCount; i++)
        {
            packed |= unlockLevels[i] << (i * UnlockBits);
        }
        return packed;
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

    public int GetVolumeStep(int channel)
    {
        return volumeSteps[channel];
    }

    // 0~1 음량
    public float GetVolume01(int channel)
    {
        return volumeSteps[channel] / (float)MaxVolumeStep;
    }

    public void SetVolumeStep(int channel, int step)
    {
        volumeSteps[channel] = Mathf.Clamp(step, 0, MaxVolumeStep);
        dirty = true;
    }
}