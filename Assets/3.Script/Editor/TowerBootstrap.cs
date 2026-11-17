using System.Collections.Generic;
using System.IO;
using System.Text;
using kcp2k;
using LayerLab.ArtMakerUnity;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// 메뉴 Tower > 프로젝트 초기 구성. 레이어, 몬스터 데이터, 프리팹, 게임 씬을 코드로 만든다.
// 다시 실행하면 같은 경로의 프리팹과 씬을 새로 만든다(수동으로 고친 값은 덮어쓴다).
public static class TowerBootstrap
{
    private const string PackageRoot = "Assets/Layer Lab/2D Minimal-CharacterMaker";
    private const string DemoScenePath = PackageRoot + "/Common/Scenes/DemoScene.unity";
    private const string ShadowSpritePath = PackageRoot + "/Extenstions/Parts Pack Base/Character/Default/Shadow.png";
    private const string MapSpritePath = PackageRoot + "/Common/Sprites/_Sample/sample_map.png";
    private const string FontPath = PackageRoot + "/Common/Font/LINESeedSans_SDF_20_Outline.asset";
    private const string TmpPackagePath = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    private const string SpriteRoot = "Assets/4.Sprite";
    // Tools/DungeonGen/GenerateDungeon.ps1로 만든 방 그림
    private const string DungeonSpriteRoot = SpriteRoot + "/Dungeon";
    // 북쪽 벽 정면의 높이와 나머지 벽 윗면의 두께(유닛). 그림 140픽셀, 70픽셀과 같다.
    private const float WallFrontHeight = 1.4f;
    private const float WallRimThickness = 0.7f;
    private const string PrefabFolder = "Assets/2.Model/Prefabs";
    private const string DataFolder = "Assets/2.Model/Data";
    private const string MonsterDataFolder = "Assets/2.Model/Data/Monster";
    private const string WeaponDataFolder = "Assets/2.Model/Data/Weapon";
    private const string ItemBalancePath = DataFolder + "/ItemBalance.asset";
    // 사용자가 클립을 넣는 에셋이라 다시 만들지 않고 빠진 칸만 채운다.
    private const string SoundLibraryPath = DataFolder + "/SoundLibrary.asset";
    private const string JobDataFolder = "Assets/2.Model/Data/Job";
    private static readonly string[] JobAssetNames = { "Swordsman", "Lancer", "Archer", "Mage", "Berserker", "Destroyer", "Sniper", "Gunslinger", "Shaman", "Priest" };
    private static readonly string[] WeaponAssetNames = { "Sword", "Spear", "Bow", "Staff", "Axe", "Blunt", "Crossbow", "Gun", "Wand" };
    private const string SourceScenePath = "Assets/1.Scene/SampleScene.unity";
    private const string GameScenePath = GameNetworkManager.GameScenePath;
    private const string LobbyScenePath = GameNetworkManager.LobbyScenePath;
    private const string KoreanFontSourcePath = "Assets/9.Font/public/static/alternative/Pretendard-Bold.ttf";
    private const string KoreanFontAssetPath = "Assets/9.Font/Pretendard-Bold SDF.asset";
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";
    private const string ResultPath = "Temp/tower_build_result.txt";

    private const float CharacterScale = 1.3f;
    private static readonly Color ShadowColor = new Color(1f, 1f, 1f, 0.45f);
    private static readonly Vector2 ArenaHalfSize = new Vector2(9.2f, 5f);
    private static bool tmpImportTried;
    private static StringBuilder report;

    [MenuItem("Tower/프로젝트 초기 구성 (프리팹과 씬 생성)")]
    public static void BuildAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteResult("fail 플레이 중에는 실행할 수 없습니다.");
        }
        else if (!File.Exists(TmpSettingsPath) && !tmpImportTried)
        {
            tmpImportTried = true;
            AssetDatabase.importPackageCompleted += OnTmpImported;
            AssetDatabase.ImportPackage(TmpPackagePath, false);
            WriteResult("pending TMP 기본 리소스를 가져오는 중");
        }
        else
        {
            report = new StringBuilder();
            try
            {
                BuildInternal();
                WriteResult("ok\n" + report);
                Debug.Log("[Tower] 초기 구성 완료\n" + report);
            }
            catch (System.Exception exception)
            {
                WriteResult("fail " + exception + "\n" + report);
                Debug.LogException(exception);
            }
        }
    }

    private static void OnTmpImported(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnTmpImported;
        EditorApplication.delayCall += BuildAll;
    }

    private static void WriteResult(string text)
    {
        File.WriteAllText(ResultPath, text);
    }

    private static void BuildInternal()
    {
        // 씬을 단독으로 열면 유니티가 쓰지 않는 에셋을 내리면서 이미 불러 둔 참조가 끊긴다.
        // 그래서 씬 전환을 가장 먼저 하고, 씬에 연결할 에셋은 연결 직전에 경로로 다시 불러온다.
        CloseBuiltScenesIfOpen();
        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);
        EnsureFolder(MonsterDataFolder);
        EnsureFolder(WeaponDataFolder);
        EnsureFolder(JobDataFolder);
        SetupLayers();
        SetupRendererSorting();
        AssetDatabase.ImportAsset(SpriteRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        EnsureKoreanFont();
        MonsterDefinition[] definitions = BuildMonsterDefinitions();
        BuildWeaponDefinitions();
        BuildJobDefinitions();
        EnsureSoundLibrary();
        AssetDatabase.SaveAssets();
        // 프리팹을 만드는 동안 임시 오브젝트가 열린 씬을 거쳐 가므로, 사용자가 남긴 변경이 있는지는 만들기 전에 확인해 둔다.
        bool sceneWasDirty = SceneManager.GetActiveScene().isDirty;
        GameObject playerPrefab = BuildPlayerPrefab();
        GameObject monsterPrefab = BuildMonsterPrefab();
        Projectile projectilePrefab = BuildProjectilePrefab();
        BuildItemPickupPrefab();
        DamageText damageTextPrefab = BuildDamageTextPrefab();
        AssetDatabase.SaveAssets();

        Scene previousScene = SceneManager.GetActiveScene();
        BuildGameScene(definitions.Length, previousScene);
        BuildLobbyScene(previousScene);

        // 게임은 로비에서 시작한다.
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(LobbyScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
        AssetDatabase.SaveAssets();

        if (sceneWasDirty)
        {
            report.AppendLine("열려 있던 씬에 저장하지 않은 변경이 있어 새 씬을 열지 않았습니다(참조 검사 생략).");
        }
        else
        {
            VerifyScenes();
            report.AppendLine("로비 씬을 열었습니다.");
        }
    }

    // 효과음 목록 에셋. 이미 있으면 넣어 둔 클립을 그대로 두고 새 종류의 칸만 추가한다.
    private static void EnsureSoundLibrary()
    {
        SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(library, SoundLibraryPath);
            report.AppendLine("효과음 목록을 새로 만들었습니다: " + SoundLibraryPath);
        }
        library.EnsureAllEntries();
        EditorUtility.SetDirty(library);
    }

    // ------------------------------------------------------------ 프로젝트 설정

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    private static void SetupLayers()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        layers.GetArrayElementAtIndex(GameLayers.Player).stringValue = "Player";
        layers.GetArrayElementAtIndex(GameLayers.Monster).stringValue = "Monster";
        layers.GetArrayElementAtIndex(GameLayers.Wall).stringValue = "Wall";
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine("레이어 6 Player, 7 Monster, 8 Wall 등록");
    }

    // 탑다운 시점이라 Y가 낮은(화면 아래쪽) 스프라이트가 앞에 그려지도록 정렬 축을 바꾼다.
    private static void SetupRendererSorting()
    {
        Object rendererData = AssetDatabase.LoadMainAssetAtPath(RendererPath);
        SerializedObject serialized = new SerializedObject(rendererData);
        serialized.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
        serialized.FindProperty("m_TransparencySortAxis").vector3Value = new Vector3(0f, 1f, 0f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine("Renderer2D 정렬 축을 Y로 설정");
    }

    // ------------------------------------------------------------ 직렬화 필드 설정 도우미

    private static SerializedProperty Find(SerializedObject serialized, string property)
    {
        SerializedProperty found = serialized.FindProperty(property);
        if (found == null)
        {
            throw new System.InvalidOperationException(serialized.targetObject.GetType().Name + "." + property + " 필드를 찾을 수 없습니다.");
        }
        return found;
    }

    private static void SetRef(Object target, string property, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        Find(serialized, property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRefArray(SerializedObject serialized, string property, Object[] values)
    {
        SerializedProperty array = Find(serialized, property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new FileNotFoundException("스프라이트를 찾을 수 없습니다: " + path);
        }
        return sprite;
    }

    private static Sprite[] LoadFrames(string monsterName, string state)
    {
        string folder = SpriteRoot + "/Monster/" + monsterName;
        string[] files = Directory.GetFiles(folder, state + "_*.png");
        System.Array.Sort(files, string.CompareOrdinal);
        Sprite[] sprites = new Sprite[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            sprites[i] = LoadSprite(files[i].Replace('\\', '/'));
        }
        if (sprites.Length == 0)
        {
            throw new FileNotFoundException(monsterName + " " + state + " 프레임이 없습니다.");
        }
        return sprites;
    }

    // ------------------------------------------------------------ 몬스터 데이터

    private static MonsterDefinition[] BuildMonsterDefinitions()
    {
        MonsterDefinition slime = CreateDefinition("Slime", "슬라임", MonsterAttackKind.Melee, 0.55f);
        SerializedObject slimeData = new SerializedObject(slime);
        Find(slimeData, "maxHp").intValue = 30;
        Find(slimeData, "moveSpeed").floatValue = 1.6f;
        Find(slimeData, "bodyRadius").floatValue = 0.28f;
        Find(slimeData, "shadowScale").floatValue = 0.9f;
        Find(slimeData, "damage").intValue = 8;
        Find(slimeData, "attackRange").floatValue = 0.95f;
        Find(slimeData, "attackReach").floatValue = 0.6f;
        Find(slimeData, "attackRadius").floatValue = 0.6f;
        Find(slimeData, "attackWindup").floatValue = 0.55f;
        Find(slimeData, "attackCooldown").floatValue = 1.2f;
        slimeData.ApplyModifiedPropertiesWithoutUndo();

        MonsterDefinition bat = CreateDefinition("Bat", "박쥐", MonsterAttackKind.Lunge, 0.4f);
        SerializedObject batData = new SerializedObject(bat);
        Find(batData, "maxHp").intValue = 18;
        Find(batData, "moveSpeed").floatValue = 2.6f;
        Find(batData, "bodyRadius").floatValue = 0.22f;
        Find(batData, "shadowScale").floatValue = 0.6f;
        Find(batData, "damage").intValue = 6;
        Find(batData, "attackRange").floatValue = 2.6f;
        Find(batData, "attackReach").floatValue = 0.4f;
        Find(batData, "attackRadius").floatValue = 0.45f;
        Find(batData, "attackWindup").floatValue = 0.45f;
        Find(batData, "attackCooldown").floatValue = 1.6f;
        Find(batData, "lungeSpeed").floatValue = 8f;
        Find(batData, "lungeDuration").floatValue = 0.3f;
        batData.ApplyModifiedPropertiesWithoutUndo();

        MonsterDefinition mushroom = CreateDefinition("Mushroom", "독버섯", MonsterAttackKind.Ranged, 0.58f);
        SerializedObject mushroomData = new SerializedObject(mushroom);
        Find(mushroomData, "maxHp").intValue = 24;
        Find(mushroomData, "moveSpeed").floatValue = 1.2f;
        Find(mushroomData, "bodyRadius").floatValue = 0.26f;
        Find(mushroomData, "shadowScale").floatValue = 0.8f;
        Find(mushroomData, "damage").intValue = 7;
        Find(mushroomData, "attackRange").floatValue = 5.5f;
        Find(mushroomData, "preferredDistance").floatValue = 4f;
        Find(mushroomData, "attackWindup").floatValue = 0.6f;
        Find(mushroomData, "attackCooldown").floatValue = 2.2f;
        Find(mushroomData, "projectileSpeed").floatValue = 4.5f;
        Find(mushroomData, "projectileLifetime").floatValue = 3f;
        Find(mushroomData, "projectileRadius").floatValue = 0.3f;
        mushroomData.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine("몬스터 데이터 3종 생성(슬라임, 박쥐, 독버섯)");
        return new[] { slime, bat, mushroom };
    }

    private static MonsterDefinition CreateDefinition(string assetName, string displayName, MonsterAttackKind kind, float hitNormalized)
    {
        string path = MonsterDataFolder + "/" + assetName + ".asset";
        MonsterDefinition definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        SerializedObject serialized = new SerializedObject(definition);
        Find(serialized, "displayName").stringValue = displayName;
        Find(serialized, "attackKind").enumValueIndex = (int)kind;
        Find(serialized, "attackHitNormalized").floatValue = hitNormalized;
        Find(serialized, "framesPerSecond").floatValue = 20f;
        SetRefArray(serialized, "idleFrames", LoadFrames(assetName, "Idle"));
        SetRefArray(serialized, "walkFrames", LoadFrames(assetName, "Walk"));
        SetRefArray(serialized, "attackFrames", LoadFrames(assetName, "Attack"));
        SetRefArray(serialized, "hitFrames", LoadFrames(assetName, "Hit"));
        SetRefArray(serialized, "deadFrames", LoadFrames(assetName, "Dead"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    // ------------------------------------------------------------ 무기 데이터
    // 첫 목표의 4계열. 수치는 여기서 정하고, 다시 구성하면 에셋에 덮어쓴다.

    private static void BuildWeaponDefinitions()
    {
        SerializedObject sword = CreateWeapon("Sword", "검", WeaponKind.Sword, PartsType.Sword);
        Find(sword, "damage").intValue = 12;
        Find(sword, "manaOnHit").intValue = 3;
        Find(sword, "hitDelay").floatValue = 0.11f;
        Find(sword, "duration").floatValue = 0.34f;
        Find(sword, "moveFactor").floatValue = 0.25f;
        Find(sword, "allowsShield").boolValue = true;
        Find(sword, "animStartTime").floatValue = 0.75f;
        Find(sword, "animSpeed").floatValue = 2.2f;
        Find(sword, "range").floatValue = 1.5f;
        Find(sword, "arcDegrees").floatValue = 120f;
        Find(sword, "lineHalfWidth").floatValue = 0f;
        Find(sword, "slashScale").vector2Value = new Vector2(1f, 1f);
        Find(sword, "attackSound").enumValueIndex = (int)SoundId.SwordSwing;
        sword.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject spear = CreateWeapon("Spear", "창", WeaponKind.Spear, PartsType.Spear);
        Find(spear, "damage").intValue = 16;
        Find(spear, "manaOnHit").intValue = 4;
        Find(spear, "hitDelay").floatValue = 0.16f;
        Find(spear, "duration").floatValue = 0.5f;
        Find(spear, "moveFactor").floatValue = 0.15f;
        Find(spear, "allowsShield").boolValue = false;
        Find(spear, "animStartTime").floatValue = 0.75f;
        Find(spear, "animSpeed").floatValue = 1.8f;
        Find(spear, "range").floatValue = 2.3f;
        Find(spear, "lineHalfWidth").floatValue = 0.35f;
        Find(spear, "slashScale").vector2Value = new Vector2(1.5f, 0.4f);
        Find(spear, "statusKind").enumValueIndex = (int)StatusKind.Bleed;
        Find(spear, "statusDuration").floatValue = 3f;
        Find(spear, "statusPower").floatValue = 0.08f;
        Find(spear, "attackSound").enumValueIndex = (int)SoundId.SpearThrust;
        spear.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject bow = CreateWeapon("Bow", "활", WeaponKind.Bow, PartsType.Bow);
        Find(bow, "damage").intValue = 22;
        Find(bow, "manaOnHit").intValue = 4;
        Find(bow, "hitDelay").floatValue = 0.04f;
        Find(bow, "duration").floatValue = 0.28f;
        Find(bow, "moveFactor").floatValue = 0.45f;
        Find(bow, "allowsShield").boolValue = false;
        Find(bow, "animStartTime").floatValue = 0.75f;
        Find(bow, "animSpeed").floatValue = 2.2f;
        Find(bow, "projectileSpeed").floatValue = 13f;
        Find(bow, "projectileLifetime").floatValue = 1.2f;
        Find(bow, "projectileRadius").floatValue = 0.22f;
        Find(bow, "explosionRadius").floatValue = 0f;
        Find(bow, "projectileLook").enumValueIndex = (int)ProjectileLook.Arrow;
        Find(bow, "projectileColor").colorValue = new Color(0.98f, 0.93f, 0.78f);
        Find(bow, "chargeTime").floatValue = 0.7f;
        Find(bow, "minChargeFactor").floatValue = 0.35f;
        Find(bow, "attackSound").enumValueIndex = (int)SoundId.BowRelease;
        bow.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject staff = CreateWeapon("Staff", "지팡이", WeaponKind.Staff, PartsType.Staff);
        Find(staff, "damage").intValue = 14;
        Find(staff, "manaOnHit").intValue = 2;
        Find(staff, "hitDelay").floatValue = 0.22f;
        Find(staff, "duration").floatValue = 0.55f;
        Find(staff, "moveFactor").floatValue = 0.35f;
        Find(staff, "allowsShield").boolValue = false;
        Find(staff, "anim").enumValueIndex = (int)CharacterAnim.Skill;
        Find(staff, "animStartTime").floatValue = 0.3f;
        Find(staff, "animSpeed").floatValue = 1.6f;
        Find(staff, "projectileSpeed").floatValue = 7f;
        Find(staff, "projectileLifetime").floatValue = 0.9f;
        Find(staff, "projectileRadius").floatValue = 0.25f;
        Find(staff, "explosionRadius").floatValue = 1.1f;
        Find(staff, "projectileLook").enumValueIndex = (int)ProjectileLook.Orb;
        Find(staff, "projectileColor").colorValue = new Color(0.55f, 0.75f, 1f);
        Find(staff, "chargeTime").floatValue = 0f;
        Find(staff, "statusKind").enumValueIndex = (int)StatusKind.Burn;
        Find(staff, "statusDuration").floatValue = 3f;
        Find(staff, "statusPower").floatValue = 0.1f;
        Find(staff, "attackSound").enumValueIndex = (int)SoundId.MagicShot;
        staff.ApplyModifiedPropertiesWithoutUndo();

        // 도끼: 넓고 느린 휘두르기, 출혈
        SerializedObject axe = CreateWeapon("Axe", "도끼", WeaponKind.Axe, PartsType.Axe);
        Find(axe, "damage").intValue = 18;
        Find(axe, "manaOnHit").intValue = 4;
        Find(axe, "hitDelay").floatValue = 0.17f;
        Find(axe, "duration").floatValue = 0.5f;
        Find(axe, "moveFactor").floatValue = 0.2f;
        Find(axe, "allowsShield").boolValue = false;
        Find(axe, "range").floatValue = 1.7f;
        Find(axe, "arcDegrees").floatValue = 160f;
        Find(axe, "lineHalfWidth").floatValue = 0f;
        Find(axe, "slashScale").vector2Value = new Vector2(1.15f, 1.15f);
        Find(axe, "statusKind").enumValueIndex = (int)StatusKind.Bleed;
        Find(axe, "statusDuration").floatValue = 3f;
        Find(axe, "statusPower").floatValue = 0.1f;
        Find(axe, "attackSound").enumValueIndex = (int)SoundId.HeavySwing;
        axe.ApplyModifiedPropertiesWithoutUndo();

        // 둔기: 느리고 묵직하다, 기절
        SerializedObject blunt = CreateWeapon("Blunt", "둔기", WeaponKind.Blunt, PartsType.Blunt);
        Find(blunt, "damage").intValue = 20;
        Find(blunt, "manaOnHit").intValue = 5;
        Find(blunt, "hitDelay").floatValue = 0.2f;
        Find(blunt, "duration").floatValue = 0.56f;
        Find(blunt, "moveFactor").floatValue = 0.2f;
        Find(blunt, "allowsShield").boolValue = true;
        Find(blunt, "range").floatValue = 1.4f;
        Find(blunt, "arcDegrees").floatValue = 110f;
        Find(blunt, "lineHalfWidth").floatValue = 0f;
        Find(blunt, "slashScale").vector2Value = new Vector2(0.95f, 0.95f);
        Find(blunt, "statusKind").enumValueIndex = (int)StatusKind.Stun;
        Find(blunt, "statusDuration").floatValue = 0.5f;
        Find(blunt, "statusPower").floatValue = 0f;
        Find(blunt, "attackSound").enumValueIndex = (int)SoundId.HeavySwing;
        blunt.ApplyModifiedPropertiesWithoutUndo();

        // 석궁: 차지 없이 빠르고 강한 한 발
        SerializedObject crossbow = CreateWeapon("Crossbow", "석궁", WeaponKind.Crossbow, PartsType.Crossbow);
        Find(crossbow, "damage").intValue = 19;
        Find(crossbow, "manaOnHit").intValue = 4;
        Find(crossbow, "hitDelay").floatValue = 0.05f;
        Find(crossbow, "duration").floatValue = 0.55f;
        Find(crossbow, "moveFactor").floatValue = 0.45f;
        Find(crossbow, "allowsShield").boolValue = false;
        Find(crossbow, "projectileSpeed").floatValue = 18f;
        Find(crossbow, "projectileLifetime").floatValue = 0.9f;
        Find(crossbow, "projectileRadius").floatValue = 0.22f;
        Find(crossbow, "explosionRadius").floatValue = 0f;
        Find(crossbow, "projectileLook").enumValueIndex = (int)ProjectileLook.Arrow;
        Find(crossbow, "projectileColor").colorValue = new Color(0.85f, 0.85f, 0.95f);
        Find(crossbow, "chargeTime").floatValue = 0f;
        Find(crossbow, "attackSound").enumValueIndex = (int)SoundId.CrossbowShot;
        crossbow.ApplyModifiedPropertiesWithoutUndo();

        // 총: 짧은 사거리의 빠른 연사
        SerializedObject gun = CreateWeapon("Gun", "총", WeaponKind.Gun, PartsType.Gun);
        Find(gun, "damage").intValue = 8;
        Find(gun, "manaOnHit").intValue = 2;
        Find(gun, "hitDelay").floatValue = 0.02f;
        Find(gun, "duration").floatValue = 0.2f;
        Find(gun, "moveFactor").floatValue = 0.6f;
        Find(gun, "allowsShield").boolValue = false;
        Find(gun, "projectileSpeed").floatValue = 20f;
        Find(gun, "projectileLifetime").floatValue = 0.4f;
        Find(gun, "projectileRadius").floatValue = 0.18f;
        Find(gun, "explosionRadius").floatValue = 0f;
        Find(gun, "projectileLook").enumValueIndex = (int)ProjectileLook.Bullet;
        Find(gun, "projectileColor").colorValue = new Color(1f, 0.85f, 0.4f);
        Find(gun, "chargeTime").floatValue = 0f;
        Find(gun, "attackSound").enumValueIndex = (int)SoundId.GunShot;
        gun.ApplyModifiedPropertiesWithoutUndo();

        // 완드: 가볍고 빠른 마법 탄(폭발 없음)
        SerializedObject wand = CreateWeapon("Wand", "완드", WeaponKind.Wand, PartsType.Wand);
        Find(wand, "damage").intValue = 10;
        Find(wand, "manaOnHit").intValue = 3;
        Find(wand, "hitDelay").floatValue = 0.08f;
        Find(wand, "duration").floatValue = 0.3f;
        Find(wand, "moveFactor").floatValue = 0.5f;
        Find(wand, "allowsShield").boolValue = true;
        Find(wand, "projectileSpeed").floatValue = 11f;
        Find(wand, "projectileLifetime").floatValue = 0.8f;
        Find(wand, "projectileRadius").floatValue = 0.2f;
        Find(wand, "explosionRadius").floatValue = 0f;
        Find(wand, "projectileLook").enumValueIndex = (int)ProjectileLook.Orb;
        Find(wand, "projectileColor").colorValue = new Color(0.75f, 0.55f, 1f);
        Find(wand, "chargeTime").floatValue = 0f;
        Find(wand, "attackSound").enumValueIndex = (int)SoundId.MagicShot;
        wand.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine("무기 데이터 9종 생성(검, 창, 활, 지팡이, 도끼, 둔기, 석궁, 총, 완드)");
    }

    private static SerializedObject CreateWeapon(string assetName, string displayName, WeaponKind kind, PartsType partsType)
    {
        string path = WeaponDataFolder + "/" + assetName + ".asset";
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        SerializedObject serialized = new SerializedObject(definition);
        Find(serialized, "displayName").stringValue = displayName;
        Find(serialized, "kind").enumValueIndex = (int)kind;
        Find(serialized, "partsType").enumValueIndex = (int)partsType;
        Find(serialized, "partsIndex").intValue = 0;
        Find(serialized, "anim").enumValueIndex = (int)CharacterAnim.Attack;
        EditorUtility.SetDirty(definition);
        return serialized;
    }

    // ------------------------------------------------------------ 프리팹

    private static GameObject SavePrefab(GameObject root, string fileName)
    {
        string path = PrefabFolder + "/" + fileName + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        // Mirror는 프리팹 GUID로 assetId를 만든다. 코드로 만든 프리팹에 값이 비어 있으면 직접 채운다.
        NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
        if (identity != null)
        {
            SerializedObject serialized = new SerializedObject(identity);
            SerializedProperty assetId = Find(serialized, "_assetId");
            if (assetId.uintValue == 0)
            {
                System.Guid guid = new System.Guid(AssetDatabase.AssetPathToGUID(path));
                assetId.uintValue = NetworkIdentity.AssetGuidToUint(guid);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(prefab);
            }
            report.AppendLine(fileName + " assetId=" + Find(new SerializedObject(identity), "_assetId").uintValue);
        }
        return prefab;
    }

    private static Rigidbody2D AddBody(GameObject root, float damping)
    {
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearDamping = damping;
        return body;
    }

    private static void AddNetworkTransform(GameObject root, SyncDirection direction)
    {
        NetworkTransformUnreliable networkTransform = root.AddComponent<NetworkTransformUnreliable>();
        networkTransform.target = root.transform;
        networkTransform.syncDirection = direction;
        networkTransform.syncRotation = false;
        networkTransform.syncScale = false;
    }

    private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        return renderer;
    }

    // 패키지 데모 씬에 있는 설정 완료된 캐릭터(PartsManager 포함)를 복제해 온다.
    private static GameObject CloneDemoCharacter()
    {
        Scene demoScene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
        PartsManager source = null;
        GameObject[] roots = demoScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && source == null; i++)
        {
            source = roots[i].GetComponentInChildren<PartsManager>(true);
        }
        if (source == null)
        {
            EditorSceneManager.CloseScene(demoScene, true);
            throw new System.InvalidOperationException("데모 씬에서 PartsManager를 찾지 못했습니다.");
        }

        GameObject clone = Object.Instantiate(source.gameObject);
        clone.name = "Visual";
        report.AppendLine("데모 캐릭터 원본: " + source.gameObject.name + ", lossyScale=" + source.transform.lossyScale);
        EditorSceneManager.CloseScene(demoScene, true);

        // 패키지의 Player는 싱글턴이라 여러 명이 있으면 서로 파괴한다. 제거한다.
        Player demoPlayer = clone.GetComponent<Player>();
        if (demoPlayer != null)
        {
            Object.DestroyImmediate(demoPlayer);
        }

        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);
        clone.SetActive(true);

        Component[] components = clone.GetComponents<Component>();
        StringBuilder names = new StringBuilder();
        for (int i = 0; i < components.Length; i++)
        {
            names.Append(components[i].GetType().Name).Append(' ');
        }
        report.AppendLine("Visual 컴포넌트: " + names);
        return clone;
    }

    // ------------------------------------------------------------ 직업과 스킬
    // 직업 10개(WeaponKind 순서 9개 + 사제). 직업마다 스킬 6개와 스킬 트리(갈래 3개 x 4단계).
    // 갈래마다 [스킬(1단계), 능력치(3단계), 스킬(1단계), 능력치(3단계)]. 갈래 b의 스킬은 b*2, b*2+1번이다.
    // 다 찍으려면 24포인트가 들고 최대 레벨까지 15포인트를 받으므로, 갈래를 골라 찍게 된다.

    private static readonly Color FxRed = new Color(1f, 0.45f, 0.3f);
    private static readonly Color FxOrange = new Color(1f, 0.65f, 0.25f);
    private static readonly Color FxYellow = new Color(1f, 0.9f, 0.4f);
    private static readonly Color FxBlue = new Color(0.5f, 0.75f, 1f);
    private static readonly Color FxCyan = new Color(0.55f, 0.95f, 1f);
    private static readonly Color FxPurple = new Color(0.8f, 0.55f, 1f);
    private static readonly Color FxGreen = new Color(0.55f, 1f, 0.5f);
    private static readonly Color FxPoison = new Color(0.6f, 0.9f, 0.3f);
    private static readonly Color FxGold = new Color(1f, 0.85f, 0.45f);
    private static readonly Color FxWhite = new Color(0.95f, 0.95f, 1f);

    private static void BuildJobDefinitions()
    {
        // 0 검사
        SkillDefinition s00 = Fx(MakeArea("SwordSpin", "회전 베기", 12, 5f, 0.35f, 1.6f, 2.2f, CharacterAnim.DoubleAttack, FxRed), SkillFx.Slash, SoundId.SkillSlash, "몸을 한 바퀴 돌려 주변을 베고 출혈을 남깁니다.");
        SkillDefinition s01 = Fx(MakeArea("SwordCross", "십자 베기", 16, 8f, 0.4f, 2.6f, 1.6f, CharacterAnim.DoubleAttack, FxWhite), SkillFx.Slash, SoundId.SkillSlash, "가까운 적을 두 번 크게 베어 큰 피해를 줍니다.");
        SkillDefinition s02 = Fx(MakeDash("SwordBash", "방패 밀치기", 10, 7f, 0.2f, 1.2f, 2f, 0.8f, false, CharacterAnim.JumpAttack, FxGold), SkillFx.Shockwave, SoundId.SkillDash, "짧게 밀고 들어가 길목의 적을 기절시킵니다.");
        SkillDefinition s03 = Fx(Ally(MakeBuff("SwordGuard", "수호의 함성", 18, 18f, 0.3f, JobNodeEffect.DefensePercent, 30f, 8f, FxGold), 3.5f), SkillFx.Aura, SoundId.SkillBuff, "주변 아군 모두 받는 피해가 줄어듭니다.");
        SkillDefinition s04 = Fx(MakeDash("SwordCharge", "돌진 베기", 10, 6f, 0.25f, 1.3f, 4.5f, 0.8f, true, CharacterAnim.JumpAttack, FxBlue), SkillFx.Wind, SoundId.SkillDash, "조준 방향으로 무적 돌진하며 베고 기절시킵니다.");
        SkillDefinition s05 = Fx(MakeBuff("SwordRoar", "전투 함성", 20, 18f, 0.3f, JobNodeEffect.DamagePercent, 35f, 8f, FxRed), SkillFx.Aura, SoundId.SkillBuff, "잠시 공격력이 크게 오릅니다.");
        SetSkillStatus(s00, StatusKind.Bleed, 4f, 0.12f);
        SetSkillStatus(s02, StatusKind.Stun, 1f, 0f);
        SetSkillStatus(s04, StatusKind.Stun, 0.8f, 0f);
        MakeJob(0, "검사", WeaponKind.Sword, "검과 방패를 쓰는 균형 잡힌 근접 직업.", new[] { "검술", "수호", "질풍" },
            new[] { s00, s01, s02, s03, s04, s05 },
            new[] { "날 세우기", "급소 노리기", "단련", "버티기", "가벼운 발", "숙련" },
            new[] { JobNodeEffect.DamagePercent, JobNodeEffect.CritChancePercent, JobNodeEffect.MaxHp, JobNodeEffect.DefensePercent, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.CooldownPercent },
            new[] { 5f, 4f, 12f, 4f, 4f, 5f });

        // 1 창기사
        SkillDefinition s10 = Fx(MakeDash("SpearPierce", "꿰뚫기", 12, 6f, 0.28f, 1.8f, 5.5f, 0.7f, false, CharacterAnim.JumpAttack, FxYellow), SkillFx.Wind, SoundId.SkillDash, "길게 찔러 들어가며 길목의 적에게 출혈을 남깁니다.");
        SkillDefinition s11 = Fx(MakeVolley("SpearThrow", "뇌창 투척", 16, 8f, 0.3f, 2.2f, 1, 0f, 16f, 0.8f, 0.3f, 1.4f, ProjectileLook.Arrow, FxCyan), SkillFx.Lightning, SoundId.SkillLightning, "번개를 두른 창을 던져 닿은 곳에 번개를 떨어뜨립니다.");
        SkillDefinition s12 = Fx(MakeArea("SpearSweep", "휩쓸기", 12, 5f, 0.35f, 1.3f, 2.8f, CharacterAnim.DoubleAttack, FxYellow), SkillFx.Slash, SoundId.SkillSlash, "긴 창으로 넓게 휩쓸어 적을 느리게 합니다.");
        SkillDefinition s13 = Fx(MakeBuff("SpearGale", "질풍", 18, 18f, 0.3f, JobNodeEffect.AttackSpeedPercent, 40f, 8f, FxBlue), SkillFx.Aura, SoundId.SkillBuff, "잠시 공격 속도가 크게 오릅니다.");
        SkillDefinition s14 = Fx(Slam(MakeDash("SpearLeap", "도약 찍기", 16, 9f, 0.4f, 0f, 4f, 0.5f, true, CharacterAnim.JumpAttack, FxOrange), 2.2f, 2f), SkillFx.Shockwave, SoundId.SkillDash, "높이 뛰어 착지한 자리를 내려찍어 기절시킵니다.");
        SkillDefinition s15 = Fx(MakeZone("SpearStorm", "낙뢰 지대", 22, 14f, 0.35f, 0.5f, 1.8f, 2.5f, 4f, 0f, FxCyan), SkillFx.Lightning, SoundId.SkillLightning, "창을 꽂은 자리에 번개가 계속 떨어집니다.");
        SetSkillStatus(s10, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s12, StatusKind.Slow, 3f, 0f);
        SetSkillStatus(s14, StatusKind.Stun, 0.6f, 0f);
        SetSkillStatus(s15, StatusKind.Slow, 1f, 0f);
        MakeJob(1, "창기사", WeaponKind.Spear, "긴 사거리로 찌르고 휩쓰는 근접 직업.", new[] { "돌파", "연격", "용기병" },
            new[] { s10, s11, s12, s13, s14, s15 },
            new[] { "창술", "힘 싣기", "빠른 손", "기세", "발놀림", "용의 피" },
            new[] { JobNodeEffect.SkillDamagePercent, JobNodeEffect.DamagePercent, JobNodeEffect.AttackSpeedPercent, JobNodeEffect.ManaOnHit, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.MaxHp },
            new[] { 8f, 5f, 5f, 1f, 4f, 12f });

        // 2 레인저
        SkillDefinition s20 = Fx(MakeVolley("BowFan", "부채 사격", 10, 4f, 0.3f, 0.9f, 5, 40f, 14f, 0.9f, 0.18f, 0f, ProjectileLook.Arrow, FxYellow), SkillFx.Wind, SoundId.BowRelease, "화살 다섯 발을 부채꼴로 쏘아 적을 느리게 합니다.");
        SkillDefinition s21 = Fx(MakeVolley("BowBlast", "폭발 화살", 20, 10f, 0.35f, 2.5f, 1, 0f, 14f, 1f, 0.2f, 2f, ProjectileLook.Arrow, FxRed), SkillFx.Fire, SoundId.SkillFire, "닿으면 터지는 화살로 주변을 불태웁니다.");
        SkillDefinition s22 = Fx(MakeDash("BowLeap", "회피 도약", 8, 5f, 0.22f, 0f, 4f, 0.5f, true, CharacterAnim.Roll, FxBlue), SkillFx.Wind, SoundId.SkillDash, "무적 상태로 빠르게 몸을 날립니다.");
        SkillDefinition s23 = Fx(MakeBuff("BowFocus", "사냥 본능", 16, 16f, 0.3f, JobNodeEffect.CritChancePercent, 25f, 8f, FxOrange), SkillFx.Aura, SoundId.SkillBuff, "잠시 치명타 확률이 크게 오릅니다.");
        SkillDefinition s24 = Fx(MakeZone("BowRain", "독 화살비", 20, 12f, 0.35f, 0.45f, 2.2f, 4f, 3.5f, 0f, FxPoison), SkillFx.Poison, SoundId.SkillDark, "조준한 곳에 독 화살비를 내려 적을 느리게 합니다.");
        SkillDefinition s25 = Fx(MakeHeal("BowBless", "자연의 숨결", 18, 18f, 0.4f, 1.2f, 25f, FxGreen), SkillFx.Heal, SoundId.Heal, "숨을 고르며 체력을 회복합니다.");
        SetSkillStatus(s20, StatusKind.Slow, 2.5f, 0f);
        SetSkillStatus(s21, StatusKind.Burn, 4f, 0.12f);
        SetSkillStatus(s24, StatusKind.Slow, 1f, 0f);
        MakeJob(2, "레인저", WeaponKind.Bow, "활을 당겨 쏘는 기동형 원거리 직업.", new[] { "속사", "사냥꾼", "자연" },
            new[] { s20, s21, s22, s23, s24, s25 },
            new[] { "빠른 시위", "강한 시위", "가벼운 발", "약점 간파", "숲의 지혜", "질긴 몸" },
            new[] { JobNodeEffect.AttackSpeedPercent, JobNodeEffect.DamagePercent, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.CritChancePercent, JobNodeEffect.CooldownPercent, JobNodeEffect.MaxHp },
            new[] { 5f, 5f, 4f, 5f, 5f, 10f });

        // 3 원소술사
        SkillDefinition s30 = Fx(MakeVolley("StaffFireball", "화염구", 14, 4f, 0.3f, 2.2f, 1, 0f, 10f, 1.2f, 0.25f, 2.2f, ProjectileLook.Orb, FxRed), SkillFx.Fire, SoundId.SkillFire, "터지는 불덩이를 던져 주변을 불태웁니다.");
        SkillDefinition s31 = Fx(MakeZone("StaffFlame", "화염 지대", 20, 12f, 0.35f, 0.5f, 2f, 3.5f, 4f, 0f, FxOrange), SkillFx.Fire, SoundId.SkillFire, "조준한 곳을 불바다로 만듭니다.");
        SkillDefinition s32 = Fx(MakeVolley("StaffIce", "얼음 창", 14, 5f, 0.3f, 1.2f, 3, 20f, 13f, 1f, 0.22f, 0f, ProjectileLook.Orb, FxCyan), SkillFx.Frost, SoundId.SkillIce, "얼음 창 세 개를 날려 적을 느리게 합니다.");
        SkillDefinition s33 = Fx(MakeArea("StaffNova", "서리 폭발", 18, 9f, 0.4f, 1.4f, 2.6f, CharacterAnim.Skill, FxCyan), SkillFx.Frost, SoundId.SkillIce, "주변을 얼려 적을 크게 느리게 합니다.");
        SkillDefinition s34 = Fx(MakeVolley("StaffStorm", "연쇄 번개", 22, 12f, 0.4f, 1.2f, 8, 315f, 9f, 1.1f, 0.22f, 0f, ProjectileLook.Orb, FxPurple), SkillFx.Lightning, SoundId.SkillLightning, "사방으로 번개 구슬을 흩뿌려 적을 약하게 만듭니다.");
        SkillDefinition s35 = Fx(MakeDash("StaffBlink", "점멸", 10, 6f, 0.12f, 0f, 4.5f, 0.5f, true, CharacterAnim.Skill, FxPurple), SkillFx.Arcane, SoundId.SkillDark, "조준 방향으로 순간 이동합니다.");
        SetSkillStatus(s30, StatusKind.Burn, 4f, 0.15f);
        SetSkillStatus(s31, StatusKind.Burn, 2f, 0.1f);
        SetSkillStatus(s32, StatusKind.Slow, 2.5f, 0f);
        SetSkillStatus(s33, StatusKind.Slow, 4f, 0f);
        SetSkillStatus(s34, StatusKind.Weaken, 5f, 0f);
        MakeJob(3, "원소술사", WeaponKind.Staff, "불, 얼음, 번개를 다루는 광역 마법 직업.", new[] { "화염", "냉기", "번개" },
            new[] { s30, s31, s32, s33, s34, s35 },
            new[] { "불길", "마력 그릇", "빠른 영창", "냉정", "마력 흡수", "집중" },
            new[] { JobNodeEffect.SkillDamagePercent, JobNodeEffect.MaxMana, JobNodeEffect.CooldownPercent, JobNodeEffect.DefensePercent, JobNodeEffect.ManaOnHit, JobNodeEffect.SkillDamagePercent },
            new[] { 8f, 10f, 5f, 4f, 1f, 6f });

        // 4 광전사
        SkillDefinition s40 = Fx(MakeBuff("AxeRage", "피의 분노", 16, 16f, 0.3f, JobNodeEffect.DamagePercent, 45f, 6f, FxRed), SkillFx.Aura, SoundId.SkillBuff, "짧은 시간 공격력이 폭발적으로 오릅니다.");
        SkillDefinition s41 = Fx(MakeArea("AxeWhirl", "광폭 회전", 16, 6f, 0.45f, 1.8f, 2.4f, CharacterAnim.DoubleAttack, FxRed), SkillFx.Slash, SoundId.SkillSlash, "도끼를 휘돌려 주변 적에게 깊은 출혈을 남깁니다.");
        SkillDefinition s42 = Fx(MakeVolley("AxeThrow", "도끼 투척", 12, 5f, 0.3f, 2f, 1, 0f, 12f, 0.8f, 0.32f, 0f, ProjectileLook.Orb, FxOrange), SkillFx.Slash, SoundId.HeavySwing, "도끼를 던져 맞은 적에게 출혈을 남깁니다.");
        SkillDefinition s43 = Fx(MakeDash("AxeExecute", "처형", 18, 9f, 0.3f, 2.8f, 2.5f, 0.8f, false, CharacterAnim.JumpAttack, FxRed), SkillFx.Slash, SoundId.SkillSlash, "짧게 달려들어 앞의 적에게 강력한 일격을 내립니다.");
        SkillDefinition s44 = Fx(MakeArea("AxeSplit", "대지 가르기", 16, 8f, 0.45f, 2.4f, 1.8f, CharacterAnim.JumpAttack, FxOrange), SkillFx.Shockwave, SoundId.HeavySwing, "땅을 내리쳐 주변 적을 잠시 기절시킵니다.");
        SkillDefinition s45 = Fx(MakeBuff("AxeEndure", "불굴", 16, 18f, 0.3f, JobNodeEffect.DefensePercent, 35f, 6f, FxOrange), SkillFx.Aura, SoundId.SkillBuff, "잠시 받는 피해가 크게 줄어듭니다.");
        SetSkillStatus(s41, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s42, StatusKind.Bleed, 4f, 0.12f);
        SetSkillStatus(s44, StatusKind.Stun, 0.6f, 0f);
        MakeJob(4, "광전사", WeaponKind.Axe, "도끼를 휘두르며 피를 흘리게 하는 공격형 근접 직업.", new[] { "분노", "학살", "불굴" },
            new[] { s40, s41, s42, s43, s44, s45 },
            new[] { "광기", "피 냄새", "급소 노리기", "무거운 손", "질긴 몸", "투지" },
            new[] { JobNodeEffect.DamagePercent, JobNodeEffect.ManaOnHit, JobNodeEffect.CritChancePercent, JobNodeEffect.SkillDamagePercent, JobNodeEffect.MaxHp, JobNodeEffect.DefensePercent },
            new[] { 6f, 1f, 4f, 8f, 15f, 4f });

        // 5 파괴자
        SkillDefinition s50 = Fx(MakeArea("BluntSlam", "내려찍기", 14, 6f, 0.45f, 2f, 2f, CharacterAnim.JumpAttack, FxOrange), SkillFx.Shockwave, SoundId.HeavySwing, "둔기로 땅을 내려찍어 주변 적을 기절시킵니다.");
        SkillDefinition s51 = Fx(MakeZone("BluntQuake", "지진", 22, 14f, 0.45f, 0.6f, 2.4f, 0f, 3f, 0f, FxOrange), SkillFx.Shockwave, SoundId.HeavySwing, "발밑에 지진을 일으켜 주변 적을 계속 흔들고 느리게 합니다.");
        SkillDefinition s52 = Fx(Slam(MakeDash("BluntLeap", "도약 강타", 18, 9f, 0.45f, 0f, 4f, 0.5f, true, CharacterAnim.JumpAttack, FxOrange), 2.4f, 2.2f), SkillFx.Shockwave, SoundId.SkillDash, "뛰어올라 착지한 자리를 강하게 내려칩니다.");
        SkillDefinition s53 = Fx(MakeDash("BluntRam", "파쇄 돌진", 14, 7f, 0.3f, 1.4f, 4f, 0.9f, false, CharacterAnim.JumpAttack, FxYellow), SkillFx.Wind, SoundId.SkillDash, "몸으로 밀고 들어가 길목의 적을 기절시킵니다.");
        SkillDefinition s54 = Fx(MakeBuff("BluntWall", "철벽", 18, 18f, 0.3f, JobNodeEffect.DefensePercent, 45f, 6f, FxGold), SkillFx.Aura, SoundId.SkillBuff, "잠시 받는 피해가 아주 크게 줄어듭니다.");
        SkillDefinition s55 = Fx(MakeHeal("BluntVigor", "대지의 활력", 18, 20f, 0.4f, 1.2f, 25f, FxGreen), SkillFx.Heal, SoundId.Heal, "땅의 기운으로 체력을 회복합니다.");
        SetSkillStatus(s50, StatusKind.Stun, 0.8f, 0f);
        SetSkillStatus(s51, StatusKind.Slow, 1f, 0f);
        SetSkillStatus(s52, StatusKind.Stun, 0.8f, 0f);
        SetSkillStatus(s53, StatusKind.Stun, 0.6f, 0f);
        MakeJob(5, "파괴자", WeaponKind.Blunt, "느리지만 묵직하게 적을 기절시키는 근접 직업.", new[] { "분쇄", "돌격", "철벽" },
            new[] { s50, s51, s52, s53, s54, s55 },
            new[] { "무게 싣기", "울림", "돌격", "가속", "단단한 몸", "재생" },
            new[] { JobNodeEffect.DamagePercent, JobNodeEffect.SkillDamagePercent, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.CooldownPercent, JobNodeEffect.MaxHp, JobNodeEffect.DefensePercent },
            new[] { 6f, 8f, 4f, 5f, 15f, 5f });

        // 6 저격수
        SkillDefinition s60 = Fx(MakeVolley("CrossPierce", "관통 볼트", 14, 6f, 0.35f, 3f, 1, 0f, 24f, 1f, 0.25f, 0f, ProjectileLook.Arrow, FxWhite), SkillFx.Wind, SoundId.CrossbowShot, "아주 빠른 볼트로 큰 피해와 출혈을 줍니다.");
        SkillDefinition s61 = Fx(MakeBuff("CrossAim", "집중 조준", 16, 16f, 0.3f, JobNodeEffect.CritChancePercent, 35f, 6f, FxYellow), SkillFx.Aura, SoundId.SkillBuff, "잠시 치명타 확률이 아주 크게 오릅니다.");
        SkillDefinition s62 = Fx(MakeVolley("CrossBurst", "연발 볼트", 12, 5f, 0.3f, 1f, 3, 12f, 20f, 0.9f, 0.22f, 0f, ProjectileLook.Arrow, FxWhite), SkillFx.Wind, SoundId.CrossbowShot, "볼트 세 발을 좁게 연달아 쏩니다.");
        SkillDefinition s63 = Fx(MakeVolley("CrossBomb", "폭발 볼트", 18, 9f, 0.35f, 2.2f, 1, 0f, 18f, 0.9f, 0.22f, 2f, ProjectileLook.Arrow, FxRed), SkillFx.Fire, SoundId.SkillFire, "닿으면 터지는 볼트로 주변을 불태웁니다.");
        SkillDefinition s64 = Fx(MakeZone("CrossTrap", "얼음 덫", 16, 12f, 0.3f, 0.4f, 1.6f, 3f, 6f, 0f, FxCyan), SkillFx.Frost, SoundId.SkillIce, "조준한 곳에 얼음 덫을 깔아 밟은 적을 느리게 합니다.");
        SkillDefinition s65 = Fx(MakeDash("CrossRoll", "후방 구르기", 8, 5f, 0.22f, 0f, 3.5f, 0.5f, true, CharacterAnim.Roll, FxBlue), SkillFx.Wind, SoundId.Roll, "무적 상태로 몸을 굴려 거리를 벌립니다.");
        SetSkillStatus(s60, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s63, StatusKind.Burn, 4f, 0.12f);
        SetSkillStatus(s64, StatusKind.Slow, 1.5f, 0f);
        MakeJob(6, "저격수", WeaponKind.Crossbow, "석궁으로 한 발 한 발 강하게 쏘는 원거리 직업.", new[] { "저격", "폭약", "덫" },
            new[] { s60, s61, s62, s63, s64, s65 },
            new[] { "강한 시위", "약점 간파", "장전 숙련", "화약 다루기", "가벼운 발", "숙련" },
            new[] { JobNodeEffect.DamagePercent, JobNodeEffect.CritChancePercent, JobNodeEffect.AttackSpeedPercent, JobNodeEffect.SkillDamagePercent, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.CooldownPercent },
            new[] { 6f, 5f, 5f, 8f, 4f, 5f });

        // 7 건슬링어
        SkillDefinition s70 = Fx(MakeVolley("GunSpray", "난사", 14, 6f, 0.35f, 0.7f, 8, 50f, 20f, 0.45f, 0.16f, 0f, ProjectileLook.Bullet, FxYellow), SkillFx.Fire, SoundId.GunShot, "총알 여덟 발을 넓게 흩뿌립니다.");
        SkillDefinition s71 = Fx(MakeBuff("GunRapid", "속사 태세", 16, 16f, 0.3f, JobNodeEffect.AttackSpeedPercent, 50f, 6f, FxYellow), SkillFx.Aura, SoundId.SkillBuff, "잠시 공격 속도가 아주 크게 오릅니다.");
        SkillDefinition s72 = Fx(MakeVolley("GunShotgun", "산탄", 12, 5f, 0.3f, 0.9f, 6, 35f, 18f, 0.3f, 0.18f, 0f, ProjectileLook.Bullet, FxOrange), SkillFx.Fire, SoundId.GunShot, "가까운 적에게 산탄을 퍼부어 느리게 합니다.");
        SkillDefinition s73 = Fx(MakeVolley("GunGrenade", "수류탄", 18, 9f, 0.35f, 2.2f, 1, 0f, 9f, 0.8f, 0.24f, 2.4f, ProjectileLook.Orb, FxOrange), SkillFx.Fire, SoundId.Explosion, "수류탄을 던져 넓게 터뜨립니다.");
        SkillDefinition s74 = Fx(MakeZone("GunPowder", "화약 지대", 18, 12f, 0.35f, 0.5f, 2f, 3f, 3f, 0f, FxRed), SkillFx.Fire, SoundId.SkillFire, "화약을 뿌리고 불을 붙여 그 자리를 불태웁니다.");
        SkillDefinition s75 = Fx(MakeDash("GunRoll", "구르며 쏘기", 8, 5f, 0.22f, 0f, 3.5f, 0.5f, true, CharacterAnim.Roll, FxBlue), SkillFx.Wind, SoundId.Roll, "무적 상태로 몸을 굴립니다.");
        SetSkillStatus(s72, StatusKind.Slow, 2f, 0f);
        SetSkillStatus(s73, StatusKind.Burn, 3f, 0.12f);
        SetSkillStatus(s74, StatusKind.Burn, 2f, 0.1f);
        MakeJob(7, "건슬링어", WeaponKind.Gun, "짧은 사거리에서 빠르게 쏘아 대는 원거리 직업.", new[] { "난사", "화약", "기교" },
            new[] { s70, s71, s72, s73, s74, s75 },
            new[] { "빠른 손", "탄 아끼기", "화약 다루기", "넓은 폭발", "발놀림", "숙련" },
            new[] { JobNodeEffect.AttackSpeedPercent, JobNodeEffect.ManaOnHit, JobNodeEffect.SkillDamagePercent, JobNodeEffect.DamagePercent, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.CooldownPercent },
            new[] { 5f, 1f, 8f, 5f, 4f, 5f });

        // 8 주술사
        SkillDefinition s80 = Fx(MakeVolley("WandCurse", "저주 탄", 12, 4f, 0.3f, 1f, 3, 25f, 11f, 0.9f, 0.2f, 0f, ProjectileLook.Orb, FxPurple), SkillFx.Arcane, SoundId.SkillDark, "저주 구슬 세 개를 날려 적을 약하게 만듭니다.");
        SkillDefinition s81 = Fx(MakeArea("WandHex", "저주 폭발", 18, 9f, 0.4f, 1.4f, 2.4f, CharacterAnim.Skill, FxPurple), SkillFx.Arcane, SoundId.SkillDark, "주변에 저주를 터뜨려 적을 약하게 만듭니다.");
        SkillDefinition s82 = Fx(MakeZone("WandMiasma", "독 구름", 18, 12f, 0.35f, 0.45f, 2.2f, 3.5f, 5f, 0f, FxPoison), SkillFx.Poison, SoundId.SkillDark, "조준한 곳에 독 구름을 피워 적을 계속 중독시킵니다.");
        SkillDefinition s83 = Fx(MakeVolley("WandVenom", "독 폭탄", 16, 7f, 0.3f, 1.6f, 1, 0f, 10f, 0.9f, 0.24f, 2.2f, ProjectileLook.Orb, FxPoison), SkillFx.Poison, SoundId.SkillDark, "터지는 독 폭탄을 던져 주변 적에게 독을 남깁니다.");
        SkillDefinition s84 = Fx(MakeDash("WandSpirit", "혼령 질주", 12, 6f, 0.25f, 0.8f, 4f, 0.7f, true, CharacterAnim.Skill, FxPurple), SkillFx.Arcane, SoundId.SkillDash, "혼령이 되어 적을 꿰뚫고 지나갑니다.");
        SkillDefinition s85 = Fx(MakeHeal("WandDrain", "영혼 흡수", 16, 16f, 0.4f, 1.2f, 18f, FxPurple), SkillFx.Heal, SoundId.Heal, "떠도는 영혼을 흡수해 체력을 회복합니다.");
        SetSkillStatus(s80, StatusKind.Weaken, 4f, 0f);
        SetSkillStatus(s81, StatusKind.Weaken, 5f, 0f);
        SetSkillStatus(s82, StatusKind.Burn, 2f, 0.1f);
        SetSkillStatus(s83, StatusKind.Burn, 4f, 0.12f);
        MakeJob(8, "주술사", WeaponKind.Wand, "저주와 독으로 적을 약하게 만드는 마법 직업.", new[] { "저주", "독", "혼령" },
            new[] { s80, s81, s82, s83, s84, s85 },
            new[] { "깊은 저주", "마력 그릇", "독 다루기", "빠른 영창", "영혼 그릇", "마력 흡수" },
            new[] { JobNodeEffect.SkillDamagePercent, JobNodeEffect.MaxMana, JobNodeEffect.DamagePercent, JobNodeEffect.CooldownPercent, JobNodeEffect.MaxHp, JobNodeEffect.ManaOnHit },
            new[] { 8f, 10f, 5f, 5f, 10f, 1f });

        // 9 사제
        SkillDefinition s90 = Fx(MakeHeal("PriestHeal", "치유의 빛", 16, 8f, 0.4f, 3f, 30f, FxGreen), SkillFx.Heal, SoundId.Heal, "주변 아군 모두의 체력을 회복합니다.");
        SkillDefinition s91 = Fx(MakeZone("PriestSanctum", "성역", 24, 18f, 0.4f, 0f, 2.4f, 0f, 6f, 4f, FxGold), SkillFx.Holy, SoundId.SkillHoly, "발밑에 성역을 펼쳐 안에 있는 아군을 계속 회복합니다.");
        SkillDefinition s92 = Fx(MakeVolley("PriestBolt", "신성 탄", 12, 4f, 0.3f, 1.8f, 1, 0f, 11f, 1f, 0.24f, 1.4f, ProjectileLook.Orb, FxGold), SkillFx.Holy, SoundId.SkillHoly, "빛의 구슬을 날려 닿은 곳을 정화합니다.");
        SkillDefinition s93 = Fx(MakeArea("PriestJudge", "심판", 20, 10f, 0.45f, 1.6f, 2.4f, CharacterAnim.Skill, FxGold), SkillFx.Holy, SoundId.SkillHoly, "빛기둥을 내려 주변 적을 잠시 기절시킵니다.");
        SkillDefinition s94 = Fx(Ally(MakeBuff("PriestBless", "축복", 20, 20f, 0.4f, JobNodeEffect.DamagePercent, 25f, 10f, FxGold), 4f), SkillFx.Aura, SoundId.SkillBuff, "주변 아군 모두의 공격력을 올립니다.");
        SkillDefinition s95 = Fx(MakeRevive("PriestRevive", "부활의 기도", 30, 40f, 0.8f, 3f, 40f, FxWhite), SkillFx.Holy, SoundId.Revive, "주변에 쓰러진 아군을 일으킵니다.");
        SetSkillStatus(s93, StatusKind.Stun, 0.5f, 0f);
        MakeJob(9, "사제", WeaponKind.Wand, "아군을 치유하고 되살리는 회복 직업(제단에서 완드로 전직).", new[] { "치유", "신성", "축복" },
            new[] { s90, s91, s92, s93, s94, s95 },
            new[] { "따뜻한 손", "깊은 기도", "빛의 힘", "빠른 영창", "신앙", "인내" },
            new[] { JobNodeEffect.MaxMana, JobNodeEffect.CooldownPercent, JobNodeEffect.SkillDamagePercent, JobNodeEffect.ManaOnHit, JobNodeEffect.MaxHp, JobNodeEffect.DefensePercent },
            new[] { 10f, 5f, 8f, 1f, 12f, 4f });
    }

    private static void SetSkillStatus(SkillDefinition skill, StatusKind kind, float duration, float power)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "statusKind").enumValueIndex = (int)kind;
        Find(data, "statusDuration").floatValue = duration;
        Find(data, "statusPower").floatValue = power;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    // 이펙트, 소리, 설명
    private static SkillDefinition Fx(SkillDefinition skill, SkillFx fx, SoundId sound, string description)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "fx").enumValueIndex = (int)fx;
        Find(data, "sound").enumValueIndex = (int)sound;
        Find(data, "description").stringValue = description;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 강화 스킬을 반경 안의 아군 모두에게 건다.
    private static SkillDefinition Ally(SkillDefinition skill, float radius)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "radius").floatValue = radius;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 돌진이 끝난 자리를 내려찍는다(반경, 피해 배율).
    private static SkillDefinition Slam(SkillDefinition skill, float radius, float damageFactor)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "explosionRadius").floatValue = radius;
        Find(data, "damageFactor").floatValue = damageFactor;
        // 돌진 길목은 치지 않고 착지만 친다.
        Find(data, "radius").floatValue = 0f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SerializedObject OpenSkill(string assetName, string displayName, SkillKind kind, int manaCost, float cooldown, float castTime, float damageFactor, CharacterAnim anim, Color color)
    {
        string path = JobDataFolder + "/" + assetName + ".asset";
        SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
        if (skill == null)
        {
            skill = ScriptableObject.CreateInstance<SkillDefinition>();
            AssetDatabase.CreateAsset(skill, path);
        }
        SerializedObject data = new SerializedObject(skill);
        Find(data, "displayName").stringValue = displayName;
        Find(data, "kind").enumValueIndex = (int)kind;
        Find(data, "manaCost").intValue = manaCost;
        Find(data, "cooldown").floatValue = cooldown;
        Find(data, "castTime").floatValue = castTime;
        Find(data, "damageFactor").floatValue = damageFactor;
        Find(data, "anim").enumValueIndex = (int)anim;
        Find(data, "animStartTime").floatValue = 0f;
        Find(data, "animSpeed").floatValue = 1.5f;
        Find(data, "fxColor").colorValue = color;
        // 종류마다 쓰는 값만 다시 채운다(이전 값이 남지 않게 기본값으로 되돌림).
        Find(data, "radius").floatValue = 0f;
        Find(data, "range").floatValue = 0f;
        Find(data, "invulnerable").boolValue = false;
        Find(data, "count").intValue = 1;
        Find(data, "spreadDegrees").floatValue = 0f;
        Find(data, "explosionRadius").floatValue = 0f;
        Find(data, "healPercent").floatValue = 0f;
        Find(data, "buffDuration").floatValue = 0f;
        Find(data, "statusKind").enumValueIndex = (int)StatusKind.None;
        return data;
    }

    private static SkillDefinition CloseSkill(SerializedObject data)
    {
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data.targetObject);
        return (SkillDefinition)data.targetObject;
    }

    private static SkillDefinition MakeArea(string assetName, string displayName, int manaCost, float cooldown, float castTime, float damageFactor, float radius, CharacterAnim anim, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.AreaAround, manaCost, cooldown, castTime, damageFactor, anim, color);
        Find(data, "radius").floatValue = radius;
        return CloseSkill(data);
    }

    private static SkillDefinition MakeDash(string assetName, string displayName, int manaCost, float cooldown, float castTime, float damageFactor, float range, float halfWidth, bool invulnerable, CharacterAnim anim, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.Dash, manaCost, cooldown, castTime, damageFactor, anim, color);
        Find(data, "range").floatValue = range;
        Find(data, "radius").floatValue = halfWidth;
        Find(data, "invulnerable").boolValue = invulnerable;
        return CloseSkill(data);
    }

    private static SkillDefinition MakeVolley(string assetName, string displayName, int manaCost, float cooldown, float castTime, float damageFactor, int count, float spreadDegrees, float speed, float lifetime, float radius, float explosionRadius, ProjectileLook look, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.Volley, manaCost, cooldown, castTime, damageFactor, CharacterAnim.Attack, color);
        Find(data, "count").intValue = count;
        Find(data, "spreadDegrees").floatValue = spreadDegrees;
        Find(data, "projectileSpeed").floatValue = speed;
        Find(data, "projectileLifetime").floatValue = lifetime;
        Find(data, "radius").floatValue = radius;
        Find(data, "explosionRadius").floatValue = explosionRadius;
        Find(data, "projectileLook").enumValueIndex = (int)look;
        return CloseSkill(data);
    }

    private static SkillDefinition MakeBuff(string assetName, string displayName, int manaCost, float cooldown, float castTime, JobNodeEffect effect, float value, float duration, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.Buff, manaCost, cooldown, castTime, 0f, CharacterAnim.Victory, color);
        Find(data, "buffEffect").enumValueIndex = (int)effect;
        Find(data, "buffValue").floatValue = value;
        Find(data, "buffDuration").floatValue = duration;
        return CloseSkill(data);
    }

    private static SkillDefinition MakeHeal(string assetName, string displayName, int manaCost, float cooldown, float castTime, float radius, float healPercent, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.Heal, manaCost, cooldown, castTime, 0f, CharacterAnim.Victory, color);
        Find(data, "radius").floatValue = radius;
        Find(data, "healPercent").floatValue = healPercent;
        return CloseSkill(data);
    }

    private static SkillDefinition MakeRevive(string assetName, string displayName, int manaCost, float cooldown, float castTime, float radius, float hpPercent, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.Revive, manaCost, cooldown, castTime, 0f, CharacterAnim.Victory, color);
        Find(data, "radius").floatValue = radius;
        Find(data, "healPercent").floatValue = hpPercent;
        return CloseSkill(data);
    }

    // 장판: 0.5초마다 반경 안의 적에게 피해(배율이 0이면 피해 없음), 아군은 최대 체력의 healPercent%만큼 회복
    private static SkillDefinition MakeZone(string assetName, string displayName, int manaCost, float cooldown, float castTime, float damageFactor, float radius, float range, float duration, float healPercent, Color color)
    {
        SerializedObject data = OpenSkill(assetName, displayName, SkillKind.Zone, manaCost, cooldown, castTime, damageFactor, CharacterAnim.Skill, color);
        Find(data, "radius").floatValue = radius;
        Find(data, "range").floatValue = range;
        Find(data, "buffDuration").floatValue = duration;
        Find(data, "healPercent").floatValue = healPercent;
        return CloseSkill(data);
    }

    // 갈래마다 [스킬, 능력치(3단계), 스킬, 능력치(3단계)]. statNames 등은 갈래 순서로 2개씩.
    private static void MakeJob(int jobIndex, string displayName, WeaponKind weaponKind, string description, string[] branchNames, SkillDefinition[] skills, string[] statNames, JobNodeEffect[] statEffects, float[] statValues)
    {
        string path = JobDataFolder + "/" + JobAssetNames[jobIndex] + ".asset";
        JobDefinition job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
        if (job == null)
        {
            job = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(job, path);
        }
        SerializedObject data = new SerializedObject(job);
        Find(data, "displayName").stringValue = displayName;
        Find(data, "weaponKind").enumValueIndex = (int)weaponKind;
        Find(data, "description").stringValue = description;
        SerializedProperty branches = Find(data, "branchNames");
        branches.arraySize = JobDefinition.BranchCount;
        for (int i = 0; i < JobDefinition.BranchCount; i++)
        {
            branches.GetArrayElementAtIndex(i).stringValue = branchNames[i];
        }
        SetRefArray(data, "skills", skills);
        SerializedProperty nodes = Find(data, "nodes");
        nodes.arraySize = JobDefinition.NodeCount;
        for (int i = 0; i < JobDefinition.NodeCount; i++)
        {
            int branch = i / JobDefinition.BranchSize;
            int step = i % JobDefinition.BranchSize;
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            if (step % 2 == 0)
            {
                int skillIndex = branch * 2 + step / 2;
                node.FindPropertyRelative("displayName").stringValue = skills[skillIndex].DisplayName;
                node.FindPropertyRelative("effect").enumValueIndex = (int)JobNodeEffect.UnlockSkill;
                node.FindPropertyRelative("value").floatValue = skillIndex;
                node.FindPropertyRelative("maxRank").intValue = 1;
            }
            else
            {
                int stat = branch * 2 + step / 2;
                node.FindPropertyRelative("displayName").stringValue = statNames[stat];
                node.FindPropertyRelative("effect").enumValueIndex = (int)statEffects[stat];
                node.FindPropertyRelative("value").floatValue = statValues[stat];
                node.FindPropertyRelative("maxRank").intValue = 3;
            }
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(job);
    }

    // 이름으로 자식(손자 포함)을 찾는다. 없으면 초기 구성을 멈춘다.
    private static Transform FindChild(Transform root, string childName)
    {
        Transform found = null;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length && found == null; i++)
        {
            if (all[i].name == childName)
            {
                found = all[i];
            }
        }
        if (found == null)
        {
            throw new System.InvalidOperationException(root.name + "에서 " + childName + "을(를) 찾지 못했습니다.");
        }
        return found;
    }

    private static GameObject BuildPlayerPrefab()
    {
        GameObject root = new GameObject("Player");
        root.layer = GameLayers.Player;

        GameObject visualObject = CloneDemoCharacter();
        visualObject.transform.SetParent(root.transform, false);
        PartsManager partsManager = visualObject.GetComponent<PartsManager>();
        Animator animator = visualObject.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            throw new System.InvalidOperationException("캐릭터에서 Animator를 찾지 못했습니다.");
        }

        // 몬스터와 Y 정렬이 섞이도록 정렬 순서를 0으로 맞춘다.
        SortingGroup[] groups = visualObject.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].sortingOrder = 0;
        }
        report.AppendLine("SortingGroup 수: " + groups.Length + ", Animator: " + animator.gameObject.name);

        CharacterVisual visual = visualObject.AddComponent<CharacterVisual>();
        SetRef(visual, "partsManager", partsManager);
        SetRef(visual, "animator", animator);
        SetRef(visual, "flipRoot", visualObject.transform);
        SetRef(visual, "handRight", FindChild(visualObject.transform, "HandRight"));

        SpriteRenderer slash = AddSprite(root.transform, "Slash", LoadSprite(SpriteRoot + "/Fx/Slash.png"), 200);
        slash.enabled = false;

        // 조준선(본인에게만 보임)과 막기 표시
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        SpriteRenderer aim = AddSprite(root.transform, "AimLine", pixel, 150);
        aim.enabled = false;
        SpriteRenderer guard = AddSprite(root.transform, "Guard", LoadSprite(SpriteRoot + "/Fx/Slash.png"), 190);
        guard.transform.localScale = new Vector3(0.55f, 0.9f, 1f);
        guard.enabled = false;

        Rigidbody2D body = AddBody(root, 0f);
        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.radius = 0.22f;
        collider.offset = new Vector2(0f, 0.2f);

        root.AddComponent<NetworkIdentity>();
        AddNetworkTransform(root, SyncDirection.ClientToServer);

        PlayerController controller = root.AddComponent<PlayerController>();
        SetRef(controller, "body", body);
        SetRef(controller, "visual", visual);
        SetRef(controller, "slashRenderer", slash);
        SetRef(controller, "aimRenderer", aim);
        SetRef(controller, "guardRenderer", guard);

        Object[] weapons = new Object[WeaponAssetNames.Length];
        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i] = LoadRequired<WeaponDefinition>(WeaponDataFolder + "/" + WeaponAssetNames[i] + ".asset");
        }
        PlayerInventory inventory = root.AddComponent<PlayerInventory>();
        SerializedObject inventoryData = new SerializedObject(inventory);
        Find(inventoryData, "controller").objectReferenceValue = controller;
        Find(inventoryData, "visual").objectReferenceValue = visual;
        Find(inventoryData, "balance").objectReferenceValue = BuildItemBalance(partsManager);
        SetRefArray(inventoryData, "weapons", weapons);
        inventoryData.ApplyModifiedPropertiesWithoutUndo();
        SetRef(controller, "inventory", inventory);

        Object[] jobs = new Object[JobAssetNames.Length];
        for (int i = 0; i < jobs.Length; i++)
        {
            jobs[i] = LoadRequired<JobDefinition>(JobDataFolder + "/" + JobAssetNames[i] + ".asset");
        }
        PlayerSkills skills = root.AddComponent<PlayerSkills>();
        SerializedObject skillsData = new SerializedObject(skills);
        Find(skillsData, "controller").objectReferenceValue = controller;
        Find(skillsData, "inventory").objectReferenceValue = inventory;
        SetRefArray(skillsData, "jobs", jobs);
        skillsData.ApplyModifiedPropertiesWithoutUndo();
        SetRef(controller, "skills", skills);
        SetRef(inventory, "skills", skills);

        // 방 시설 상호작용(제단, 광맥, 낚시터, 마구간)과 영구 해금 적용
        PlayerFacility facility = root.AddComponent<PlayerFacility>();
        SetRef(facility, "controller", controller);
        SetRef(facility, "inventory", inventory);
        SetRef(facility, "skills", skills);
        SetRef(controller, "facility", facility);
        SetRef(inventory, "facility", facility);

        return SavePrefab(root, "Player");
    }

    private static GameObject BuildMonsterPrefab()
    {
        GameObject root = new GameObject("Monster");
        root.layer = GameLayers.Monster;

        SpriteRenderer telegraph = AddSprite(root.transform, "Telegraph", LoadSprite(SpriteRoot + "/Fx/Circle.png"), -20);
        telegraph.enabled = false;
        SpriteRenderer aura = AddSprite(root.transform, "Aura", LoadSprite(SpriteRoot + "/Fx/Circle.png"), -15);
        aura.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        aura.enabled = false;
        SpriteRenderer shadow = AddSprite(root.transform, "Shadow", LoadSprite(ShadowSpritePath), -10);
        shadow.color = ShadowColor;
        SpriteRenderer sprite = AddSprite(root.transform, "Sprite", null, 0);

        GameObject hpBarRoot = new GameObject("HpBar");
        hpBarRoot.transform.SetParent(root.transform, false);
        hpBarRoot.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        SpriteRenderer barBack = AddSprite(hpBarRoot.transform, "Back", pixel, 300);
        barBack.color = new Color(0f, 0f, 0f, 0.6f);
        barBack.transform.localScale = new Vector3(16f, 2.5f, 1f);
        SpriteRenderer barFill = AddSprite(hpBarRoot.transform, "Fill", pixel, 301);
        barFill.color = new Color(0.9f, 0.25f, 0.2f);
        barFill.transform.localScale = new Vector3(15f, 1.5f, 1f);
        hpBarRoot.SetActive(false);

        Rigidbody2D body = AddBody(root, 6f);
        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.radius = 0.28f;
        collider.offset = new Vector2(0f, 0.2f);

        root.AddComponent<NetworkIdentity>();
        AddNetworkTransform(root, SyncDirection.ServerToClient);

        MonsterController controller = root.AddComponent<MonsterController>();
        SetRef(controller, "body", body);
        SetRef(controller, "bodyCollider", collider);
        SetRef(controller, "spriteRenderer", sprite);
        SetRef(controller, "shadow", shadow.transform);
        SetRef(controller, "telegraphRenderer", telegraph);
        SetRef(controller, "auraRenderer", aura);
        SetRef(controller, "hpBarRoot", hpBarRoot);
        SetRef(controller, "hpBarFill", barFill.transform);

        // 타락한 모험가용 인간형 외형. 평소에는 꺼 두고, 타락한 모험가로 나올 때만 켠다.
        GameObject humanoidObject = CloneDemoCharacter();
        humanoidObject.name = "Humanoid";
        humanoidObject.transform.SetParent(root.transform, false);
        SortingGroup[] humanoidGroups = humanoidObject.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < humanoidGroups.Length; i++)
        {
            humanoidGroups[i].sortingOrder = 0;
        }
        CharacterVisual humanoid = humanoidObject.AddComponent<CharacterVisual>();
        SetRef(humanoid, "partsManager", humanoidObject.GetComponent<PartsManager>());
        SetRef(humanoid, "animator", humanoidObject.GetComponentInChildren<Animator>(true));
        SetRef(humanoid, "flipRoot", humanoidObject.transform);
        SetRef(humanoid, "handRight", FindChild(humanoidObject.transform, "HandRight"));
        humanoidObject.SetActive(false);
        SetRef(controller, "humanoidRoot", humanoidObject);
        SetRef(controller, "humanoid", humanoid);
        SetRef(controller, "balance", LoadRequired<ItemBalance>(ItemBalancePath));

        return SavePrefab(root, "Monster");
    }

    private static Projectile BuildProjectilePrefab()
    {
        GameObject root = new GameObject("Projectile");
        SpriteRenderer sprite = AddSprite(root.transform, "Sprite", LoadSprite(SpriteRoot + "/Fx/Spore.png"), 0);
        sprite.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        SpriteRenderer aura = AddSprite(root.transform, "Aura", LoadSprite(SpriteRoot + "/Fx/Circle.png"), -15);
        aura.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        aura.enabled = false;
        SpriteRenderer shadow = AddSprite(root.transform, "Shadow", LoadSprite(ShadowSpritePath), -10);
        shadow.color = ShadowColor;
        shadow.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        Projectile projectile = root.AddComponent<Projectile>();
        SetRef(projectile, "spriteRenderer", sprite);
        SetRef(projectile, "shadowRenderer", shadow);
        SetRef(projectile, "sporeSprite", LoadSprite(SpriteRoot + "/Fx/Spore.png"));
        SetRef(projectile, "pixelSprite", LoadSprite(SpriteRoot + "/Fx/Pixel.png"));
        SetRef(projectile, "circleSprite", LoadSprite(SpriteRoot + "/Fx/Circle.png"));
        return SavePrefab(root, "Projectile").GetComponent<Projectile>();
    }

    // 장비 수치 규칙 에셋. 없으면 기본값으로 만들고, 외형 파츠 수는 캐릭터에서 읽어 매번 채운다.
    private static ItemBalance BuildItemBalance(PartsManager partsManager)
    {
        ItemBalance balance = AssetDatabase.LoadAssetAtPath<ItemBalance>(ItemBalancePath);
        if (balance == null)
        {
            balance = ScriptableObject.CreateInstance<ItemBalance>();
            AssetDatabase.CreateAsset(balance, ItemBalancePath);
        }

        SerializedObject partsData = new SerializedObject(partsManager);
        PartsType[] weaponTypes = new PartsType[WeaponDefinition.KindCount];
        for (int i = 0; i < weaponTypes.Length; i++)
        {
            weaponTypes[i] = ItemBalance.GetWeaponPartsType((WeaponKind)i);
        }
        SerializedObject data = new SerializedObject(balance);
        // 무기 계열 이름(WeaponKind 순서). 예전 에셋에 4개만 남아 있을 수 있어 매번 다시 쓴다.
        string[] kindNames = { "검", "창", "활", "지팡이", "도끼", "둔기", "석궁", "총", "완드" };
        SerializedProperty names = Find(data, "weaponKindNames");
        names.arraySize = kindNames.Length;
        for (int i = 0; i < kindNames.Length; i++)
        {
            names.GetArrayElementAtIndex(i).stringValue = kindNames[i];
        }
        SerializedProperty weaponCounts = Find(data, "weaponPartsCounts");
        weaponCounts.arraySize = weaponTypes.Length;
        StringBuilder counts = new StringBuilder("장비 외형 수(무기 9계열, 방패, 투구, 갑옷):");
        for (int i = 0; i < weaponTypes.Length; i++)
        {
            int count = CountParts(partsData, weaponTypes[i]);
            weaponCounts.GetArrayElementAtIndex(i).intValue = count;
            counts.Append(' ').Append(count);
        }
        int shieldCount = CountParts(partsData, PartsType.Shield);
        int helmetCount = CountParts(partsData, PartsType.Helmet);
        int armorCount = CountParts(partsData, PartsType.Chest);
        Find(data, "shieldPartsCount").intValue = shieldCount;
        Find(data, "helmetPartsCount").intValue = helmetCount;
        Find(data, "armorPartsCount").intValue = armorCount;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(balance);
        counts.Append(' ').Append(shieldCount).Append(' ').Append(helmetCount).Append(' ').Append(armorCount);
        report.AppendLine(counts.ToString());
        return balance;
    }

    // 패키지 캐릭터가 가진 부위별 스프라이트 수
    private static int CountParts(SerializedObject partsData, PartsType type)
    {
        int count = 0;
        SerializedProperty categories = Find(partsData, "categories");
        for (int i = 0; i < categories.arraySize; i++)
        {
            SerializedProperty category = categories.GetArrayElementAtIndex(i);
            if (category.FindPropertyRelative("type").intValue == (int)type)
            {
                SerializedProperty renderers = category.FindPropertyRelative("renderers");
                for (int r = 0; r < renderers.arraySize; r++)
                {
                    int sprites = renderers.GetArrayElementAtIndex(r).FindPropertyRelative("sprites").arraySize;
                    count = Mathf.Max(count, sprites);
                }
            }
        }
        if (count == 0)
        {
            throw new System.InvalidOperationException(type + " 부위의 스프라이트가 없습니다.");
        }
        return count;
    }

    // 바닥에 떨어진 장비 표시(네트워크 오브젝트 아님)
    private static void BuildItemPickupPrefab()
    {
        GameObject root = new GameObject("ItemPickup");
        SpriteRenderer glow = AddSprite(root.transform, "Glow", LoadSprite(SpriteRoot + "/Fx/Circle.png"), -15);
        glow.transform.localScale = new Vector3(0.9f, 0.45f, 1f);
        SpriteRenderer icon = AddSprite(root.transform, "Icon", null, 0);
        icon.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        ItemPickup pickup = root.AddComponent<ItemPickup>();
        SetRef(pickup, "iconRenderer", icon);
        SetRef(pickup, "glowRenderer", glow);
        SavePrefab(root, "ItemPickup");
    }

    private static DamageText BuildDamageTextPrefab()
    {
        GameObject root = new GameObject("DamageText");
        TextMeshPro label = root.AddComponent<TextMeshPro>();
        label.font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        label.fontSize = 3.4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(2f, 0.6f);
        label.text = "0";
        root.GetComponent<MeshRenderer>().sortingOrder = 500;

        DamageText damageText = root.AddComponent<DamageText>();
        SetRef(damageText, "label", label);
        return SavePrefab(root, "DamageText").GetComponent<DamageText>();
    }

    // ------------------------------------------------------------ 글꼴

    // 한글 표시용 TMP 글꼴(Pretendard Bold, 동적 아틀라스). 없을 때만 만든다.
    private static void EnsureKoreanFont()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath) == null)
        {
            Font source = LoadRequired<Font>(KoreanFontSourcePath);
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            AssetDatabase.CreateAsset(fontAsset, KoreanFontAssetPath);
            fontAsset.material.name = "Pretendard-Bold SDF Material";
            fontAsset.atlasTexture.name = "Pretendard-Bold SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            report.AppendLine("한글 글꼴 에셋 생성: " + KoreanFontAssetPath);
        }
    }

    // ------------------------------------------------------------ 씬 공통

    // 만들 씬이 열려 있으면 지울 수 없으므로 원본 씬으로 바꾼다.
    private static void CloseBuiltScenesIfOpen()
    {
        bool open = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            string path = SceneManager.GetSceneAt(i).path;
            if (path == GameScenePath || path == LobbyScenePath)
            {
                open = true;
            }
        }
        if (open)
        {
            EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        }
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new FileNotFoundException("에셋을 불러오지 못했습니다: " + path);
        }
        return asset;
    }

    private static Scene CreateScene(string path)
    {
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.CopyAsset(SourceScenePath, path);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        return scene;
    }

    private static void SaveAndClose(Scene scene, Scene previousScene)
    {
        // Esc 메뉴는 나중에 만든 패널보다 위에 그려져야 한다.
        PauseMenuUi[] menus = Object.FindObjectsByType<PauseMenuUi>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i].gameObject.scene == scene)
            {
                menus[i].transform.SetAsLastSibling();
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine("씬 저장: " + scene.path);
        SceneManager.SetActiveScene(previousScene);
        EditorSceneManager.CloseScene(scene, true);
    }

    // 로비와 전투 씬이 함께 쓰는 구성: 카메라, 배경, 벽, 허브, 피해 숫자, HUD, 네트워크 관리자
    private static GameHub BuildCommon(Scene scene, Color backgroundTint, out HudController hud, out GameObject canvasObject, out GameObject waveLabelObject)
    {
        Camera mainCamera = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length && mainCamera == null; i++)
        {
            mainCamera = roots[i].GetComponentInChildren<Camera>(true);
        }
        if (mainCamera == null)
        {
            throw new System.InvalidOperationException("원본 씬에 카메라가 없습니다.");
        }
        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 4.2f;
        mainCamera.backgroundColor = new Color(0.16f, 0.2f, 0.16f);
        CameraFollow cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
        SetRef(cameraFollow, "targetCamera", mainCamera);

        GameObject background = new GameObject("Background");
        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = LoadSprite(MapSpritePath);
        backgroundRenderer.sortingOrder = -1000;
        backgroundRenderer.color = backgroundTint;

        BuildWalls();

        GameObject hubObject = new GameObject("GameHub");
        GameHub hub = hubObject.AddComponent<GameHub>();

        GameObject damageTextPoolObject = new GameObject("DamageTextPool");
        DamageTextPool damageTextPool = damageTextPoolObject.AddComponent<DamageTextPool>();
        SetRef(damageTextPool, "textPrefab", LoadRequired<DamageText>(PrefabFolder + "/DamageText.prefab"));

        GameObject dropPoolObject = new GameObject("ItemDropPool");
        ItemDropPool itemDropPool = dropPoolObject.AddComponent<ItemDropPool>();
        SetRef(itemDropPool, "pickupPrefab", LoadRequired<ItemPickup>(PrefabFolder + "/ItemPickup.prefab"));

        hud = BuildHud(out canvasObject, out waveLabelObject);
        SetRef(hud, "hub", hub);
        InventoryUi inventoryUi = BuildInventoryPanel(canvasObject.transform);
        SkillUi skillUi = BuildSkillUi(canvasObject.transform, hub);
        SetRef(hub, "skillUi", skillUi);
        SetRef(hub, "skillFx", BuildSkillFx());

        GameObject soundObject = new GameObject("SoundManager");
        SoundManager sound = soundObject.AddComponent<SoundManager>();
        SetRef(sound, "library", LoadRequired<SoundLibrary>(SoundLibraryPath));
        PauseMenuUi pauseMenu = BuildPauseMenu(canvasObject.transform, hub);
        UiClickSound clickSound = canvasObject.AddComponent<UiClickSound>();
        SetRef(clickSound, "sound", sound);

        // 가방과 로비 버튼을 누르므로 UI 입력 처리가 필요하다.
        canvasObject.AddComponent<GraphicRaycaster>();
        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();

        SerializedObject hubData = new SerializedObject(hub);
        Find(hubData, "itemDropPool").objectReferenceValue = itemDropPool;
        Find(hubData, "inventoryUi").objectReferenceValue = inventoryUi;
        Find(hubData, "eventSystem").objectReferenceValue = eventSystem;
        Find(hubData, "mainCamera").objectReferenceValue = mainCamera;
        Find(hubData, "cameraFollow").objectReferenceValue = cameraFollow;
        Find(hubData, "hud").objectReferenceValue = hud;
        Find(hubData, "damageTextPool").objectReferenceValue = damageTextPool;
        Find(hubData, "sound").objectReferenceValue = sound;
        Find(hubData, "pauseMenu").objectReferenceValue = pauseMenu;
        Find(hubData, "arenaHalfSize").vector2Value = ArenaHalfSize;
        SerializedProperty spawnPoints = Find(hubData, "spawnPoints");
        spawnPoints.arraySize = 4;
        spawnPoints.GetArrayElementAtIndex(0).vector2Value = new Vector2(-1.5f, -1f);
        spawnPoints.GetArrayElementAtIndex(1).vector2Value = new Vector2(-0.5f, -1f);
        spawnPoints.GetArrayElementAtIndex(2).vector2Value = new Vector2(0.5f, -1f);
        spawnPoints.GetArrayElementAtIndex(3).vector2Value = new Vector2(1.5f, -1f);
        hubData.ApplyModifiedPropertiesWithoutUndo();

        // 두 씬 모두에 관리자를 둔다. 로비에서 넘어오면 Mirror가 중복을 지우고, 전투 씬을 바로 실행해도 혼자 플레이할 수 있다.
        GameObject managerObject = new GameObject("NetworkManager");
        KcpTransport transport = managerObject.AddComponent<KcpTransport>();
        GameNetworkManager manager = managerObject.AddComponent<GameNetworkManager>();
        manager.transport = transport;
        manager.playerPrefab = LoadRequired<GameObject>(PrefabFolder + "/Player.prefab");
        manager.autoCreatePlayer = true;
        manager.offlineScene = LobbyScenePath;
        manager.onlineScene = string.Empty;
        manager.maxConnections = GameNetworkManager.MaxPlayers;
        EditorUtility.SetDirty(manager);
        return hub;
    }

    private static void BuildWalls()
    {
        GameObject walls = new GameObject("Walls");
        float thickness = 1f;
        float x = ArenaHalfSize.x + thickness * 0.5f;
        float y = ArenaHalfSize.y + thickness * 0.5f;
        float width = ArenaHalfSize.x * 2f + thickness * 2f;
        float height = ArenaHalfSize.y * 2f + thickness * 2f;
        AddWall(walls.transform, "Left", new Vector2(-x, 0f), new Vector2(thickness, height));
        AddWall(walls.transform, "Right", new Vector2(x, 0f), new Vector2(thickness, height));
        AddWall(walls.transform, "Bottom", new Vector2(0f, -y), new Vector2(width, thickness));
        AddWall(walls.transform, "Top", new Vector2(0f, y), new Vector2(width, thickness));
    }

    private static void AddWall(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject wall = new GameObject(name);
        wall.layer = GameLayers.Wall;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
    }

    // ------------------------------------------------------------ 전투 씬

    private static void BuildGameScene(int definitionCount, Scene previousScene)
    {
        Scene scene = CreateScene(GameScenePath);
        HudController hud;
        GameObject canvasObject;
        GameObject waveLabelObject;
        GameHub hub = BuildCommon(scene, Color.white, out hud, out canvasObject, out waveLabelObject);
        SerializedObject musicData = new SerializedObject(hub);
        Find(musicData, "sceneMusic").enumValueIndex = (int)MusicId.Combat;
        musicData.ApplyModifiedPropertiesWithoutUndo();
        RoomDecor decor = BuildDungeonRoom(hub);

        string[] definitionNames = { "Slime", "Bat", "Mushroom" };
        MonsterDefinition[] definitions = new MonsterDefinition[definitionCount];
        for (int i = 0; i < definitionCount; i++)
        {
            definitions[i] = LoadRequired<MonsterDefinition>(MonsterDataFolder + "/" + definitionNames[i] + ".asset");
        }

        GameObject directorObject = new GameObject("MonsterDirector");
        directorObject.AddComponent<NetworkIdentity>();
        MonsterDirector director = directorObject.AddComponent<MonsterDirector>();
        SerializedObject directorData = new SerializedObject(director);
        Find(directorData, "hub").objectReferenceValue = hub;
        Find(directorData, "monsterPrefab").objectReferenceValue = LoadRequired<GameObject>(PrefabFolder + "/Monster.prefab");
        SetRefArray(directorData, "definitions", definitions);
        directorData.ApplyModifiedPropertiesWithoutUndo();

        GameObject projectilePoolObject = new GameObject("ProjectilePool");
        ProjectilePool projectilePool = projectilePoolObject.AddComponent<ProjectilePool>();
        SetRef(projectilePool, "hub", hub);
        SetRef(projectilePool, "projectilePrefab", LoadRequired<Projectile>(PrefabFolder + "/Projectile.prefab"));

        SetRef(hub, "monsterDirector", director);
        SetRef(hub, "projectilePool", projectilePool);

        // 층 관리자와 방 표시물: 문 4개(북, 동, 남, 서), 보물 상자, 계단, 시설 표시
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        Sprite circle = LoadSprite(SpriteRoot + "/Fx/Circle.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        float doorInset = 0.5f;
        Vector2[] doorPositions =
        {
            new Vector2(0f, ArenaHalfSize.y - doorInset), new Vector2(ArenaHalfSize.x - doorInset, 0f),
            new Vector2(0f, -(ArenaHalfSize.y - doorInset)), new Vector2(-(ArenaHalfSize.x - doorInset), 0f)
        };
        string[] doorNames = { "DoorNorth", "DoorEast", "DoorSouth", "DoorWest" };
        Object[] doorViews = new Object[doorNames.Length];
        for (int i = 0; i < doorNames.Length; i++)
        {
            doorViews[i] = BuildDoor(doorNames[i], doorPositions[i], i, font);
        }
        GameObject chest = BuildMarker("Chest", Vector2.zero, circle, new Color(1f, 0.85f, 0.3f, 0.75f), 1.4f, "보물 상자", font);
        GameObject stairs = BuildMarker("Stairs", Vector2.zero, circle, new Color(0.6f, 0.8f, 1f, 0.75f), 2.2f, "계단 (모두 모이면 위층으로)", font);
        GameObject facility = BuildMarker("Facility", new Vector2(0f, 1.6f), circle, new Color(1f, 1f, 1f, 0.5f), 1.8f, "시설", font);

        GameObject floorObject = new GameObject("FloorDirector");
        floorObject.AddComponent<NetworkIdentity>();
        FloorDirector floor = floorObject.AddComponent<FloorDirector>();
        SerializedObject floorData = new SerializedObject(floor);
        Find(floorData, "hub").objectReferenceValue = hub;
        Find(floorData, "monsterDirector").objectReferenceValue = director;
        SetRefArray(floorData, "doors", doorViews);
        Find(floorData, "backgroundRenderer").objectReferenceValue = GameObject.Find("Background").GetComponent<SpriteRenderer>();
        Find(floorData, "chestObject").objectReferenceValue = chest;
        Find(floorData, "stairsObject").objectReferenceValue = stairs;
        Find(floorData, "facilityObject").objectReferenceValue = facility;
        Find(floorData, "facilityLabel").objectReferenceValue = facility.GetComponentInChildren<TextMeshPro>(true);
        Find(floorData, "doorInset").floatValue = doorInset;
        Find(floorData, "decor").objectReferenceValue = decor;
        floorData.ApplyModifiedPropertiesWithoutUndo();
        chest.SetActive(false);
        stairs.SetActive(false);
        facility.SetActive(false);
        SetRef(hub, "floorDirector", floor);

        BuildMinimap(canvasObject.transform, floor, pixel);

        FacilityUi facilityUi = BuildFacilityPanel(canvasObject.transform, hub);
        SetRef(hub, "facilityUi", facilityUi);
        SetRef(hub, "resultUi", BuildResultPanel(canvasObject.transform));
        SetRef(canvasObject.GetComponentInChildren<InventoryUi>(true), "facilityUi", facilityUi);

        SaveAndClose(scene, previousScene);
    }

    // 문(direction 0 북, 1 동, 2 남, 3 서). 북쪽은 벽 정면의 아치(벽 밑동 y=5에 세움, 0.8배 = 1.76x1.6),
    // 나머지는 벽 윗면(두께 0.7)에 뚫린 문턱(2.2x0.9, 동서는 90도 돌림). 문 너머 방 이름은 방 안쪽으로 0.55 들여 쓴다.
    private static DoorView BuildDoor(string name, Vector2 position, int direction, TMP_FontAsset font)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        Sprite open;
        Sprite closed;
        SpriteRenderer doorRenderer;
        if (direction == 0)
        {
            open = LoadSprite(DungeonSpriteRoot + "/GateOpen.png");
            closed = LoadSprite(DungeonSpriteRoot + "/GateClosed.png");
            doorRenderer = AddSprite(root.transform, "Door", open, -800);
            doorRenderer.transform.position = new Vector3(position.x, ArenaHalfSize.y, 0f);
            doorRenderer.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        }
        else
        {
            open = LoadSprite(DungeonSpriteRoot + "/DoorOpen.png");
            closed = LoadSprite(DungeonSpriteRoot + "/DoorClosed.png");
            doorRenderer = AddSprite(root.transform, "Door", open, -800);
            // 벽 윗면 가운데(방 가장자리에서 바깥으로 0.35)
            Vector2 outward = position.normalized;
            Vector2 rimCenter = new Vector2(outward.x * (ArenaHalfSize.x + WallRimThickness * 0.5f), outward.y * (ArenaHalfSize.y + WallRimThickness * 0.5f));
            doorRenderer.transform.position = rimCenter;
            // 그림의 위쪽이 방 안쪽을 향하게 돌린다(남 0, 동 90, 서 -90).
            float angle = 0f;
            if (direction == 1)
            {
                angle = 90f;
            }
            else if (direction == 3)
            {
                angle = -90f;
            }
            doorRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Vector2 inward = -position.normalized * 0.55f;
        TextMeshPro label = AddWorldLabel(root.transform, "Label", font, string.Empty, inward, 3f);
        DoorView door = root.AddComponent<DoorView>();
        SetRef(door, "doorRenderer", doorRenderer);
        SetRef(door, "openSprite", open);
        SetRef(door, "closedSprite", closed);
        SetRef(door, "label", label);
        return door;
    }

    // 던전 방 그림: 바닥 타일(방 크기 18.4x10), 북쪽 벽 정면(높이 1.4), 동·서·남 벽 윗면(두께 0.7), 북쪽 벽 횃불 4개,
    // 장애물 8칸(RoomDecor가 방마다 놓는다). 카메라는 벽까지 보이도록 범위를 넓히고 바깥은 어둡게 한다.
    private static RoomDecor BuildDungeonRoom(GameHub hub)
    {
        SpriteRenderer floorRenderer = GameObject.Find("Background").GetComponent<SpriteRenderer>();
        floorRenderer.sprite = LoadSprite(DungeonSpriteRoot + "/Floor.png");
        floorRenderer.drawMode = SpriteDrawMode.Tiled;
        floorRenderer.size = ArenaHalfSize * 2f;
        // 큰 맵 그림에서 바꾸면 에디터가 크기 비율을 Transform에 넣으므로 되돌린다.
        floorRenderer.transform.localScale = Vector3.one;

        GameObject room = new GameObject("DungeonRoom");
        float outerX = ArenaHalfSize.x + WallRimThickness;
        Sprite front = LoadSprite(DungeonSpriteRoot + "/WallFront.png");
        Sprite rim = LoadSprite(DungeonSpriteRoot + "/WallRim.png");
        SpriteRenderer north = AddTiled(room.transform, "WallNorth", front, new Vector2(0f, ArenaHalfSize.y + WallFrontHeight * 0.5f), new Vector2(outerX * 2f, WallFrontHeight), 0f);
        north.sortingOrder = -950;
        // 남쪽은 그림의 외곽선(아래쪽)이 방을 향하도록 180도, 서쪽 90도, 동쪽 -90도
        float sideLength = ArenaHalfSize.y * 2f + WallRimThickness;
        float sideCenterY = -WallRimThickness * 0.5f;
        AddTiled(room.transform, "WallSouth", rim, new Vector2(0f, -(ArenaHalfSize.y + WallRimThickness * 0.5f)), new Vector2(outerX * 2f, WallRimThickness), 180f);
        AddTiled(room.transform, "WallWest", rim, new Vector2(-(ArenaHalfSize.x + WallRimThickness * 0.5f), sideCenterY), new Vector2(sideLength, WallRimThickness), 90f);
        AddTiled(room.transform, "WallEast", rim, new Vector2(ArenaHalfSize.x + WallRimThickness * 0.5f, sideCenterY), new Vector2(sideLength, WallRimThickness), -90f);

        Sprite[] torchFrames = new Sprite[4];
        for (int i = 0; i < torchFrames.Length; i++)
        {
            torchFrames[i] = LoadSprite(DungeonSpriteRoot + "/Torch_" + i.ToString("00") + ".png");
        }
        float[] torchX = { -6.5f, -3.2f, 3.2f, 6.5f };
        for (int i = 0; i < torchX.Length; i++)
        {
            SpriteRenderer torch = AddSprite(room.transform, "Torch" + i, torchFrames[0], -900);
            torch.transform.position = new Vector2(torchX[i], ArenaHalfSize.y + 0.62f);
            torch.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            SpriteLoop loop = torch.gameObject.AddComponent<SpriteLoop>();
            SerializedObject loopData = new SerializedObject(loop);
            Find(loopData, "spriteRenderer").objectReferenceValue = torch;
            SetRefArray(loopData, "frames", torchFrames);
            Find(loopData, "timeOffset").floatValue = i * 0.37f;
            loopData.ApplyModifiedPropertiesWithoutUndo();
        }

        // 장애물 칸: 그림은 캐릭터와 같은 정렬 순서(0)라 Y 위치로 앞뒤가 정해진다.
        GameObject decorObject = new GameObject("RoomDecor");
        decorObject.transform.SetParent(room.transform, false);
        RoomDecor decor = decorObject.AddComponent<RoomDecor>();
        Object[] slots = new Object[8];
        Object[] colliders = new Object[slots.Length];
        Sprite rock = LoadSprite(DungeonSpriteRoot + "/Prop_Rock.png");
        for (int i = 0; i < slots.Length; i++)
        {
            SpriteRenderer slot = AddSprite(decorObject.transform, "Obstacle" + i, rock, 0);
            slot.gameObject.layer = GameLayers.Wall;
            BoxCollider2D box = slot.gameObject.AddComponent<BoxCollider2D>();
            slot.gameObject.SetActive(false);
            slots[i] = slot;
            colliders[i] = box;
        }
        Object[] kinds =
        {
            LoadSprite(DungeonSpriteRoot + "/Prop_Pillar.png"), rock,
            LoadSprite(DungeonSpriteRoot + "/Prop_Crate.png"), LoadSprite(DungeonSpriteRoot + "/Prop_Barrel.png")
        };
        SerializedObject decorData = new SerializedObject(decor);
        SetRefArray(decorData, "slots", slots);
        SetRefArray(decorData, "slotColliders", colliders);
        SetRefArray(decorData, "kindSprites", kinds);
        decorData.ApplyModifiedPropertiesWithoutUndo();

        Camera camera = (Camera)new SerializedObject(hub).FindProperty("mainCamera").objectReferenceValue;
        camera.backgroundColor = new Color(0.07f, 0.06f, 0.08f);
        SerializedObject followData = new SerializedObject(camera.GetComponent<CameraFollow>());
        Find(followData, "mapHalfSize").vector2Value = new Vector2(outerX, ArenaHalfSize.y + WallFrontHeight * 0.75f);
        followData.ApplyModifiedPropertiesWithoutUndo();
        return decor;
    }

    private static SpriteRenderer AddTiled(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, float angle)
    {
        SpriteRenderer renderer = AddSprite(parent, name, sprite, -950);
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = size;
        renderer.transform.position = position;
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        return renderer;
    }

    // 바닥 표시(원)와 그 위의 이름. 보물 상자, 계단, 시설에 쓴다.
    private static GameObject BuildMarker(string name, Vector2 position, Sprite circle, Color color, float diameter, string text, TMP_FontAsset font)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        SpriteRenderer renderer = AddSprite(root.transform, "Circle", circle, -850);
        renderer.color = color;
        // Circle 스프라이트는 지름 0.64 유닛
        float scale = diameter / 0.64f;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
        AddWorldLabel(root.transform, "Label", font, text, new Vector2(0f, diameter * 0.5f + 0.3f), 3f);
        return root;
    }

    private static TextMeshPro AddWorldLabel(Transform parent, string name, TMP_FontAsset font, string text, Vector2 localPosition, float fontSize)
    {
        GameObject labelObject = new GameObject(name);
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(8f, 0.6f);
        label.text = text;
        labelObject.GetComponent<MeshRenderer>().sortingOrder = 400;
        return label;
    }

    // 미니맵. 기준 해상도 1920x1080에서 오른쪽 위, 가장자리에서 40. 크기 250x250(화면 x 1630~1880, 위에서 y 40~290).
    // 방 칸 30x30, 칸 사이 16(여기에 길목 선 16x8), 안쪽 여백 18. 5x5 격자이며 아래 줄이 y=0이다.
    // 칸 중심 = 33 + 46 * 칸번호, 길목 중심 = 56 + 46 * 칸번호.
    private static void BuildMinimap(Transform canvas, FloorDirector floor, Sprite pixel)
    {
        const float cellSize = 30f;
        const float step = 46f;
        const float first = 33f;
        const float linkLength = 16f;
        const float linkThickness = 8f;
        GameObject panelObject = new GameObject("Minimap");
        panelObject.transform.SetParent(canvas, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);
        panelImage.raycastTarget = false;
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-40f, -40f);
        panel.sizeDelta = new Vector2(250f, 250f);

        int grid = FloorDirector.GridSize;
        // 길목은 칸보다 먼저 만들어 칸 뒤에 그려지게 한다.
        Object[] horizontalLinks = new Object[grid * (grid - 1)];
        Object[] verticalLinks = new Object[grid * (grid - 1)];
        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                if (x < grid - 1)
                {
                    Image link = AddMinimapImage(panel, "LinkH" + x + "_" + y, pixel, linkLength);
                    link.rectTransform.sizeDelta = new Vector2(linkLength, linkThickness);
                    link.rectTransform.anchoredPosition = new Vector2(first + step * x + step * 0.5f, first + step * y);
                    link.enabled = false;
                    horizontalLinks[y * (grid - 1) + x] = link;
                }
                if (y < grid - 1)
                {
                    Image link = AddMinimapImage(panel, "LinkV" + x + "_" + y, pixel, linkLength);
                    link.rectTransform.sizeDelta = new Vector2(linkThickness, linkLength);
                    link.rectTransform.anchoredPosition = new Vector2(first + step * x, first + step * y + step * 0.5f);
                    link.enabled = false;
                    verticalLinks[y * grid + x] = link;
                }
            }
        }

        // 현재 방 표시(칸보다 조금 큰 흰 테두리). 칸보다 먼저 만들어 칸 뒤에 그려지게 한다.
        Image marker = AddMinimapImage(panel, "Current", pixel, cellSize + 8f);
        marker.color = Color.white;

        Object[] cells = new Object[grid * grid];
        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                Image cell = AddMinimapImage(panel, "Cell" + x + "_" + y, pixel, cellSize);
                cell.rectTransform.anchoredPosition = new Vector2(first + step * x, first + step * y);
                cell.enabled = false;
                cells[y * grid + x] = cell;
            }
        }

        MinimapUi minimap = panelObject.AddComponent<MinimapUi>();
        SerializedObject data = new SerializedObject(minimap);
        Find(data, "floor").objectReferenceValue = floor;
        SetRefArray(data, "cells", cells);
        SetRefArray(data, "horizontalLinks", horizontalLinks);
        SetRefArray(data, "verticalLinks", verticalLinks);
        Find(data, "currentMarker").objectReferenceValue = marker.rectTransform;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image AddMinimapImage(RectTransform panel, string name, Sprite pixel, float size)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(panel, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = pixel;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        return image;
    }

    // ------------------------------------------------------------ 로비 씬

    private static void BuildLobbyScene(Scene previousScene)
    {
        Scene scene = CreateScene(LobbyScenePath);
        HudController hud;
        GameObject canvasObject;
        GameObject waveLabelObject;
        GameHub hub = BuildCommon(scene, new Color(1f, 0.93f, 0.8f), out hud, out canvasObject, out waveLabelObject);
        waveLabelObject.SetActive(false);

        Vector2[] stationPositions = BuildVillage();

        // 출발 지점: 탑 입구 앞(문 아래)의 옅은 빛
        Vector2 zoneCenter = new Vector2(0f, 2.0f);
        float zoneRadius = 1.1f;
        GameObject zone = new GameObject("StartZone");
        zone.transform.position = zoneCenter;
        SpriteRenderer zoneRenderer = zone.AddComponent<SpriteRenderer>();
        zoneRenderer.sprite = LoadSprite(SpriteRoot + "/Fx/Circle.png");
        zoneRenderer.color = new Color(0.75f, 0.55f, 1f, 0.3f);
        zoneRenderer.sortingOrder = -900;
        float zoneScale = zoneRadius * 2f / 0.64f;
        zone.transform.localScale = new Vector3(zoneScale, zoneScale, 1f);

        GameObject zoneLabelObject = new GameObject("StartZoneLabel");
        zoneLabelObject.transform.position = zoneCenter + new Vector2(0f, -zoneRadius - 0.1f);
        TextMeshPro zoneLabel = zoneLabelObject.AddComponent<TextMeshPro>();
        zoneLabel.font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        zoneLabel.fontSize = 3.2f;
        zoneLabel.alignment = TextAlignmentOptions.Center;
        zoneLabel.textWrappingMode = TextWrappingModes.NoWrap;
        zoneLabel.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
        zoneLabel.text = "탑 입구 (호스트가 F)";
        zoneLabelObject.GetComponent<MeshRenderer>().sortingOrder = 400;

        SerializedObject hubData = new SerializedObject(hub);
        Find(hubData, "hasStartZone").boolValue = true;
        Find(hubData, "startZoneCenter").vector2Value = zoneCenter;
        Find(hubData, "startZoneRadius").floatValue = zoneRadius;
        hubData.ApplyModifiedPropertiesWithoutUndo();

        BuildLobbyPanel(canvasObject.transform, hub);

        SetRef(hub, "metaUi", BuildMetaPanel(canvasObject.transform, hub, stationPositions));
        SetRef(hub, "customizeUi", BuildCustomizePanel(canvasObject.transform, hub));

        SaveAndClose(scene, previousScene);
    }

    // 로비 마을(방 크기 18.4x10). Tools/VillageGen/GenerateVillage.ps1의 길 그림과 좌표를 맞춘다.
    // 위 가운데 탑 입구(밑동 y 2.7), 왼쪽 해금 상점·오른쪽 도서관(밑동 y 0.4), 오른쪽 아래 미용실(밑동 y -3.4),
    // 왼쪽 아래 우물, 가운데 광장(출발 위치 근처). 시설 위치(문 앞)를 해금 상점, 도서관, 미용실 순서로 돌려준다.
    private static Vector2[] BuildVillage()
    {
        const string root = SpriteRoot + "/Village";
        SpriteRenderer ground = GameObject.Find("Background").GetComponent<SpriteRenderer>();
        ground.sprite = LoadSprite(root + "/Ground_Grass.png");
        ground.drawMode = SpriteDrawMode.Tiled;
        ground.size = new Vector2(20f, 11.2f);
        ground.transform.localScale = Vector3.one;
        ground.color = Color.white;

        GameObject village = new GameObject("Village");
        SpriteRenderer paths = AddSprite(village.transform, "Paths", LoadSprite(root + "/Ground_Paths.png"), -990);
        paths.transform.position = Vector2.zero;

        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        SpriteRenderer gate = AddVillageProp(village.transform, "TowerGate", root + "/Prop_TowerGate.png", new Vector2(0f, 2.7f), new Vector2(3.4f, 0.6f));
        // 탑 입구의 움직이는 부분: 아치 속 소용돌이, 양쪽 횃불, 깃발 두 개(그림 픽셀 좌표는 Tools/VillageGen/anims.py와 맞춘다)
        AddPropLoop(gate, "Anim_Portal", 8, new Vector2(200f, 161f), 10f, 0f);
        AddPropLoop(gate, "Anim_Flame", 6, new Vector2(106f, 156f), 12f, 0f);
        AddPropLoop(gate, "Anim_Flame", 6, new Vector2(294f, 156f), 12f, 0.21f);
        AddPropLoop(gate, "Anim_Banner", 6, new Vector2(95f, 102f), 8f, 0f);
        AddPropLoop(gate, "Anim_Banner", 6, new Vector2(305f, 102f), 8f, 0.33f);
        Vector2 shop = new Vector2(-6f, 0.4f);
        Vector2 library = new Vector2(6f, 0.4f);
        Vector2 barber = new Vector2(5.8f, -3.4f);
        SpriteRenderer[] houses =
        {
            AddVillageProp(village.transform, "HouseShop", root + "/Prop_HouseShop.png", shop, new Vector2(2.2f, 0.6f)),
            AddVillageProp(village.transform, "HouseLibrary", root + "/Prop_HouseLibrary.png", library, new Vector2(2.2f, 0.6f)),
            AddVillageProp(village.transform, "HouseBarber", root + "/Prop_HouseBarber.png", barber, new Vector2(2.2f, 0.6f))
        };
        // 굴뚝 연기(집마다 시작 시점을 달리한다)
        for (int i = 0; i < houses.Length; i++)
        {
            AddPropLoop(houses[i], "Anim_Smoke", 8, new Vector2(206f, -14f), 7f, i * 0.4f);
        }
        // 지붕 위 이름(문 앞에서 F를 누르라는 안내는 가까이 가면 화면 아래에 나온다)
        AddWorldLabel(village.transform, "ShopName", font, "해금 상점", shop + new Vector2(0f, 2.75f), 3f);
        AddWorldLabel(village.transform, "LibraryName", font, "도서관", library + new Vector2(0f, 2.75f), 3f);
        AddWorldLabel(village.transform, "BarberName", font, "미용실", barber + new Vector2(0f, 2.75f), 3f);

        AddVillageProp(village.transform, "Well", root + "/Prop_Well.png", new Vector2(-5.6f, -3.2f), new Vector2(1.1f, 0.5f));
        Vector2[] lamps = { new Vector2(-1.7f, 1.3f), new Vector2(1.7f, 1.3f), new Vector2(-3.4f, -2.6f), new Vector2(3.4f, -2.6f) };
        for (int i = 0; i < lamps.Length; i++)
        {
            SpriteRenderer lamp = AddVillageProp(village.transform, "Lamp" + i, root + "/Prop_Lamp.png", lamps[i], new Vector2(0.25f, 0.2f));
            AddPropLoop(lamp, "Anim_LampGlow", 6, new Vector2(32f, 37f), 6f, i * 0.29f);
        }
        Vector2[] trees =
        {
            new Vector2(-3.3f, 3.4f), new Vector2(3.3f, 3.4f), new Vector2(-8.7f, 1.2f), new Vector2(8.7f, 1.2f),
            new Vector2(-8.5f, -4.3f), new Vector2(-2.3f, -4.4f), new Vector2(2.5f, -4.6f), new Vector2(-8.6f, 3.9f), new Vector2(8.6f, 3.9f)
        };
        for (int i = 0; i < trees.Length; i++)
        {
            AddVillageProp(village.transform, "Tree" + i, root + "/Prop_Tree.png", trees[i], new Vector2(0.5f, 0.3f));
        }
        // 탑 양옆 울타리(위쪽 가장자리)
        Sprite fence = LoadSprite(root + "/Prop_Fence.png");
        float[] fenceX = { -6.55f, 6.55f };
        for (int i = 0; i < fenceX.Length; i++)
        {
            SpriteRenderer fenceRenderer = AddSprite(village.transform, "Fence" + i, fence, 0);
            fenceRenderer.drawMode = SpriteDrawMode.Tiled;
            fenceRenderer.size = new Vector2(5.3f, 0.8f);
            fenceRenderer.transform.position = new Vector2(fenceX[i], 4.5f);
        }

        Vector2[] stations = { shop + new Vector2(0f, -0.5f), library + new Vector2(0f, -0.5f), barber + new Vector2(0f, -0.5f) };
        return stations;
    }

    // 마을 소품: 발밑 피벗이라 캐릭터와 같은 정렬 순서(0)에서 Y로 앞뒤가 정해진다. 발밑 충돌은 Wall 레이어.
    private static SpriteRenderer AddVillageProp(Transform parent, string name, string spritePath, Vector2 position, Vector2 colliderSize)
    {
        SpriteRenderer renderer = AddSprite(parent, name, LoadSprite(spritePath), 0);
        renderer.transform.position = position;
        renderer.gameObject.layer = GameLayers.Wall;
        BoxCollider2D box = renderer.gameObject.AddComponent<BoxCollider2D>();
        box.size = colliderSize;
        box.offset = new Vector2(0f, colliderSize.y * 0.3f);
        return renderer;
    }

    // 소품 위에 반복 애니메이션(연기, 불꽃, 깃발 등)을 겹친다. pixel은 소품 그림의 왼쪽 위 기준 픽셀 좌표이고, 겹치는 그림의 가운데가 그 자리에 온다.
    // 소품에 SortingGroup을 붙여 소품과 겹친 그림이 한 덩어리로 Y 정렬되게 한다(겹친 그림은 그룹 안에서 소품 위).
    private static void AddPropLoop(SpriteRenderer prop, string prefix, int frameCount, Vector2 pixel, float fps, float timeOffset)
    {
        if (prop.GetComponent<SortingGroup>() == null)
        {
            prop.gameObject.AddComponent<SortingGroup>();
        }
        Sprite[] frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = LoadSprite(SpriteRoot + "/Village/" + prefix + "_" + i.ToString("00") + ".png");
        }
        SpriteRenderer overlay = AddSprite(prop.transform, prefix, frames[0], 1);
        Sprite sprite = prop.sprite;
        float unit = sprite.pixelsPerUnit;
        overlay.transform.localPosition = new Vector3((pixel.x - sprite.pivot.x) / unit, (sprite.rect.height - pixel.y - sprite.pivot.y) / unit, 0f);
        SpriteLoop loop = overlay.gameObject.AddComponent<SpriteLoop>();
        SerializedObject loopData = new SerializedObject(loop);
        Find(loopData, "spriteRenderer").objectReferenceValue = overlay;
        SetRefArray(loopData, "frames", frames);
        Find(loopData, "framesPerSecond").floatValue = fps;
        Find(loopData, "timeOffset").floatValue = timeOffset;
        loopData.ApplyModifiedPropertiesWithoutUndo();
    }

    // 캐릭터 꾸미기 패널. 기준 해상도 1920x1080에서 왼쪽 가운데, 가장자리에서 40, 크기 480x660(화면 x 40~520, y 210~870).
    // 줄 7개: 이름(20~180) + [<](190~246) + 값(254~390, 색 줄은 80x30 견본) + [>](398~454), y 140부터 간격 62, 높이 48.
    // 아래 버튼 3개(무작위, 취소, 완료)는 y 588, 각 140x52, x 20 / 170 / 320.
    private static CustomizeUi BuildCustomizePanel(Transform canvas, GameHub hub)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        GameObject rootObject = new GameObject("CustomizeUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        CustomizeUi ui = rootObject.AddComponent<CustomizeUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0f, 0.5f);
        panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(40f, 0f);
        panel.sizeDelta = new Vector2(480f, 660f);

        AddPanelText(panel, "Title", font, "캐릭터 꾸미기", new Vector2(20f, 16f), new Vector2(440f, 44f), 32f, false);
        TextMeshProUGUI hint = AddPanelText(panel, "Hint", font, string.Empty, new Vector2(20f, 66f), new Vector2(440f, 60f), 18f, true);

        string[] rowNames = { "머리 모양", "머리 색", "눈", "수염", "수염 색", "피부", "피부 색" };
        Object[] values = new Object[CustomizeUi.RowCount];
        Object[] swatches = new Object[CustomizeUi.RowCount];
        Object[] prevButtons = new Object[CustomizeUi.RowCount];
        Object[] nextButtons = new Object[CustomizeUi.RowCount];
        TextMeshProUGUI label;
        for (int i = 0; i < CustomizeUi.RowCount; i++)
        {
            float y = 140f + 62f * i;
            AddPanelText(panel, "Name" + i, font, rowNames[i], new Vector2(20f, y), new Vector2(160f, 48f), 24f, false);
            prevButtons[i] = AddPanelButton(panel, "Prev" + i, pixel, font, "<", new Vector2(190f, y), new Vector2(56f, 48f), out label);
            TextMeshProUGUI value = AddPanelText(panel, "Value" + i, font, string.Empty, new Vector2(254f, y), new Vector2(136f, 48f), 24f, false);
            value.alignment = TextAlignmentOptions.Center;
            values[i] = value;
            GameObject swatchObject = new GameObject("Swatch" + i);
            swatchObject.transform.SetParent(panel, false);
            Image swatch = swatchObject.AddComponent<Image>();
            swatch.sprite = pixel;
            swatch.raycastTarget = false;
            PlaceTopLeft(swatch.rectTransform, new Vector2(282f, y + 9f), new Vector2(80f, 30f));
            swatch.enabled = false;
            swatches[i] = swatch;
            nextButtons[i] = AddPanelButton(panel, "Next" + i, pixel, font, ">", new Vector2(398f, y), new Vector2(56f, 48f), out label);
        }
        Button random = AddPanelButton(panel, "Random", pixel, font, "무작위", new Vector2(20f, 588f), new Vector2(140f, 52f), out label);
        Button cancel = AddPanelButton(panel, "Cancel", pixel, font, "취소", new Vector2(170f, 588f), new Vector2(140f, 52f), out label);
        cancel.image.color = new Color(0.45f, 0.45f, 0.5f);
        Button done = AddPanelButton(panel, "Done", pixel, font, "완료", new Vector2(320f, 588f), new Vector2(140f, 52f), out label);
        done.image.color = new Color(0.85f, 0.6f, 0.2f);

        SerializedObject data = new SerializedObject(ui);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        SetRefArray(data, "rowValues", values);
        SetRefArray(data, "rowSwatches", swatches);
        SetRefArray(data, "rowPrevButtons", prevButtons);
        SetRefArray(data, "rowNextButtons", nextButtons);
        Find(data, "randomButton").objectReferenceValue = random;
        Find(data, "cancelButton").objectReferenceValue = cancel;
        Find(data, "doneButton").objectReferenceValue = done;
        Find(data, "hintLabel").objectReferenceValue = hint;
        data.ApplyModifiedPropertiesWithoutUndo();
        panelObject.SetActive(false);
        return ui;
    }

    // 마을 시설 패널(해금 상인, 도감, 설정이 함께 쓴다). 화면 가운데에서 위로 40, 크기 560x520.
    // 화면 좌표로는 x 680~1240이라 오른쪽 로비 패널(1460부터)과 겹치지 않는다. 안쪽 요소는 패널 왼쪽 위 기준.
    // 제목 y 16, 닫기 버튼 오른쪽 위 40x40, 본문 y 72부터 높이 250, 버튼 3개 520x46은 y 336부터 간격 56.
    private static MetaUi BuildMetaPanel(Transform canvas, GameHub hub, Vector2[] stationPositions)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        // 패널을 꺼도 MetaUi는 켜져 있어야 하므로 빈 부모를 둔다.
        GameObject rootObject = new GameObject("MetaUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        MetaUi metaUi = rootObject.AddComponent<MetaUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 40f);
        panel.sizeDelta = new Vector2(560f, 520f);

        TextMeshProUGUI title = AddPanelText(panel, "Title", font, string.Empty, new Vector2(20f, 16f), new Vector2(440f, 44f), 32f, false);
        TextMeshProUGUI closeLabel;
        Button closeButton = AddPanelButton(panel, "CloseButton", pixel, font, "X", new Vector2(500f, 16f), new Vector2(40f, 40f), out closeLabel);
        TextMeshProUGUI body = AddPanelText(panel, "Body", font, string.Empty, new Vector2(20f, 72f), new Vector2(520f, 250f), 22f, true);

        Object[] buttons = new Object[3];
        Object[] labels = new Object[3];
        for (int i = 0; i < buttons.Length; i++)
        {
            TextMeshProUGUI label;
            buttons[i] = AddPanelButton(panel, "Action" + i, pixel, font, string.Empty, new Vector2(20f, 336f + 56f * i), new Vector2(520f, 46f), out label);
            labels[i] = label;
        }

        SerializedObject data = new SerializedObject(metaUi);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        Find(data, "bodyLabel").objectReferenceValue = body;
        Find(data, "closeButton").objectReferenceValue = closeButton;
        SetRefArray(data, "actionButtons", buttons);
        SetRefArray(data, "actionLabels", labels);
        SerializedProperty positions = Find(data, "stationPositions");
        positions.arraySize = stationPositions.Length;
        for (int i = 0; i < stationPositions.Length; i++)
        {
            positions.GetArrayElementAtIndex(i).vector2Value = stationPositions[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return metaUi;
    }

    // Esc 메뉴. 화면 전체를 어둡게 덮고 가운데에 560x640 상자(화면 x 680~1240, y 220~860)를 둔다.
    // 다른 패널보다 위에 그려지도록 씬 저장 직전에 캔버스의 마지막 자식으로 옮긴다(SaveAndClose).
    private static PauseMenuUi BuildPauseMenu(Transform canvas, GameHub hub)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        GameObject rootObject = new GameObject("PauseMenu");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        Stretch(root, 0f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        PauseMenuUi menu = rootObject.AddComponent<PauseMenuUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image dim = panelObject.AddComponent<Image>();
        dim.sprite = pixel;
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        Stretch(dim.rectTransform, 0f);
        dim.rectTransform.offsetMin = Vector2.zero;
        dim.rectTransform.offsetMax = Vector2.zero;

        GameObject boxObject = new GameObject("Box");
        boxObject.transform.SetParent(panelObject.transform, false);
        Image boxImage = boxObject.AddComponent<Image>();
        boxImage.sprite = pixel;
        boxImage.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        RectTransform box = boxImage.rectTransform;
        box.anchorMin = new Vector2(0.5f, 0.5f);
        box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = Vector2.zero;
        box.sizeDelta = new Vector2(560f, 640f);

        TextMeshProUGUI title = AddPanelText(box, "Title", font, "메뉴", new Vector2(20f, 16f), new Vector2(520f, 48f), 34f, false);
        title.alignment = TextAlignmentOptions.Center;

        // 메인: 버튼 4개(계속하기, 설정, 나가기, 게임 종료), 폭 440, 간격 76
        RectTransform mainPage = AddPage(box, "MainPage");
        TextMeshProUGUI label;
        Button resume = AddPanelButton(mainPage, "Resume", pixel, font, "계속하기", new Vector2(60f, 100f), new Vector2(440f, 60f), out label);
        Button settings = AddPanelButton(mainPage, "Settings", pixel, font, "설정", new Vector2(60f, 176f), new Vector2(440f, 60f), out label);
        TextMeshProUGUI exitLabel;
        Button exit = AddPanelButton(mainPage, "Exit", pixel, font, "로비로 돌아가기", new Vector2(60f, 252f), new Vector2(440f, 60f), out exitLabel);
        exit.image.color = new Color(0.75f, 0.5f, 0.2f);
        Button quit = AddPanelButton(mainPage, "Quit", pixel, font, "게임 종료", new Vector2(60f, 328f), new Vector2(440f, 60f), out label);
        quit.image.color = new Color(0.6f, 0.25f, 0.25f);

        // 설정: 한 줄 = 이름(20~220) + [<](236~292) + 값(300~460) + [>](468~524), 줄 간격 62
        RectTransform settingsPage = AddPage(box, "SettingsPage");
        string[] rowNames = { "전체 화면", "해상도", "화면 흔들림", "피해 숫자", "전체 음량", "효과음", "배경음" };
        Object[] values = new Object[PauseMenuUi.RowCount];
        Object[] prevButtons = new Object[PauseMenuUi.RowCount];
        Object[] nextButtons = new Object[PauseMenuUi.RowCount];
        for (int i = 0; i < PauseMenuUi.RowCount; i++)
        {
            float y = 84f + 62f * i;
            AddPanelText(settingsPage, "Name" + i, font, rowNames[i], new Vector2(20f, y), new Vector2(200f, 48f), 24f, false);
            prevButtons[i] = AddPanelButton(settingsPage, "Prev" + i, pixel, font, "<", new Vector2(236f, y), new Vector2(56f, 48f), out label);
            TextMeshProUGUI value = AddPanelText(settingsPage, "Value" + i, font, string.Empty, new Vector2(300f, y), new Vector2(160f, 48f), 24f, false);
            value.alignment = TextAlignmentOptions.Center;
            values[i] = value;
            nextButtons[i] = AddPanelButton(settingsPage, "Next" + i, pixel, font, ">", new Vector2(468f, y), new Vector2(56f, 48f), out label);
        }
        Button back = AddPanelButton(settingsPage, "Back", pixel, font, "뒤로", new Vector2(60f, 560f), new Vector2(440f, 56f), out label);

        SerializedObject data = new SerializedObject(menu);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "mainPage").objectReferenceValue = mainPage.gameObject;
        Find(data, "settingsPage").objectReferenceValue = settingsPage.gameObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        Find(data, "box").objectReferenceValue = box;
        Find(data, "resumeButton").objectReferenceValue = resume;
        Find(data, "settingsButton").objectReferenceValue = settings;
        Find(data, "exitButton").objectReferenceValue = exit;
        Find(data, "exitLabel").objectReferenceValue = exitLabel;
        Find(data, "quitButton").objectReferenceValue = quit;
        Find(data, "backButton").objectReferenceValue = back;
        SetRefArray(data, "rowValues", values);
        SetRefArray(data, "rowPrevButtons", prevButtons);
        SetRefArray(data, "rowNextButtons", nextButtons);
        data.ApplyModifiedPropertiesWithoutUndo();

        settingsPage.gameObject.SetActive(false);
        panelObject.SetActive(false);
        return menu;
    }

    // 상자 전체를 덮는 빈 페이지(자식 좌표는 상자 왼쪽 위 기준)
    private static RectTransform AddPage(RectTransform box, string name)
    {
        GameObject pageObject = new GameObject(name);
        pageObject.transform.SetParent(box, false);
        RectTransform page = pageObject.AddComponent<RectTransform>();
        page.anchorMin = Vector2.zero;
        page.anchorMax = Vector2.one;
        page.offsetMin = Vector2.zero;
        page.offsetMax = Vector2.zero;
        return page;
    }

    // 로비 패널. 기준 해상도 1920x1080에서 오른쪽 위, 화면 가장자리에서 40 띄움. 크기 420x520.
    // 화면 좌표로는 x 1460~1880, y 520~1040. 안쪽 요소는 패널 왼쪽 위 기준 (x, 아래로 y).
    private static void BuildLobbyPanel(Transform canvas, GameHub hub)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        GameObject panelObject = new GameObject("LobbyPanel");
        panelObject.transform.SetParent(canvas, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-40f, -40f);
        panel.sizeDelta = new Vector2(420f, 520f);

        AddPanelText(panel, "Title", font, "로비", new Vector2(20f, 16f), new Vector2(380f, 44f), 32f, false);

        Object[] rows = new Object[GameNetworkManager.MaxPlayers];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = AddPanelText(panel, "PlayerRow" + i, font, (i + 1) + ". 비어 있음", new Vector2(20f, 70f + 40f * i), new Vector2(380f, 32f), 22f, false);
        }

        TextMeshProUGUI addressLabel = AddPanelText(panel, "AddressLabel", font, "초대 주소", new Vector2(20f, 240f), new Vector2(380f, 30f), 20f, false);
        TextMeshProUGUI copyLabel;
        Button copyButton = AddPanelButton(panel, "CopyButton", pixel, font, "초대 주소 복사", new Vector2(20f, 276f), new Vector2(380f, 40f), out copyLabel);
        TMP_InputField addressInput = AddPanelInput(panel, "AddressInput", pixel, font, "참가할 주소 입력", new Vector2(20f, 330f), new Vector2(250f, 40f));
        TextMeshProUGUI joinLabel;
        Button joinButton = AddPanelButton(panel, "JoinButton", pixel, font, "참가", new Vector2(280f, 330f), new Vector2(120f, 40f), out joinLabel);
        TextMeshProUGUI actionLabel;
        Button actionButton = AddPanelButton(panel, "ActionButton", pixel, font, "게임 시작", new Vector2(20f, 384f), new Vector2(380f, 46f), out actionLabel);
        actionButton.image.color = new Color(0.85f, 0.6f, 0.2f);
        TextMeshProUGUI statusLabel = AddPanelText(panel, "StatusLabel", font, string.Empty, new Vector2(20f, 440f), new Vector2(380f, 64f), 16f, true);

        LobbyUi lobbyUi = panelObject.AddComponent<LobbyUi>();
        SerializedObject data = new SerializedObject(lobbyUi);
        Find(data, "hub").objectReferenceValue = hub;
        SetRefArray(data, "playerRows", rows);
        Find(data, "addressLabel").objectReferenceValue = addressLabel;
        Find(data, "statusLabel").objectReferenceValue = statusLabel;
        Find(data, "addressInput").objectReferenceValue = addressInput;
        Find(data, "copyButton").objectReferenceValue = copyButton;
        Find(data, "joinButton").objectReferenceValue = joinButton;
        Find(data, "actionButton").objectReferenceValue = actionButton;
        Find(data, "actionLabel").objectReferenceValue = actionLabel;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(position.x, -position.y);
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, 0f);
        rect.offsetMax = new Vector2(-padding, 0f);
    }

    private static TextMeshProUGUI AddText(Transform parent, string name, TMP_FontAsset font, string text, float fontSize, bool wrap)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        if (wrap)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.TopLeft;
        }
        else
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        label.text = text;
        return label;
    }

    private static TextMeshProUGUI AddPanelText(RectTransform panel, string name, TMP_FontAsset font, string text, Vector2 position, Vector2 size, float fontSize, bool wrap)
    {
        TextMeshProUGUI label = AddText(panel, name, font, text, fontSize, wrap);
        PlaceTopLeft(label.rectTransform, position, size);
        return label;
    }

    private static Button AddPanelButton(RectTransform panel, string name, Sprite sprite, TMP_FontAsset font, string text, Vector2 position, Vector2 size, out TextMeshProUGUI label)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(panel, false);
        Image image = buttonObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(0.3f, 0.45f, 0.75f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        PlaceTopLeft(image.rectTransform, position, size);

        label = AddText(buttonObject.transform, "Label", font, text, 22f, false);
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform, 4f);
        return button;
    }

    private static TMP_InputField AddPanelInput(RectTransform panel, string name, Sprite sprite, TMP_FontAsset font, string placeholderText, Vector2 position, Vector2 size)
    {
        GameObject inputObject = new GameObject(name);
        inputObject.transform.SetParent(panel, false);
        Image image = inputObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, 0.92f);
        PlaceTopLeft(image.rectTransform, position, size);

        GameObject areaObject = new GameObject("TextArea");
        areaObject.transform.SetParent(inputObject.transform, false);
        RectTransform area = areaObject.AddComponent<RectTransform>();
        areaObject.AddComponent<RectMask2D>();
        Stretch(area, 10f);

        TextMeshProUGUI placeholder = AddText(areaObject.transform, "Placeholder", font, placeholderText, 20f, false);
        placeholder.color = new Color(0f, 0f, 0f, 0.4f);
        Stretch(placeholder.rectTransform, 0f);

        TextMeshProUGUI text = AddText(areaObject.transform, "Text", font, string.Empty, 20f, false);
        text.color = Color.black;
        Stretch(text.rectTransform, 0f);

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = font;
        input.pointSize = 20f;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 64;
        return input;
    }

    // ------------------------------------------------------------ HUD
    // 기준 해상도 1920x1080. 좌표는 왼쪽 아래 기준(웨이브 표시는 위쪽 가운데 기준).

    private static HudController BuildHud(out GameObject canvasObject, out GameObject waveLabelObject)
    {
        canvasObject = new GameObject("HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        Transform parent = canvasObject.transform;

        AddBar(parent, "HpBack", pixel, new Vector2(40f, 72f), new Vector2(360f, 28f), new Color(0f, 0f, 0f, 0.6f), false);
        Image hpFill = AddBar(parent, "HpFill", pixel, new Vector2(43f, 75f), new Vector2(354f, 22f), new Color(0.88f, 0.26f, 0.22f), true);
        AddBar(parent, "ManaBack", pixel, new Vector2(40f, 40f), new Vector2(300f, 20f), new Color(0f, 0f, 0f, 0.6f), false);
        Image manaFill = AddBar(parent, "ManaFill", pixel, new Vector2(43f, 43f), new Vector2(294f, 14f), new Color(0.25f, 0.55f, 0.95f), true);
        Image pip0 = AddBar(parent, "RollPip0", pixel, new Vector2(40f, 112f), new Vector2(24f, 24f), Color.white, false);
        Image pip1 = AddBar(parent, "RollPip1", pixel, new Vector2(72f, 112f), new Vector2(24f, 24f), Color.white, false);

        TextMeshProUGUI waveLabel = AddText(parent, "WaveLabel", LoadRequired<TMP_FontAsset>(KoreanFontAssetPath), string.Empty, 40f, false);
        waveLabel.alignment = TextAlignmentOptions.Center;
        waveLabelObject = waveLabel.gameObject;
        RectTransform waveRect = waveLabel.rectTransform;
        waveRect.anchorMin = new Vector2(0.5f, 1f);
        waveRect.anchorMax = new Vector2(0.5f, 1f);
        waveRect.pivot = new Vector2(0.5f, 1f);
        waveRect.anchoredPosition = new Vector2(0f, -30f);
        waveRect.sizeDelta = new Vector2(400f, 60f);

        // 무기 이름: 구르기 충전 칸 위(간격 12). 왼쪽 아래 기준 (40,148), 크기 400x28.
        TextMeshProUGUI weaponLabel = AddText(parent, "WeaponLabel", LoadRequired<TMP_FontAsset>(KoreanFontAssetPath), string.Empty, 22f, false);
        RectTransform weaponRect = weaponLabel.rectTransform;
        weaponRect.anchorMin = Vector2.zero;
        weaponRect.anchorMax = Vector2.zero;
        weaponRect.pivot = Vector2.zero;
        weaponRect.anchoredPosition = new Vector2(40f, 148f);
        weaponRect.sizeDelta = new Vector2(600f, 28f);

        // 골드·광석·포션: 구르기 충전 칸 오른쪽. 왼쪽 아래 기준 (112,110), 크기 520x28.
        TMP_FontAsset hudFont = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        TextMeshProUGUI goldLabel = AddHudText(parent, "GoldLabel", hudFont, 0f, new Vector2(112f, 110f), new Vector2(520f, 28f), 22f);
        goldLabel.color = new Color(1f, 0.85f, 0.3f);
        // 줍기 안내와 알림: 아래쪽 가운데 기준, 가운데 정렬. 안내 (0,190) 900x34, 알림 (0,236) 900x34.
        TextMeshProUGUI promptLabel = AddHudText(parent, "PromptLabel", hudFont, 0.5f, new Vector2(0f, 190f), new Vector2(900f, 34f), 26f);
        promptLabel.alignment = TextAlignmentOptions.Center;
        promptLabel.enabled = false;
        TextMeshProUGUI noticeLabel = AddHudText(parent, "NoticeLabel", hudFont, 0.5f, new Vector2(0f, 236f), new Vector2(900f, 34f), 24f);
        noticeLabel.alignment = TextAlignmentOptions.Center;
        noticeLabel.color = new Color(1f, 0.6f, 0.5f);
        noticeLabel.enabled = false;

        // 걸린 상태이상: 체력 바 오른쪽. 왼쪽 아래 기준 (412,72), 크기 400x28.
        TextMeshProUGUI statusLabel = AddHudText(parent, "StatusLabel", hudFont, 0f, new Vector2(412f, 72f), new Vector2(400f, 28f), 22f);
        statusLabel.color = new Color(1f, 0.6f, 0.9f);

        // 보스 체력 바: 위쪽 가운데 기준. 층·방 이름(위에서 30~90) 아래에 이름 600x30(위에서 96), 바 600x20(위에서 130).
        GameObject bossRoot = new GameObject("BossBar");
        bossRoot.transform.SetParent(parent, false);
        RectTransform bossRect = bossRoot.AddComponent<RectTransform>();
        bossRect.anchorMin = new Vector2(0.5f, 1f);
        bossRect.anchorMax = new Vector2(0.5f, 1f);
        bossRect.pivot = new Vector2(0.5f, 1f);
        bossRect.anchoredPosition = new Vector2(0f, -96f);
        bossRect.sizeDelta = new Vector2(600f, 54f);
        TextMeshProUGUI bossLabel = AddPanelText(bossRect, "Name", hudFont, string.Empty, new Vector2(0f, 0f), new Vector2(600f, 30f), 24f, false);
        bossLabel.alignment = TextAlignmentOptions.Center;
        bossLabel.color = new Color(0.9f, 0.7f, 1f);
        Image bossBack = AddBar(bossRoot.transform, "Back", pixel, Vector2.zero, new Vector2(600f, 20f), new Color(0f, 0f, 0f, 0.6f), false);
        PlaceTopLeft(bossBack.rectTransform, new Vector2(0f, 34f), new Vector2(600f, 20f));
        Image bossFill = AddBar(bossRoot.transform, "Fill", pixel, Vector2.zero, new Vector2(594f, 14f), new Color(0.75f, 0.3f, 0.9f), true);
        PlaceTopLeft(bossFill.rectTransform, new Vector2(3f, 37f), new Vector2(594f, 14f));
        bossRoot.SetActive(false);

        HudController hud = canvasObject.AddComponent<HudController>();
        SerializedObject hudData = new SerializedObject(hud);
        Find(hudData, "statusLabel").objectReferenceValue = statusLabel;
        Find(hudData, "bossBarRoot").objectReferenceValue = bossRoot;
        Find(hudData, "bossFill").objectReferenceValue = bossFill;
        Find(hudData, "bossLabel").objectReferenceValue = bossLabel;
        Find(hudData, "goldLabel").objectReferenceValue = goldLabel;
        Find(hudData, "promptLabel").objectReferenceValue = promptLabel;
        Find(hudData, "noticeLabel").objectReferenceValue = noticeLabel;
        Find(hudData, "weaponLabel").objectReferenceValue = weaponLabel;
        Find(hudData, "hpFill").objectReferenceValue = hpFill;
        Find(hudData, "manaFill").objectReferenceValue = manaFill;
        SetRefArray(hudData, "rollPips", new Object[] { pip0, pip1 });
        Find(hudData, "waveLabel").objectReferenceValue = waveLabel;
        hudData.ApplyModifiedPropertiesWithoutUndo();
        return hud;
    }

    // 화면 아래쪽 기준 글자. anchorX 0은 왼쪽 아래, 0.5는 아래쪽 가운데 기준이다.
    private static TextMeshProUGUI AddHudText(Transform parent, string name, TMP_FontAsset font, float anchorX, Vector2 position, Vector2 size, float fontSize)
    {
        TextMeshProUGUI label = AddText(parent, name, font, string.Empty, fontSize, false);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(anchorX, 0f);
        rect.anchorMax = new Vector2(anchorX, 0f);
        rect.pivot = new Vector2(anchorX, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return label;
    }

    // ------------------------------------------------------------ 가방 화면
    // 기준 해상도 1920x1080에서 왼쪽 가운데. 크기 512x640, 화면 왼쪽에서 40.
    // 화면 좌표로는 x 40~552, y 250~890(아래쪽 HUD는 y 176까지). 안쪽 요소는 패널 왼쪽 위 기준 (x, 아래로 y).
    // 칸은 72x72, 간격 8. 장착 4칸은 y 58, 가방 6x2칸은 y 206부터.

    private static InventoryUi BuildInventoryPanel(Transform canvas)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        // 패널을 꺼도 InventoryUi는 켜져 있어야 하므로 빈 부모를 둔다.
        GameObject rootObject = new GameObject("InventoryUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        InventoryUi inventoryUi = rootObject.AddComponent<InventoryUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.78f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0f, 0.5f);
        panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(40f, 30f);
        panel.sizeDelta = new Vector2(512f, 640f);

        float slotSize = 72f;
        float step = 80f;
        AddPanelText(panel, "EquipTitle", font, "장비", new Vector2(20f, 14f), new Vector2(300f, 36f), 28f, false);
        string[] equipNames = { "무기", "보조", "투구", "갑옷" };
        Object[] equipSlots = new Object[PlayerInventory.EquipSlotCount];
        for (int i = 0; i < equipSlots.Length; i++)
        {
            equipSlots[i] = AddInventorySlot(panel, "Equip" + i, pixel, new Vector2(20f + step * i, 58f), slotSize, inventoryUi, true, i);
            TextMeshProUGUI slotName = AddPanelText(panel, "EquipName" + i, font, equipNames[i], new Vector2(20f + step * i, 134f), new Vector2(slotSize, 22f), 16f, false);
            slotName.alignment = TextAlignmentOptions.Center;
        }

        AddPanelText(panel, "BagTitle", font, "가방", new Vector2(20f, 166f), new Vector2(300f, 32f), 24f, false);
        Object[] bagSlots = new Object[PlayerInventory.BagSize];
        for (int i = 0; i < bagSlots.Length; i++)
        {
            int column = i % 6;
            int row = i / 6;
            bagSlots[i] = AddInventorySlot(panel, "Bag" + i, pixel, new Vector2(20f + step * column, 206f + step * row), slotSize, inventoryUi, false, i);
        }

        TextMeshProUGUI help = AddPanelText(panel, "Help", font, "좌클릭 장착 · 해제     우클릭 버리기", new Vector2(20f, 368f), new Vector2(472f, 24f), 16f, false);
        help.color = new Color(1f, 1f, 1f, 0.6f);
        TextMeshProUGUI detail = AddPanelText(panel, "Detail", font, string.Empty, new Vector2(20f, 400f), new Vector2(472f, 224f), 18f, true);

        SerializedObject data = new SerializedObject(inventoryUi);
        Find(data, "panel").objectReferenceValue = panelObject;
        SetRefArray(data, "equipSlots", equipSlots);
        SetRefArray(data, "bagSlots", bagSlots);
        Find(data, "detailLabel").objectReferenceValue = detail;
        data.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return inventoryUi;
    }

    // 결과 화면: 화면 가운데, 크기 560x440. 제목은 위에서 24(높이 56), 본문은 위에서 100부터(520x320).
    private static ResultUi BuildResultPanel(Transform canvas)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        GameObject rootObject = new GameObject("ResultUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        ResultUi resultUi = rootObject.AddComponent<ResultUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0.05f, 0.05f, 0.06f, 0.95f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(560f, 440f);

        TextMeshProUGUI title = AddPanelText(panel, "Title", font, string.Empty, new Vector2(20f, 24f), new Vector2(520f, 56f), 44f, false);
        title.alignment = TextAlignmentOptions.Center;
        TextMeshProUGUI body = AddPanelText(panel, "Body", font, string.Empty, new Vector2(20f, 100f), new Vector2(520f, 320f), 24f, true);
        body.alignment = TextAlignmentOptions.Top;

        SerializedObject data = new SerializedObject(resultUi);
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        Find(data, "bodyLabel").objectReferenceValue = body;
        data.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return resultUi;
    }

    // ------------------------------------------------------------ 스킬 화면
    // 스킬 표시 줄: 아래쪽 가운데 기준. 칸 3개 64x64, 가운데 x가 -76, 0, 76, 아래에서 40(위쪽 끝 104).
    // 경험치 줄 216x6은 아래에서 112, 레벨 글자 600x26은 아래에서 124(줍기 안내는 190부터).
    // 시작 직업 목록(로비만) 1100x26은 아래에서 154(화면 x 410~1510, 왼쪽 무기 글자는 x 382까지).
    // 스킬 트리 패널: 가운데에서 오른쪽 16, 위로 20, 크기 820x720(화면 x 566~1386, y 160~880이라 가방 552와 상점 1400 사이).
    // 안쪽 요소는 패널 왼쪽 위 기준. 직업 상자 220x44는 (300, 56), 갈래 가운데 x 135/405/675, 갈래 이름 y 146,
    // 노드 230x64는 y 196부터 간격 96(마지막 아래 끝 548), 장착 칸 124x52는 x 95부터 간격 130·y 580(오른쪽 끝 479), 설명 300x130은 (500, 576).
    private static SkillUi BuildSkillUi(Transform canvas, GameHub hub)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        Sprite[] icons = LoadSkillIcons();

        GameObject rootObject = new GameObject("SkillUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        SkillUi skillUi = rootObject.AddComponent<SkillUi>();

        string[] keys = { "Q", "E", "R" };
        Object[] frames = new Object[keys.Length];
        Object[] fills = new Object[keys.Length];
        Object[] labels = new Object[keys.Length];
        Object[] barIcons = new Object[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            Image frame = AddBottomCenterImage(root, "Skill" + keys[i], pixel, new Vector2(-76f + 76f * i, 40f), new Vector2(64f, 64f), new Color(0f, 0f, 0f, 0.35f));
            Image icon = AddChildImage(frame.transform, "Icon", pixel, new Vector2(0f, 4f), new Vector2(48f, 48f));
            TextMeshProUGUI name = AddText(frame.transform, "Name", font, string.Empty, 14f, true);
            name.alignment = TextAlignmentOptions.BottomRight;
            Stretch(name.rectTransform, 3f);

            GameObject fillObject = new GameObject("Cooldown");
            fillObject.transform.SetParent(frame.transform, false);
            Image fill = fillObject.AddComponent<Image>();
            fill.sprite = pixel;
            fill.color = new Color(0f, 0f, 0f, 0.7f);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform, 0f);

            TextMeshProUGUI key = AddText(frame.transform, "Key", font, keys[i], 18f, false);
            key.alignment = TextAlignmentOptions.TopLeft;
            Stretch(key.rectTransform, 4f);

            frames[i] = frame;
            fills[i] = fill;
            labels[i] = name;
            barIcons[i] = icon;
        }
        AddBottomCenterImage(root, "XpBack", pixel, new Vector2(0f, 112f), new Vector2(216f, 6f), new Color(0f, 0f, 0f, 0.6f));
        Image xpFill = AddBottomCenterImage(root, "XpFill", pixel, new Vector2(0f, 112f), new Vector2(216f, 6f), new Color(0.6f, 0.9f, 0.5f));
        xpFill.type = Image.Type.Filled;
        xpFill.fillMethod = Image.FillMethod.Horizontal;
        xpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        xpFill.fillAmount = 0f;
        TextMeshProUGUI levelLabel = AddHudText(root, "LevelLabel", font, 0.5f, new Vector2(0f, 124f), new Vector2(600f, 26f), 20f);
        levelLabel.alignment = TextAlignmentOptions.Center;
        TextMeshProUGUI jobList = AddHudText(root, "JobList", font, 0.5f, new Vector2(0f, 154f), new Vector2(1100f, 26f), 18f);
        jobList.alignment = TextAlignmentOptions.Center;
        jobList.richText = true;

        GameObject panelObject = new GameObject("TreePanel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0.05f, 0.05f, 0.06f, 0.95f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(16f, 20f);
        panel.sizeDelta = new Vector2(820f, 720f);

        Color lineColor = new Color(0.3f, 0.3f, 0.32f, 1f);
        TextMeshProUGUI title = AddPanelText(panel, "Title", font, string.Empty, new Vector2(20f, 14f), new Vector2(780f, 34f), 26f, false);

        // 뿌리: 직업 이름 상자와 세 갈래로 갈라지는 선
        Image rootBox = AddPanelImage(panel, "RootBox", pixel, new Vector2(300f, 56f), new Vector2(220f, 44f), new Color(0.35f, 0.28f, 0.12f, 1f));
        TextMeshProUGUI rootLabel = AddText(rootBox.transform, "Label", font, string.Empty, 22f, false);
        rootLabel.alignment = TextAlignmentOptions.Center;
        Stretch(rootLabel.rectTransform, 4f);
        AddPanelImage(panel, "RootStem", pixel, new Vector2(407f, 100f), new Vector2(6f, 24f), lineColor);
        AddPanelImage(panel, "RootBar", pixel, new Vector2(132f, 124f), new Vector2(546f, 6f), lineColor);

        Object[] branchLabels = new Object[JobDefinition.BranchCount];
        Object[] branchLinks = new Object[JobDefinition.BranchCount];
        Object[] nodeViews = new Object[JobDefinition.NodeCount];
        Object[] nodeLinks = new Object[JobDefinition.NodeCount];
        for (int branch = 0; branch < JobDefinition.BranchCount; branch++)
        {
            float center = 135f + 270f * branch;
            branchLinks[branch] = AddPanelImage(panel, "BranchLink" + branch, pixel, new Vector2(center - 3f, 130f), new Vector2(6f, 16f), lineColor);
            TextMeshProUGUI branchLabel = AddPanelText(panel, "Branch" + branch, font, string.Empty, new Vector2(center - 125f, 146f), new Vector2(250f, 28f), 20f, false);
            branchLabel.alignment = TextAlignmentOptions.Center;
            branchLabels[branch] = branchLabel;
            for (int step = 0; step < JobDefinition.BranchSize; step++)
            {
                int index = branch * JobDefinition.BranchSize + step;
                float top = 196f + 96f * step;
                float linkTop = 174f;
                float linkHeight = 22f;
                if (step > 0)
                {
                    linkTop = top - 32f;
                    linkHeight = 32f;
                }
                nodeLinks[index] = AddPanelImage(panel, "Link" + index, pixel, new Vector2(center - 3f, linkTop), new Vector2(6f, linkHeight), lineColor);
                nodeViews[index] = AddSkillNode(panel, "Node" + index, skillUi, pixel, font, new Vector2(center - 115f, top), new Vector2(230f, 64f), index, false);
            }
        }

        // 장착 칸(Q, E, R)과 설명
        AddPanelImage(panel, "Separator", pixel, new Vector2(20f, 566f), new Vector2(780f, 2f), lineColor);
        TextMeshProUGUI equipLabel = AddPanelText(panel, "EquipLabel", font, "장착", new Vector2(20f, 580f), new Vector2(70f, 52f), 22f, false);
        equipLabel.alignment = TextAlignmentOptions.Left;
        Object[] slotViews = new Object[JobDefinition.SlotCount];
        for (int i = 0; i < JobDefinition.SlotCount; i++)
        {
            slotViews[i] = AddSkillNode(panel, "Slot" + keys[i], skillUi, pixel, font, new Vector2(95f + 130f * i, 580f), new Vector2(124f, 52f), i, true);
        }
        TextMeshProUGUI detail = AddPanelText(panel, "Detail", font, string.Empty, new Vector2(500f, 576f), new Vector2(300f, 130f), 16f, true);

        SerializedObject data = new SerializedObject(skillUi);
        Find(data, "hub").objectReferenceValue = hub;
        SetRefArray(data, "skillFrames", frames);
        SetRefArray(data, "skillFills", fills);
        SetRefArray(data, "skillLabels", labels);
        SetRefArray(data, "skillIcons", barIcons);
        SetRefArray(data, "fxIcons", icons);
        Find(data, "levelLabel").objectReferenceValue = levelLabel;
        Find(data, "xpFill").objectReferenceValue = xpFill;
        Find(data, "jobListLabel").objectReferenceValue = jobList;
        Find(data, "treePanel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        Find(data, "rootLabel").objectReferenceValue = rootLabel;
        SetRefArray(data, "branchLabels", branchLabels);
        SetRefArray(data, "branchLinks", branchLinks);
        SetRefArray(data, "nodeViews", nodeViews);
        SetRefArray(data, "nodeLinks", nodeLinks);
        SetRefArray(data, "slotViews", slotViews);
        Find(data, "detailLabel").objectReferenceValue = detail;
        data.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return skillUi;
    }

    // 패널 왼쪽 위 기준 그림(클릭을 받지 않는다)
    private static Image AddPanelImage(RectTransform panel, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(panel, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        PlaceTopLeft(image.rectTransform, position, size);
        return image;
    }

    // 부모 가운데 기준 그림(클릭을 받지 않는다)
    private static Image AddChildImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    // 스킬 트리 노드 또는 장착 칸: 선택 테두리, 바탕, 왼쪽 아이콘, 글자
    private static SkillNodeView AddSkillNode(RectTransform panel, string name, SkillUi owner, Sprite pixel, TMP_FontAsset font, Vector2 position, Vector2 size, int index, bool isSlot)
    {
        Image outline = AddPanelImage(panel, name + "Outline", pixel, position - new Vector2(4f, 4f), size + new Vector2(8f, 8f), new Color(1f, 0.85f, 0.3f, 1f));
        outline.enabled = false;

        GameObject nodeObject = new GameObject(name);
        nodeObject.transform.SetParent(panel, false);
        Image frame = nodeObject.AddComponent<Image>();
        frame.sprite = pixel;
        PlaceTopLeft(frame.rectTransform, position, size);

        float iconSize = size.y - 12f;
        Image icon = AddChildImage(nodeObject.transform, "Icon", pixel, new Vector2(-size.x * 0.5f + 6f + iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
        icon.enabled = false;

        TextMeshProUGUI label = AddText(nodeObject.transform, "Label", font, string.Empty, 18f, true);
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform, 4f);
        label.rectTransform.offsetMin = new Vector2(iconSize + 10f, 4f);

        SkillNodeView view = nodeObject.AddComponent<SkillNodeView>();
        SerializedObject viewData = new SerializedObject(view);
        Find(viewData, "owner").objectReferenceValue = owner;
        Find(viewData, "frame").objectReferenceValue = frame;
        Find(viewData, "outline").objectReferenceValue = outline;
        Find(viewData, "icon").objectReferenceValue = icon;
        Find(viewData, "label").objectReferenceValue = label;
        Find(viewData, "index").intValue = index;
        Find(viewData, "isSlot").boolValue = isSlot;
        viewData.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ------------------------------------------------------------ 스킬 이펙트
    // Tools/SkillFxGen이 만든 Assets/4.Sprite/Fx/Skill/<이름>_00.png ... 와 Icon_<이름>.png (SkillFx 순서, 0번 None은 비움)

    private const string SkillFxFolder = "Assets/4.Sprite/Fx/Skill";
    private const int SkillFxPoolSize = 24;

    private static string GetSkillFxName(int index)
    {
        return ((SkillFx)index).ToString();
    }

    private static Sprite[] LoadSkillIcons()
    {
        int count = System.Enum.GetValues(typeof(SkillFx)).Length;
        Sprite[] icons = new Sprite[count];
        for (int i = 1; i < count; i++)
        {
            icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(SkillFxFolder + "/Icon_" + GetSkillFxName(i) + ".png");
        }
        return icons;
    }

    private static SkillFxPlayer BuildSkillFx()
    {
        GameObject fxObject = new GameObject("SkillFxPlayer");
        SkillFxPlayer player = fxObject.AddComponent<SkillFxPlayer>();
        Object[] renderers = new Object[SkillFxPoolSize];
        for (int i = 0; i < SkillFxPoolSize; i++)
        {
            GameObject child = new GameObject("Fx" + i);
            child.transform.SetParent(fxObject.transform, false);
            SpriteRenderer spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.enabled = false;
            renderers[i] = spriteRenderer;
        }

        // 바닥에 깔리는 종류: 충격파, 회복, 독, 냉기, 신성(장판), 바람 자국
        SkillFx[] groundKinds = { SkillFx.Shockwave, SkillFx.Heal, SkillFx.Poison, SkillFx.Frost, SkillFx.Holy, SkillFx.Wind };
        int count = System.Enum.GetValues(typeof(SkillFx)).Length;
        SerializedObject data = new SerializedObject(player);
        SerializedProperty clips = Find(data, "clips");
        clips.arraySize = count;
        StringBuilder loaded = new StringBuilder("스킬 이펙트 프레임 수:");
        for (int i = 0; i < count; i++)
        {
            SerializedProperty clip = clips.GetArrayElementAtIndex(i);
            SerializedProperty frames = clip.FindPropertyRelative("frames");
            List<Sprite> sprites = new List<Sprite>();
            if (i > 0)
            {
                string fxName = GetSkillFxName(i);
                for (int frame = 0; frame < 32; frame++)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkillFxFolder + "/" + fxName + "_" + frame.ToString("00") + ".png");
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                }
                loaded.Append(' ').Append(fxName).Append('=').Append(sprites.Count);
            }
            frames.arraySize = sprites.Count;
            for (int frame = 0; frame < sprites.Count; frame++)
            {
                frames.GetArrayElementAtIndex(frame).objectReferenceValue = sprites[frame];
            }
            clip.FindPropertyRelative("frameRate").floatValue = 18f;
            clip.FindPropertyRelative("ground").boolValue = System.Array.IndexOf(groundKinds, (SkillFx)i) >= 0;
        }
        SetRefArray(data, "renderers", renderers);
        data.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine(loaded.ToString());
        return player;
    }

    // 아래쪽 가운데 기준 그림. position.x는 가운데에서의 거리, position.y는 아래에서의 거리(그림의 아래쪽 가운데가 기준점).
    private static Image AddBottomCenterImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    // 상점·대장간 패널. 기준 해상도 1920x1080에서 오른쪽 가운데. 크기 480x560, 화면 오른쪽에서 40.
    // 화면 좌표로는 x 1400~1880, y 290~850(위쪽 미니맵은 y 870부터). 안쪽 요소는 패널 왼쪽 위 기준 (x, 아래로 y).
    // 칸 4개는 72x72, x 간격 110(아래 가격·이름 글자 폭 100), y 58. 버튼 3개는 440x42, y 176부터 간격 50.
    private static FacilityUi BuildFacilityPanel(Transform canvas, GameHub hub)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

        GameObject rootObject = new GameObject("FacilityUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        FacilityUi facilityUi = rootObject.AddComponent<FacilityUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.78f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(1f, 0.5f);
        panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-40f, 30f);
        panel.sizeDelta = new Vector2(480f, 560f);

        TextMeshProUGUI title = AddPanelText(panel, "Title", font, "상점", new Vector2(20f, 14f), new Vector2(300f, 36f), 28f, false);
        Object[] slots = new Object[4];
        Object[] slotLabels = new Object[4];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = AddInventorySlot(panel, "Slot" + i, pixel, new Vector2(20f + 110f * i, 58f), 72f, facilityUi, false, i);
            slotLabels[i] = AddPanelText(panel, "SlotLabel" + i, font, string.Empty, new Vector2(20f + 110f * i, 134f), new Vector2(100f, 24f), 17f, false);
        }

        Object[] buttons = new Object[3];
        Object[] buttonLabels = new Object[3];
        for (int i = 0; i < buttons.Length; i++)
        {
            TextMeshProUGUI buttonLabel;
            buttons[i] = AddPanelButton(panel, "Button" + i, pixel, font, string.Empty, new Vector2(20f, 176f + 50f * i), new Vector2(440f, 42f), out buttonLabel);
            buttonLabel.fontSize = 19f;
            buttonLabels[i] = buttonLabel;
        }
        TextMeshProUGUI detail = AddPanelText(panel, "Detail", font, string.Empty, new Vector2(20f, 336f), new Vector2(440f, 208f), 18f, true);

        SerializedObject data = new SerializedObject(facilityUi);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        SetRefArray(data, "slots", slots);
        SetRefArray(data, "slotLabels", slotLabels);
        SetRefArray(data, "buttons", buttons);
        SetRefArray(data, "buttonLabels", buttonLabels);
        Find(data, "detailLabel").objectReferenceValue = detail;
        data.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return facilityUi;
    }

    // 칸 하나: 테두리(등급 색) 안에 어두운 바탕(여백 4)과 아이콘(여백 8).
    private static InventorySlotView AddInventorySlot(RectTransform panel, string name, Sprite pixel, Vector2 position, float size, SlotPanel owner, bool equipSlot, int index)
    {
        GameObject slotObject = new GameObject(name);
        slotObject.transform.SetParent(panel, false);
        Image frame = slotObject.AddComponent<Image>();
        frame.sprite = pixel;
        frame.color = new Color(1f, 1f, 1f, 0.15f);
        PlaceTopLeft(frame.rectTransform, position, new Vector2(size, size));

        Image inner = AddSlotImage(slotObject.transform, "Inner", 4f);
        inner.sprite = pixel;
        inner.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        Image icon = AddSlotImage(slotObject.transform, "Icon", 8f);
        icon.preserveAspect = true;
        icon.enabled = false;

        InventorySlotView view = slotObject.AddComponent<InventorySlotView>();
        SerializedObject data = new SerializedObject(view);
        Find(data, "owner").objectReferenceValue = owner;
        Find(data, "frame").objectReferenceValue = frame;
        Find(data, "icon").objectReferenceValue = icon;
        Find(data, "equipSlot").boolValue = equipSlot;
        Find(data, "index").intValue = index;
        data.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static Image AddSlotImage(Transform parent, string name, float padding)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
        return image;
    }

    private static Image AddBar(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, bool filled)
    {
        GameObject barObject = new GameObject(name);
        barObject.transform.SetParent(parent, false);
        Image image = barObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (filled)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
        }
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    // ------------------------------------------------------------ 검사
    // 저장된 씬을 다시 열어 핵심 참조가 실제로 남아 있는지 확인한다.

    private static bool HasRef(Object target, string property)
    {
        return Find(new SerializedObject(target), property).objectReferenceValue != null;
    }

    private static void VerifyScenes()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        MonsterDirector director = Object.FindFirstObjectByType<MonsterDirector>(FindObjectsInactive.Include);
        ProjectilePool projectilePool = Object.FindFirstObjectByType<ProjectilePool>(FindObjectsInactive.Include);
        SerializedObject directorData = new SerializedObject(director);
        bool gameOk = HasRef(director, "monsterPrefab")
            && Find(directorData, "definitions").GetArrayElementAtIndex(0).objectReferenceValue != null
            && HasRef(projectilePool, "projectilePrefab")
            && VerifyCommon();

        EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        LobbyUi lobbyUi = Object.FindFirstObjectByType<LobbyUi>(FindObjectsInactive.Include);
        bool lobbyOk = HasRef(lobbyUi, "hub") && HasRef(lobbyUi, "addressInput") && HasRef(lobbyUi, "actionButton") && VerifyCommon();

        report.AppendLine("씬 참조 검사: 전투=" + gameOk + " 로비=" + lobbyOk);
        if (!(gameOk && lobbyOk))
        {
            throw new System.InvalidOperationException("씬의 참조가 비어 있습니다.");
        }
    }

    private static bool VerifyCommon()
    {
        GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>(FindObjectsInactive.Include);
        DamageTextPool damageTextPool = Object.FindFirstObjectByType<DamageTextPool>(FindObjectsInactive.Include);
        GameHub hub = Object.FindFirstObjectByType<GameHub>(FindObjectsInactive.Include);
        return manager.playerPrefab != null && manager.transport != null && HasRef(damageTextPool, "textPrefab")
            && HasRef(hub, "hud") && HasRef(hub, "mainCamera");
    }
}
