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

    // 시설 사용 연출 종류(RpcFacilityAction)
    public const byte ActionAltar = 0;
    public const byte ActionMine = 1;
    public const byte ActionForge = 2;
    public const byte ActionShop = 3;
    // 탈것 종류(말, 멧돼지, 물범)마다 패키지 탈것 파츠 번호(0 검정, 1 갈색, 2 파랑)
    private readonly int[] mountPartsIndex = { -1, 1, 0, 2 };

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
    [SerializeField] private string[] mountNames = { "없음", "말", "멧돼지", "물범" };
    [SerializeField] private string[] mountEffects = { "", "이동 속도 +12%", "최대 체력 +20", "최대 마나 +15" };
    [SerializeField] private float horseSpeedFactor = 1.12f;
    [SerializeField] private int boarHpBonus = 20;
    [SerializeField] private int owlManaBonus = 15;
    [Tooltip("Shift로 말에 타 있는 동안의 이동 속도 배수")]
    [SerializeField] private float rideSpeedFactor = 1.6f;

    [Header("영구 해금(단계마다)")]
    [SerializeField] private int metaHpPerLevel = 10;
    [SerializeField] private int metaGoldPerLevel = 30;
    [Tooltip("능력치판 단계마다: 공격력 배율, 받는 피해 감소, 이동 속도, 치명타 확률, 최대 마나, 재사용 대기 감소")]
    [SerializeField] private float metaDamagePerLevel = 0.03f;
    [SerializeField] private float metaDefensePerLevel = 0.03f;
    [SerializeField] private float metaMovePerLevel = 0.02f;
    [SerializeField] private float metaCritPerLevel = 0.02f;
    [SerializeField] private int metaManaPerLevel = 6;
    [SerializeField] private float metaCooldownPerLevel = 0.03f;

    [SyncVar(hook = nameof(OnMountChanged))] private MountKind mount;
    [SyncVar] private MountKind offeredMount;
    // Shift로 탄 상태. 마구간에서 얻은 탈것이 없으면 말을 탄다. 공격·스킬·구르기·막기·시설 사용·사망 때 내린다.
    [SyncVar(hook = nameof(OnRidingChanged))] private bool riding;
    [SyncVar] private byte minesLeft;
    [SyncVar] private byte fishLeft;
    [SyncVar(hook = nameof(OnFishStateChanged))] private byte fishState;
    // 능력치판 단계 묶음(MetaProgress.PackUnlockLevels). 판이 시작될 때 본인 기기에서 받는다.
    [SyncVar(hook = nameof(OnMetaLevelsChanged))] private int metaLevels;

    // ------------------------------------------------------------ 유물(한 판 동안 유지되는 강화)
    // 보스·엘리트 방을 깨면 세 개 중 하나를 고른다. 같은 유물을 또 고르면 효과가 겹친다.
    public const int RelicTypeCount = 16;
    private readonly string[] relicNames =
    {
        "광전사의 반지", "바람 깃털", "쌍날 손잡이", "매의 눈", "거인의 심장", "수정 목걸이", "시간의 모래", "마법 서적",
        "강철 피부", "흡혈 송곳니", "불씨 부적", "서리 결정", "피의 서약", "연쇄 부적", "마나 샘", "황금 주머니"
    };
    private readonly string[] relicDescriptions =
    {
        "주는 피해 +15%", "이동 속도 +12%", "공격 속도 +15%", "치명타 확률 +10%", "최대 체력 +30", "최대 마나 +25",
        "스킬 재사용 대기 -15%", "스킬 피해 +20%", "받는 피해 -12%", "치명타를 넣으면\n체력 3 회복",
        "기본 공격이\n화상을 건다", "기본 공격이\n둔화를 건다", "주는 피해 +30%\n받는 피해 +15%",
        "연계 피해 +60%\n(출혈 폭발, 열충격, 파쇄)", "타격마다 마나 +2", "골드 +120"
    };
    private readonly Color[] relicColors =
    {
        new Color(1f, 0.45f, 0.4f), new Color(0.6f, 1f, 0.8f), new Color(1f, 0.8f, 0.45f), new Color(1f, 0.95f, 0.5f),
        new Color(1f, 0.5f, 0.6f), new Color(0.55f, 0.75f, 1f), new Color(0.95f, 0.85f, 0.6f), new Color(0.75f, 0.6f, 1f),
        new Color(0.75f, 0.8f, 0.9f), new Color(0.95f, 0.3f, 0.35f), new Color(1f, 0.6f, 0.25f), new Color(0.55f, 0.9f, 1f),
        new Color(0.85f, 0.15f, 0.25f), new Color(0.9f, 0.55f, 1f), new Color(0.45f, 0.65f, 1f), new Color(1f, 0.82f, 0.3f)
    };

    public string RelicName(int relic)
    {
        return relicNames[relic];
    }

    public string RelicDescription(int relic)
    {
        return relicDescriptions[relic];
    }

    public Color RelicColor(int relic)
    {
        return relicColors[relic];
    }

    private const int RelicDamage = 0;
    private const int RelicMove = 1;
    private const int RelicAttackSpeed = 2;
    private const int RelicCrit = 3;
    private const int RelicHp = 4;
    private const int RelicMana = 5;
    private const int RelicCooldown = 6;
    private const int RelicSkillDamage = 7;
    private const int RelicDefense = 8;
    private const int RelicLifesteal = 9;
    private const int RelicEmber = 10;
    private const int RelicFrost = 11;
    private const int RelicBloodOath = 12;
    private const int RelicCombo = 13;
    private const int RelicManaSpring = 14;
    private const int RelicGold = 15;
    private const int OfferValid = 1 << 15;

    // 유물 종류마다 가진 개수(4비트씩 16종). 값이 바뀌면 능력치를 다시 계산한다.
    [SyncVar(hook = nameof(OnRelicsChanged))] private ulong relicCounts;
    // 지금 고를 수 있는 세 유물(5비트씩) + 유효 표시. 0이면 고를 것이 없다.
    [SyncVar(hook = nameof(OnRelicOfferChanged))] private int relicOffer;
    private int pendingOffers;
    private RelicUi relicUi;
    // 다음 층 포탈 확인 창: 수락 콜백(한 번만 만든다)과, 이번에 포탈 앞에 선 동안 창을 이미 띄웠는지
    // (취소하면 포탈에서 떨어졌다가 다시 와야 또 뜬다)
    private System.Action portalYes;
    private bool portalPromptDone;

    private readonly StringBuilder builder = new StringBuilder(64);
    private GameHub hub;
    private bool promptShown;
    private int promptSignature = -1;
    private float fishTimer;
    private bool metaApplied;

    public MountKind Mount => mount;
    public bool Riding => riding;

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
        riding = false;
        minesLeft = 0;
        fishLeft = 0;
        fishState = FishIdle;
        metaLevels = 0;
        metaApplied = false;
        relicCounts = 0;
        relicOffer = 0;
        pendingOffers = 0;
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
            CmdApplyMeta(meta.PackUnlockLevels());
        }
    }

    private void OnMountChanged(MountKind oldValue, MountKind newValue)
    {
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
        ApplyMountVisual();
    }

    private void OnRidingChanged(bool oldValue, bool newValue)
    {
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
        ApplyMountVisual();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyMountVisual();
    }

    // 탈것 그림은 타 있을 때만 보인다. 마구간에서 얻은 탈것이 없으면 말(갈색)을 탄다.
    private void ApplyMountVisual()
    {
        if (controller != null && controller.Visual != null)
        {
            int partsIndex = -1;
            if (riding)
            {
                MountKind shown = mount;
                if (shown == MountKind.None)
                {
                    shown = MountKind.Horse;
                }
                partsIndex = mountPartsIndex[Mathf.Clamp((int)shown, 0, mountPartsIndex.Length - 1)];
            }
            controller.Visual.SetMount(partsIndex);
        }
    }

    // 본인: Shift로 타고 내린다.
    [Command]
    public void CmdSetRiding(bool value)
    {
        if (!value || !controller.IsDead)
        {
            riding = value;
        }
    }

    // 서버: 죽거나 시설을 쓰면 내린다.
    public void ServerDismount()
    {
        riding = false;
    }

    // ------------------------------------------------------------ 시설 연출

    // 서버: 시설을 썼을 때(구매, 강화, 채굴, 전직) 모두에게 시설 애니메이션과 캐릭터 동작을 보여 준다.
    public void ServerFacilityAction(byte action)
    {
        riding = false;
        RpcFacilityAction(action);
    }

    [ClientRpc]
    private void RpcFacilityAction(byte action)
    {
        FloorDirector floor = null;
        if (hub != null)
        {
            floor = hub.FloorDirector;
        }
        if (floor != null && floor.FacilityView != null)
        {
            floor.FacilityView.PlayAction();
        }
        if (action == ActionMine || action == ActionForge)
        {
            controller.Visual.PoseStrike(action == ActionMine);
        }
    }

    private void OnFishStateChanged(byte oldValue, byte newValue)
    {
        if (newValue == FishWaiting)
        {
            controller.Visual.PoseFishCast();
            ShowFishFx(1);
        }
        else if (newValue == FishBite)
        {
            controller.Visual.PoseFishBite();
            ShowFishFx(2);
        }
        else
        {
            ShowFishFx(0);
        }
    }

    // 낚시가 끝났을 때: 잡았으면 끌어올리는 동작과 물보라, 아니면 자세를 푼다.
    [ClientRpc]
    private void RpcFishEnd(bool caught)
    {
        ShowFishFx(0);
        if (caught)
        {
            controller.Visual.PoseFishCatch();
            ShowFishFx(3);
        }
        else
        {
            controller.Visual.EndPose(true);
        }
    }

    private FrameAnimator fishFx;
    private int fishFxMode;
    private float fishFxDelay;

    // 0 없음, 1 찌가 떠 있음, 2 입질, 3 물보라(한 번)
    private void ShowFishFx(int mode)
    {
        FloorDirector floor = null;
        if (hub != null)
        {
            floor = hub.FloorDirector;
        }
        FacilityView view = null;
        if (floor != null)
        {
            view = floor.FacilityView;
        }
        if (view == null)
        {
            return;
        }
        if (fishFx == null)
        {
            GameObject fxObject = new GameObject("FishFx");
            SpriteRenderer fxRenderer = fxObject.AddComponent<SpriteRenderer>();
            fxRenderer.sortingOrder = -840;
            fishFx = fxObject.AddComponent<FrameAnimator>();
            fishFx.Init(fxRenderer);
        }
        fishFxMode = mode;
        if (mode == 0)
        {
            fishFx.Stop();
            fishFx.gameObject.SetActive(false);
            return;
        }
        fishFx.gameObject.SetActive(true);
        if (mode == 1)
        {
            // 찌가 물에 닿은 뒤부터 보인다.
            fishFxDelay = 0.7f;
            fishFx.Renderer.enabled = false;
            fishFx.PlayLoop(view.BobberFrames, 6f);
        }
        else if (mode == 2)
        {
            fishFxDelay = 0f;
            fishFx.Renderer.enabled = true;
            fishFx.PlayLoop(view.BiteFrames, 12f);
        }
        else
        {
            fishFxDelay = 0f;
            fishFx.Renderer.enabled = true;
            fishFx.Stop();
            fishFx.PlayOnce(view.SplashFrames, 14f);
            fishFxMode = 3;
            fishFxTimer = view.SplashFrames.Length / 14f;
        }
    }

    private float fishFxTimer;

    private void LateUpdate()
    {
        if (fishFx != null && fishFx.gameObject.activeSelf)
        {
            if (fishFxDelay > 0f)
            {
                fishFxDelay -= Time.deltaTime;
                if (fishFxDelay <= 0f)
                {
                    fishFx.Renderer.enabled = true;
                }
            }
            if (fishFxMode == 3)
            {
                fishFxTimer -= Time.deltaTime;
                if (fishFxTimer <= 0f)
                {
                    fishFx.gameObject.SetActive(false);
                    return;
                }
            }
            else
            {
                // 찌를 따라간다. 낚시 자세가 끊기면(움직임) 물결도 지운다.
                if (!controller.Visual.IsPosing)
                {
                    fishFx.gameObject.SetActive(false);
                    return;
                }
                fishFx.transform.position = controller.Visual.FishingPointPosition;
            }
        }
    }

    private void OnDestroy()
    {
        if (fishFx != null)
        {
            Destroy(fishFx.gameObject);
        }
    }

    private void OnMetaLevelsChanged(int oldValue, int newValue)
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
        if (riding)
        {
            stats.moveSpeedMultiplier *= rideSpeedFactor;
        }
        stats.maxHpBonus += metaHpPerLevel * GetMetaLevel(MetaProgress.UnlockHp);
        stats.damageMultiplier *= 1f + metaDamagePerLevel * GetMetaLevel(MetaProgress.UnlockDamage);
        stats.damageTakenMultiplier *= 1f - metaDefensePerLevel * GetMetaLevel(MetaProgress.UnlockDefense);
        stats.moveSpeedMultiplier *= 1f + metaMovePerLevel * GetMetaLevel(MetaProgress.UnlockMove);
        stats.critChance += metaCritPerLevel * GetMetaLevel(MetaProgress.UnlockCrit);
        stats.maxManaBonus += metaManaPerLevel * GetMetaLevel(MetaProgress.UnlockMana);
        stats.cooldownMultiplier *= 1f - metaCooldownPerLevel * GetMetaLevel(MetaProgress.UnlockCooldown);

        int n = GetRelicCount(RelicDamage);
        stats.damageMultiplier *= Mathf.Pow(1.15f, n);
        stats.moveSpeedMultiplier *= Mathf.Pow(1.12f, GetRelicCount(RelicMove));
        stats.attackSpeedMultiplier *= Mathf.Pow(1.15f, GetRelicCount(RelicAttackSpeed));
        stats.critChance += 0.1f * GetRelicCount(RelicCrit);
        stats.maxHpBonus += 30 * GetRelicCount(RelicHp);
        stats.maxManaBonus += 25 * GetRelicCount(RelicMana);
        stats.cooldownMultiplier = Mathf.Max(0.4f, stats.cooldownMultiplier * Mathf.Pow(0.85f, GetRelicCount(RelicCooldown)));
        stats.skillDamageMultiplier *= Mathf.Pow(1.2f, GetRelicCount(RelicSkillDamage));
        stats.damageTakenMultiplier = Mathf.Max(0.2f, stats.damageTakenMultiplier * Mathf.Pow(0.88f, GetRelicCount(RelicDefense)));
        int oath = GetRelicCount(RelicBloodOath);
        stats.damageMultiplier *= Mathf.Pow(1.3f, oath);
        stats.damageTakenMultiplier *= Mathf.Pow(1.15f, oath);
        stats.manaOnHitBonus += 2 * GetRelicCount(RelicManaSpring);
    }

    private int GetMetaLevel(int index)
    {
        return (metaLevels >> (index * MetaProgress.UnlockBits)) & ((1 << MetaProgress.UnlockBits) - 1);
    }

    public int GetRelicCount(int relic)
    {
        return (int)((relicCounts >> (relic * 4)) & 15UL);
    }

    // 연계(상태이상 조합) 피해 배율. MonsterController가 서버에서 읽는다.
    public float ComboDamageFactor => 1f + 0.6f * GetRelicCount(RelicCombo);

    // 기본 공격에 덧붙는 상태이상(불씨 부적, 서리 결정). 무기에 상태이상이 없을 때만 쓴다.
    public StatusKind ExtraHitStatus
    {
        get
        {
            StatusKind status = StatusKind.None;
            if (GetRelicCount(RelicEmber) > 0)
            {
                status = StatusKind.Burn;
            }
            else if (GetRelicCount(RelicFrost) > 0)
            {
                status = StatusKind.Slow;
            }
            return status;
        }
    }

    // 치명타를 넣었을 때(서버): 흡혈 송곳니.
    public void ServerOnCritLanded()
    {
        int count = GetRelicCount(RelicLifesteal);
        if (count > 0)
        {
            controller.ServerHeal(3 * count);
        }
    }

    // 방을 깼을 때 FloorDirector가 부른다. 이미 고를 것이 있으면 다음 차례로 미룬다.
    public void ServerOfferRelic()
    {
        if (relicOffer != 0)
        {
            pendingOffers++;
        }
        else
        {
            int a = Random.Range(0, RelicTypeCount);
            int b = (a + Random.Range(1, RelicTypeCount)) % RelicTypeCount;
            int c = a;
            while (c == a || c == b)
            {
                c = Random.Range(0, RelicTypeCount);
            }
            relicOffer = OfferValid | a | (b << 5) | (c << 10);
        }
    }

    public void RequestPickRelic(int choice)
    {
        CmdPickRelic(choice);
    }

    [Command]
    private void CmdPickRelic(int choice)
    {
        if (relicOffer != 0 && choice >= 0 && choice < 3)
        {
            int relic = (relicOffer >> (choice * 5)) & 31;
            if (GetRelicCount(relic) < 15)
            {
                relicCounts += 1UL << (relic * 4);
            }
            if (relic == RelicGold)
            {
                inventory.ServerAddGold(120);
            }
            else if (relic == RelicHp)
            {
                controller.ServerHeal(30);
            }
            relicOffer = 0;
            inventory.RecomputeStats();
            TargetRelicPicked(relic);
            if (pendingOffers > 0)
            {
                pendingOffers--;
                ServerOfferRelic();
            }
        }
    }

    [TargetRpc]
    private void TargetRelicPicked(int relic)
    {
        hub.Hud.ShowNotice("유물 획득: " + relicNames[relic]);
    }

    private void OnRelicsChanged(ulong oldValue, ulong newValue)
    {
        if (hub != null)
        {
            inventory.RecomputeStats();
        }
    }

    private void OnRelicOfferChanged(int oldValue, int newValue)
    {
        if (hub != null && isLocalPlayer)
        {
            if (newValue != 0)
            {
                if (relicUi == null)
                {
                    Canvas canvas = hub.Hud.GetComponentInParent<Canvas>();
                    GameObject holder = new GameObject("RelicUi", typeof(RectTransform));
                    holder.transform.SetParent(canvas.rootCanvas.transform, false);
                    relicUi = holder.AddComponent<RelicUi>();
                    relicUi.Setup(hub.Hud.NoticeLabel.font, this);
                }
                int[] choices = { newValue & 31, (newValue >> 5) & 31, (newValue >> 10) & 31 };
                relicUi.Open(choices, OwnedRelicsText());
            }
            else if (relicUi != null)
            {
                relicUi.Close();
            }
        }
    }

    public bool RelicChoiceOpen => relicUi != null && relicUi.IsOpen;

    // 가진 유물 목록(유물 화면 아래쪽)
    public string OwnedRelicsText()
    {
        builder.Clear();
        for (int i = 0; i < RelicTypeCount; i++)
        {
            int count = GetRelicCount(i);
            if (count > 0)
            {
                if (builder.Length == 0)
                {
                    builder.Append("가진 유물: ");
                }
                else
                {
                    builder.Append(", ");
                }
                builder.Append(relicNames[i]);
                if (count > 1)
                {
                    builder.Append(" x").Append(count);
                }
            }
        }
        return builder.ToString();
    }

    // ------------------------------------------------------------ 본인 조작

    private bool IsFacilityRoom(RoomType type)
    {
        return type == RoomType.Ore || type == RoomType.Fishing || type == RoomType.Stable;
    }

    // 보스를 잡으면 열리는 다음 층 포탈: 가까이 가면 확인 창을 띄운다. 멀어지면 닫는다.
    private void UpdatePortalPrompt(FloorDirector floor, bool blocked)
    {
        ConfirmUi confirm = hub.ConfirmUi;
        if (confirm != null)
        {
            if (portalYes == null)
            {
                portalYes = RequestClimb;
            }
            float radius = floor.PortalRadius;
            bool near = floor.StairsOpen && !floor.RoomLocked && !controller.IsDead
                && ((Vector2)transform.position - floor.PortalPosition).sqrMagnitude <= radius * radius;
            if (near)
            {
                if (!portalPromptDone && !blocked)
                {
                    portalPromptDone = true;
                    string message = "다음 층으로 이동할까요?\n<size=70%>" + floor.FloorNumber + "층 → " + (floor.FloorNumber + 1) + "층</size>";
                    if (floor.FloorNumber >= floor.MaxFloors)
                    {
                        message = "탑 꼭대기로 나갈까요?";
                    }
                    confirm.Show(message, "이동", portalYes, null);
                }
            }
            else
            {
                portalPromptDone = false;
                if (confirm.IsShowing(portalYes))
                {
                    confirm.Hide();
                }
            }
        }
    }

    private bool IsConfirmBusy()
    {
        return hub.ConfirmUi != null && hub.ConfirmUi.IsBusy;
    }

    private void RequestClimb()
    {
        CmdClimbPortal();
    }

    // 큰 지도에서 고른 방으로 순간이동을 부탁한다.
    public void RequestTeleport(int room)
    {
        CmdTeleport(room);
    }

    [Command]
    private void CmdClimbPortal()
    {
        FloorDirector floor = hub.FloorDirector;
        if (floor != null && !controller.IsDead)
        {
            if (!floor.ServerTryClimbPortal())
            {
                TargetNotice(5);
            }
        }
    }

    [Command]
    private void CmdTeleport(int room)
    {
        FloorDirector floor = hub.FloorDirector;
        if (floor != null && !controller.IsDead)
        {
            if (floor.ServerTryTeleport(room) == 1)
            {
                TargetNotice(6);
            }
        }
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
            UpdatePortalPrompt(floor, blocked);
            FacilityView view = floor.FacilityView;
            RoomType currentType = floor.CurrentRoomType;
            if (view != null)
            {
                if (currentType == RoomType.Ore)
                {
                    view.SetEmpty(minesLeft == 0);
                }
                else if (currentType == RoomType.Stable)
                {
                    bool canTake = offeredMount != MountKind.None && offeredMount != mount;
                    int shownMount = -1;
                    if (canTake)
                    {
                        shownMount = (int)offeredMount - 1;
                    }
                    view.ShowMount(shownMount);
                }
            }
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
                builder.Append(hub.GetKeyName(GameInput.KeyInteract, "F")).Append("  ").Append(metaUi.GetStationName(station));
                hub.Hud.SetPrompt(builder, promptColor);
            }
            if (!blocked && input.InteractPressed && !IsConfirmBusy())
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
                + 4096 * (int)offeredMount + 16384 * skills.ClassIndex + 65536 * held + 262144 * skills.GetMastery(skills.ClassOf(held));
            if (!promptShown || signature != promptSignature)
            {
                promptShown = true;
                promptSignature = signature;
                BuildPrompt(roomType);
                hub.Hud.SetPrompt(builder, promptColor);
            }
            if (!blocked && input.InteractPressed && !IsConfirmBusy())
            {
                CmdInteract();
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
        if (roomType == RoomType.Ore)
        {
            if (minesLeft > 0)
            {
                builder.Append(hub.GetKeyName(GameInput.KeyInteract, "F")).Append("  광석 캐기 (남은 횟수 ").Append(minesLeft).Append(')');
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
                builder.Append("!!  지금 ").Append(hub.GetKeyName(GameInput.KeyInteract, "F"));
            }
            else if (fishLeft > 0)
            {
                builder.Append(hub.GetKeyName(GameInput.KeyInteract, "F")).Append("  낚싯대 던지기 (남은 횟수 ").Append(fishLeft).Append(')');
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
            builder.Append(hub.GetKeyName(GameInput.KeyInteract, "F")).Append("  ").Append(mountNames[(int)offeredMount]).Append(" 타기: ").Append(mountEffects[(int)offeredMount]);
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
    private void CmdApplyMeta(int packedLevels)
    {
        if (hub.IsCombatScene && !metaApplied)
        {
            metaApplied = true;
            // 단계마다 최대 단계를 넘지 않게 고쳐 담는다.
            int sanitized = 0;
            int mask = (1 << MetaProgress.UnlockBits) - 1;
            for (int i = 0; i < MetaProgress.UnlockCount; i++)
            {
                int level = Mathf.Min((packedLevels >> (i * MetaProgress.UnlockBits)) & mask, MetaProgress.MaxUnlockLevel);
                sanitized |= level << (i * MetaProgress.UnlockBits);
            }
            metaLevels = sanitized;
            inventory.ServerSetBonusPotions(GetMetaLevel(MetaProgress.UnlockPotion));
            inventory.ServerAddGold(metaGoldPerLevel * GetMetaLevel(MetaProgress.UnlockGold));
            inventory.RecomputeStats();
            controller.ServerHeal(controller.MaxHp);
        }
    }

    [Command]
    private void CmdInteract()
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
                if (roomType == RoomType.Ore)
                {
                    if (minesLeft > 0)
                    {
                        minesLeft--;
                        inventory.ServerAddOre(orePerMine);
                        RpcFacilityAction(ActionMine);
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
                        RpcFacilityAction(ActionShop);
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
            RpcFishEnd(false);
            TargetNotice(1);
        }
        else
        {
            fishLeft--;
            fishState = FishIdle;
            RpcFishEnd(true);
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
                    RpcFishEnd(false);
                    TargetNotice(2);
                }
            }
        }
    }

    [TargetRpc]
    private void TargetNotice(byte code)
    {
        if (code == 1)
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
        else if (code == 4)
        {
            hub.Hud.ShowNotice("값나가는 물고기를 낚아 골드를 얻었습니다");
        }
        else if (code == 5)
        {
            hub.Hud.ShowNotice("모두 포탈 앞에 모여야 합니다");
        }
        else
        {
            hub.Hud.ShowNotice("전투 중에는 순간이동할 수 없습니다");
        }
    }
}
