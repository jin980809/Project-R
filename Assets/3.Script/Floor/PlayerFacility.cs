using System.Text;
using Mirror;
using UnityEngine;

// 탈것. 마구간 방에서 얻고 한 판 동안 유지된다.
public enum MountKind : byte
{
    None,
    // 말: 이동 속도 증가
    Horse,
    // 멧돼지: 최대 체력 증가
    Boar,
    // 올빼미: 최대 마나 증가
    Owl
}

// 방 시설과의 상호작용(F): 전직 제단(시작 방), 광맥, 낚시터, 마구간. 영구 해금 효과도 여기서 적용한다.
// 상점과 대장간은 화면이 따로 있어 PlayerInventory가 연다.
public class PlayerFacility : NetworkBehaviour
{
    private const byte FishIdle = 0;
    private const byte FishWaiting = 1;
    private const byte FishBite = 2;

    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerSkills skills;
    [SerializeField] private float range = 2f;
    [SerializeField] private Color promptColor = new Color(0.8f, 1f, 0.8f);

    [Header("광맥")]
    [SerializeField] private int mineCount = 3;
    [SerializeField] private int orePerMine = 1;

    [Header("낚시터")]
    [SerializeField] private int fishAttempts = 3;
    [SerializeField] private float biteDelayMin = 1f;
    [SerializeField] private float biteDelayMax = 3f;
    [SerializeField] private float biteWindow = 0.8f;
    [SerializeField] private float fishHealFraction = 0.2f;
    [SerializeField] private int fishGoldMin = 8;
    [SerializeField] private int fishGoldMax = 20;

    [Header("탈것(없음, 말, 멧돼지, 올빼미 순서)")]
    [SerializeField] private string[] mountNames = { "없음", "말", "멧돼지", "올빼미" };
    [SerializeField] private string[] mountEffects = { "", "이동 속도 +12%", "최대 체력 +20", "최대 마나 +15" };
    [SerializeField] private float horseSpeedFactor = 1.12f;
    [SerializeField] private int boarHpBonus = 20;
    [SerializeField] private int owlManaBonus = 15;

    [Header("영구 해금(단계마다)")]
    [SerializeField] private int metaHpPerLevel = 10;
    [SerializeField] private int metaGoldPerLevel = 30;

    [SyncVar(hook = nameof(OnMountChanged))] private MountKind mount;
    [SyncVar] private MountKind offeredMount;
    [SyncVar] private byte minesLeft;
    [SyncVar] private byte fishLeft;
    [SyncVar] private byte fishState;
    [SyncVar(hook = nameof(OnMetaHpChanged))] private byte metaHpLevel;

    private readonly StringBuilder builder = new StringBuilder(64);
    private GameHub hub;
    private bool promptShown;
    private int promptSignature = -1;
    private float fishTimer;
    private bool metaApplied;

    public MountKind Mount => mount;

    public string MountName
    {
        get
        {
            return mountNames[(int)mount];
        }
    }

    // ------------------------------------------------------------ 초기화(PlayerController가 부른다)

    public void ServerInit(GameHub sceneHub)
    {
        hub = sceneHub;
        mount = MountKind.None;
        offeredMount = MountKind.None;
        minesLeft = 0;
        fishLeft = 0;
        fishState = FishIdle;
        metaHpLevel = 0;
        metaApplied = false;
    }

    public void ClientInit(GameHub sceneHub)
    {
        hub = sceneHub;
    }

    // 본인 클라이언트가 처음 시작할 때: 전투 씬이면 자기 기기의 영구 해금 단계를 서버에 알린다.
    public void OwnerStart()
    {
        if (hub.IsCombatScene)
        {
            MetaProgress meta = hub.Meta;
            CmdApplyMeta(meta.GetUnlockLevel(0), meta.GetUnlockLevel(1), meta.GetUnlockLevel(2));
        }
    }

    private void OnMountChanged(MountKind oldValue, MountKind newValue)
    {
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
    }

    private void OnMetaHpChanged(byte oldValue, byte newValue)
    {
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
    }

    // 장비·직업 능력치 위에 탈것과 영구 해금을 더한다(PlayerInventory가 부른다).
    public void ModifyStats(ref EquipStats stats)
    {
        if (mount == MountKind.Horse)
        {
            stats.moveSpeedMultiplier *= horseSpeedFactor;
        }
        else if (mount == MountKind.Boar)
        {
            stats.maxHpBonus += boarHpBonus;
        }
        else if (mount == MountKind.Owl)
        {
            stats.maxManaBonus += owlManaBonus;
        }
        stats.maxHpBonus += metaHpPerLevel * metaHpLevel;
    }

    // ------------------------------------------------------------ 본인 조작

    private bool IsFacilityRoom(RoomType type)
    {
        return type == RoomType.Start || type == RoomType.Ore || type == RoomType.Fishing || type == RoomType.Stable;
    }

    // 매 프레임: 시설 가까이 있으면 안내를 보여 주고 F로 상호작용한다. 주울 아이템이 있으면 줍기가 먼저다.
    public void OwnerTick(GameInput input, bool blocked)
    {
        bool near = false;
        int station = -1;
        RoomType roomType = RoomType.Combat;
        FloorDirector floor = hub.FloorDirector;
        MetaUi metaUi = hub.MetaUi;
        if (floor != null)
        {
            if (!controller.IsDead && !inventory.HasNearbyDrop)
            {
                roomType = floor.CurrentRoomType;
                if (IsFacilityRoom(roomType))
                {
                    near = ((Vector2)transform.position - floor.FacilityPosition).sqrMagnitude <= range * range;
                }
            }
        }
        else if (metaUi != null)
        {
            // 로비 마을: 해금 상점, 도서관, 미용실
            if (!inventory.HasNearbyDrop)
            {
                station = metaUi.FindStation(transform.position);
            }
            // 시설에서 멀어지면 닫힌다(Esc는 PlayerController가 처리한다).
            if (metaUi.IsOpen && station < 0)
            {
                metaUi.Close();
            }
        }

        if (station >= 0)
        {
            int stationSignature = 1000 + station;
            if (!promptShown || stationSignature != promptSignature)
            {
                promptShown = true;
                promptSignature = stationSignature;
                builder.Clear();
                builder.Append("F  ").Append(metaUi.GetStationName(station));
                hub.Hud.SetPrompt(builder, promptColor);
            }
            if (!blocked && input.InteractPressed)
            {
                if (station == MetaUi.BarberStation)
                {
                    metaUi.Close();
                    hub.CustomizeUi.Open(controller, false);
                }
                else
                {
                    metaUi.Toggle(station);
                }
            }
        }
        else if (near)
        {
            // 안내 글자는 내용이 바뀔 때만 다시 만든다.
            int held = inventory.WeaponKindIndex;
            int signature = (int)roomType + 16 * minesLeft + 64 * fishLeft + 256 * fishState + 1024 * (int)mount
                + 4096 * (int)offeredMount + 16384 * skills.JobIndex + 65536 * held + 262144 * skills.GetMastery(held);
            if (!promptShown || signature != promptSignature)
            {
                promptShown = true;
                promptSignature = signature;
                BuildPrompt(roomType);
                hub.Hud.SetPrompt(builder, promptColor);
            }
            if (!blocked && input.InteractPressed)
            {
                CmdInteract(false);
            }
            else if (!blocked && input.InteractAltPressed && roomType == RoomType.Start)
            {
                CmdInteract(true);
            }
        }
        else if (promptShown)
        {
            promptShown = false;
            if (!inventory.HasNearbyDrop)
            {
                hub.Hud.ClearPrompt();
            }
        }
    }

    private void BuildPrompt(RoomType roomType)
    {
        builder.Clear();
        if (roomType == RoomType.Start)
        {
            int held = inventory.WeaponKindIndex;
            int first = skills.GetAltarTarget(false);
            int second = skills.GetAltarTarget(true);
            bool sameWeapon = (int)skills.Job.WeaponKind == held;
            bool ready = sameWeapon || skills.GetMastery(held) >= skills.MasteryRequired;
            if (!ready)
            {
                builder.Append(skills.GetJobName(first)).Append(" 숙련도 ").Append(skills.GetMastery(held)).Append('/')
                    .Append(skills.MasteryRequired).Append("  (그 무기를 들고 처치하면 오릅니다)");
            }
            else
            {
                builder.Append("전직 제단 (지금 ").Append(skills.Job.DisplayName).Append(')');
                if (first != skills.JobIndex)
                {
                    builder.Append("   F  ").Append(skills.GetJobName(first)).Append("(으)로 전직");
                }
                if (second >= 0 && second != skills.JobIndex)
                {
                    builder.Append("   G  ").Append(skills.GetJobName(second)).Append("(으)로 전직");
                }
                if (first == skills.JobIndex && (second < 0 || second == skills.JobIndex))
                {
                    builder.Append("  다른 계열 무기를 들고 오면 전직");
                }
            }
        }
        else if (roomType == RoomType.Ore)
        {
            if (minesLeft > 0)
            {
                builder.Append("F  광석 캐기 (남은 횟수 ").Append(minesLeft).Append(')');
            }
            else
            {
                builder.Append("광맥이 바닥났습니다");
            }
        }
        else if (roomType == RoomType.Fishing)
        {
            if (fishState == FishWaiting)
            {
                builder.Append("입질을 기다리는 중...");
            }
            else if (fishState == FishBite)
            {
                builder.Append("!!  지금 F");
            }
            else if (fishLeft > 0)
            {
                builder.Append("F  낚싯대 던지기 (남은 횟수 ").Append(fishLeft).Append(')');
            }
            else
            {
                builder.Append("더 낚을 수 없습니다");
            }
        }
        else if (offeredMount == MountKind.None || offeredMount == mount)
        {
            builder.Append("지금 탈것: ").Append(mountNames[(int)mount]);
        }
        else
        {
            builder.Append("F  ").Append(mountNames[(int)offeredMount]).Append(" 타기: ").Append(mountEffects[(int)offeredMount]);
            if (mount != MountKind.None)
            {
                builder.Append("  (지금 ").Append(mountNames[(int)mount]).Append(" 대신)");
            }
        }
    }

    // ------------------------------------------------------------ 서버

    // 방에 들어올 때 FloorDirector가 플레이어마다 부른다. 처음 들어온 방이면 사용 횟수를 채운다.
    public void ServerOnRoomEntered(bool firstVisit, RoomType roomType)
    {
        fishState = FishIdle;
        if (firstVisit)
        {
            if (roomType == RoomType.Ore)
            {
                minesLeft = (byte)mineCount;
            }
            else if (roomType == RoomType.Fishing)
            {
                fishLeft = (byte)fishAttempts;
            }
            else if (roomType == RoomType.Stable)
            {
                offeredMount = (MountKind)Random.Range(1, 4);
            }
        }
    }

    [Command]
    private void CmdApplyMeta(int hpLevel, int potionLevel, int goldLevel)
    {
        if (hub.IsCombatScene && !metaApplied)
        {
            metaApplied = true;
            metaHpLevel = (byte)Mathf.Clamp(hpLevel, 0, MetaProgress.MaxUnlockLevel);
            inventory.ServerSetBonusPotions(Mathf.Clamp(potionLevel, 0, MetaProgress.MaxUnlockLevel));
            inventory.ServerAddGold(metaGoldPerLevel * Mathf.Clamp(goldLevel, 0, MetaProgress.MaxUnlockLevel));
            inventory.RecomputeStats();
            controller.ServerHeal(controller.MaxHp);
        }
    }

    [Command]
    private void CmdInteract(bool alternate)
    {
        FloorDirector floor = hub.FloorDirector;
        if (floor != null && !controller.IsDead)
        {
            RoomType roomType = floor.CurrentRoomType;
            // 통신 지연을 감안해 거리는 조금 너그럽게 본다.
            float allowed = range + 0.5f;
            bool near = ((Vector2)transform.position - floor.FacilityPosition).sqrMagnitude <= allowed * allowed;
            if (near)
            {
                if (roomType == RoomType.Start)
                {
                    if (skills.ServerTryChangeJob(alternate))
                    {
                        TargetNotice(0);
                    }
                }
                else if (roomType == RoomType.Ore)
                {
                    if (minesLeft > 0)
                    {
                        minesLeft--;
                        inventory.ServerAddOre(orePerMine);
                    }
                }
                else if (roomType == RoomType.Fishing)
                {
                    ServerFish();
                }
                else if (roomType == RoomType.Stable)
                {
                    if (offeredMount != MountKind.None && offeredMount != mount)
                    {
                        mount = offeredMount;
                        inventory.RecomputeStats();
                    }
                }
            }
        }
    }

    // 낚시: 던지고(F) 입질이 올 때까지 기다렸다가, 입질이 온 짧은 시간 안에 다시 F를 누르면 성공이다.
    private void ServerFish()
    {
        if (fishState == FishIdle)
        {
            if (fishLeft > 0)
            {
                fishState = FishWaiting;
                fishTimer = Random.Range(biteDelayMin, biteDelayMax);
            }
        }
        else if (fishState == FishWaiting)
        {
            fishLeft--;
            fishState = FishIdle;
            TargetNotice(1);
        }
        else
        {
            fishLeft--;
            fishState = FishIdle;
            if (Random.value < 0.5f && controller.Hp < controller.MaxHp)
            {
                controller.ServerHeal(Mathf.RoundToInt(controller.MaxHp * fishHealFraction));
                TargetNotice(3);
            }
            else
            {
                inventory.ServerAddGold(Random.Range(fishGoldMin, fishGoldMax + 1));
                TargetNotice(4);
            }
        }
    }

    private void Update()
    {
        if (isServer && fishState != FishIdle)
        {
            fishTimer -= Time.deltaTime;
            if (fishTimer <= 0f)
            {
                if (fishState == FishWaiting)
                {
                    fishState = FishBite;
                    fishTimer = biteWindow;
                }
                else
                {
                    fishLeft--;
                    fishState = FishIdle;
                    TargetNotice(2);
                }
            }
        }
    }

    [TargetRpc]
    private void TargetNotice(byte code)
    {
        if (code == 0)
        {
            hub.Hud.ShowNotice("전직했습니다");
        }
        else if (code == 1)
        {
            hub.Hud.ShowNotice("너무 일찍 당겼습니다");
        }
        else if (code == 2)
        {
            hub.Hud.ShowNotice("물고기를 놓쳤습니다");
        }
        else if (code == 3)
        {
            hub.Hud.ShowNotice("물고기를 구워 먹어 체력을 회복했습니다");
        }
        else
        {
            hub.Hud.ShowNotice("값나가는 물고기를 낚아 골드를 얻었습니다");
        }
    }
}
