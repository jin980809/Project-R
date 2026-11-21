using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

// 서버가 기억하는 "이 플레이어만 주울 수 있는" 바닥 아이템
public struct PendingDrop
{
    public int id;
    public ItemData item;
    public Vector2 position;
}

public enum InventoryNotice : byte
{
    BagFull,
    NoGold,
    NoOre,
    PotionFull,
    Busy,
    NoPotion,
    NotNeeded
}

// 플레이어의 장착 장비 4칸(무기, 보조, 투구, 갑옷)과 가방 12칸, 골드.
// 변경은 모두 서버(호스트)가 확정하고 목록이 동기화된다. 능력치 합계는 서버와 클라이언트가 같은 목록에서 각자 계산한다.
// 드랍은 개인별이다: 서버가 주인에게만 알려 주고, 주인 화면에만 보인다.
public class PlayerInventory : NetworkBehaviour
{
    public const int EquipSlotCount = 4;
    public const int BagSize = 12;
    public const int TotalSlotCount = EquipSlotCount + BagSize;

    [SerializeField] private PlayerController controller;
    [SerializeField] private CharacterVisual visual;
    [SerializeField] private PlayerSkills skills;
    [SerializeField] private PlayerFacility facility;
    [SerializeField] private ItemBalance balance;
    [Tooltip("WeaponKind 순서(검, 창, 활, 지팡이)")]
    [SerializeField] private WeaponDefinition[] weapons;
    [SerializeField] private float pickupRange = 1.2f;
    [SerializeField] private int maxPendingDrops = 24;
    [SerializeField] private float facilityRange = 2f;
    [Tooltip("갑옷을 입지 않았을 때 보이는 옷")]
    [SerializeField] private int defaultChestIndex;

    private readonly SyncList<ItemData> equipment = new SyncList<ItemData>();
    private readonly SyncList<ItemData> bag = new SyncList<ItemData>();
    [SyncVar] private int gold;
    [SyncVar] private int ore;
    // 포션 등록 칸 2개(키 1·2). 등록한 포션 종류를 가방에서 찾아 마신다.
    [SyncVar] private PotionKind quickPotion0 = PotionKind.HealthSmall;
    [SyncVar] private PotionKind quickPotion1;
    // 영구 해금으로 늘어난 포션 칸
    [SyncVar] private int bonusPotions;
    // 상점 진열품. 플레이어마다 따로 받는다.
    private readonly SyncList<ItemData> shopStock = new SyncList<ItemData>();

    private GameNetworkManager manager;
    private GameHub hub;
    private WeaponDefinition weapon;
    private EquipStats stats;
    private bool shieldActive;
    private bool visualReady;
    private int revision;

    // 본인 클라이언트
    private readonly StringBuilder promptBuilder = new StringBuilder(64);
    private int nearestDropId;

    // 서버
    private readonly List<PendingDrop> pendingDrops = new List<PendingDrop>(24);
    private int nextDropId = 1;
    private float nextPotionTime;

    public WeaponDefinition Weapon => weapon;
    public EquipStats Stats => stats;
    // 방패는 한손 무기(검)와 함께일 때만 쓸 수 있다.
    public bool ShieldActive => shieldActive;
    // 장비나 가방이 바뀔 때마다 1씩 늘어난다(UI가 바뀐 때만 다시 그리도록).
    public int Revision => revision;
    public int Gold => gold;
    public int Ore => ore;
    public bool HasNearbyDrop => nearestDropId != 0;
    public int ShopStockCount => shopStock.Count;

    public ItemData GetShopItem(int index)
    {
        ItemData item = new ItemData();
        if (index >= 0 && index < shopStock.Count)
        {
            item = shopStock[index];
        }
        return item;
    }
    public ItemBalance Balance => balance;
    public WeaponDefinition[] Weapons => weapons;

    public int WeaponKindIndex
    {
        get
        {
            int kind = 0;
            if (equipment.Count > 0)
            {
                kind = equipment[0].kind;
            }
            return kind;
        }
    }

    public ItemData GetEquipped(int index)
    {
        ItemData item = new ItemData();
        if (index >= 0 && index < equipment.Count)
        {
            item = equipment[index];
        }
        return item;
    }

    public ItemData GetBagItem(int index)
    {
        ItemData item = new ItemData();
        if (index >= 0 && index < bag.Count)
        {
            item = bag[index];
        }
        return item;
    }

    // 등록 칸(0, 1)에 넣어 둔 포션 종류
    public PotionKind GetQuickPotion(int slot)
    {
        PotionKind kind = quickPotion0;
        if (slot == 1)
        {
            kind = quickPotion1;
        }
        return kind;
    }

    // 가방에 든 그 종류 포션의 개수(여러 칸 합)
    public int CountPotions(PotionKind kind)
    {
        int count = 0;
        for (int i = 0; i < bag.Count; i++)
        {
            ItemData item = bag[i];
            if (item.Exists && item.slot == ItemSlot.Potion && item.kind == (byte)kind)
            {
                count += item.count;
            }
        }
        return count;
    }

    public WeaponDefinition GetWeaponDefinition(int kind)
    {
        return weapons[Mathf.Clamp(kind, 0, weapons.Length - 1)];
    }

    public Sprite GetIcon(in ItemData item)
    {
        Sprite icon;
        if (item.slot == ItemSlot.Potion)
        {
            icon = balance.GetPotionIcon((PotionKind)item.kind);
        }
        else
        {
            icon = visual.PartsManager.GetThumbnail(balance.GetPartsType(item), item.partsIndex);
        }
        return icon;
    }

    // ------------------------------------------------------------ 초기화(PlayerController가 순서를 정해 부른다)

    public void ServerInit(GameNetworkManager networkManager, GameHub sceneHub)
    {
        manager = networkManager;
        hub = sceneHub;
        int slot = controller.Slot;
        if (!manager.IsSlotKitReady(slot))
        {
            WriteStartKit(manager.GetSlotWeapon(slot));
        }
        for (int i = 0; i < EquipSlotCount; i++)
        {
            equipment.Add(manager.GetSlotItem(slot, i));
        }
        for (int i = 0; i < BagSize; i++)
        {
            bag.Add(manager.GetSlotItem(slot, EquipSlotCount + i));
        }
        gold = manager.GetSlotGold(slot);
        ore = 0;
        bonusPotions = 0;
        Recompute();
    }

    public void ClientInit(GameNetworkManager networkManager, GameHub sceneHub)
    {
        manager = networkManager;
        hub = sceneHub;
        visualReady = true;
        equipment.OnChange = OnEquipmentListChanged;
        bag.OnChange = OnBagListChanged;
        shopStock.OnChange = OnShopListChanged;
        Recompute();
    }

    private void OnEquipmentListChanged(SyncList<ItemData>.Operation operation, int index, ItemData item)
    {
        Recompute();
    }

    private void OnBagListChanged(SyncList<ItemData>.Operation operation, int index, ItemData item)
    {
        revision++;
        // 본인이 얻은 장비는 도감에 기록한다.
        if (isLocalPlayer && index >= 0 && index < bag.Count)
        {
            ItemData current = bag[index];
            if (current.Exists && current.slot != ItemSlot.Potion)
            {
                hub.Meta.RecordItem((int)current.slot, (int)current.rarity);
            }
        }
    }

    private void OnShopListChanged(SyncList<ItemData>.Operation operation, int index, ItemData item)
    {
        revision++;
    }

    // 장착 목록에서 무기 정의, 능력치 합계, 외형을 다시 계산한다.
    private void Recompute()
    {
        if (equipment.Count >= EquipSlotCount)
        {
            ItemData weaponItem = equipment[(int)ItemSlot.Weapon];
            ItemData offhand = equipment[(int)ItemSlot.Offhand];
            ItemData helmet = equipment[(int)ItemSlot.Helmet];
            ItemData armor = equipment[(int)ItemSlot.Armor];

            weapon = GetWeaponDefinition(weaponItem.kind);
            shieldActive = offhand.Exists && weapon.AllowsShield;
            ItemData activeOffhand = new ItemData();
            if (shieldActive)
            {
                activeOffhand = offhand;
            }
            stats = balance.ComputeStats(weaponItem, activeOffhand, helmet, armor);
            skills.ModifyStats(ref stats);
            facility.ModifyStats(ref stats);
            revision++;
            controller.OnEquipmentChanged();

            if (visualReady)
            {
                visual.EquipWeapon(balance.GetPartsType(weaponItem), weaponItem.partsIndex, shieldActive, offhand.partsIndex);
                visual.EquipGear(helmet.Exists, helmet.partsIndex, armor.Exists, armor.partsIndex, defaultChestIndex);
                // 강화 빛: 무기는 무기 단계, 몸 반짝이는 투구·갑옷·(쓰는) 방패 중 가장 높은 단계
                int gearLevel = Mathf.Max(helmet.level, armor.level);
                if (shieldActive)
                {
                    gearLevel = Mathf.Max(gearLevel, offhand.level);
                }
                visual.SetUpgradeGlow(weaponItem.level, balance.GetUpgradeTierColor(weaponItem.level), gearLevel, balance.GetUpgradeTierColor(gearLevel));
            }
        }
    }

    // 노드를 찍었거나 강화 스킬이 켜지고 꺼졌을 때 PlayerSkills가 부른다.
    public void RecomputeStats()
    {
        Recompute();
    }

    // 피해량 계산(서버). 장비 배율과 치명타를 적용한다.
    public HitInfo RollHit(float baseDamage)
    {
        HitInfo hit = new HitInfo();
        float damage = baseDamage * stats.damageMultiplier * controller.DamageDealtFactor * skills.ConditionalDamageFactor;
        if (Random.value < stats.critChance)
        {
            damage *= balance.CritDamageMultiplier + stats.critDamageBonus;
            hit.crit = true;
        }
        hit.damage = Mathf.Max(1, Mathf.RoundToInt(damage));
        return hit;
    }

    // 기본 공격: 무기에 붙은 상태이상을 함께 담는다.
    public HitInfo RollWeaponHit(WeaponDefinition usedWeapon, float factor)
    {
        HitInfo hit = RollHit(usedWeapon.Damage * factor);
        hit.status = usedWeapon.StatusKind;
        hit.statusDuration = usedWeapon.StatusDuration;
        hit.statusPower = usedWeapon.StatusPower;
        // 유물(불씨 부적, 서리 결정): 무기에 상태이상이 없으면 덧붙인다.
        if (hit.status == StatusKind.None)
        {
            StatusKind extra = facility.ExtraHitStatus;
            if (extra != StatusKind.None)
            {
                hit.status = extra;
                hit.statusDuration = 3f;
                hit.statusPower = 0.15f;
            }
        }
        return hit;
    }

    // ------------------------------------------------------------ 본인 조작

    // 매 프레임: 가까운 바닥 아이템 안내, F 줍기, Tab 가방, (시작 직업 선택은 PlayerSkills).
    public void OwnerTick(GameInput input, bool blocked)
    {
        ItemPickup nearest = hub.ItemDropPool.FindNearest(transform.position, pickupRange);
        int nearestId = 0;
        if (nearest != null)
        {
            nearestId = nearest.Id;
        }
        if (nearestId != nearestDropId)
        {
            nearestDropId = nearestId;
            if (nearest != null)
            {
                promptBuilder.Clear();
                promptBuilder.Append(input.GetKeyName(GameInput.KeyInteract)).Append("  ");
                balance.AppendName(promptBuilder, nearest.Item);
                promptBuilder.Append(" 줍기");
                hub.Hud.SetPrompt(promptBuilder, balance.GetRarityColor(nearest.Item.rarity));
            }
            else
            {
                hub.Hud.ClearPrompt();
            }
        }

        if (!blocked)
        {
            if (input.InteractPressed && nearestDropId != 0)
            {
                CmdPickup(nearestDropId);
            }
            int potionKey = input.PotionKeyPressed;
            if (potionKey >= 0 && hub.IsCombatScene)
            {
                // 가방에서 포션 칸에 마우스를 올린 채 누르면 그 포션을 이 칸에 등록한다.
                int hoveredPotion = hub.InventoryUi.HoveredPotionBagIndex;
                if (hoveredPotion >= 0)
                {
                    CmdSetQuickPotion(potionKey, (PotionKind)bag[hoveredPotion].kind);
                }
                else
                {
                    CmdUseQuickPotion(potionKey);
                }
            }
            // 상점·대장간 방에서 시설 가까이 F를 누르면 화면이 열린다(주울 것이 있으면 줍기가 먼저).
            if (input.InteractPressed && nearestDropId == 0 && hub.FloorDirector != null)
            {
                FloorDirector floor = hub.FloorDirector;
                RoomType roomType = floor.CurrentRoomType;
                bool facility = roomType == RoomType.Shop || roomType == RoomType.Forge;
                if (facility && ((Vector2)transform.position - floor.FacilityPosition).sqrMagnitude <= facilityRange * facilityRange)
                {
                    hub.FacilityUi.Open(roomType);
                    hub.InventoryUi.Open();
                }
            }
            if (input.InventoryPressed)
            {
                hub.InventoryUi.Toggle();
            }
        }
    }

    public void RequestEquip(int bagIndex)
    {
        CmdEquipFromBag(bagIndex);
    }

    public void RequestUnequip(int equipIndex)
    {
        CmdUnequip(equipIndex);
    }

    public void RequestDiscard(int bagIndex)
    {
        CmdDiscard(bagIndex);
    }

    public void RequestMoveBag(int fromIndex, int toIndex)
    {
        CmdMoveBag(fromIndex, toIndex);
    }

    public void RequestUnequipTo(int equipIndex, int bagIndex)
    {
        CmdUnequipTo(equipIndex, bagIndex);
    }

    public void RequestUseBagPotion(int bagIndex)
    {
        CmdUseBagPotion(bagIndex);
    }

    // ------------------------------------------------------------ 서버: 장비 변경

    private void ServerSetEquip(int index, ItemData item)
    {
        equipment[index] = item;
        manager.SetSlotItem(controller.Slot, index, item);
    }

    private void ServerSetBag(int index, ItemData item)
    {
        bag[index] = item;
        manager.SetSlotItem(controller.Slot, EquipSlotCount + index, item);
    }

    private int FindEmptyBagIndex()
    {
        int found = -1;
        for (int i = 0; i < bag.Count && found < 0; i++)
        {
            if (!bag[i].Exists)
            {
                found = i;
            }
        }
        return found;
    }

    // 시작 장비를 자리 저장소에 적는다. 검은 방패와 함께 시작한다.
    private void WriteStartKit(int kind)
    {
        int slot = controller.Slot;
        int clamped = Mathf.Clamp(kind, 0, weapons.Length - 1);
        ItemData empty = new ItemData();
        for (int i = 0; i < TotalSlotCount; i++)
        {
            manager.SetSlotItem(slot, i, empty);
        }
        ItemData weaponItem = balance.CreateWeapon((WeaponKind)clamped, ItemRarity.Common);
        // 등급은 외형마다 정해져 있으므로, 외형을 고정할 때도 일반 등급인 첫 외형을 쓰고 등급을 다시 맞춘다.
        weaponItem.partsIndex = balance.GetFirstPart(ItemSlot.Weapon, clamped, ItemRarity.Common);
        weaponItem.rarity = balance.GetFixedRarity(ItemSlot.Weapon, clamped, weaponItem.partsIndex);
        manager.SetSlotItem(slot, (int)ItemSlot.Weapon, weaponItem);
        if (weapons[clamped].AllowsShield)
        {
            ItemData shield = balance.CreateGear(ItemSlot.Offhand, ItemRarity.Common);
            shield.partsIndex = balance.GetFirstPart(ItemSlot.Offhand, 0, ItemRarity.Common);
            shield.rarity = balance.GetFixedRarity(ItemSlot.Offhand, 0, shield.partsIndex);
            manager.SetSlotItem(slot, (int)ItemSlot.Offhand, shield);
        }
        manager.SetSlotItem(slot, EquipSlotCount, balance.CreatePotion(PotionKind.HealthSmall, balance.StartPotions));
        manager.SetSlotKitReady(slot, true);
    }

    // 로비에서 시작 무기를 고른다(장비와 가방을 시작 상태로 되돌린다).
    public void ServerSetStartWeapon(int kind)
    {
        if (kind >= 0 && kind < weapons.Length)
        {
            int slot = controller.Slot;
            manager.SetSlotWeapon(slot, kind);
            WriteStartKit(kind);
            for (int i = 0; i < EquipSlotCount; i++)
            {
                equipment[i] = manager.GetSlotItem(slot, i);
            }
            for (int i = 0; i < BagSize; i++)
            {
                bag[i] = manager.GetSlotItem(slot, EquipSlotCount + i);
            }
            controller.ServerOnEquipmentSwapped();
            Recompute();
        }
    }

    // 주운 장비를 넣는다. 해당 부위가 비어 있으면 바로 장착하고, 아니면 가방의 빈칸에 넣는다.
    public bool ServerAddItem(ItemData item)
    {
        if (item.slot == ItemSlot.Potion)
        {
            return ServerAddPotion((PotionKind)item.kind, item.count);
        }
        bool added = false;
        int equipIndex = (int)item.slot;
        if (!equipment[equipIndex].Exists)
        {
            ServerSetEquip(equipIndex, item);
            Recompute();
            added = true;
        }
        else
        {
            int bagIndex = FindEmptyBagIndex();
            if (bagIndex >= 0)
            {
                ServerSetBag(bagIndex, item);
                added = true;
            }
        }
        return added;
    }

    // 포션을 가방에 넣는다. 같은 종류 칸에 먼저 쌓고 남으면 빈칸에 넣는다. 다 들어갈 자리가 없으면 넣지 않는다.
    public bool ServerAddPotion(PotionKind kind, int amount)
    {
        int stack = balance.PotionStackSize;
        int room = 0;
        for (int i = 0; i < bag.Count; i++)
        {
            ItemData item = bag[i];
            if (!item.Exists)
            {
                room += stack;
            }
            else if (item.slot == ItemSlot.Potion && item.kind == (byte)kind)
            {
                room += stack - item.count;
            }
        }
        bool added = amount > 0 && room >= amount;
        if (added)
        {
            int left = amount;
            for (int i = 0; i < bag.Count && left > 0; i++)
            {
                ItemData item = bag[i];
                if (item.Exists && item.slot == ItemSlot.Potion && item.kind == (byte)kind && item.count < stack)
                {
                    int put = Mathf.Min(left, stack - item.count);
                    item.count = (byte)(item.count + put);
                    left -= put;
                    ServerSetBag(i, item);
                }
            }
            for (int i = 0; i < bag.Count && left > 0; i++)
            {
                if (!bag[i].Exists)
                {
                    int put = Mathf.Min(left, stack);
                    left -= put;
                    ServerSetBag(i, balance.CreatePotion(kind, put));
                }
            }
            // 등록 칸에 없는 종류이고 빈 등록 칸이 있으면 알아서 등록한다.
            if (quickPotion0 != kind && quickPotion1 != kind)
            {
                if (quickPotion0 == PotionKind.None)
                {
                    quickPotion0 = kind;
                }
                else if (quickPotion1 == PotionKind.None)
                {
                    quickPotion1 = kind;
                }
            }
        }
        return added;
    }

    public void ServerAddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount);
        manager.SetSlotGold(controller.Slot, gold);
    }

    // 장착 중인 장비를 고쳐 쓴다(대장간 강화, 옵션 재설정).
    public void ServerReplaceEquipped(int index, ItemData item)
    {
        ServerSetEquip(index, item);
        Recompute();
    }

    [Command]
    private void CmdEquipFromBag(int bagIndex)
    {
        if (bagIndex >= 0 && bagIndex < bag.Count && bag[bagIndex].Exists && bag[bagIndex].slot != ItemSlot.Potion)
        {
            if (controller.ServerCanSwapEquipment())
            {
                ItemData item = bag[bagIndex];
                int equipIndex = (int)item.slot;
                ItemData previous = equipment[equipIndex];
                ServerSetEquip(equipIndex, item);
                ServerSetBag(bagIndex, previous);
                controller.ServerOnEquipmentSwapped();
                Recompute();
            }
            else
            {
                TargetNotice(InventoryNotice.Busy);
            }
        }
    }

    // 무기는 벗을 수 없다(다른 무기로 바꾸기만 가능).
    [Command]
    private void CmdUnequip(int equipIndex)
    {
        ServerUnequip(equipIndex);
    }

    // 가방의 빈 칸 중 앞쪽으로 내린다.
    private void ServerUnequip(int equipIndex)
    {
        if (equipIndex > 0 && equipIndex < equipment.Count && equipment[equipIndex].Exists)
        {
            if (controller.ServerCanSwapEquipment())
            {
                int bagIndex = FindEmptyBagIndex();
                if (bagIndex >= 0)
                {
                    ItemData item = equipment[equipIndex];
                    ServerSetEquip(equipIndex, new ItemData());
                    ServerSetBag(bagIndex, item);
                    controller.ServerOnEquipmentSwapped();
                    Recompute();
                }
                else
                {
                    TargetNotice(InventoryNotice.BagFull);
                }
            }
            else
            {
                TargetNotice(InventoryNotice.Busy);
            }
        }
    }

    // 끌어 놓기: 가방 칸끼리 자리를 바꾼다(빈 칸이면 옮기기만).
    [Command]
    private void CmdMoveBag(int fromIndex, int toIndex)
    {
        bool valid = fromIndex >= 0 && fromIndex < bag.Count && toIndex >= 0 && toIndex < bag.Count;
        if (valid && fromIndex != toIndex && bag[fromIndex].Exists)
        {
            ItemData moving = bag[fromIndex];
            ServerSetBag(fromIndex, bag[toIndex]);
            ServerSetBag(toIndex, moving);
        }
    }

    // 끌어 놓기: 장착한 장비를 가방의 그 칸으로 내린다. 그 칸에 같은 부위 장비가 있으면 서로 바꿔 끼고,
    // 다른 물건이 있으면 보통 해제처럼 앞쪽 빈 칸으로 내린다. 무기는 다른 무기와 바꾸기만 된다.
    [Command]
    private void CmdUnequipTo(int equipIndex, int bagIndex)
    {
        bool valid = equipIndex >= 0 && equipIndex < equipment.Count && bagIndex >= 0 && bagIndex < bag.Count;
        if (valid && equipment[equipIndex].Exists)
        {
            ItemData target = bag[bagIndex];
            bool swap = target.Exists && target.slot != ItemSlot.Potion && (int)target.slot == equipIndex;
            bool place = !target.Exists && equipIndex > 0;
            if (swap || place)
            {
                if (controller.ServerCanSwapEquipment())
                {
                    ItemData item = equipment[equipIndex];
                    ServerSetEquip(equipIndex, target);
                    ServerSetBag(bagIndex, item);
                    controller.ServerOnEquipmentSwapped();
                    Recompute();
                }
                else
                {
                    TargetNotice(InventoryNotice.Busy);
                }
            }
            else
            {
                ServerUnequip(equipIndex);
            }
        }
    }

    // 가방에서 버리면 발밑에 떨어진다(다시 주울 수 있다).
    [Command]
    private void CmdDiscard(int bagIndex)
    {
        if (bagIndex >= 0 && bagIndex < bag.Count && bag[bagIndex].Exists)
        {
            ItemData item = bag[bagIndex];
            ServerSetBag(bagIndex, new ItemData());
            ServerOfferDrop(item, (Vector2)transform.position + Random.insideUnitCircle * 0.4f);
        }
    }

    // ------------------------------------------------------------ 상점, 대장간, 포션

    public void RequestBuy(int stockIndex)
    {
        CmdBuy(stockIndex);
    }

    public void RequestBuyPotion(PotionKind kind)
    {
        CmdBuyPotion(kind);
    }

    public void RequestSell(int bagIndex)
    {
        CmdSell(bagIndex);
    }

    public void RequestUpgrade(int equipIndex)
    {
        CmdUpgrade(equipIndex);
    }

    public void RequestReroll(int equipIndex, int optionIndex)
    {
        CmdReroll(equipIndex, optionIndex);
    }


    private bool ServerIsInRoom(RoomType type)
    {
        FloorDirector floor = hub.FloorDirector;
        return floor != null && floor.CurrentRoomType == type;
    }

    // 상점 방에 처음 들어갔을 때 FloorDirector가 플레이어마다 부른다.
    public void ServerSetShopStock(float rarityBonus)
    {
        shopStock.Clear();
        for (int i = 0; i < balance.ShopStockCount; i++)
        {
            shopStock.Add(balance.CreateRandom(rarityBonus));
        }
    }

    public void ServerAddOre(int amount)
    {
        ore = Mathf.Max(0, ore + amount);
    }

    // 영구 해금: 포션 칸을 늘리고 가득 채운다.
    public void ServerSetBonusPotions(int amount)
    {
        // 이제 포션 칸 대신 판을 시작할 때 체력 포션(소)을 단계만큼 더 준다(한 판에 한 번).
        if (bonusPotions == 0 && amount > 0)
        {
            bonusPotions = amount;
            ServerAddPotion(PotionKind.HealthSmall, amount);
        }
    }

    // 층을 올라가면 포션이 다시 채워진다.
    public void ServerRefillPotions()
    {
        // 층마다 다시 채우지 않는다(시작 포션 + 상점·드랍만). 부르는 곳 없음.
    }

    [Command]
    private void CmdBuy(int stockIndex)
    {
        if (ServerIsInRoom(RoomType.Shop) && stockIndex >= 0 && stockIndex < shopStock.Count && shopStock[stockIndex].Exists)
        {
            ItemData item = shopStock[stockIndex];
            int price = balance.GetBuyPrice(item);
            if (gold < price)
            {
                TargetNotice(InventoryNotice.NoGold);
            }
            else if (ServerAddItem(item))
            {
                ServerAddGold(-price);
                shopStock[stockIndex] = new ItemData();
                facility.ServerFacilityAction(PlayerFacility.ActionShop);
            }
            else
            {
                TargetNotice(InventoryNotice.BagFull);
            }
        }
    }

    [Command]
    private void CmdBuyPotion(PotionKind kind)
    {
        if (ServerIsInRoom(RoomType.Shop) && kind != PotionKind.None && (int)kind < balance.PotionKindCount)
        {
            int price = balance.GetPotionPrice(kind);
            if (gold < price)
            {
                TargetNotice(InventoryNotice.NoGold);
            }
            else if (ServerAddPotion(kind, 1))
            {
                ServerAddGold(-price);
                facility.ServerFacilityAction(PlayerFacility.ActionShop);
            }
            else
            {
                TargetNotice(InventoryNotice.BagFull);
            }
        }
    }

    [Command]
    private void CmdSell(int bagIndex)
    {
        if (ServerIsInRoom(RoomType.Shop) && bagIndex >= 0 && bagIndex < bag.Count && bag[bagIndex].Exists)
        {
            ItemData item = bag[bagIndex];
            ServerSetBag(bagIndex, new ItemData());
            ServerAddGold(balance.GetSellPrice(item));
            facility.ServerFacilityAction(PlayerFacility.ActionShop);
        }
    }

    [Command]
    private void CmdUpgrade(int equipIndex)
    {
        if (ServerIsInRoom(RoomType.Forge) && equipIndex >= 0 && equipIndex < equipment.Count)
        {
            ItemData item = equipment[equipIndex];
            if (balance.CanUpgrade(item))
            {
                int cost = balance.GetUpgradeOreCost(item);
                if (ore >= cost)
                {
                    ore -= cost;
                    item.level++;
                    ServerReplaceEquipped(equipIndex, item);
                    facility.ServerFacilityAction(PlayerFacility.ActionForge);
                }
                else
                {
                    TargetNotice(InventoryNotice.NoOre);
                }
            }
        }
    }

    [Command]
    private void CmdReroll(int equipIndex, int optionIndex)
    {
        if (ServerIsInRoom(RoomType.Forge) && equipIndex >= 0 && equipIndex < equipment.Count)
        {
            ItemData item = equipment[equipIndex];
            if (balance.HasOption(item, optionIndex))
            {
                int cost = balance.GetRerollCost(item);
                if (gold >= cost)
                {
                    ServerAddGold(-cost);
                    balance.RerollOption(ref item, optionIndex);
                    if (item.rerollCount < 250)
                    {
                        item.rerollCount++;
                    }
                    ServerReplaceEquipped(equipIndex, item);
                    facility.ServerFacilityAction(PlayerFacility.ActionForge);
                }
                else
                {
                    TargetNotice(InventoryNotice.NoGold);
                }
            }
        }
    }

    [Command]
    private void CmdUseQuickPotion(int slot)
    {
        PotionKind kind = GetQuickPotion(slot);
        if (kind != PotionKind.None)
        {
            int bagIndex = -1;
            for (int i = 0; i < bag.Count && bagIndex < 0; i++)
            {
                if (bag[i].Exists && bag[i].slot == ItemSlot.Potion && bag[i].kind == (byte)kind)
                {
                    bagIndex = i;
                }
            }
            if (bagIndex >= 0)
            {
                ServerUsePotion(bagIndex);
            }
            else
            {
                TargetNotice(InventoryNotice.NoPotion);
            }
        }
    }

    [Command]
    private void CmdUseBagPotion(int bagIndex)
    {
        if (bagIndex >= 0 && bagIndex < bag.Count && bag[bagIndex].Exists && bag[bagIndex].slot == ItemSlot.Potion)
        {
            ServerUsePotion(bagIndex);
        }
    }

    // 등록 칸에 포션 종류를 넣는다. 다른 칸에 같은 종류가 있으면 두 칸을 서로 바꾼다.
    [Command]
    private void CmdSetQuickPotion(int slot, PotionKind kind)
    {
        if ((slot == 0 || slot == 1) && (int)kind < balance.PotionKindCount)
        {
            PotionKind previous = GetQuickPotion(slot);
            if (slot == 0)
            {
                quickPotion0 = kind;
                if (quickPotion1 == kind)
                {
                    quickPotion1 = previous;
                }
            }
            else
            {
                quickPotion1 = kind;
                if (quickPotion0 == kind)
                {
                    quickPotion0 = previous;
                }
            }
        }
    }

    // 가방 칸의 포션 하나를 마신다(서버). 효과가 없을 때(체력이 가득 찼는데 체력 포션)는 아끼고 알린다.
    private void ServerUsePotion(int bagIndex)
    {
        if (controller.IsDead || Time.time < nextPotionTime)
        {
            return;
        }
        ItemData item = bag[bagIndex];
        PotionKind kind = (PotionKind)item.kind;
        bool used = true;
        if (kind == PotionKind.HealthSmall || kind == PotionKind.HealthLarge)
        {
            used = controller.Hp < controller.MaxHp;
            if (used)
            {
                float fraction = balance.SmallHealFraction;
                if (kind == PotionKind.HealthLarge)
                {
                    fraction = balance.LargeHealFraction;
                }
                controller.ServerHeal(Mathf.RoundToInt(controller.MaxHp * fraction));
            }
        }
        else if (kind == PotionKind.Mana)
        {
            used = controller.Mana < controller.MaxMana;
            if (used)
            {
                controller.ServerRestoreMana(Mathf.RoundToInt(controller.MaxMana * balance.ManaRestoreFraction));
            }
        }
        else if (kind == PotionKind.Cleanse)
        {
            controller.ServerCleanse(balance.CleanseImmuneTime);
        }
        else
        {
            controller.ServerApplyTonic(kind, balance.TonicDuration);
        }

        if (used)
        {
            nextPotionTime = Time.time + balance.PotionCooldown;
            if (item.count > 1)
            {
                item.count--;
                ServerSetBag(bagIndex, item);
            }
            else
            {
                ServerSetBag(bagIndex, new ItemData());
            }
            RpcPotionUsed(kind);
        }
        else
        {
            TargetNotice(InventoryNotice.NotNeeded);
        }
    }

    // 마시는 소리와 몸의 반짝임(모두에게)
    [ClientRpc]
    private void RpcPotionUsed(PotionKind kind)
    {
        hub.Sound.PlayAt(SoundId.PotionUse, transform.position);
    }

    // ------------------------------------------------------------ 서버: 개인 드랍

    // 몬스터가 죽었을 때 서버에서 플레이어마다 따로 호출한다.
    public void ServerOnMonsterKilled(Vector2 position, float dropChance, float rarityBonus, int goldAmount)
    {
        ServerAddGold(goldAmount);
        if (Random.value < dropChance)
        {
            ServerOfferDrop(balance.CreateRandom(rarityBonus), position + Random.insideUnitCircle * 0.3f);
        }
        if (Random.value < balance.PotionDropChance)
        {
            ServerOfferDrop(balance.CreatePotion(balance.RollPotionKind(), 1), position + Random.insideUnitCircle * 0.3f);
        }
    }

    public void ServerOfferDrop(ItemData item, Vector2 position)
    {
        if (pendingDrops.Count >= maxPendingDrops)
        {
            TargetRemoveDrop(pendingDrops[0].id);
            pendingDrops.RemoveAt(0);
        }
        PendingDrop drop = new PendingDrop();
        drop.id = nextDropId;
        drop.item = item;
        drop.position = position;
        nextDropId++;
        pendingDrops.Add(drop);
        TargetAddDrop(drop.id, item, position);
    }

    [Command]
    private void CmdPickup(int dropId)
    {
        int found = -1;
        for (int i = 0; i < pendingDrops.Count && found < 0; i++)
        {
            if (pendingDrops[i].id == dropId)
            {
                found = i;
            }
        }
        if (found >= 0 && !controller.IsDead)
        {
            PendingDrop drop = pendingDrops[found];
            float allowed = pickupRange + 1f;
            if ((drop.position - (Vector2)transform.position).sqrMagnitude <= allowed * allowed)
            {
                if (ServerAddItem(drop.item))
                {
                    pendingDrops.RemoveAt(found);
                    TargetRemoveDrop(dropId);
                }
                else
                {
                    TargetNotice(InventoryNotice.BagFull);
                }
            }
        }
    }

    [TargetRpc]
    private void TargetAddDrop(int dropId, ItemData item, Vector2 position)
    {
        hub.ItemDropPool.Show(dropId, item, position, GetIcon(item), balance.GetRarityColor(item.rarity));
    }

    [TargetRpc]
    private void TargetRemoveDrop(int dropId)
    {
        hub.ItemDropPool.Hide(dropId);
    }

    // 방을 옮기면 바닥에 남은 것은 사라진다.
    public void ServerClearDrops()
    {
        if (pendingDrops.Count > 0)
        {
            pendingDrops.Clear();
            TargetClearDrops();
        }
    }

    [TargetRpc]
    private void TargetClearDrops()
    {
        hub.ItemDropPool.HideAll();
    }

    [TargetRpc]
    private void TargetNotice(InventoryNotice notice)
    {
        if (notice == InventoryNotice.BagFull)
        {
            hub.Hud.ShowNotice("가방이 가득 찼습니다");
        }
        else if (notice == InventoryNotice.NoGold)
        {
            hub.Hud.ShowNotice("골드가 부족합니다");
        }
        else if (notice == InventoryNotice.NoOre)
        {
            hub.Hud.ShowNotice("광석이 부족합니다");
        }
        else if (notice == InventoryNotice.PotionFull)
        {
            hub.Hud.ShowNotice("포션을 더 들 수 없습니다");
        }
        else if (notice == InventoryNotice.NoPotion)
        {
            hub.Hud.ShowNotice("등록한 포션이 없습니다");
        }
        else if (notice == InventoryNotice.NotNeeded)
        {
            hub.Hud.ShowNotice("지금은 마실 필요가 없습니다");
        }
        else
        {
            hub.Hud.ShowNotice("지금은 장비를 바꿀 수 없습니다");
        }
    }
}
