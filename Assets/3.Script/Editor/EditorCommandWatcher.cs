using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

// 개발 보조 도구. Temp/tower_cmd.txt에 명령을 쓰면 에디터가 대신 실행하고 Temp/tower_cmd_result.txt에 결과를 남긴다.
// Claude Code가 에디터 창을 직접 조작하지 않고도 새로 고침, 초기 구성, 플레이 시작·정지를 할 수 있게 한다.
// 실행 중 발생한 오류와 예외는 Temp/tower_log.txt에 기록한다. 빌드에는 포함되지 않는다(Editor 폴더).
[InitializeOnLoad]
public static class EditorCommandWatcher
{
    private const string CommandPath = "Temp/tower_cmd.txt";
    private const string ResultPath = "Temp/tower_cmd_result.txt";
    private const string LogPath = "Temp/tower_log.txt";
    private const double PollInterval = 0.5;

    private const int GcSampleFrames = 300;

    private static double nextPollTime;
    // trace 명령: 정해진 프레임 동안 로컬 플레이어의 상태·애니메이션·무기 각도를 매 프레임 기록한다.
    private static int traceFrames;
    private static int traceLastFrame;
    private static System.Text.StringBuilder traceBuilder;
    // script 명령: Temp/tower_script.txt의 줄을 차례로 실행한다("wait 초"는 기다림). 결과는 Temp/tower_script_result.txt.
    private static System.Collections.Generic.Queue<string> scriptLines;
    private static double scriptNextTime;
    private static System.Text.StringBuilder scriptLog;
    private static bool gcSampling;
    private static int parryHitFrame;
    private static ProfilerRecorder gcRecorder;
    private static ProfilerRecorder gcUsedRecorder;
    private static ProfilerRecorder totalUsedRecorder;
    private static ProfilerRecorder textureRecorder;

    static EditorCommandWatcher()
    {
        EditorApplication.update += Poll;
        Application.logMessageReceived += OnLog;
    }

    private static void Poll()
    {
        if (traceFrames > 0 && EditorApplication.isPlaying && Time.frameCount != traceLastFrame)
        {
            traceLastFrame = Time.frameCount;
            TraceFrame();
        }

        if (parryHitFrame > 0 && EditorApplication.isPlaying && Time.frameCount >= parryHitFrame)
        {
            parryHitFrame = 0;
            File.WriteAllText("Temp/tower_parry.txt", HitLocalPlayer(true));
        }

        if (gcSampling)
        {
            if (gcRecorder.Count >= GcSampleFrames)
            {
                FinishGcSample();
            }
        }

        if (scriptLines != null && EditorApplication.timeSinceStartup >= scriptNextTime)
        {
            if (scriptLines.Count == 0)
            {
                File.WriteAllText("Temp/tower_script_result.txt", scriptLog.ToString() + "done\n");
                scriptLines = null;
            }
            else
            {
                string line = scriptLines.Dequeue().Trim();
                if (line.StartsWith("wait "))
                {
                    scriptNextTime = EditorApplication.timeSinceStartup + double.Parse(line.Substring(5), System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (line.Length > 0)
                {
                    Run(line);
                    string scriptResult = "";
                    if (File.Exists(ResultPath))
                    {
                        scriptResult = File.ReadAllText(ResultPath);
                    }
                    scriptLog.Append(line).Append(" => ").Append(scriptResult).Append('\n');
                    scriptNextTime = EditorApplication.timeSinceStartup + 0.05;
                }
            }
        }

        if (EditorApplication.timeSinceStartup >= nextPollTime)
        {
            nextPollTime = EditorApplication.timeSinceStartup + PollInterval;
            if (File.Exists(CommandPath))
            {
                // 명령 파일을 쓰는 도중에 읽으면 공유 위반이 난다. 그때는 다음 차례에 다시 읽는다.
                string command = null;
                try
                {
                    command = File.ReadAllText(CommandPath).Trim();
                    File.Delete(CommandPath);
                }
                catch (IOException)
                {
                    command = null;
                }
                if (command != null)
                {
                    Run(command);
                }
            }
        }
    }

    private static void Run(string command)
    {
        string result = "ok " + command;
        try
        {
            if (command == "refresh")
            {
                AssetDatabase.Refresh();
            }
            else if (command.StartsWith("copyfile "))
            {
                // copyfile 원본|대상 : 프로젝트 안의 파일을 복사한다(깊은 폴더의 그림을 Temp로 꺼낼 때).
                string[] paths = command.Substring(9).Split('|');
                File.Copy(paths[0].Trim(), paths[1].Trim(), true);
                result = "ok copy";
            }
            else if (command.StartsWith("unzip "))
            {
                // Temp/<이름>.zip을 프로젝트 폴더에 풀고(덮어씀) 에셋을 다시 읽는다. 파일을 한꺼번에 들여올 때 쓴다.
                string zipPath = Path.Combine("Temp", command.Substring(6).Trim());
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, ".", true);
                AssetDatabase.Refresh();
                result = "ok unzip " + zipPath;
            }
            else if (command.StartsWith("roomtype "))
            {
                // 시험용: 지금 방의 종류를 바꾸고 처음 들어온 것처럼 처리한다. 예: "roomtype 7"
                FloorDirector floor = Object.FindFirstObjectByType<FloorDirector>();
                RoomType type = (RoomType)int.Parse(command.Substring(9));
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(FloorDirector).GetMethod("ServerSetRoomType", flags).Invoke(floor, new object[] { floor.CurrentRoom, type });
                typeof(FloorDirector).GetField("viewDirty", flags).SetValue(floor, true);
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerFacility>().ServerOnRoomEntered(true, type);
            }
            else if (command.StartsWith("aim "))
            {
                // 본인 조준을 고정한다. 예: "aim 1 0.5", "aim 0 0"은 마우스로 되돌린다.
                string[] parts = command.Split(' ');
                Vector2 aim = new Vector2(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>().editorAimOverride = aim;
            }
            else if (command.StartsWith("srinfo "))
            {
                // 스프라이트 이름에 글자가 들어간 렌더러의 위치·크기·그리기 방식. 예: "srinfo Floor"
                string key = command.Substring(7);
                System.Text.StringBuilder info = new System.Text.StringBuilder();
                SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].sprite != null && renderers[i].sprite.name.Contains(key))
                    {
                        info.Append(renderers[i].name).Append(" mode=").Append(renderers[i].drawMode).Append(" size=").Append(renderers[i].size)
                            .Append(" scale=").Append(renderers[i].transform.lossyScale).Append(" bounds=").Append(renderers[i].bounds.size)
                            .Append(" ppu=").Append(renderers[i].sprite.pixelsPerUnit).Append(" rect=").Append(renderers[i].sprite.rect)
                            .Append(" color=").Append(renderers[i].color).Append(" order=").Append(renderers[i].sortingOrder).Append(" on=").Append(renderers[i].enabled).Append(" | ");
                    }
                }
                result = "ok " + info;
            }
            else if (command == "sortaudit")
            {
                // 켜진 스프라이트마다 정렬 값(순서, 정렬 기준점, 그림 기준점 높이 비율, 높이, 묶음 정렬)을 적는다. 플레이어·몬스터·투사체는 뺀다.
                System.Text.StringBuilder info = new System.Text.StringBuilder();
                SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer renderer = renderers[i];
                    bool skip = !renderer.enabled || renderer.sprite == null || !renderer.gameObject.activeInHierarchy;
                    if (!skip && (renderer.GetComponentInParent<PlayerController>() != null || renderer.GetComponentInParent<MonsterController>() != null || renderer.GetComponentInParent<Projectile>() != null))
                    {
                        skip = true;
                    }
                    if (!skip)
                    {
                        UnityEngine.Rendering.SortingGroup group = renderer.GetComponentInParent<UnityEngine.Rendering.SortingGroup>();
                        string groupText = "-";
                        if (group != null)
                        {
                            groupText = group.name + ":" + group.sortingOrder;
                        }
                        string parentText = "";
                        if (renderer.transform.parent != null)
                        {
                            parentText = renderer.transform.parent.name + "/";
                        }
                        float pivotRatio = renderer.sprite.pivot.y / Mathf.Max(1f, renderer.sprite.rect.height);
                        info.Append(parentText).Append(renderer.name)
                            .Append(" sprite=").Append(renderer.sprite.name).Append(" order=").Append(renderer.sortingOrder)
                            .Append(" point=").Append(renderer.spriteSortPoint).Append(" pivotY=").Append(pivotRatio.ToString("0.00"))
                            .Append(" h=").Append(renderer.bounds.size.y.ToString("0.00")).Append(" y=").Append(renderer.transform.position.y.ToString("0.00"))
                            .Append(" group=").Append(groupText).Append('\n');
                    }
                }
                result = "ok\n" + info;
            }
            else if (command == "lookreset")
            {
                // 저장한 외형을 지워 다음 실행을 '처음 실행'으로 만든다.
                PlayerPrefs.DeleteKey("ProjectR.Look.Parts");
                PlayerPrefs.DeleteKey("ProjectR.Look.Colors");
                PlayerPrefs.Save();
            }
            else if (command.StartsWith("customize"))
            {
                // 미용실과 같은 경로로 꾸미기 화면을 연다.
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                Object.FindFirstObjectByType<GameHub>().CustomizeUi.Open(player, false);
            }
            else if (command == "esc")
            {
                // Esc를 누른 것과 같은 경로
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                typeof(PlayerController).GetMethod("HandleMenuKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(player, null);
                result = "ok esc timeScale=" + Time.timeScale;
            }
            else if (command.StartsWith("click "))
            {
                // 이름이 같은 활성 버튼을 누른다. 예: "click Settings"
                string buttonName = command.Substring(6);
                UnityEngine.UI.Button[] buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
                int clicked = 0;
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i].name == buttonName && buttons[i].isActiveAndEnabled)
                    {
                        buttons[i].onClick.Invoke();
                        clicked++;
                    }
                }
                result = "ok click " + buttonName + " " + clicked;
            }
            else if (command.StartsWith("timescale "))
            {
                // 짧은 연출을 화면으로 확인할 때 시간을 느리게 한다. 예: "timescale 0.05"
                Time.timeScale = float.Parse(command.Substring(10), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (command.StartsWith("swing"))
            {
                // 지금 조준 방향으로 기본 공격을 한 번 한 것과 같은 경로(로비에서도 동작 확인용).
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                typeof(PlayerController).GetMethod("StartAttack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(player, new object[] { 1f });
            }
            else if (command == "handinfo")
            {
                // 플레이어 프리팹의 오른손 뼈대 경로와 무기 파츠의 로컬 위치·회전·스프라이트 크기를 Temp/tower_hand.txt에 적는다.
                result = "ok " + DescribeHand();
            }
            else if (command == "openlobby")
            {
                // 열려 있는 씬의 저장하지 않은 변경을 버리고 로비 씬을 연다(씬은 모두 코드로 만든다).
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(GameNetworkManager.LobbyScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            else if (command == "build")
            {
                TowerBootstrap.BuildAll();
            }
            else if (command == "play")
            {
                EditorApplication.isPlaying = true;
            }
            else if (command == "stop")
            {
                EditorApplication.isPlaying = false;
            }
            else if (command == "screenshot")
            {
                ScreenCapture.CaptureScreenshot("Temp/tower_shot.png");
            }
            else if (command == "gcsample")
            {
                StartGcSample();
            }
            else if (command == "clearlog")
            {
                File.WriteAllText(LogPath, string.Empty);
            }
            else if (command == "status")
            {
                result = "ok " + DescribeStatus();
            }
            else if (command == "startrun")
            {
                // 호스트가 출발 지점에서 F를 누른 것과 같은 경로.
                (Mirror.NetworkManager.singleton as GameNetworkManager).ServerStartRun();
            }
            else if (command == "exitrun")
            {
                // 전투 중 Esc와 같은 경로.
                (Mirror.NetworkManager.singleton as GameNetworkManager).RequestExit();
            }
            else if (command == "killall")
            {
                // 서버에서 살아 있는 몬스터를 전부 처치한다(풀 재사용과 웨이브 진행 검사용).
                MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
                int killed = 0;
                for (int i = 0; i < monsters.Length; i++)
                {
                    if (monsters[i].isServer && monsters[i].State != MonsterState.Dead)
                    {
                        monsters[i].ServerTakeDamage(99999, Vector2.right, 0);
                        killed++;
                    }
                }
                result = "ok killall " + killed;
            }
            else if (command.StartsWith("weapon "))
            {
                // 시작 무기를 바꾼다(호스트의 본인 캐릭터). 전투 씬에서도 검사용으로 쓸 수 있다.
                LocalInventory().ServerSetStartWeapon(int.Parse(command.Substring(7)));
            }
            else if (command.StartsWith("drop "))
            {
                // 본인 주변에 무작위 장비를 N개 떨어뜨린다.
                PlayerInventory inventory = LocalInventory();
                int count = int.Parse(command.Substring(5));
                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = new Vector2(Mathf.Cos(i * 1.3f), Mathf.Sin(i * 1.3f)) * 0.9f;
                    inventory.ServerOfferDrop(inventory.Balance.CreateRandom(1f), (Vector2)inventory.transform.position + offset);
                }
            }
            else if (command == "pickupall")
            {
                // 보이는 바닥 아이템을 모두 F로 주운 것과 같은 경로.
                ItemDropPool pool = (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.ItemDropPool;
                int[] ids = new int[pool.ActiveCount];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = pool.GetActive(i).Id;
                }
                for (int i = 0; i < ids.Length; i++)
                {
                    InvokeOnInventory("CmdPickup", new object[] { ids[i] });
                }
                result = "ok pickupall " + ids.Length;
            }
            else if (command.StartsWith("equip "))
            {
                InvokeOnInventory("CmdEquipFromBag", new object[] { int.Parse(command.Substring(6)) });
            }
            else if (command.StartsWith("unequip "))
            {
                InvokeOnInventory("CmdUnequip", new object[] { int.Parse(command.Substring(8)) });
            }
            else if (command.StartsWith("discard "))
            {
                InvokeOnInventory("CmdDiscard", new object[] { int.Parse(command.Substring(8)) });
            }
            else if (command.StartsWith("door "))
            {
                // 문을 지난 것과 같은 경로(0 북, 1 동, 2 남, 3 서).
                bool moved = (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.FloorDirector.ServerTravel(int.Parse(command.Substring(5)));
                result = "ok door moved=" + moved;
            }
            else if (command.StartsWith("drag "))
            {
                // drag 시작칸 끝칸|x,y: UI 끌어 놓기를 EventSystem으로 바로 흉내 낸다. 칸 이름은 "부모/이름"도 된다(예: Panel/Bag0, SlotQ, Node3).
                string[] parts = command.Split(' ');
                result = SimulateDrag(parts[1], parts[2]);
            }
            else if (command.StartsWith("prefint "))
            {
                // 시험으로 바뀐 기기 저장값을 되돌린다(플레이 중이 아닐 때). 예: "prefint ProjectR.StartClass 0"
                string[] parts = command.Split(' ');
                PlayerPrefs.SetInt(parts[1], int.Parse(parts[2]));
                PlayerPrefs.Save();
                result = "ok prefint " + parts[1] + "=" + PlayerPrefs.GetInt(parts[1]);
            }
            else if (command.StartsWith("tproom "))
            {
                // 큰 지도에서 그 방을 누른 것과 같은 경로(본인 PlayerFacility의 순간이동 요청). 예: "tproom 3"
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerFacility>().RequestTeleport(int.Parse(command.Substring(7)));
            }
            else if (command == "climb")
            {
                (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.FloorDirector.ServerClimb();
            }
            else if (command == "rooms")
            {
                result = "ok " + DescribeRooms();
            }
            else if (command.StartsWith("goto "))
            {
                // 본인 캐릭터를 지정한 좌표로 옮긴다. 예: "goto 0 0"
                string[] parts = command.Split(' ');
                Vector2 target = new Vector2(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>().ServerTeleport(target);
            }
            else if (command.StartsWith("inv "))
            {
                // 인벤토리 명령을 그대로 부른다. 예: "inv CmdBuy 0", "inv CmdReroll 0 1", "inv CmdUsePotion"
                string[] parts = command.Split(' ');
                object[] arguments = new object[parts.Length - 2];
                for (int i = 0; i < arguments.Length; i++)
                {
                    arguments[i] = int.Parse(parts[i + 2]);
                }
                InvokeOnInventory(parts[1], arguments);
            }
            else if (command.StartsWith("give "))
            {
                // "give 골드 광석"
                string[] parts = command.Split(' ');
                LocalInventory().ServerAddGold(int.Parse(parts[1]));
                LocalInventory().ServerAddOre(int.Parse(parts[2]));
            }
            else if (command.StartsWith("pstatus "))
            {
                // 본인에게 상태이상(종류 번호 K)을 3초 건다. 예: "pstatus 1"
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                player.ServerApplyStatus((StatusKind)int.Parse(command.Substring(8)), 3f, 2);
            }
            else if (command.StartsWith("ptonic "))
            {
                // 본인에게 물약 효과(PotionKind 번호)를 20초 건다(HUD 강화 아이콘 시험용). 정화는 8초 면역.
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                PotionKind kind = (PotionKind)int.Parse(command.Substring(7));
                if (kind == PotionKind.Cleanse)
                {
                    player.ServerCleanse(8f);
                }
                else
                {
                    player.ServerApplyTonic(kind, 20f);
                }
            }
            else if (command == "pbuff")
            {
                // 본인 계열의 첫 강화 스킬을 그 지속 시간만큼 건다(HUD 강화 아이콘 시험용).
                PlayerSkills skills = LocalSkills();
                ClassDefinition[] classList = (ClassDefinition[])typeof(PlayerSkills).GetField("classes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(skills);
                int classIndex = skills.ClassIndex;
                ClassDefinition definition = classList[classIndex];
                for (int i = 0; i < definition.SkillCount; i++)
                {
                    SkillDefinition skill = definition.GetSkill(i);
                    if (skill != null && skill.Kind == SkillKind.Buff)
                    {
                        skills.ServerApplyBuff(classIndex * ClassDefinition.MaxSkills + i, skill.BuffDuration);
                        result = "ok buff " + skill.name + " " + skill.BuffDuration;
                        break;
                    }
                }
            }
            else if (command == "interact")
            {
                // 방 시설에서 F를 누른 것과 같은 경로(제단, 광맥, 낚시터, 마구간).
                PlayerFacility facility = Mirror.NetworkClient.localPlayer.GetComponent<PlayerFacility>();
                typeof(PlayerFacility).GetMethod("CmdInteract", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(facility, new object[0]);
            }
            else if (command.StartsWith("fallen "))
            {
                // 타락한 모험가를 한 마리 부른다. 1 = 엘리트(확률), 2 = 보스(항상).
                MonsterDirector director = Object.FindFirstObjectByType<MonsterDirector>();
                director.ServerSpawnGroup(1, 1f, 1, 0f, 1f, (MonsterRank)int.Parse(command.Substring(7)), 0, true);
            }
            else if (command.StartsWith("meta "))
            {
                // 로비 마을 화면을 연다(0 해금 상인, 1 도감, 2 설정).
                Object.FindFirstObjectByType<GameHub>().MetaUi.Toggle(int.Parse(command.Substring(5)));
            }
            else if (command.StartsWith("metabuy "))
            {
                // 마을 화면의 버튼 N을 누른 것과 같은 경로.
                MetaUi metaUi = Object.FindFirstObjectByType<GameHub>().MetaUi;
                typeof(MetaUi).GetMethod("OnAction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(metaUi, new object[] { int.Parse(command.Substring(8)) });
            }
            else if (command.StartsWith("soul "))
            {
                GameHub hub = Object.FindFirstObjectByType<GameHub>();
                hub.Meta.AddSoulstones(int.Parse(command.Substring(5)));
                hub.Meta.Save();
            }
            else if (command == "mastery")
            {
                // 지금 든 무기 계열의 숙련도를 가득 채운다.
                for (int i = 0; i < 10; i++)
                {
                    LocalSkills().ServerAddMastery();
                }
            }
            else if (command == "pinfo")
            {
                // 본인의 상태이상, 탈것, 직업, 숙련도, 체력, 포션, 영구 해금을 한 줄로 돌려준다.
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                PlayerSkills skills = player.Skills;
                GameHub hub = Object.FindFirstObjectByType<GameHub>();
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                builder.Append("status=").Append(player.StatusMask).Append(" mount=").Append(player.Facility.MountName)
                    .Append(" class=").Append(skills.Class.DisplayName).Append(" holdingClassWeapon=").Append(skills.HoldingClassWeapon)
                    .Append(" mastery=");
                for (int i = 0; i < PlayerSkills.ClassCount; i++)
                {
                    builder.Append(skills.GetMastery(i)).Append(',');
                }
                builder.Append(" hp=").Append(player.Hp).Append('/').Append(player.MaxHp)
                    .Append(" mana=").Append(player.Mana).Append('/').Append(player.MaxMana)
                    .Append(" potions=").Append(player.Inventory.CountPotions(PotionKind.HealthSmall))
                    .Append(" gold=").Append(player.Inventory.Gold).Append(" ore=").Append(player.Inventory.Ore)
                    .Append(" soul=").Append(hub.Meta.Soulstones)
                    .Append(" unlock=").Append(hub.Meta.GetUnlockLevel(0)).Append(hub.Meta.GetUnlockLevel(1)).Append(hub.Meta.GetUnlockLevel(2));
                result = "ok " + builder;
            }
            else if (command.StartsWith("timescale "))
            {
                // 시험용: 게임 시간을 느리게(빠르게) 한다. 짧은 동작을 화면으로 찍을 때 쓴다.
                Time.timeScale = float.Parse(command.Substring(10), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (command == "pstate")
            {
                // 본인의 행동 상태(강화 평타 시험용): 상태, 상태 시간, 강화 평타 여부, 강화 차지 여부, 체력·몬스터 체력 합
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                builder.Append("state=").Append(typeof(PlayerController).GetField("state", flags).GetValue(player))
                    .Append(" t=").Append(((float)typeof(PlayerController).GetField("stateTimer", flags).GetValue(player)).ToString("0.00"))
                    .Append(" power=").Append(typeof(PlayerController).GetField("attackIsPower", flags).GetValue(player))
                    .Append(" chargingPower=").Append(typeof(PlayerController).GetField("chargingPower", flags).GetValue(player))
                    .Append(" glow=").Append(typeof(PlayerController).GetField("chargeGlowing", flags).GetValue(player))
                    .Append(" full=").Append(typeof(PlayerController).GetField("chargeGlowFull", flags).GetValue(player))
                    .Append(" pos=").Append(player.transform.position.ToString("0.00"));
                result = "ok " + builder;
            }
            else if (command == "god")
            {
                // 시험용: 본인을 오랫동안 무적으로 만든다.
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>().ServerSetInvulnerable(3600f);
            }
            else if (command == "monsters")
            {
                // 살아 있는 몬스터의 등급, 특성, 체력, 상태이상을 한 줄로 돌려준다.
                MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                for (int i = 0; i < monsters.Length; i++)
                {
                    MonsterController monster = monsters[i];
                    if (monster.State != MonsterState.Dead)
                    {
                        builder.Append(monster.Rank).Append('/').Append(monster.Modifier).Append(' ').Append(monster.Hp).Append('/').Append(monster.MaxHp)
                            .Append(" st=").Append(monster.StatusMask).Append(" enraged=").Append(monster.Enraged).Append(' ').Append(monster.State);
                        SpriteRenderer[] renderers = monster.GetComponentsInChildren<SpriteRenderer>(false);
                        int shown = 0;
                        float minAlpha = 1f;
                        for (int r = 0; r < renderers.Length; r++)
                        {
                            if (renderers[r].enabled && renderers[r].sprite != null)
                            {
                                shown++;
                                minAlpha = Mathf.Min(minAlpha, renderers[r].color.a);
                            }
                        }
                        builder.Append(" pos=").Append(monster.transform.position.ToString("F1")).Append(" fallen=").Append(monster.Fallen)
                            .Append(" shown=").Append(shown).Append(" minA=").Append(minAlpha.ToString("F2")).Append(" scale=").Append(monster.transform.localScale.x.ToString("F2")).Append("; ");
                    }
                }
                result = "ok " + builder;
            }
            else if (command.StartsWith("hurt "))
            {
                // 모든 몬스터에게 피해 N과 상태이상(종류 번호 K)을 준다. 예: "hurt 20 1"
                string[] parts = command.Split(' ');
                HitInfo hit = new HitInfo();
                hit.damage = int.Parse(parts[1]);
                hit.crit = true;
                hit.status = (StatusKind)int.Parse(parts[2]);
                hit.statusDuration = 3f;
                hit.statusPower = 0.2f;
                MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
                for (int i = 0; i < monsters.Length; i++)
                {
                    monsters[i].ServerTakeHit(hit, Vector2.right, 0);
                }
            }
            else if (command.StartsWith("xp "))
            {
                LocalSkills().ServerAddXp(int.Parse(command.Substring(3)));
            }
            else if (command.StartsWith("node "))
            {
                LocalSkills().RequestAllocate(int.Parse(command.Substring(5)));
            }
            else if (command.StartsWith("cast "))
            {
                // Q, E, R을 누른 것과 같은 경로(0, 1, 2).
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                // 커서 자리에 놓는 스킬은 aim 명령 값(몸 가운데에서의 거리)을 놓을 자리로 쓴다.
                typeof(PlayerController).GetField("skillTargetOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(player, player.editorAimOverride);
                System.Reflection.MethodInfo method = typeof(PlayerController).GetMethod("TryCast",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object started = method.Invoke(player, new object[] { int.Parse(command.Substring(5)) });
                result = "ok cast started=" + started + " mana=" + player.Mana;
            }
            else if (command.StartsWith("ccast "))
            {
                // 차징 스킬을 모은 정도(0~1)만큼 모았다가 뗀 것과 같다. 예: "ccast 0 1"
                string[] parts = command.Split(' ');
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                typeof(PlayerController).GetField("skillTargetOffset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(player, player.editorAimOverride);
                System.Reflection.MethodInfo method = typeof(PlayerController).GetMethod("CastSkill",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object started = method.Invoke(player, new object[] { int.Parse(parts[1]), float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) });
                result = "ok ccast started=" + started + " mana=" + player.Mana;
            }
            else if (command.StartsWith("keys"))
            {
                // keys W D Space: 키보드를 그 키들이 눌린 상태로 만든다(빈 목록이면 모두 뗀다). 입력 시험용.
                string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                UnityEngine.InputSystem.Key[] keys = new UnityEngine.InputSystem.Key[parts.Length - 1];
                for (int i = 1; i < parts.Length; i++)
                {
                    keys[i - 1] = (UnityEngine.InputSystem.Key)System.Enum.Parse(typeof(UnityEngine.InputSystem.Key), parts[i], true);
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
                result = "ok keys " + (parts.Length - 1);
            }
            else if (command.StartsWith("mouse "))
            {
                // mouse 0|1: 왼쪽 버튼을 누르거나 뗀다.
                bool down = command.Substring(6).Trim() == "1";
                // 게임 화면에 포커스가 없으면 입력이 무시되므로 먼저 포커스를 준다.
                System.Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView != null)
                {
                    EditorWindow.FocusWindowIfItsOpen(gameView);
                }
                UnityEngine.InputSystem.LowLevel.MouseState state = new UnityEngine.InputSystem.LowLevel.MouseState();
                state.position = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, down);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current, state);
            }
            else if (command == "relic")
            {
                // 유물 고르기 화면을 띄운다(서버에서 제안).
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerFacility>().ServerOfferRelic();
            }
            else if (command.StartsWith("pickrelic "))
            {
                Mirror.NetworkClient.localPlayer.GetComponent<PlayerFacility>().RequestPickRelic(int.Parse(command.Substring(10)));
                result = "ok " + Mirror.NetworkClient.localPlayer.GetComponent<PlayerFacility>().OwnedRelicsText();
            }
            else if (command.StartsWith("spawnboss "))
            {
                // spawnboss 몬스터종류: 지금 방에 보스 한 마리를 내보낸다.
                MonsterDirector director = Object.FindFirstObjectByType<MonsterDirector>();
                director.ServerSpawnGroup(1, 1f, 1, 0f, 1f, MonsterRank.Boss, int.Parse(command.Substring(10)), false);
            }
            else if (command.StartsWith("spawn "))
            {
                // spawn 마리수: 지금 방에 일반 몬스터 무리를 내보낸다(관통 시험용).
                MonsterDirector director = Object.FindFirstObjectByType<MonsterDirector>();
                director.ServerSpawnGroup(int.Parse(command.Substring(6)), 1f, 1, 0f, 1f, MonsterRank.Normal, 0, false);
            }
            else if (command.StartsWith("spawnelite "))
            {
                // spawnelite 몬스터종류: 지금 방에 엘리트 한 마리를 내보낸다(타락한 모험가는 아님).
                MonsterDirector director = Object.FindFirstObjectByType<MonsterDirector>();
                director.ServerSpawnGroup(1, 1f, 1, 0f, 1f, MonsterRank.Elite, int.Parse(command.Substring(11)), false);
            }
            else if (command.StartsWith("hitall "))
            {
                // hitall 피해 상태이상번호 치명타(0/1): 살아 있는 몬스터 전부를 플레이어가 때린 것처럼 친다(연계 시험용).
                string[] parts = command.Split(' ');
                HitInfo hit = new HitInfo();
                hit.damage = int.Parse(parts[1]);
                hit.status = (StatusKind)int.Parse(parts[2]);
                hit.crit = parts.Length > 3 && parts[3] == "1";
                hit.statusDuration = 4f;
                hit.statusPower = 0.3f;
                uint attacker = Mirror.NetworkClient.localPlayer.netId;
                MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
                System.Text.StringBuilder hits = new System.Text.StringBuilder();
                for (int i = 0; i < monsters.Length; i++)
                {
                    if (monsters[i].isServer && monsters[i].State != MonsterState.Dead)
                    {
                        int before = monsters[i].Hp;
                        monsters[i].ServerTakeHit(hit, Vector2.right, attacker);
                        hits.Append(before).Append("->").Append(monsters[i].Hp).Append(" st=").Append(monsters[i].StatusMask).Append("; ");
                    }
                }
                result = "ok " + hits;
            }
            else if (command == "script")
            {
                scriptLines = new System.Collections.Generic.Queue<string>(File.ReadAllLines("Temp/tower_script.txt"));
                scriptLog = new System.Text.StringBuilder();
                scriptNextTime = 0;
            }
            else if (command == "bones")
            {
                // 본인 무기 그림에서 캐릭터 뿌리까지 각 뼈의 로컬 z 회전과 월드 회전을 적는다(조준 각도 문제 확인용).
                CharacterVisual characterVisual = LocalSkills().GetComponent<PlayerController>().Visual;
                SpriteRenderer weaponSprite = (SpriteRenderer)typeof(CharacterVisual).GetField("weaponRenderer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(characterVisual);
                System.Text.StringBuilder bonesText = new System.Text.StringBuilder("ok");
                Transform boneNow = weaponSprite.transform;
                while (boneNow != null && boneNow != characterVisual.transform.parent)
                {
                    bonesText.Append(" | ").Append(boneNow.name).Append(' ').Append(boneNow.localEulerAngles.z.ToString("0")).Append('/').Append(boneNow.eulerAngles.z.ToString("0")).Append(" s").Append(boneNow.localScale.x.ToString("0.#"));
                    boneNow = boneNow.parent;
                }
                result = bonesText.ToString();
            }
            else if (command.StartsWith("trace "))
            {
                traceFrames = int.Parse(command.Substring(6));
                traceBuilder = new System.Text.StringBuilder();
            }
            else if (command.StartsWith("vfx "))
            {
                // vfx 계열 스킬 [x y]: 해금·마나와 상관없이 그 스킬의 이펙트만 띄운다(투사체는 표시용으로 쏜다).
                string[] parts = command.Split(' ');
                PlayerSkills skills = LocalSkills();
                PlayerController player = skills.GetComponent<PlayerController>();
                ClassDefinition[] classList = (ClassDefinition[])typeof(PlayerSkills).GetField("classes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(skills);
                SkillDefinition def = classList[int.Parse(parts[1])].GetSkill(int.Parse(parts[2]));
                Vector2 aimDir = Vector2.right;
                if (parts.Length >= 5)
                {
                    aimDir = new Vector2(float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture)).normalized;
                }
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(PlayerSkills).GetMethod("PlayCastFx", flags).Invoke(skills, new object[] { def, player.Center, aimDir, aimDir * def.Range });
                if (def.Kind == SkillKind.Volley)
                {
                    typeof(PlayerSkills).GetMethod("LaunchVolley", flags).Invoke(skills, new object[] { def, player.Center, aimDir, false, 0f });
                }
                if (def.Kind == SkillKind.Dash && def.ExplosionRadius > 0f && def.Vfx.Has(def.Vfx.impact))
                {
                    Object.FindFirstObjectByType<SkillFxPlayer>().PlayFrames(def.Vfx.impact, def.Vfx.impactFps, def.Vfx.impactGround, player.Center + aimDir * def.Range, def.ExplosionRadius * def.Vfx.impactScale, 0f, null, 0f, false);
                }
                result = "ok vfx " + def.name;
            }
            else if (command.StartsWith("job "))
            {
                // 로비에서 숫자 키로 시작 계열을 고른 것과 같다(0~2).
                LocalSkills().ServerSetStartClass(int.Parse(command.Substring(4)));
            }
            else if (command.StartsWith("slot "))
            {
                // slot 칸 스킬: 스킬을 장착 칸에 넣는다.
                string[] parts = command.Split(' ');
                LocalSkills().RequestEquip(int.Parse(parts[1]), int.Parse(parts[2]));
            }
            else if (command.StartsWith("fx "))
            {
                // 본인 위치에 스킬 이펙트를 재생한다(fx 종류번호 반경 지속시간).
                string[] parts = command.Split(' ');
                PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.SkillFx.Play((SkillFx)int.Parse(parts[1]), player.Center,
                    float.Parse(parts[2]), Color.white, float.Parse(parts[3]), null);
            }
            else if (command == "tree")
            {
                (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.SkillUi.ToggleTree();
            }
            else if (command == "skills")
            {
                result = "ok " + DescribeSkills();
            }
            else if (command.StartsWith("gearset "))
            {
                // 투구·갑옷을 파츠 번호로 바로 입히고(옵션 없음, 등급은 파츠 고정 등급) 세트와 능력치를 돌려준다. 예: "gearset 8 30"
                string[] parts = command.Split(' ');
                PlayerInventory inventory = LocalInventory();
                ItemBalance balance = inventory.Balance;
                Mirror.SyncList<ItemData> equipment = (Mirror.SyncList<ItemData>)typeof(PlayerInventory)
                    .GetField("equipment", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(inventory);
                ItemSlot[] slots = { ItemSlot.Helmet, ItemSlot.Armor };
                System.Text.StringBuilder info = new System.Text.StringBuilder();
                for (int i = 0; i < 2; i++)
                {
                    ItemData item = new ItemData();
                    item.exists = 1;
                    item.slot = slots[i];
                    item.partsIndex = (ushort)int.Parse(parts[i + 1]);
                    item.rarity = balance.GetFixedRarity(slots[i], 0, item.partsIndex);
                    equipment[(int)slots[i]] = item;
                    balance.AppendDescription(info, item, null);
                    info.Append(" | ");
                }
                inventory.RecomputeStats();
                EquipStats stats = inventory.Stats;
                info.Append("set=").Append(balance.GetSetClass(equipment[(int)ItemSlot.Helmet], equipment[(int)ItemSlot.Armor]))
                    .Append(" hp+").Append(stats.maxHpBonus).Append(" mana+").Append(stats.maxManaBonus)
                    .Append(" dmgx=").Append(stats.damageMultiplier.ToString("0.000")).Append(" taken=").Append(stats.damageTakenMultiplier.ToString("0.000"))
                    .Append(" atkspd=").Append(stats.attackSpeedMultiplier.ToString("0.000")).Append(" move=").Append(stats.moveSpeedMultiplier.ToString("0.000"))
                    .Append(" crit=").Append(stats.critChance.ToString("0.000")).Append(" cdx=").Append(stats.cooldownMultiplier.ToString("0.000"))
                    .Append(" skillx=").Append(stats.skillDamageMultiplier.ToString("0.000"));
                result = "ok " + info.Replace('\n', ' ');
            }
            else if (command.StartsWith("uplevel "))
            {
                // 장착 칸 S의 강화 단계를 L로 바꾼다(서버). 예: "uplevel 0 5"
                string[] parts = command.Split(' ');
                PlayerInventory inventory = LocalInventory();
                Mirror.SyncList<ItemData> equipment = (Mirror.SyncList<ItemData>)typeof(PlayerInventory)
                    .GetField("equipment", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(inventory);
                int slotIndex = int.Parse(parts[1]);
                ItemData item = equipment[slotIndex];
                item.level = (byte)int.Parse(parts[2]);
                equipment[slotIndex] = item;
                result = "ok uplevel " + item.level;
            }
            else if (command.StartsWith("uislot "))
            {
                // 이름이 같은 켜진 칸에 마우스를 올리거나(h) 좌·우클릭(l·r)한다. 예: "uislot Potion0 l", "uislot Bag0 h"
                string[] parts = command.Split(' ');
                InventorySlotView[] views = Object.FindObjectsByType<InventorySlotView>(FindObjectsSortMode.None);
                int done = 0;
                for (int i = 0; i < views.Length; i++)
                {
                    if (views[i].name == parts[1] && views[i].gameObject.activeInHierarchy)
                    {
                        UnityEngine.EventSystems.PointerEventData data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                        if (parts[2] == "h")
                        {
                            views[i].OnPointerEnter(data);
                        }
                        else
                        {
                            if (parts[2] == "r")
                            {
                                data.button = UnityEngine.EventSystems.PointerEventData.InputButton.Right;
                            }
                            views[i].OnPointerClick(data);
                        }
                        done++;
                    }
                }
                result = "ok uislot " + done;
            }
            else if (command == "facility")
            {
                GameHub hub = (Mirror.NetworkManager.singleton as GameNetworkManager).Hub;
                hub.FacilityUi.Open(hub.FloorDirector.CurrentRoomType);
                hub.InventoryUi.Open();
            }
            else if (command == "inv")
            {
                result = "ok " + DescribeInventory();
            }
            else if (command == "invui")
            {
                (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.InventoryUi.Toggle();
            }
            else if (command.StartsWith("attack"))
            {
                // 가장 가까운 몬스터를 향해 공격한 것과 같은 경로. "attack 0.5"처럼 차지 값을 줄 수 있다.
                float charge = 1f;
                if (command.Length > 7)
                {
                    charge = float.Parse(command.Substring(7), System.Globalization.CultureInfo.InvariantCulture);
                }
                result = "ok attack " + AttackNearestMonster(charge);
            }
            else if (command.StartsWith("block "))
            {
                // "block 1"은 가장 가까운 몬스터 쪽으로 방패를 들고, "block 0"은 내린다.
                bool raise = command.Substring(6) == "1";
                InvokeOnLocalPlayer("CmdSetBlocking", new object[] { raise, AimAtNearestMonster() });
            }
            else if (command == "hit front" || command == "hit back")
            {
                // 방패를 든 방향의 앞(또는 뒤)에서 20 피해의 공격이 들어온 것으로 처리한다.
                result = "ok " + command + " hp " + HitLocalPlayer(command == "hit front");
            }
            else if (command == "parrytest")
            {
                // 방패를 든 직후(3프레임 뒤) 앞에서 공격이 들어오게 해 패링을 검사한다.
                InvokeOnLocalPlayer("CmdSetBlocking", new object[] { true, Vector2.right });
                parryHitFrame = Time.frameCount + 3;
            }
            else if (command == "buildplayer")
            {
                // 두 명 접속 검사용 Windows 빌드. 결과물은 Builds/Test (git 제외 대상).
                BuildPlayerOptions options = new BuildPlayerOptions();
                options.scenes = new[] { GameNetworkManager.LobbyScenePath, GameNetworkManager.GameScenePath };
                options.locationPathName = "Builds/Test/ProjectR.exe";
                options.target = BuildTarget.StandaloneWindows64;
                options.options = BuildOptions.Development;
                UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
                result = "ok buildplayer " + report.summary.result + " errors=" + report.summary.totalErrors;
            }
            else if (command.StartsWith("open "))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(command.Substring(5), UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            else if (command.StartsWith("menu "))
            {
                bool executed = EditorApplication.ExecuteMenuItem(command.Substring(5));
                if (!executed)
                {
                    result = "fail menu not found: " + command.Substring(5);
                }
            }
            else
            {
                result = "fail unknown command: " + command;
            }
        }
        catch (System.Exception exception)
        {
            result = "fail " + exception;
        }
        File.WriteAllText(ResultPath, result);
    }

    // 켜져 있는 UI 가운데 이름(뒤에서부터 "부모/이름"으로 맞춘다)이 맞는 첫 물체
    private static GameObject FindUi(string path)
    {
        string[] names = path.Split('/');
        GameObject found = null;
        RectTransform[] all = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length && found == null; i++)
        {
            Transform current = all[i];
            bool match = true;
            for (int k = names.Length - 1; k >= 0 && match; k--)
            {
                if (current == null || current.name != names[k])
                {
                    match = false;
                }
                else
                {
                    current = current.parent;
                }
            }
            if (match)
            {
                found = all[i].gameObject;
            }
        }
        return found;
    }

    private static string SimulateDrag(string fromPath, string toPath)
    {
        GameObject from = FindUi(fromPath);
        if (from == null)
        {
            return "fail no " + fromPath;
        }
        Vector2 start = RectTransformUtility.WorldToScreenPoint(null, from.transform.position);
        Vector2 end;
        if (toPath.Contains(","))
        {
            string[] xy = toPath.Split(',');
            end = new Vector2(float.Parse(xy[0]), float.Parse(xy[1]));
        }
        else
        {
            GameObject to = FindUi(toPath);
            if (to == null)
            {
                return "fail no " + toPath;
            }
            end = RectTransformUtility.WorldToScreenPoint(null, to.transform.position);
        }
        UnityEngine.EventSystems.EventSystem system = UnityEngine.EventSystems.EventSystem.current;
        UnityEngine.EventSystems.PointerEventData data = new UnityEngine.EventSystems.PointerEventData(system);
        data.button = UnityEngine.EventSystems.PointerEventData.InputButton.Left;
        data.position = start;
        data.pressPosition = start;
        System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        system.RaycastAll(data, hits);
        if (hits.Count == 0)
        {
            return "fail nothing under " + fromPath;
        }
        data.pointerPressRaycast = hits[0];
        data.pointerCurrentRaycast = hits[0];
        GameObject handler = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IBeginDragHandler>(hits[0].gameObject);
        if (handler == null)
        {
            return "fail no drag handler on " + hits[0].gameObject.name;
        }
        data.pointerDrag = handler;
        UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
        data.position = (start + end) * 0.5f;
        UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
        data.position = end;
        hits.Clear();
        system.RaycastAll(data, hits);
        UnityEngine.EventSystems.RaycastResult endHit = new UnityEngine.EventSystems.RaycastResult();
        string endName = "none";
        if (hits.Count > 0)
        {
            endHit = hits[0];
            endName = endHit.gameObject.name;
        }
        data.pointerCurrentRaycast = endHit;
        UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.dragHandler);
        UnityEngine.EventSystems.ExecuteEvents.Execute(handler, data, UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
        return "ok drag " + handler.name + " -> " + endName;
    }

    private static string HitLocalPlayer(bool fromFront)
    {
        PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
        System.Reflection.FieldInfo field = typeof(PlayerController).GetField("blockAim",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Vector2 blockAim = (Vector2)field.GetValue(player);
        // hitDirection은 공격이 날아가는 방향이다. 앞에서 오는 공격은 방패 방향의 반대로 날아온다.
        Vector2 hitDirection = blockAim;
        if (fromFront)
        {
            hitDirection = -blockAim;
        }
        int before = player.Hp;
        player.ServerTakeDamage(20, hitDirection, 0);
        return before + "->" + player.Hp;
    }

    // 검사용: 본인 캐릭터의 비공개 명령 함수를 직접 부른다(입력 장치 없이 같은 네트워크 경로를 타게 한다).
    private static void InvokeOnLocalPlayer(string methodName, object[] arguments)
    {
        PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
        System.Reflection.MethodInfo method = typeof(PlayerController).GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Invoke(player, arguments);
    }

    // 층의 방 목록(위치, 종류, 문, 방문·클리어 여부)을 한 줄로 돌려준다.
    private static string DescribeRooms()
    {
        FloorDirector floor = (Mirror.NetworkManager.singleton as GameNetworkManager).Hub.FloorDirector;
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append("floor=").Append(floor.FloorNumber).Append(" current=").Append(floor.CurrentRoom).Append(" locked=").Append(floor.RoomLocked).Append(" rooms=");
        for (int i = 0; i < floor.RoomCount; i++)
        {
            RoomInfo room = floor.GetRoom(i);
            builder.Append(i).Append(':').Append(room.type).Append('(').Append(room.x).Append(',').Append(room.y).Append(")d").Append(room.doors)
                .Append('v').Append(room.visited).Append('c').Append(room.cleared).Append(' ');
        }
        return builder.ToString();
    }

    private static void TraceFrame()
    {
        traceFrames--;
        if (Mirror.NetworkClient.localPlayer != null)
        {
            PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
            Animator animator = player.GetComponentInChildren<Animator>();
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            string stateName = "?";
            string[] names = { "Idle", "Walk", "Run", "Roll", "Attack", "Double Attack", "Jump Attack", "Skill", "Stun", "Dead1", "Victory" };
            for (int i = 0; i < names.Length; i++)
            {
                if (info.IsName(names[i]))
                {
                    stateName = names[i];
                }
            }
            string weaponInfo = "";
            // 장착 무기 그림(CharacterVisual.weaponRenderer)의 각도와 캐릭터 기준 위치, 몸통 위치
            SpriteRenderer heldWeapon = typeof(CharacterVisual).GetField("weaponRenderer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(player.GetComponentInChildren<CharacterVisual>()) as SpriteRenderer;
            if (heldWeapon != null)
            {
                Transform body = animator.transform.Find("Body");
                weaponInfo = " x=" + player.transform.position.x.ToString("F3") + " held=" + heldWeapon.transform.eulerAngles.z.ToString("0.0") + " hp=" + ((Vector2)(heldWeapon.transform.position - player.transform.position)).ToString("F3");
                if (body != null)
                {
                    weaponInfo += " body=" + ((Vector2)(body.position - player.transform.position)).ToString("F3");
                }
            }
            SpriteRenderer[] renderers = player.GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].transform.parent != null && renderers[i].transform.parent.parent != null && renderers[i].transform.parent.parent.name == "HandRight" && renderers[i].enabled && renderers[i].sprite != null && weaponInfo.Length == 0)
                {
                    Transform t = renderers[i].transform;
                    weaponInfo = " w=" + t.eulerAngles.z.ToString("0.0") + " wp=" + ((Vector2)(t.position - player.transform.position)).ToString("F3") + " chain=";
                    Transform c = t;
                    while (c != null && c != player.transform)
                    {
                        weaponInfo += c.name + ":" + c.localEulerAngles.z.ToString("0.0") + "/";
                        c = c.parent;
                    }
                }
            }
            object state = typeof(PlayerController).GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(player);
            traceBuilder.Append(Time.frameCount).Append(' ').Append(state).Append(' ').Append(stateName).Append(" t=").Append(info.normalizedTime.ToString("0.00"))
                .Append(" spd=").Append(animator.speed.ToString("0.00")).Append(weaponInfo).Append('\n');
        }
        if (traceFrames <= 0)
        {
            File.WriteAllText("Temp/tower_trace.txt", traceBuilder.ToString());
        }
    }

    private static PlayerSkills LocalSkills()
    {
        return Mirror.NetworkClient.localPlayer.GetComponent<PlayerSkills>();
    }

    // 직업, 레벨, 포인트, 노드 단계, 스킬 해금 여부를 한 줄로 돌려준다.
    private static string DescribeSkills()
    {
        PlayerSkills skills = LocalSkills();
        PlayerController player = skills.GetComponent<PlayerController>();
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append("class=").Append(skills.Class.DisplayName).Append(" level=").Append(skills.Level)
            .Append(" xp=").Append(skills.Xp).Append('/').Append(skills.XpToNext)
            .Append(" points=").Append(skills.SkillPoints).Append(" ranks=");
        for (int i = 0; i < skills.Class.NodeCount; i++)
        {
            builder.Append(skills.GetRank(i));
        }
        builder.Append(" awakened=");
        for (int i = 0; i < ClassDefinition.JobCount; i++)
        {
            builder.Append(skills.IsJobAwakened(i)).Append(',');
        }
        builder.Append(" unlocked=");
        for (int i = 0; i < skills.Class.SkillCount; i++)
        {
            if (skills.IsSkillUnlocked(i))
            {
                builder.Append(i).Append(',');
            }
        }
        System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        builder.Append(" rawClass=").Append(typeof(PlayerSkills).GetField("classIndex", flags).GetValue(skills))
            .Append(" lobbyClass=").Append(typeof(PlayerSkills).GetField("lobbyClass", flags).GetValue(skills));
        builder.Append(" slots=");
        for (int i = 0; i < ClassDefinition.SlotCount; i++)
        {
            builder.Append(skills.GetSlotSkill(i)).Append(',');
        }
        EquipStats stats = player.Inventory.Stats;
        builder.Append(" mana=").Append(player.Mana).Append('/').Append(player.MaxMana)
            .Append(" hp=").Append(player.Hp).Append('/').Append(player.MaxHp)
            .Append(" dmgx=").Append(stats.damageMultiplier.ToString("0.00"))
            .Append(" cdx=").Append(stats.cooldownMultiplier.ToString("0.00"))
            .Append(" skillx=").Append(stats.skillDamageMultiplier.ToString("0.00"))
            .Append(" pos=").Append(player.transform.position.x.ToString("0.0")).Append(',').Append(player.transform.position.y.ToString("0.0"));
        return builder.ToString();
    }

    private static PlayerInventory LocalInventory()
    {
        return Mirror.NetworkClient.localPlayer.GetComponent<PlayerInventory>();
    }

    private static void InvokeOnInventory(string methodName, object[] arguments)
    {
        System.Reflection.MethodInfo method = typeof(PlayerInventory).GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method.Invoke(LocalInventory(), arguments);
    }

    // 장착 4칸과 가방의 내용, 능력치 합계를 한 줄로 돌려준다.
    private static string DescribeInventory()
    {
        PlayerInventory inventory = LocalInventory();
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.Append("equip=[");
        for (int i = 0; i < PlayerInventory.EquipSlotCount; i++)
        {
            AppendItem(builder, inventory, inventory.GetEquipped(i));
        }
        builder.Append("] bag=[");
        for (int i = 0; i < PlayerInventory.BagSize; i++)
        {
            AppendItem(builder, inventory, inventory.GetBagItem(i));
        }
        EquipStats stats = inventory.Stats;
        PlayerController player = inventory.GetComponent<PlayerController>();
        builder.Append("] shop=[");
        for (int i = 0; i < inventory.ShopStockCount; i++)
        {
            AppendItem(builder, inventory, inventory.GetShopItem(i));
        }
        builder.Append("] gold=").Append(inventory.Gold).Append(" ore=").Append(inventory.Ore).Append(" potions=").Append(inventory.CountPotions(PotionKind.HealthSmall)).Append(" quick=").Append(inventory.GetQuickPotion(0)).Append('/').Append(inventory.GetQuickPotion(1))
            .Append(" dmgx=").Append(stats.damageMultiplier.ToString("F2"))
            .Append(" aspd=").Append(stats.attackSpeedMultiplier.ToString("F2"))
            .Append(" move=").Append(stats.moveSpeedMultiplier.ToString("F2"))
            .Append(" crit=").Append(stats.critChance.ToString("F2"))
            .Append(" taken=").Append(stats.damageTakenMultiplier.ToString("F2"))
            .Append(" block=").Append(stats.blockDamageFactor.ToString("F2"))
            .Append(" shield=").Append(inventory.ShieldActive)
            .Append(" hp=").Append(player.Hp).Append('/').Append(player.MaxHp)
            .Append(" mana=").Append(player.Mana).Append('/').Append(player.MaxMana)
            .Append(" drops=").Append((Mirror.NetworkManager.singleton as GameNetworkManager).Hub.ItemDropPool.ActiveCount);
        return builder.ToString();
    }

    private static void AppendItem(System.Text.StringBuilder builder, PlayerInventory inventory, ItemData item)
    {
        if (item.Exists)
        {
            inventory.Balance.AppendName(builder, item);
            builder.Append('(').Append(item.option1).Append(item.value1).Append(',').Append(item.option2).Append(item.value2).Append(')');
        }
        else
        {
            builder.Append('-');
        }
        builder.Append("; ");
    }

    private static Vector2 AimAtNearestMonster()
    {
        Vector2 aim = Vector2.right;
        Vector2 origin = Mirror.NetworkClient.localPlayer.transform.position;
        MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
        float nearestSqr = float.MaxValue;
        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i].State != MonsterState.Dead)
            {
                Vector2 toMonster = (Vector2)monsters[i].transform.position - origin;
                if (toMonster.sqrMagnitude < nearestSqr && toMonster.sqrMagnitude > 0.0001f)
                {
                    nearestSqr = toMonster.sqrMagnitude;
                    aim = toMonster.normalized;
                }
            }
        }
        return aim;
    }

    private static string AttackNearestMonster(float charge)
    {
        Vector2 aim = AimAtNearestMonster();
        InvokeOnLocalPlayer("CmdAttack", new object[] { aim, charge });
        return "aim=" + aim.ToString("F2");
    }

    private static string DescribeHand()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/Player.prefab");
        if (EditorApplication.isPlaying && Mirror.NetworkClient.localPlayer != null)
        {
            prefab = Mirror.NetworkClient.localPlayer.gameObject;
        }
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        Transform hand = null;
        Transform[] all = prefab.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == "HandRight")
            {
                hand = all[i];
            }
        }
        Transform walker = hand;
        while (walker != null)
        {
            builder.Append(walker.name).Append(" pos=").Append(walker.localPosition.ToString("F3"))
                .Append(" rotZ=").Append(walker.localEulerAngles.z.ToString("F1"))
                .Append(" scale=").Append(walker.localScale.ToString("F2")).Append('\n');
            walker = walker.parent;
        }
        builder.Append("--children\n");
        SpriteRenderer[] renderers = hand.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            Transform t = renderer.transform;
            builder.Append(t.parent.name).Append('/').Append(t.name)
                .Append(" pos=").Append(t.localPosition.ToString("F3"))
                .Append(" rotZ=").Append(t.localEulerAngles.z.ToString("F1"))
                .Append(" scale=").Append(t.localScale.ToString("F2"))
                .Append(" flipX=").Append(renderer.flipX)
                .Append(" order=").Append(renderer.sortingOrder);
            if (renderer.sprite != null)
            {
                builder.Append(" sprite=").Append(renderer.sprite.name).Append(' ').Append(renderer.sprite.rect.size.ToString("F0"))
                    .Append(" pivot=").Append(renderer.sprite.pivot.ToString("F0"));
            }
            builder.Append('\n');
        }
        File.WriteAllText("Temp/tower_hand.txt", builder.ToString());
        return "written";
    }

    // 플레이 여부, 현재 씬, 네트워크 상태, 플레이어·몬스터 수를 한 줄로 돌려준다.
    private static string DescribeStatus()
    {
        string text = "playing=" + EditorApplication.isPlaying
            + " frame=" + Time.frameCount
            + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            + " server=" + Mirror.NetworkServer.active
            + " client=" + Mirror.NetworkClient.isConnected;
        if (EditorApplication.isPlaying)
        {
            GameNetworkManager manager = Mirror.NetworkManager.singleton as GameNetworkManager;
            if (manager != null && manager.Hub != null)
            {
                text += " players=" + manager.Hub.PlayerCount + " lobbyStatus=" + manager.Status;
                if (manager.Hub.MonsterDirector != null)
                {
                    text += " floor=" + manager.Hub.FloorDirector.FloorNumber + " room=" + manager.Hub.FloorDirector.CurrentRoom + " locked=" + manager.Hub.FloorDirector.RoomLocked + " alive=" + manager.Hub.MonsterDirector.AliveCount;
                }
                if (Mirror.NetworkClient.localPlayer != null)
                {
                    PlayerController player = Mirror.NetworkClient.localPlayer.GetComponent<PlayerController>();
                    text += " weapon=" + player.WeaponIndex + " hp=" + player.Hp + " mana=" + player.Mana;
                    MonsterController[] monsters = Object.FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
                    int monsterHp = 0;
                    for (int i = 0; i < monsters.Length; i++)
                    {
                        monsterHp += monsters[i].Hp;
                    }
                    text += " monsterHpSum=" + monsterHp;
                }
            }
        }
        return text;
    }

    // 플레이 중 프레임당 관리 힙 할당량(GC Allocated In Frame)을 300프레임 동안 측정해 Temp/tower_gc.txt에 기록한다.
    // 에디터 자체 할당도 섞이므로 정확한 값은 빌드에서 프로파일러로 다시 확인해야 한다.
    private static void StartGcSample()
    {
        gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", GcSampleFrames);
        gcUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
        totalUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        textureRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        gcSampling = true;
    }

    private static void FinishGcSample()
    {
        gcSampling = false;
        long total = 0;
        long max = 0;
        int zeroFrames = 0;
        int count = gcRecorder.Count;
        for (int i = 0; i < count; i++)
        {
            long value = gcRecorder.GetSample(i).Value;
            total += value;
            if (value > max)
            {
                max = value;
            }
            if (value == 0)
            {
                zeroFrames++;
            }
        }
        string text = "frames=" + count + " avgBytesPerFrame=" + (total / count) + " maxBytes=" + max + " zeroAllocFrames=" + zeroFrames
            + " gcUsedMB=" + (gcUsedRecorder.LastValue / 1048576f).ToString("F1")
            + " totalUsedMB=" + (totalUsedRecorder.LastValue / 1048576f).ToString("F1")
            + " textureMB=" + (textureRecorder.LastValue / 1048576f).ToString("F1");
        File.WriteAllText("Temp/tower_gc.txt", text);
        gcRecorder.Dispose();
        gcUsedRecorder.Dispose();
        totalUsedRecorder.Dispose();
        textureRecorder.Dispose();
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            File.AppendAllText(LogPath, "[" + type + "] " + condition + "\n" + stackTrace + "\n");
        }
    }
}
