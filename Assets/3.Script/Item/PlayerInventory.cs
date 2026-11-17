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
    Busy
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
    [SyncVar] private int potions;
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

    public WeaponDefinition Weapon => weapon;
    public EquipStats Stats => stats;
    // 방패는 한손 무기(검)와 함께일 때만 쓸 수 있다.
    public bool ShieldActive => shieldActive;
    // 장비나 가방이 바뀔 때마다 1씩 늘어난다(UI가 바뀐 때만 다시 그리도록).
    public int Revision => revision;
    public int Gold => gold;
    public int Ore => ore;
    public int Potions => potions;
    public int MaxPotions => balance.MaxPotions + bonusPotions;
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

    public WeaponDefinition GetWeaponDefinition(int kind)
    {
        return weapons[Mathf.Clamp(kind, 0, weapons.Length - 1)];
    }

    public Sprite GetIcon(in ItemData item)
    {
        return visual.PartsManager.GetThumbnail(balance.GetPartsType(item), item.partsIndex);
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
        potions = MaxPotions;
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
            if (current.Exists)
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
        float damage = baseDamage * stats.damageMultiplier * controller.DamageDealtFactor;
        if (Random.value < stats.critChance)
        {
            damage *= balance.CritDamageMultiplier;
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
        return hit;
    }

    // ------------------------------------------------------------ 본인 조작

    // 매 프레임: 가까운 바닥 아이템 안내, F 줍기, Tab 가방, 로비에서는 1~4로 시작 무기 선택.
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
                promptBuilder.Append("F  ");
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
            if (input.PotionPressed)
            {
                CmdUsePotion();
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
            if (!hub.IsCombatScene)
            {
                int pick = input.WeaponKeyPressed;
                if (pick >= 0 && pick < weapons.Length && pick != WeaponKindIndex)
                {
                    CmdSetStartWeapon(pick);
                }
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
        weaponItem.partsIndex = 0;
        manager.SetSlotItem(slot, (int)ItemSlot.Weapon, weaponItem);
        if (weapons[clamped].AllowsShield)
        {
            ItemData shield = balance.CreateGear(ItemSlot.Offhand, ItemRarity.Common);
            shield.partsIndex = 0;
            manager.SetSlotItem(slot, (int)ItemSlot.Offhand, shield);
        }
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
                if (bag[i].Exists)
                {
                    bag[i] = new ItemData();
                }
            }
            controller.ServerOnEquipmentSwapped();
            Recompute();
        }
    }

    // 주운 장비를 넣는다. 해당 부위가 비어 있으면 바로 장착하고, 아니면 가방의 빈칸에 넣는다.
    public bool ServerAddItem(ItemData item)
    {
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
    private void CmdSetStartWeapon(int kind)
    {
        if (!hub.IsCombatScene)
        {
            ServerSetStartWeapon(kind);
        }
    }

    [Command]
    private void CmdEquipFromBag(int bagIndex)
    {
        if (bagIndex >= 0 && bagIndex < bag.Count && bag[bagIndex].Exists)
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

    public void RequestBuyPotion()
    {
        CmdBuyPotion();
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

    public void RequestUsePotion()
    {
        CmdUsePotion();
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
        bonusPotions = amount;
        potions = MaxPotions;
    }

    // 층을 올라가면 포션이 다시 채워진다.
    public void ServerRefillPotions()
    {
        potions = MaxPotions;
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
            }
            else
            {
                TargetNotice(InventoryNotice.BagFull);
            }
        }
    }

    [Command]
    private void CmdBuyPotion()
    {
        if (ServerIsInRoom(RoomType.Shop))
        {
            if (potions >= MaxPotions)
            {
                TargetNotice(InventoryNotice.PotionFull);
            }
            else if (gold < balance.PotionPrice)
            {
                TargetNotice(InventoryNotice.NoGold);
            }
            else
            {
                ServerAddGold(-balance.PotionPrice);
                potions++;
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
                }
                else
                {
                    TargetNotice(InventoryNotice.NoGold);
                }
            }
        }
    }

    [Command]
    private void CmdUsePotion()
    {
        if (potions > 0 && !controller.IsDead && controller.Hp < controller.MaxHp)
        {
            potions--;
            controller.ServerHeal(Mathf.RoundToInt(controller.MaxHp * balance.PotionHealFraction));
        }
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
        else
        {
            hub.Hud.ShowNotice("지금은 장비를 바꿀 수 없습니다");
        }
    }
}
