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
    // 몬스터·투사체 발밑 그림자: 맵 소품과 같은 픽셀 그림자(32px = 1유닛, 0.69x0.28)
    private const string ShadowSpritePath = DungeonPxRoot + "/Fx_Shadow.png";
    private const string MapSpritePath = PackageRoot + "/Common/Sprites/_Sample/sample_map.png";
    private const string FontPath = PackageRoot + "/Common/Font/LINESeedSans_SDF_20_Outline.asset";
    private const string TmpPackagePath = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    private const string SpriteRoot = "Assets/4.Sprite";
    // Tools/DungeonGen/GenerateDungeon.ps1로 만든 방 그림
    private const string DungeonSpriteRoot = SpriteRoot + "/Dungeon";
    // Tools/ArtGen/px/dungeon.py로 만든 픽셀아트 방 그림(마을과 같은 32픽셀 = 1유닛)
    private const string DungeonPxRoot = SpriteRoot + "/DungeonPx";
    // 방 그림(바닥 18.4x10 + 북쪽 벽 1.4 + 동서남 벽 윗면 0.7)의 가운데 높이
    // 방 그림(634x445) 가운데의 월드 y. 위 끝은 벽 윗면(5 + 2.5625 + 0.625), 아래 끝은 남쪽 벽 윗면(-5.7).
    private const float RoomArtCenterY = 1.234375f;
    // 북쪽 벽 정면의 높이와 나머지 벽 윗면의 두께(유닛). 그림 140픽셀, 70픽셀과 같다.
    // 엔터 더 건전처럼 북쪽 벽 정면을 높게(2.56) 세우고 그 위에 어두운 벽 윗면(0.625)을 얹는다.
    private const float WallFrontHeight = 2.5625f;
    private const float WallCapHeight = 0.625f;
    private const float WallRimThickness = 0.7f;
    private const string PrefabFolder = "Assets/2.Model/Prefabs";
    private const string DataFolder = "Assets/2.Model/Data";
    private const string MonsterDataFolder = "Assets/2.Model/Data/Monster";
    private const string WeaponDataFolder = "Assets/2.Model/Data/Weapon";
    private const string ItemBalancePath = DataFolder + "/ItemBalance.asset";
    // 사용자가 클립을 넣는 에셋이라 다시 만들지 않고 빠진 칸만 채운다.
    private const string SoundLibraryPath = DataFolder + "/SoundLibrary.asset";
    // 스킬 에셋은 Job 폴더, 계열(스킬 트리) 에셋은 Class 폴더에 둔다.
    private const string JobDataFolder = "Assets/2.Model/Data/Job";
    private const string ClassDataFolder = "Assets/2.Model/Data/Class";
    private static readonly string[] ClassAssetNames = { "Warrior", "Shooter", "Mage" };
    // 직업이 10개이던 때의 직업 에셋. 초기 구성 때 지운다.
    private static readonly string[] OldJobAssetNames = { "Swordsman", "Lancer", "Archer", "Mage", "Berserker", "Destroyer", "Sniper", "Gunslinger", "Shaman", "Priest" };
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
    // 로비 마을 크기와 지형. Tools/ArtGen/px/layout.py와 맞춘다.
    private static readonly Vector2 LobbyHalfSize = new Vector2(18f, 10f);
    private const string VillageRoot = SpriteRoot + "/VillagePx";
    // 주민 의상(직접 그린 상의 Chest_*, 모자 Helmet_*). 패키지 부위와 같은 크기·피벗(100픽셀 = 1유닛, 가운데).
    private const string CostumeRoot = SpriteRoot + "/Costume";
    private const string NpcRoot = SpriteRoot + "/Npc";
    private const float VillageCliffTop = 3.9f;
    private const float VillageCliffBase = 2.8f;
    private const float VillagePlateauX = 11f;
    private const float VillageStairHalf = 1.75f;
    private static readonly Vector2 VillageGate = new Vector2(0f, 6f);
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
        EnsureFolder(ClassDataFolder);
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
        layers.GetArrayElementAtIndex(GameLayers.WorldText).stringValue = "WorldText";
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine("레이어 6 Player, 7 Monster, 8 Wall, 9 WorldText 등록");
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

        // 같은 그림에 색과 능력을 바꾼 상위 몬스터(깊은 방과 높은 층에서 나온다).
        MonsterDefinition fireSlime = CreateDefinition("FireSlime", "불꽃 슬라임", MonsterAttackKind.Melee, 0.55f, "Slime");
        SerializedObject fireData = new SerializedObject(fireSlime);
        Find(fireData, "maxHp").intValue = 46;
        Find(fireData, "moveSpeed").floatValue = 1.9f;
        Find(fireData, "bodyRadius").floatValue = 0.3f;
        Find(fireData, "shadowScale").floatValue = 0.95f;
        Find(fireData, "damage").intValue = 10;
        Find(fireData, "attackRange").floatValue = 1f;
        Find(fireData, "attackReach").floatValue = 0.65f;
        Find(fireData, "attackRadius").floatValue = 0.75f;
        Find(fireData, "attackWindup").floatValue = 0.5f;
        Find(fireData, "attackCooldown").floatValue = 1.1f;
        Find(fireData, "tint").colorValue = new Color(1f, 0.55f, 0.32f);
        Find(fireData, "hitStatus").enumValueIndex = (int)StatusKind.Burn;
        fireData.ApplyModifiedPropertiesWithoutUndo();

        MonsterDefinition frostBat = CreateDefinition("FrostBat", "서리 박쥐", MonsterAttackKind.Lunge, 0.4f, "Bat");
        SerializedObject frostData = new SerializedObject(frostBat);
        Find(frostData, "maxHp").intValue = 30;
        Find(frostData, "moveSpeed").floatValue = 3f;
        Find(frostData, "bodyRadius").floatValue = 0.24f;
        Find(frostData, "shadowScale").floatValue = 0.65f;
        Find(frostData, "damage").intValue = 8;
        Find(frostData, "attackRange").floatValue = 3.2f;
        Find(frostData, "attackReach").floatValue = 0.4f;
        Find(frostData, "attackRadius").floatValue = 0.5f;
        Find(frostData, "attackWindup").floatValue = 0.4f;
        Find(frostData, "attackCooldown").floatValue = 1.4f;
        Find(frostData, "lungeSpeed").floatValue = 10f;
        Find(frostData, "lungeDuration").floatValue = 0.3f;
        Find(frostData, "tint").colorValue = new Color(0.6f, 0.85f, 1f);
        Find(frostData, "hitStatus").enumValueIndex = (int)StatusKind.Slow;
        frostData.ApplyModifiedPropertiesWithoutUndo();

        MonsterDefinition bombShroom = CreateDefinition("BombShroom", "폭탄 버섯", MonsterAttackKind.Explode, 0.58f, "Mushroom");
        SerializedObject bombData = new SerializedObject(bombShroom);
        Find(bombData, "maxHp").intValue = 34;
        Find(bombData, "moveSpeed").floatValue = 2.2f;
        Find(bombData, "bodyRadius").floatValue = 0.28f;
        Find(bombData, "shadowScale").floatValue = 0.85f;
        Find(bombData, "damage").intValue = 18;
        Find(bombData, "attackRange").floatValue = 1.2f;
        Find(bombData, "attackReach").floatValue = 0.5f;
        Find(bombData, "attackRadius").floatValue = 1.7f;
        Find(bombData, "attackWindup").floatValue = 0.9f;
        Find(bombData, "attackCooldown").floatValue = 1f;
        Find(bombData, "tint").colorValue = new Color(1f, 0.45f, 0.45f);
        Find(bombData, "hitStatus").enumValueIndex = (int)StatusKind.Burn;
        bombData.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine("몬스터 데이터 6종 생성(슬라임, 박쥐, 독버섯, 불꽃 슬라임, 서리 박쥐, 폭탄 버섯)");
        return new[] { slime, bat, mushroom, fireSlime, frostBat, bombShroom };
    }

    private static MonsterDefinition CreateDefinition(string assetName, string displayName, MonsterAttackKind kind, float hitNormalized)
    {
        return CreateDefinition(assetName, displayName, kind, hitNormalized, assetName);
    }

    // frameSource: 그림을 가져올 몬스터(색만 바꾼 상위 몬스터는 원래 몬스터 그림을 쓴다).
    private static MonsterDefinition CreateDefinition(string assetName, string displayName, MonsterAttackKind kind, float hitNormalized, string frameSource)
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
        SetRefArray(serialized, "idleFrames", LoadFrames(frameSource, "Idle"));
        SetRefArray(serialized, "walkFrames", LoadFrames(frameSource, "Walk"));
        SetRefArray(serialized, "attackFrames", LoadFrames(frameSource, "Attack"));
        SetRefArray(serialized, "hitFrames", LoadFrames(frameSource, "Hit"));
        SetRefArray(serialized, "deadFrames", LoadFrames(frameSource, "Dead"));
        // 색·상태이상은 기본값으로 되돌려 두고, 상위 몬스터만 따로 정한다.
        Find(serialized, "tint").colorValue = Color.white;
        Find(serialized, "hitStatus").enumValueIndex = (int)StatusKind.None;
        Find(serialized, "projectileCount").intValue = 1;
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
        Find(spear, "hitDelay").floatValue = 0.11f;
        Find(spear, "duration").floatValue = 0.36f;
        Find(spear, "moveFactor").floatValue = 0.15f;
        Find(spear, "allowsShield").boolValue = false;
        Find(spear, "animStartTime").floatValue = 0.75f;
        Find(spear, "animSpeed").floatValue = 1.8f;
        Find(spear, "range").floatValue = 2.9f;
        Find(spear, "lineHalfWidth").floatValue = 0.35f;
        Find(spear, "slashScale").vector2Value = new Vector2(1.9f, 0.4f);
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
        Find(staff, "explosionRadius").floatValue = 0f;
        Find(staff, "projectileLook").enumValueIndex = (int)ProjectileLook.Orb;
        Find(staff, "projectileColor").colorValue = new Color(0.55f, 0.75f, 1f);
        Find(staff, "chargeTime").floatValue = 0f;
        // 마법사 기본 공격은 상태이상 없이 순수한 마력 구슬(화상은 화염 스킬에만)
        Find(staff, "statusKind").enumValueIndex = (int)StatusKind.None;
        Find(staff, "statusDuration").floatValue = 0f;
        Find(staff, "statusPower").floatValue = 0f;
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

        // 강화 평타(좌클릭 차지). 전사 무기는 몬스터를 꿰뚫는 검기(Power_<무기> 그림, 반지름 = 판정, reach = 날아가는 거리)를 날리고,
        // 원거리 무기는 비슷한 스킬의 이펙트 프레임을 빌려 쓴다.
        SetPower(sword, PowerAttack.Wave, 2.5f, 0.6f, 7f, 0.45f, 0.12f, 8, "Power_Sword");
        SetPower(spear, PowerAttack.Wave, 2.4f, 0.45f, 8.5f, 0.42f, 0.12f, 8, "Power_Spear");
        SetPower(axe, PowerAttack.Wave, 2.8f, 0.7f, 6f, 0.5f, 0.14f, 8, "Power_Axe");
        SetPower(blunt, PowerAttack.Wave, 2.6f, 0.7f, 6f, 0.5f, 0.14f, 8, "Power_Blunt");
        SetPowerShotSpeed(sword, 14f);
        SetPowerShotSpeed(spear, 17f);
        SetPowerShotSpeed(axe, 12f);
        SetPowerShotSpeed(blunt, 12f);
        SetPower(bow, PowerAttack.PierceShot, 1.6f, 0f, 0f, 0.3f, 0.04f, 4, null);
        SetPower(crossbow, PowerAttack.PierceShot, 2.5f, 0f, 0f, 0.4f, 0.05f, 4, "CrossPierce");
        SetPower(gun, PowerAttack.PierceShot, 2.2f, 0f, 0f, 0.35f, 0.04f, 3, null);
        SetPower(staff, PowerAttack.BigShot, 2.5f, 1.8f, 0f, 0.45f, 0.08f, 0, "Power_Staff");
        SetPower(wand, PowerAttack.BigShot, 2.3f, 1.5f, 0f, 0.4f, 0.06f, 0, "Power_Wand");
        report.AppendLine("무기 데이터 9종 생성(검, 창, 활, 지팡이, 도끼, 둔기, 석궁, 총, 완드)");
    }

    // 강화 평타 수치. vfxSkill이 null이면 강화 평타 그림 없이 평타 그림을 크게 쓴다.
    private static void SetPower(SerializedObject weapon, PowerAttack kind, float damageFactor, float radius, float reach, float duration, float hitDelay, int pierce, string vfxSkill)
    {
        Find(weapon, "powerAttack").enumValueIndex = (int)kind;
        Find(weapon, "powerChargeTime").floatValue = 0.8f;
        Find(weapon, "powerDamageFactor").floatValue = damageFactor;
        Find(weapon, "powerRadius").floatValue = radius;
        Find(weapon, "powerReach").floatValue = reach;
        Find(weapon, "powerDuration").floatValue = duration;
        Find(weapon, "powerHitDelay").floatValue = hitDelay;
        Find(weapon, "powerPierce").intValue = pierce;
        Find(weapon, "powerStunTime").floatValue = 1.2f;
        if (vfxSkill != null)
        {
            LoadSkillVfx(Find(weapon, "powerVfx"), vfxSkill);
        }
        weapon.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPowerShotSpeed(SerializedObject weapon, float speed)
    {
        Find(weapon, "powerShotSpeed").floatValue = speed;
        weapon.ApplyModifiedPropertiesWithoutUndo();
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
        LoadSkillVfx(serialized, "Weapon_" + assetName);
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
        SkillDefinition s00 = Fx(MakeArea("SwordSpin", "회전 베기", 12, 5f, 0.35f, 1.6f, 2.2f, CharacterAnim.DoubleAttack, FxRed), SkillFx.Whirl, SoundId.SkillSlash, "몸을 한 바퀴 돌려 주변을 베고 출혈을 남깁니다.");
        SkillDefinition s01 = Fx(MakeArea("SwordCross", "십자 베기", 16, 8f, 0.4f, 2.6f, 1.6f, CharacterAnim.DoubleAttack, FxWhite), SkillFx.CrossSlash, SoundId.SkillSlash, "가까운 적을 두 번 크게 베어 큰 피해를 줍니다.");
        // 차징 스킬(누르고 있다 떼기): 직업마다 하나. 같은 직업 안에서 겹치던 스킬 자리에 넣었다.
        SkillDefinition s02 = Fx(Charge(MakeDash("SwordIai", "발도 일섬", 14, 7f, 0.22f, 3.2f, 6f, 0.9f, true, CharacterAnim.JumpAttack, FxWhite), 1f, 0.35f, 0.4f), SkillFx.Wind, SoundId.SkillSlash, "[차징] 칼을 거두고 힘을 모았다가 앞으로 길게 파고들며 길목의 적을 한 번에 벱니다. 모을수록 멀리, 세게 벱니다.");
        SkillDefinition s03 = Fx(Ally(MakeBuff("SwordGuard", "수호의 함성", 18, 18f, 0.3f, JobNodeEffect.DefensePercent, 30f, 8f, FxGold), 3.5f), SkillFx.BuffGuard, SoundId.SkillBuff, "주변 아군 모두 받는 피해가 줄어듭니다.");
        SkillDefinition s04 = Fx(MakeDash("SwordCharge", "돌진 베기", 10, 6f, 0.25f, 1.3f, 4.5f, 0.8f, true, CharacterAnim.JumpAttack, FxBlue), SkillFx.Wind, SoundId.SkillDash, "조준 방향으로 무적 돌진하며 베고 기절시킵니다.");
        SkillDefinition s05 = Fx(MakeBuff("SwordRoar", "전투 함성", 20, 18f, 0.3f, JobNodeEffect.DamagePercent, 35f, 8f, FxRed), SkillFx.BuffPower, SoundId.SkillBuff, "잠시 공격력이 크게 오릅니다.");
        SetSkillStatus(s00, StatusKind.Bleed, 4f, 0.12f);
        SetSkillStatus(s02, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s04, StatusKind.Stun, 0.8f, 0f);

        // 1 창기사
        SkillDefinition s10 = Fx(MakeDash("SpearPierce", "꿰뚫기", 12, 6f, 0.28f, 1.8f, 5.5f, 0.7f, false, CharacterAnim.JumpAttack, FxYellow), SkillFx.Pierce, SoundId.SkillDash, "길게 찔러 들어가며 길목의 적에게 출혈을 남깁니다.");
        SkillDefinition s11 = Fx(MakeVolley("SpearThrow", "뇌창 투척", 16, 8f, 0.3f, 2.2f, 1, 0f, 16f, 0.8f, 0.3f, 1.4f, ProjectileLook.Arrow, FxCyan), SkillFx.Lightning, SoundId.SkillLightning, "번개를 두른 창을 던져 닿은 곳에 번개를 떨어뜨립니다.");
        SkillDefinition s12 = Fx(Arc(MakeArea("SpearSweep", "휩쓸기", 12, 5f, 0.35f, 1.3f, 2.8f, CharacterAnim.DoubleAttack, FxYellow), 200f), SkillFx.Sweep, SoundId.SkillSlash, "긴 창으로 앞쪽을 넓게 휩쓸어 적을 느리게 합니다.");
        SkillDefinition s13 = Fx(MakeBuff("SpearGale", "질풍", 18, 18f, 0.3f, JobNodeEffect.AttackSpeedPercent, 40f, 8f, FxBlue), SkillFx.BuffSpeed, SoundId.SkillBuff, "잠시 공격 속도가 크게 오릅니다.");
        SkillDefinition s14 = Fx(Slam(MakeDash("SpearLeap", "도약 찍기", 16, 9f, 0.4f, 0f, 4f, 0.5f, true, CharacterAnim.JumpAttack, FxOrange), 2.2f, 2f), SkillFx.Quake, SoundId.SkillDash, "높이 뛰어 착지한 자리를 내려찍어 기절시킵니다.");
        SkillDefinition s15 = Fx(Ground(MakeZone("SpearStorm", "낙뢰 지대", 22, 14f, 0.35f, 0.5f, 1.8f, 2.5f, 4f, 0f, FxCyan), 5f), SkillFx.Lightning, SoundId.SkillLightning, "고른 자리에 창을 꽂아 번개가 계속 떨어지게 합니다.");
        SetSkillStatus(s10, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s12, StatusKind.Slow, 3f, 0f);
        SetSkillStatus(s14, StatusKind.Stun, 0.6f, 0f);
        SetSkillStatus(s15, StatusKind.Slow, 1f, 0f);

        // 2 레인저
        SkillDefinition s20 = Fx(Pierce(Charge(MakeVolley("BowPower", "강궁", 14, 6f, 0.3f, 2.6f, 1, 0f, 22f, 0.85f, 0.35f, 0f, ProjectileLook.Arrow, FxCyan), 0.9f, 0.35f, 0.6f), 6), SkillFx.Wind, SoundId.BowRelease, "[차징] 시위를 끝까지 당겨 바람을 두른 화살을 쏩니다. 적을 꿰뚫고 날아가며, 모을수록 굵고 세집니다.");
        SkillDefinition s21 = Fx(MakeVolley("BowBlast", "폭발 화살", 20, 10f, 0.35f, 2.5f, 1, 0f, 14f, 1f, 0.2f, 2f, ProjectileLook.Arrow, FxRed), SkillFx.Fire, SoundId.SkillFire, "닿으면 터지는 화살로 주변을 불태웁니다.");
        SkillDefinition s22 = Fx(MakeDash("BowLeap", "회피 도약", 8, 5f, 0.22f, 0f, 4f, 0.5f, true, CharacterAnim.Roll, FxBlue), SkillFx.Wind, SoundId.SkillDash, "무적 상태로 빠르게 몸을 날립니다.");
        SkillDefinition s23 = Fx(MakeBuff("BowFocus", "사냥 본능", 16, 16f, 0.3f, JobNodeEffect.CritChancePercent, 25f, 8f, FxOrange), SkillFx.BuffFocus, SoundId.SkillBuff, "잠시 치명타 확률이 크게 오릅니다.");
        SkillDefinition s24 = Fx(Ground(MakeZone("BowRain", "독 화살비", 20, 12f, 0.35f, 0.45f, 2.2f, 4f, 3.5f, 0f, FxPoison), 6f), SkillFx.ArrowRain, SoundId.SkillDark, "조준한 곳에 독 화살비를 내려 적을 느리게 합니다.");
        SkillDefinition s25 = Fx(MakeHeal("BowBless", "자연의 숨결", 18, 18f, 0.4f, 1.2f, 25f, FxGreen), SkillFx.Heal, SoundId.Heal, "숨을 고르며 체력을 회복합니다.");
        SetSkillStatus(s20, StatusKind.Slow, 2f, 0f);
        SetSkillStatus(s21, StatusKind.Burn, 4f, 0.12f);
        SetSkillStatus(s24, StatusKind.Slow, 1f, 0f);

        // 3 원소술사
        SkillDefinition s30 = Fx(MakeVolley("StaffFireball", "화염구", 14, 4f, 0.3f, 2.2f, 1, 0f, 10f, 1.2f, 0.25f, 2.2f, ProjectileLook.Orb, FxRed), SkillFx.Fire, SoundId.SkillFire, "터지는 불덩이를 던져 주변을 불태웁니다.");
        SkillDefinition s31 = Fx(Charge(Ground(Delay(MakeArea("StaffMeteor", "메테오", 22, 12f, 0.5f, 3.2f, 2.8f, CharacterAnim.Skill, FxOrange), 0.55f), 6.5f), 1.2f, 0.35f, 0.5f), SkillFx.Fire, SoundId.SkillFire, "[차징] 마력을 모아 고른 자리에 운석을 떨어뜨립니다. 모을수록 큰 운석이 넓게 불태웁니다.");
        SkillDefinition s32 = Fx(MakeVolley("StaffIce", "얼음 창", 14, 5f, 0.3f, 1.2f, 3, 20f, 13f, 1f, 0.22f, 0f, ProjectileLook.Orb, FxCyan), SkillFx.Frost, SoundId.SkillIce, "얼음 창 세 개를 날려 적을 느리게 합니다.");
        SkillDefinition s33 = Fx(Ground(MakeArea("StaffNova", "서리 폭발", 18, 9f, 0.4f, 1.4f, 2.6f, CharacterAnim.Skill, FxCyan), 5.5f), SkillFx.IceNova, SoundId.SkillIce, "고른 자리를 얼려 그 주변 적을 크게 느리게 합니다.");
        SkillDefinition s34 = Fx(MakeVolley("StaffStorm", "연쇄 번개", 22, 12f, 0.4f, 1.2f, 8, 315f, 9f, 1.1f, 0.22f, 0f, ProjectileLook.Orb, FxPurple), SkillFx.Lightning, SoundId.SkillLightning, "사방으로 번개 구슬을 흩뿌려 적을 약하게 만듭니다.");
        SkillDefinition s35 = Fx(MakeDash("StaffBlink", "점멸", 10, 6f, 0.12f, 0f, 4.5f, 0.5f, true, CharacterAnim.Skill, FxPurple), SkillFx.Arcane, SoundId.SkillDark, "조준 방향으로 순간 이동합니다.");
        SetSkillStatus(s30, StatusKind.Burn, 4f, 0.15f);
        SetSkillStatus(s31, StatusKind.Burn, 4f, 0.15f);
        SetSkillStatus(s32, StatusKind.Slow, 2.5f, 0f);
        SetSkillStatus(s33, StatusKind.Slow, 4f, 0f);
        SetSkillStatus(s34, StatusKind.Weaken, 5f, 0f);

        // 4 광전사
        SkillDefinition s40 = Fx(MakeBuff("AxeRage", "피의 분노", 16, 16f, 0.3f, JobNodeEffect.DamagePercent, 45f, 6f, FxRed), SkillFx.BuffPower, SoundId.SkillBuff, "짧은 시간 공격력이 폭발적으로 오릅니다.");
        SkillDefinition s41 = Fx(MakeArea("AxeWhirl", "광폭 회전", 16, 6f, 0.45f, 1.8f, 2.4f, CharacterAnim.DoubleAttack, FxRed), SkillFx.Whirl, SoundId.SkillSlash, "도끼를 휘돌려 주변 적에게 깊은 출혈을 남깁니다.");
        SkillDefinition s42 = Fx(MakeVolley("AxeThrow", "도끼 투척", 12, 5f, 0.3f, 2f, 1, 0f, 12f, 0.8f, 0.32f, 0f, ProjectileLook.Orb, FxOrange), SkillFx.Slash, SoundId.HeavySwing, "도끼를 던져 맞은 적에게 출혈을 남깁니다.");
        SkillDefinition s43 = Fx(MakeDash("AxeExecute", "처형", 18, 9f, 0.3f, 2.8f, 2.5f, 0.8f, false, CharacterAnim.JumpAttack, FxRed), SkillFx.Cleave, SoundId.SkillSlash, "짧게 달려들어 앞의 적에게 강력한 일격을 내립니다.");
        SkillDefinition s44 = Fx(Delay(MakeArea("AxeSplit", "대지 가르기", 16, 8f, 0.6f, 2.4f, 1.8f, CharacterAnim.JumpAttack, FxOrange), 0.38f), SkillFx.Quake, SoundId.HeavySwing, "땅을 내리쳐 주변 적을 잠시 기절시킵니다.");
        SkillDefinition s45 = Fx(MakeBuff("AxeEndure", "불굴", 16, 18f, 0.3f, JobNodeEffect.DefensePercent, 35f, 6f, FxOrange), SkillFx.BuffGuard, SoundId.SkillBuff, "잠시 받는 피해가 크게 줄어듭니다.");
        SetSkillStatus(s41, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s42, StatusKind.Bleed, 4f, 0.12f);
        SetSkillStatus(s44, StatusKind.Stun, 0.6f, 0f);

        // 5 파괴자(직업은 없어지고 스킬은 전사의 세 직업에 나눠 들어간다)
        SkillDefinition s50 = Fx(Charge(Delay(MakeArea("AxeCrush", "분쇄 일격", 18, 9f, 0.6f, 3f, 3.2f, CharacterAnim.JumpAttack, FxOrange), 0.38f), 1.1f, 0.35f, 0.45f), SkillFx.Quake, SoundId.HeavySwing, "[차징] 무기를 높이 들어 힘을 모았다가 뛰어올라 땅을 깨부숩니다. 모을수록 넓고 세게 부수며 맞은 적을 기절시킵니다.");
        SkillDefinition s51 = Fx(Ground(MakeZone("BluntQuake", "지진", 22, 14f, 0.45f, 0.6f, 2.4f, 0f, 3f, 0f, FxOrange), 4.5f), SkillFx.Quake, SoundId.HeavySwing, "고른 자리에 지진을 일으켜 그 주변 적을 계속 흔들고 느리게 합니다.");
        SkillDefinition s52 = Fx(Pierce(Charge(MakeVolley("SpearDragon", "용창 투척", 18, 9f, 0.35f, 3f, 1, 0f, 20f, 0.75f, 0.45f, 0f, ProjectileLook.Arrow, FxOrange), 1f, 0.35f, 0.5f), 8), SkillFx.Pierce, SoundId.HeavySwing, "[차징] 창에 용의 불길을 모아 힘껏 던집니다. 줄지어 선 적을 모두 꿰뚫고, 모을수록 크고 세집니다.");
        SkillDefinition s53 = Fx(MakeDash("BluntRam", "파쇄 돌진", 14, 7f, 0.3f, 1.4f, 4f, 0.9f, false, CharacterAnim.JumpAttack, FxYellow), SkillFx.Wind, SoundId.SkillDash, "몸으로 밀고 들어가 길목의 적을 기절시킵니다.");
        SkillDefinition s54 = Fx(MakeBuff("BluntWall", "철벽", 18, 18f, 0.3f, JobNodeEffect.DefensePercent, 45f, 6f, FxGold), SkillFx.BuffGuard, SoundId.SkillBuff, "잠시 받는 피해가 아주 크게 줄어듭니다.");
        SkillDefinition s55 = Fx(MakeHeal("BluntVigor", "대지의 활력", 18, 20f, 0.4f, 1.2f, 25f, FxGreen), SkillFx.Heal, SoundId.Heal, "땅의 기운으로 체력을 회복합니다.");
        SetSkillStatus(s50, StatusKind.Stun, 0.8f, 0f);
        SetSkillStatus(s51, StatusKind.Slow, 1f, 0f);
        SetSkillStatus(s52, StatusKind.Burn, 3f, 0.12f);
        SetSkillStatus(s53, StatusKind.Stun, 0.6f, 0f);

        // 6 저격수
        SkillDefinition s60 = Fx(MakeVolley("CrossPierce", "관통 볼트", 14, 6f, 0.35f, 3f, 1, 0f, 24f, 1f, 0.25f, 0f, ProjectileLook.Arrow, FxWhite), SkillFx.Wind, SoundId.CrossbowShot, "아주 빠른 볼트로 큰 피해와 출혈을 줍니다.");
        SkillDefinition s61 = Fx(MakeBuff("CrossAim", "집중 조준", 16, 16f, 0.3f, JobNodeEffect.CritChancePercent, 35f, 6f, FxYellow), SkillFx.BuffFocus, SoundId.SkillBuff, "잠시 치명타 확률이 아주 크게 오릅니다.");
        SkillDefinition s62 = Fx(Pierce(Charge(MakeVolley("CrossSnipe", "저격", 16, 8f, 0.35f, 4.2f, 1, 0f, 34f, 0.6f, 0.3f, 0f, ProjectileLook.Arrow, FxRed), 1.3f, 0.25f, 0.7f), 2), SkillFx.Wind, SoundId.CrossbowShot, "[차징] 숨을 고르며 조준해 아주 빠른 볼트를 쏩니다. 다 모으면 한 발에 큰 피해를 줍니다.");
        SkillDefinition s63 = Fx(MakeVolley("CrossBomb", "폭발 볼트", 18, 9f, 0.35f, 2.2f, 1, 0f, 18f, 0.9f, 0.22f, 2f, ProjectileLook.Arrow, FxRed), SkillFx.Fire, SoundId.SkillFire, "닿으면 터지는 볼트로 주변을 불태웁니다.");
        SkillDefinition s64 = Fx(Ground(MakeZone("CrossTrap", "얼음 덫", 16, 12f, 0.3f, 0.4f, 1.6f, 3f, 6f, 0f, FxCyan), 5.5f), SkillFx.Frost, SoundId.SkillIce, "조준한 곳에 얼음 덫을 깔아 밟은 적을 느리게 합니다.");
        SkillDefinition s65 = Fx(MakeDash("CrossRoll", "후방 구르기", 8, 5f, 0.22f, 0f, 3.5f, 0.5f, true, CharacterAnim.Roll, FxBlue), SkillFx.Wind, SoundId.Roll, "무적 상태로 몸을 굴려 거리를 벌립니다.");
        SetSkillStatus(s60, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(s63, StatusKind.Burn, 4f, 0.12f);
        SetSkillStatus(s64, StatusKind.Slow, 1.5f, 0f);

        // 7 건슬링어
        SkillDefinition s70 = Fx(MakeVolley("GunSpray", "난사", 14, 6f, 0.35f, 0.7f, 8, 50f, 20f, 0.45f, 0.16f, 0f, ProjectileLook.Bullet, FxYellow), SkillFx.Fire, SoundId.GunShot, "총알 여덟 발을 넓게 흩뿌립니다.");
        SkillDefinition s71 = Fx(MakeBuff("GunRapid", "속사 태세", 16, 16f, 0.3f, JobNodeEffect.AttackSpeedPercent, 50f, 6f, FxYellow), SkillFx.BuffSpeed, SoundId.SkillBuff, "잠시 공격 속도가 아주 크게 오릅니다.");
        SkillDefinition s72 = Fx(Pierce(Charge(MakeVolley("GunRail", "레일건", 18, 9f, 0.3f, 2.8f, 1, 0f, 40f, 0.45f, 0.5f, 0f, ProjectileLook.Bullet, FxCyan), 1.1f, 0.3f, 0.5f), 8), SkillFx.Lightning, SoundId.GunShot, "[차징] 총에 전기를 모았다가 일직선으로 꿰뚫는 탄을 쏩니다. 모을수록 굵고 세집니다.");
        SkillDefinition s73 = Fx(MakeVolley("GunGrenade", "수류탄", 18, 9f, 0.35f, 2.2f, 1, 0f, 9f, 0.8f, 0.24f, 2.4f, ProjectileLook.Orb, FxOrange), SkillFx.Fire, SoundId.Explosion, "수류탄을 던져 넓게 터뜨립니다.");
        SkillDefinition s74 = Fx(Ground(MakeZone("GunPowder", "화약 지대", 18, 12f, 0.35f, 0.5f, 2f, 3f, 3f, 0f, FxRed), 5.5f), SkillFx.Fire, SoundId.SkillFire, "화약을 뿌리고 불을 붙여 그 자리를 불태웁니다.");
        SkillDefinition s75 = Fx(MakeDash("GunRoll", "구르며 쏘기", 8, 5f, 0.22f, 0f, 3.5f, 0.5f, true, CharacterAnim.Roll, FxBlue), SkillFx.Wind, SoundId.Roll, "무적 상태로 몸을 굴립니다.");
        SetSkillStatus(s72, StatusKind.Weaken, 3f, 0f);
        SetSkillStatus(s73, StatusKind.Burn, 3f, 0.12f);
        SetSkillStatus(s74, StatusKind.Burn, 2f, 0.1f);

        // 8 주술사
        SkillDefinition s80 = Fx(MakeVolley("WandCurse", "저주 탄", 12, 4f, 0.3f, 1f, 3, 25f, 11f, 0.9f, 0.2f, 0f, ProjectileLook.Orb, FxPurple), SkillFx.Arcane, SoundId.SkillDark, "저주 구슬 세 개를 날려 적을 약하게 만듭니다.");
        // 저주 폭발을 블랙홀로 바꿨다: 고른 자리에 3.5초 동안 떠서 주변 적을 끌어당기며 0.5초마다 피해와 약화를 준다.
        SkillDefinition s81 = Fx(Pull(Ground(MakeZone("WandHex", "블랙홀", 20, 12f, 0.4f, 0.3f, 2.4f, 6f, 3.5f, 0f, FxPurple), 6f), 2.6f), SkillFx.Hex, SoundId.SkillDark, "고른 자리에 블랙홀을 열어 주변 적을 끌어당기며 계속 피해를 주고 약하게 만듭니다.");
        SkillDefinition s82 = Fx(Ground(MakeZone("WandMiasma", "독 구름", 18, 12f, 0.35f, 0.45f, 2.2f, 3.5f, 5f, 0f, FxPoison), 6f), SkillFx.Poison, SoundId.SkillDark, "조준한 곳에 독 구름을 피워 적을 계속 중독시킵니다.");
        SkillDefinition s83 = Fx(Charge(MakeVolley("WandSoul", "영혼 폭발", 16, 8f, 0.3f, 2.6f, 1, 0f, 9f, 1.1f, 0.45f, 2.6f, ProjectileLook.Orb, FxPurple), 1f, 0.35f, 0.45f), SkillFx.Arcane, SoundId.SkillDark, "[차징] 떠도는 영혼을 모아 구슬로 날립니다. 닿으면 터져 주변 적을 약하게 만들고, 모을수록 크게 터집니다.");
        SkillDefinition s84 = Fx(MakeDash("WandSpirit", "혼령 질주", 12, 6f, 0.25f, 0.8f, 4f, 0.7f, true, CharacterAnim.Skill, FxPurple), SkillFx.Arcane, SoundId.SkillDash, "혼령이 되어 적을 꿰뚫고 지나갑니다.");
        SkillDefinition s85 = Fx(MakeHeal("WandDrain", "영혼 흡수", 16, 16f, 0.4f, 1.2f, 18f, FxPurple), SkillFx.Heal, SoundId.Heal, "떠도는 영혼을 흡수해 체력을 회복합니다.");
        SetSkillStatus(s80, StatusKind.Weaken, 4f, 0f);
        SetSkillStatus(s81, StatusKind.Weaken, 5f, 0f);
        SetSkillStatus(s82, StatusKind.Poison, 2f, 0.1f);
        SetSkillStatus(s83, StatusKind.Weaken, 4f, 0f);

        // 9 사제
        SkillDefinition s90 = Fx(MakeHeal("PriestHeal", "치유의 빛", 16, 8f, 0.4f, 3f, 30f, FxGreen), SkillFx.Heal, SoundId.Heal, "주변 아군 모두의 체력을 회복합니다.");
        SkillDefinition s91 = Fx(Ground(MakeZone("PriestSanctum", "성역", 24, 18f, 0.4f, 0f, 2.4f, 0f, 6f, 4f, FxGold), 4.5f), SkillFx.Holy, SoundId.SkillHoly, "고른 자리에 성역을 펼쳐 안에 있는 아군을 계속 회복합니다.");
        SkillDefinition s92 = Fx(Charge(AreaHeal(MakeArea("PriestRadiance", "성광", 18, 9f, 0.4f, 2f, 3.4f, CharacterAnim.Skill, FxGold), 12f), 1f, 0.35f, 0.45f), SkillFx.Holy, SoundId.SkillHoly, "[차징] 빛을 모았다가 몸 둘레로 터뜨립니다. 적은 불태우고 아군은 회복하며, 모을수록 넓게 퍼집니다.");
        SetSkillStatus(s92, StatusKind.Burn, 2f, 0.1f);
        SkillDefinition s93 = Fx(Ground(MakeArea("PriestJudge", "심판", 20, 10f, 0.45f, 1.6f, 2.4f, CharacterAnim.Skill, FxGold), 6f), SkillFx.Holy, SoundId.SkillHoly, "고른 자리에 빛기둥을 내려 그 주변 적을 잠시 기절시킵니다.");
        SkillDefinition s94 = Fx(Ally(MakeBuff("PriestBless", "축복", 20, 20f, 0.4f, JobNodeEffect.DamagePercent, 25f, 10f, FxGold), 4f), SkillFx.BuffPower, SoundId.SkillBuff, "주변 아군 모두의 공격력을 올립니다.");
        SkillDefinition s95 = Fx(MakeRevive("PriestRevive", "부활의 기도", 30, 40f, 0.8f, 3f, 40f, FxWhite), SkillFx.Holy, SoundId.Revive, "주변에 쓰러진 아군을 일으킵니다.");
        SetSkillStatus(s93, StatusKind.Stun, 0.5f, 0f);

        // 계열 트리 3개. 직업마다 [머리, A1~A7, B1~B6] 14칸(TreeCells 자리 순서), null인 칸은 비운다.
        // 노드는 모두 1포인트. 능력치 노드 수치는 예전 3단계짜리 한 단계의 2배다.
        DeleteOldJobAssets();
        MakeClass(0, "전사", "검·창·도끼·둔기를 쓰는 근접 계열.",
            new[] { WeaponKind.Sword, WeaponKind.Spear, WeaponKind.Axe, WeaponKind.Blunt },
            new[]
            {
                new TreeJob("검사", "검", JobAwakening.Guardian, 15f, 3f, "수호의 맹세: 받는 피해 -15%, 방의 적이 쓰러질 때마다 최대 체력의 3%를 회복합니다.",
                    new[] { Sk(s00), Sk(s01), St("날 세우기", JobNodeEffect.DamagePercent, 10f), Sk(s04), St("급소 노리기", JobNodeEffect.CritChancePercent, 8f), Sk(s05), St("가벼운 발", JobNodeEffect.MoveSpeedPercent, 8f), St("숙련", JobNodeEffect.CooldownPercent, 10f),
                        Sk(s02), St("단련", JobNodeEffect.MaxHp, 24f), Sk(s03), St("버티기", JobNodeEffect.DefensePercent, 8f), Sk(s54), Sk(s55) }),
                new TreeJob("창기사", "창", JobAwakening.Dragoon, 15f, 30f, "용기병: 이동 속도 +15%, 돌진 스킬의 재사용 시간 -30%.",
                    new[] { Sk(s10), Sk(s12), St("힘 싣기", JobNodeEffect.DamagePercent, 10f), Sk(s53), St("빠른 손", JobNodeEffect.AttackSpeedPercent, 10f), Sk(s13), St("기세", JobNodeEffect.ManaOnHit, 2f), St("발놀림", JobNodeEffect.MoveSpeedPercent, 8f),
                        Sk(s14), St("창술", JobNodeEffect.SkillDamagePercent, 16f), Sk(s11), St("용의 피", JobNodeEffect.MaxHp, 24f), Sk(s15), Sk(s52) }),
                new TreeJob("광전사", "도끼·둔기", JobAwakening.Frenzy, 35f, 0f, "광란: 체력이 절반 이하일 때 공격력 +35%.",
                    new[] { Sk(s41), Sk(s42), St("광기", JobNodeEffect.DamagePercent, 12f), Sk(s43), St("피 냄새", JobNodeEffect.ManaOnHit, 2f), Sk(s40), St("급소 노리기", JobNodeEffect.CritChancePercent, 8f), St("투지", JobNodeEffect.DefensePercent, 8f),
                        Sk(s44), St("무거운 손", JobNodeEffect.SkillDamagePercent, 16f), Sk(s50), St("질긴 몸", JobNodeEffect.MaxHp, 30f), Sk(s51), Sk(s45) }),
            });
        MakeClass(1, "사수", "활·석궁·총을 쓰는 원거리 계열.",
            new[] { WeaponKind.Bow, WeaponKind.Crossbow, WeaponKind.Gun },
            new[]
            {
                new TreeJob("레인저", "활", JobAwakening.Hunter, 15f, 10f, "바람의 사냥꾼: 이동 속도 +15%, 치명타 확률 +10%.",
                    new[] { Sk(s20), Sk(s21), St("강한 시위", JobNodeEffect.DamagePercent, 10f), Sk(s23), St("약점 간파", JobNodeEffect.CritChancePercent, 10f), St("빠른 시위", JobNodeEffect.AttackSpeedPercent, 10f), null, null,
                        Sk(s22), St("가벼운 발", JobNodeEffect.MoveSpeedPercent, 8f), Sk(s24), St("숲의 지혜", JobNodeEffect.CooldownPercent, 10f), Sk(s25), St("질긴 몸", JobNodeEffect.MaxHp, 20f) }),
                new TreeJob("저격수", "석궁", JobAwakening.Marksman, 50f, 0f, "명사수: 치명타 피해 +50%.",
                    new[] { Sk(s60), Sk(s61), St("강한 시위", JobNodeEffect.DamagePercent, 12f), Sk(s62), St("약점 간파", JobNodeEffect.CritChancePercent, 10f), St("장전 숙련", JobNodeEffect.AttackSpeedPercent, 10f), null, null,
                        Sk(s63), St("화약 다루기", JobNodeEffect.SkillDamagePercent, 16f), Sk(s64), St("가벼운 발", JobNodeEffect.MoveSpeedPercent, 8f), Sk(s65), St("숙련", JobNodeEffect.CooldownPercent, 10f) }),
                new TreeJob("건슬링어", "총", JobAwakening.Gunner, 20f, 1f, "총잡이: 공격 속도 +20%, 적중 시 마나 +1.",
                    new[] { Sk(s70), Sk(s75), St("빠른 손", JobNodeEffect.AttackSpeedPercent, 10f), Sk(s71), St("발놀림", JobNodeEffect.MoveSpeedPercent, 8f), St("탄 아끼기", JobNodeEffect.ManaOnHit, 2f), null, null,
                        Sk(s72), St("화약 다루기", JobNodeEffect.SkillDamagePercent, 16f), Sk(s73), St("넓은 폭발", JobNodeEffect.DamagePercent, 10f), Sk(s74), St("숙련", JobNodeEffect.CooldownPercent, 10f) }),
            });
        MakeClass(2, "마법사", "지팡이·완드를 쓰는 마법 계열.",
            new[] { WeaponKind.Staff, WeaponKind.Wand },
            new[]
            {
                new TreeJob("원소술사", "지팡이", JobAwakening.Elemental, 50f, 0f, "원소 지배: 스킬이 거는 상태 이상의 지속 시간 +50%.",
                    new[] { Sk(s30), Sk(s31), St("불길", JobNodeEffect.SkillDamagePercent, 16f), Sk(s34), St("마력 그릇", JobNodeEffect.MaxMana, 20f), St("집중", JobNodeEffect.SkillDamagePercent, 12f), null, null,
                        Sk(s32), St("냉정", JobNodeEffect.DefensePercent, 8f), Sk(s33), St("빠른 영창", JobNodeEffect.CooldownPercent, 10f), Sk(s35), St("마력 흡수", JobNodeEffect.ManaOnHit, 2f) }),
                new TreeJob("주술사", "완드", JobAwakening.Hexer, 5f, 0f, "저주의 군주: 방의 적이 쓰러질 때마다 마나 +5.",
                    new[] { Sk(s80), Sk(s81), St("깊은 저주", JobNodeEffect.SkillDamagePercent, 16f), Sk(s84), St("마력 그릇", JobNodeEffect.MaxMana, 20f), Sk(s85), null, null,
                        Sk(s82), St("독 다루기", JobNodeEffect.DamagePercent, 10f), Sk(s83), St("빠른 영창", JobNodeEffect.CooldownPercent, 10f), St("영혼 그릇", JobNodeEffect.MaxHp, 20f), St("마력 흡수", JobNodeEffect.ManaOnHit, 2f) }),
                new TreeJob("사제", "완드", JobAwakening.Saint, 50f, 0f, "성자: 회복·부활 효과 +50%.",
                    new[] { Sk(s90), Sk(s92), St("빛의 힘", JobNodeEffect.SkillDamagePercent, 16f), Sk(s93), St("빠른 영창", JobNodeEffect.ManaOnHit, 2f), Sk(s94), null, null,
                        Sk(s91), St("따뜻한 손", JobNodeEffect.MaxMana, 20f), Sk(s95), St("깊은 기도", JobNodeEffect.CooldownPercent, 10f), St("신앙", JobNodeEffect.MaxHp, 24f), St("인내", JobNodeEffect.DefensePercent, 8f) }),
            });
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
        Find(data, "icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SkillIconFolder + "/" + skill.name + ".png");
        LoadSkillVfx(data, skill.name);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 스킬마다 따로 그린 이펙트(Assets/4.Sprite/Fx/SkillVfx/<스킬 에셋 이름>/). 부위별 프레임(cast_00.png...)과 meta.txt의 수치를 채운다.
    private const string SkillVfxFolder = "Assets/4.Sprite/Fx/SkillVfx";
    // 스킬마다 그린 아이콘(Tools/ArtGen/SkillIcons/skillicons.mjs, 파일 이름 = 스킬 에셋 이름)
    private const string SkillIconFolder = "Assets/4.Sprite/Fx/SkillIcon";

    private static void LoadSkillVfx(SerializedObject data, string assetName)
    {
        LoadSkillVfx(Find(data, "vfx"), assetName);
    }

    private static void LoadSkillVfx(SerializedProperty vfx, string assetName)
    {
        string folder = SkillVfxFolder + "/" + assetName;
        Dictionary<string, string> meta = new Dictionary<string, string>();
        string metaPath = folder + "/meta.txt";
        if (System.IO.File.Exists(metaPath))
        {
            foreach (string line in System.IO.File.ReadAllLines(metaPath))
            {
                int equals = line.IndexOf('=');
                if (equals > 0)
                {
                    meta[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }
        }
        int total = 0;
        total += LoadVfxFrames(vfx, "cast", folder, "cast");
        total += LoadVfxFrames(vfx, "trail", folder, "trail");
        total += LoadVfxFrames(vfx, "projectile", folder, "projectile");
        total += LoadVfxFrames(vfx, "impact", folder, "impact");
        total += LoadVfxFrames(vfx, "muzzle", folder, "muzzle");
        total += LoadVfxFrames(vfx, "castIntro", folder, "intro");
        total += LoadVfxFrames(vfx, "castOutro", folder, "outro");
        vfx.FindPropertyRelative("castIntroFps").floatValue = MetaFloat(meta, "intro_fps", 20f);
        vfx.FindPropertyRelative("castOutroFps").floatValue = MetaFloat(meta, "outro_fps", 20f);
        vfx.FindPropertyRelative("projectileHidden").boolValue = MetaFloat(meta, "proj_hidden", 0f) > 0.5f;
        vfx.FindPropertyRelative("castFps").floatValue = MetaFloat(meta, "cast_fps", 20f);
        vfx.FindPropertyRelative("castGround").boolValue = MetaFloat(meta, "cast_ground", 0f) > 0.5f;
        vfx.FindPropertyRelative("castDirectional").boolValue = MetaFloat(meta, "cast_dir", 0f) > 0.5f;
        // cast_dir=2, impact_dir=2: 돌리지 않고 좌우만 뒤집는 그림
        vfx.FindPropertyRelative("castMirror").boolValue = MetaFloat(meta, "cast_dir", 0f) > 1.5f;
        vfx.FindPropertyRelative("castScale").floatValue = MetaFloat(meta, "cast_scale", 1f);
        vfx.FindPropertyRelative("castSize").floatValue = MetaFloat(meta, "cast_size", 0.9f);
        vfx.FindPropertyRelative("trailFps").floatValue = MetaFloat(meta, "trail_fps", 20f);
        vfx.FindPropertyRelative("trailWidth").floatValue = MetaFloat(meta, "trail_width", 1.4f);
        vfx.FindPropertyRelative("projectileFps").floatValue = MetaFloat(meta, "projectile_fps", 18f);
        vfx.FindPropertyRelative("projectileSize").floatValue = MetaFloat(meta, "proj_size", 1f);
        vfx.FindPropertyRelative("projectileSpin").floatValue = MetaFloat(meta, "proj_spin", 0f);
        vfx.FindPropertyRelative("impactFps").floatValue = MetaFloat(meta, "impact_fps", 20f);
        vfx.FindPropertyRelative("impactSize").floatValue = MetaFloat(meta, "impact_size", 0.8f);
        vfx.FindPropertyRelative("impactScale").floatValue = MetaFloat(meta, "impact_scale", 1f);
        vfx.FindPropertyRelative("impactGround").boolValue = MetaFloat(meta, "impact_ground", 0f) > 0.5f;
        vfx.FindPropertyRelative("impactDirectional").boolValue = MetaFloat(meta, "impact_dir", 0f) > 0.5f;
        vfx.FindPropertyRelative("impactMirror").boolValue = MetaFloat(meta, "impact_dir", 0f) > 1.5f;
        vfx.FindPropertyRelative("muzzleFps").floatValue = MetaFloat(meta, "muzzle_fps", 22f);
        vfx.FindPropertyRelative("muzzleSize").floatValue = MetaFloat(meta, "muzzle_size", 0.7f);
        vfx.FindPropertyRelative("muzzleDirectional").boolValue = MetaFloat(meta, "muzzle_dir", 0f) > 0.5f;
        report.AppendLine("스킬 이펙트 " + assetName + ": 프레임 " + total);
    }

    private static int LoadVfxFrames(SerializedProperty vfx, string field, string folder, string prefix)
    {
        List<Sprite> sprites = new List<Sprite>();
        bool more = true;
        for (int frame = 0; frame < 40 && more; frame++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/" + prefix + "_" + frame.ToString("00") + ".png");
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
            else
            {
                more = false;
            }
        }
        SerializedProperty frames = vfx.FindPropertyRelative(field);
        frames.arraySize = sprites.Count;
        for (int i = 0; i < sprites.Count; i++)
        {
            frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
        return sprites.Count;
    }

    private static float MetaFloat(Dictionary<string, string> meta, string key, float fallback)
    {
        float value = fallback;
        string text;
        if (meta.TryGetValue(key, out text))
        {
            float parsed;
            if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                value = parsed;
            }
        }
        return value;
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

    // 커서 자리에 놓는 스킬: 키를 누르고 있으면 사거리가 보이고, 떼면 커서 자리(사거리 밖이면 사거리 끝)에 놓는다.
    private static SkillDefinition Ground(SkillDefinition skill, float range)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "groundTarget").boolValue = true;
        Find(data, "range").floatValue = range;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 블랙홀: 장판 가운데로 초당 speed 유닛씩 끌어당긴다.
    // 주변 범위 공격을 조준 방향 부채꼴(전체 각도 degrees)로만 친다.
    private static SkillDefinition Arc(SkillDefinition skill, float degrees)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "arcDegrees").floatValue = degrees;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 차징 스킬: time초 동안 모으고, 하나도 모으지 않았을 때 피해는 minDamage배, 크기는 minSize배.
    private static SkillDefinition Charge(SkillDefinition skill, float time, float minDamage, float minSize)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "chargeTime").floatValue = time;
        Find(data, "chargeMinDamage").floatValue = minDamage;
        Find(data, "chargeMinSize").floatValue = minSize;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 투사체가 count마리까지 꿰뚫는다.
    private static SkillDefinition Pierce(SkillDefinition skill, int count)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "pierce").intValue = count;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 주변 범위 공격이 같은 범위의 아군을 최대 체력의 percent%만큼 회복한다.
    private static SkillDefinition AreaHeal(SkillDefinition skill, float percent)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "healPercent").floatValue = percent;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    // 주변 범위 공격을 시전 시작 delay초 뒤(뛰어올라 내려찍는 순간)에 맞힌다.
    private static SkillDefinition Delay(SkillDefinition skill, float delay)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "impactDelay").floatValue = delay;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
        return skill;
    }

    private static SkillDefinition Pull(SkillDefinition skill, float speed)
    {
        SerializedObject data = new SerializedObject(skill);
        Find(data, "pullSpeed").floatValue = speed;
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
        Find(data, "chargeTime").floatValue = 0f;
        Find(data, "pierce").intValue = 0;
        Find(data, "impactDelay").floatValue = 0f;
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

    // 트리의 칸 하나: 스킬(skill) 또는 능력치(name, effect, value)
    private class TreeEntry
    {
        public SkillDefinition skill;
        public string name;
        public JobNodeEffect effect;
        public float value;
    }

    // 계열 안의 직업 하나와 그 가지의 칸 14개(TreeCells 순서)
    private class TreeJob
    {
        public string name;
        public string weaponText;
        public JobAwakening awakening;
        public float valueA;
        public float valueB;
        public string awakeningText;
        public TreeEntry[] entries;

        public TreeJob(string name, string weaponText, JobAwakening awakening, float valueA, float valueB, string awakeningText, TreeEntry[] entries)
        {
            this.name = name;
            this.weaponText = weaponText;
            this.awakening = awakening;
            this.valueA = valueA;
            this.valueB = valueB;
            this.awakeningText = awakeningText;
            this.entries = entries;
        }
    }

    // 직업 가지 안의 칸 자리(열, 행). 머리, 왼쪽 갈래 A1~A7, 오른쪽 갈래 B1~B6 순서.
    // 머리에서 A·B로 갈라지고, A2·B2에서 다시 둘로 갈라진다.
    //            머리
    //         A1      B1
    //         A2      B2
    //       A3  A4  B3  B4
    //       A5  A6  B5  B6
    //       A7
    private static readonly Vector2[] TreeCells =
    {
        new Vector2(1.5f, 0f),
        new Vector2(0.5f, 1f), new Vector2(0.5f, 2f), new Vector2(0f, 3f), new Vector2(1f, 3f), new Vector2(0f, 4f), new Vector2(1f, 4f), new Vector2(0f, 5f),
        new Vector2(2.5f, 1f), new Vector2(2.5f, 2f), new Vector2(2f, 3f), new Vector2(3f, 3f), new Vector2(2f, 4f), new Vector2(3f, 4f)
    };
    // 각 칸의 부모 칸(-1은 직업 가지 머리라 계열 뿌리에 붙는다)
    private static readonly int[] TreeParents = { -1, 0, 1, 2, 2, 3, 4, 5, 0, 8, 9, 9, 10, 11 };

    private static TreeEntry Sk(SkillDefinition skill)
    {
        TreeEntry entry = new TreeEntry();
        entry.skill = skill;
        entry.name = skill.DisplayName;
        entry.effect = JobNodeEffect.UnlockSkill;
        return entry;
    }

    private static TreeEntry St(string name, JobNodeEffect effect, float value)
    {
        TreeEntry entry = new TreeEntry();
        entry.name = name;
        entry.effect = effect;
        entry.value = value;
        return entry;
    }

    // 직업이 10개이던 때의 직업 에셋(스크립트가 없어진 에셋)을 지운다.
    private static void DeleteOldJobAssets()
    {
        for (int i = 0; i < OldJobAssetNames.Length; i++)
        {
            string path = JobDataFolder + "/" + OldJobAssetNames[i] + ".asset";
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    // 계열 하나: 직업 가지 3개의 칸을 차례로 노드로 만들고, 스킬은 나온 순서대로 번호를 매긴다.
    private static void MakeClass(int classIndex, string displayName, string description, WeaponKind[] weaponKinds, TreeJob[] jobs)
    {
        string path = ClassDataFolder + "/" + ClassAssetNames[classIndex] + ".asset";
        ClassDefinition definition = AssetDatabase.LoadAssetAtPath<ClassDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<ClassDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }
        SerializedObject data = new SerializedObject(definition);
        Find(data, "displayName").stringValue = displayName;
        Find(data, "description").stringValue = description;
        SerializedProperty weapons = Find(data, "weaponKinds");
        weapons.arraySize = weaponKinds.Length;
        for (int i = 0; i < weaponKinds.Length; i++)
        {
            weapons.GetArrayElementAtIndex(i).enumValueIndex = (int)weaponKinds[i];
        }

        SerializedProperty branches = Find(data, "jobBranches");
        branches.arraySize = ClassDefinition.JobCount;
        List<SkillDefinition> skills = new List<SkillDefinition>();
        SerializedProperty nodes = Find(data, "nodes");
        nodes.arraySize = 0;
        for (int job = 0; job < ClassDefinition.JobCount; job++)
        {
            TreeJob spec = jobs[job];
            SerializedProperty branch = branches.GetArrayElementAtIndex(job);
            branch.FindPropertyRelative("displayName").stringValue = spec.name;
            branch.FindPropertyRelative("weaponText").stringValue = spec.weaponText;
            branch.FindPropertyRelative("awakening").enumValueIndex = (int)spec.awakening;
            branch.FindPropertyRelative("awakenValueA").floatValue = spec.valueA;
            branch.FindPropertyRelative("awakenValueB").floatValue = spec.valueB;
            branch.FindPropertyRelative("awakeningText").stringValue = spec.awakeningText;

            int[] nodeOfCell = new int[TreeCells.Length];
            for (int cell = 0; cell < TreeCells.Length; cell++)
            {
                nodeOfCell[cell] = -1;
                TreeEntry entry = spec.entries[cell];
                if (entry != null)
                {
                    int index = nodes.arraySize;
                    nodes.arraySize = index + 1;
                    nodeOfCell[cell] = index;
                    SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                    node.FindPropertyRelative("displayName").stringValue = entry.name;
                    node.FindPropertyRelative("effect").enumValueIndex = (int)entry.effect;
                    node.FindPropertyRelative("maxRank").intValue = 1;
                    node.FindPropertyRelative("job").intValue = job;
                    node.FindPropertyRelative("cell").vector2Value = TreeCells[cell] + new Vector2(job * 4f, 0f);
                    int parent = -1;
                    if (TreeParents[cell] >= 0)
                    {
                        parent = nodeOfCell[TreeParents[cell]];
                    }
                    node.FindPropertyRelative("parent").intValue = parent;
                    if (entry.skill != null)
                    {
                        node.FindPropertyRelative("value").floatValue = skills.Count;
                        skills.Add(entry.skill);
                    }
                    else
                    {
                        node.FindPropertyRelative("value").floatValue = entry.value;
                    }
                }
            }
        }
        if (nodes.arraySize > ClassDefinition.MaxNodes || skills.Count > ClassDefinition.MaxSkills)
        {
            throw new System.InvalidOperationException(displayName + " 트리가 너무 큽니다: 노드 " + nodes.arraySize + ", 스킬 " + skills.Count);
        }
        SetRefArray(data, "skills", skills.ToArray());
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
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
        SetRef(controller, "statusView", BuildStatusView(root.transform));
        SetRef(controller, "slashRenderer", slash);
        SetRef(controller, "aimRenderer", aim);
        SetRef(controller, "guardRenderer", guard);
        // 커서 자리에 놓는 스킬의 사거리 원과 놓일 자리 원(바닥 위, 본인에게만 켜진다)
        SpriteRenderer skillRange = AddSprite(root.transform, "SkillRange", LoadSprite(DungeonPxRoot + "/Fx_RangeRing.png"), -795);
        skillRange.enabled = false;
        SpriteRenderer skillTarget = AddSprite(root.transform, "SkillTarget", LoadSprite(DungeonPxRoot + "/Fx_TargetArea.png"), -794);
        skillTarget.enabled = false;
        SetRef(controller, "skillRangeRenderer", skillRange);
        SetRef(controller, "skillTargetRenderer", skillTarget);

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

        Object[] classes = new Object[ClassAssetNames.Length];
        for (int i = 0; i < classes.Length; i++)
        {
            classes[i] = LoadRequired<ClassDefinition>(ClassDataFolder + "/" + ClassAssetNames[i] + ".asset");
        }
        PlayerSkills skills = root.AddComponent<PlayerSkills>();
        SerializedObject skillsData = new SerializedObject(skills);
        Find(skillsData, "controller").objectReferenceValue = controller;
        Find(skillsData, "inventory").objectReferenceValue = inventory;
        SetRefArray(skillsData, "classes", classes);
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

    // ------------------------------------------------------------ 상태이상·강화 표시
    // Tools/ArtGen/status/status.py가 만든 Assets/4.Sprite/Fx/Status 그림(100픽셀 = 1유닛, 키 1.2 몸 기준).
    // 앞(정렬 1)과 뒤(정렬 -1) 두 묶음에 그린다. 묶음 위치가 발밑이라 캐릭터와 Y 정렬이 맞는다.
    private const string StatusFxFolder = SpriteRoot + "/Fx/Status";

    private static StatusFxView BuildStatusView(Transform parent)
    {
        GameObject root = new GameObject("StatusFx");
        root.transform.SetParent(parent, false);
        Transform front = new GameObject("Front").transform;
        front.SetParent(root.transform, false);
        front.gameObject.AddComponent<SortingGroup>().sortingOrder = 1;
        Transform back = new GameObject("Back").transform;
        back.SetParent(root.transform, false);
        back.gameObject.AddComponent<SortingGroup>().sortingOrder = -1;
        Material unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        Sprite mote = LoadSprite(StatusFxFolder + "/Particle_Mote.png");
        Sprite drop = LoadSprite(StatusFxFolder + "/Particle_Drop.png");
        Sprite snow = LoadSprite(StatusFxFolder + "/Particle_Snow.png");
        Sprite spark = LoadSprite(StatusFxFolder + "/Particle_Spark.png");

        StatusFxView view = root.AddComponent<StatusFxView>();
        SerializedObject data = new SerializedObject(view);
        SerializedProperty statuses = Find(data, "statusLayers");
        statuses.arraySize = StatusFxView.StatusCount;
        // 화상: 몸 가운데 불꽃 + 위로 튀는 불씨
        FillStatusLayer(statuses.GetArrayElementAtIndex(0), front, "Burn", 6, 12f, 0.4f, 0f, 1f, unlit,
            AddParticles(front, "Embers", mote, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.1f), 14f, 0.7f, new Vector2(0.6f, 1.2f), 0.09f, 0f, new Vector3(0.5f, 0.2f, 0f), new Vector3(0f, 0.45f, 0f), 6, unlit));
        // 출혈: 베인 자국에서 떨어지는 핏방울
        FillStatusLayer(statuses.GetArrayElementAtIndex(1), front, "Bleed", 6, 10f, 0.62f, 0f, 1f, unlit,
            AddParticles(front, "Drops", drop, new Color(0.85f, 0.12f, 0.15f), new Color(0.6f, 0.05f, 0.08f), 5f, 0.6f, new Vector2(0.1f, 0.3f), 0.12f, 0.12f, new Vector3(0.35f, 0.1f, 0f), new Vector3(0f, 0.65f, 0f), 6, unlit));
        // 둔화: 발밑 얼음 고리 + 내리는 눈
        FillStatusLayer(statuses.GetArrayElementAtIndex(2), back, "Slow", 6, 8f, 0f, 0.08f, 1f, unlit,
            AddParticles(front, "Snow", snow, new Color(0.85f, 0.97f, 1f), new Color(0.55f, 0.85f, 1f), 6f, 1.1f, new Vector2(0.05f, 0.15f), 0.1f, 0.04f, new Vector3(0.8f, 0.1f, 0f), new Vector3(0f, 1.2f, 0f), 6, unlit));
        // 약화: 가라앉는 보라 화살표 + 보라 기운
        FillStatusLayer(statuses.GetArrayElementAtIndex(3), front, "Weaken", 6, 9f, 0.55f, 0f, 0.95f, unlit,
            AddParticles(front, "Gloom", mote, new Color(0.75f, 0.45f, 1f), new Color(0.35f, 0.15f, 0.55f), 8f, 0.9f, new Vector2(0.1f, 0.35f), 0.12f, 0f, new Vector3(0.6f, 0.6f, 0f), new Vector3(0f, 0.55f, 0f), 6, unlit));
        // 기절: 머리 위를 도는 별
        FillStatusLayer(statuses.GetArrayElementAtIndex(4), front, "Stun", 8, 14f, 1f, 0.12f, 1f, unlit, null);
        // 중독: 프레임 그림 없이 몸에서 올라오는 초록 독 거품만(상태 비트 32)
        statuses.GetArrayElementAtIndex(5).FindPropertyRelative("particles").objectReferenceValue =
            AddParticles(front, "Bubbles", mote, new Color(0.6f, 1f, 0.35f), new Color(0.25f, 0.65f, 0.15f), 9f, 0.9f, new Vector2(0.2f, 0.5f), 0.11f, 0f, new Vector3(0.55f, 0.4f, 0f), new Vector3(0f, 0.45f, 0f), 6, unlit);

        SerializedProperty buffs = Find(data, "buffLayers");
        buffs.arraySize = StatusFxView.BuffCount;
        // 공격: 몸 뒤 붉은 불꽃 기운 + 불씨
        FillStatusLayer(buffs.GetArrayElementAtIndex(0), back, "AuraPower", 6, 10f, 0.5f, 0f, 0.85f, unlit,
            AddParticles(front, "PowerEmbers", mote, new Color(1f, 0.6f, 0.3f), new Color(1f, 0.25f, 0.15f), 10f, 0.8f, new Vector2(0.5f, 1f), 0.1f, 0f, new Vector3(0.7f, 0.2f, 0f), new Vector3(0f, 0.15f, 0f), 5, unlit));
        // 방어: 몸을 감싸는 황금 막 + 반짝임
        FillStatusLayer(buffs.GetArrayElementAtIndex(1), front, "AuraGuard", 6, 8f, 0.5f, 0f, 0.75f, unlit,
            AddParticles(front, "GuardSparks", spark, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.8f, 0.35f), 5f, 0.6f, new Vector2(0f, 0.2f), 0.1f, 0f, new Vector3(1f, 1f, 0f), new Vector3(0f, 0.6f, 0f), 6, unlit));
        // 속도: 발밑 바람 소용돌이 + 뒤로 흐르는 바람
        FillStatusLayer(buffs.GetArrayElementAtIndex(2), back, "AuraSpeed", 6, 14f, 0f, 0.1f, 1f, unlit,
            AddParticles(front, "Wind", mote, new Color(0.8f, 0.95f, 1f), new Color(0.45f, 0.7f, 1f), 10f, 0.4f, new Vector2(0.6f, 1.2f), 0.08f, 0f, new Vector3(0.6f, 0.8f, 0f), new Vector3(0f, 0.5f, 0f), 5, unlit));
        // 치명: 머리 위 조준선 + 반짝임
        FillStatusLayer(buffs.GetArrayElementAtIndex(3), front, "AuraFocus", 6, 10f, 1f, 0.42f, 1f, unlit,
            AddParticles(front, "FocusSparks", spark, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.6f, 0.25f), 4f, 0.6f, new Vector2(0.1f, 0.4f), 0.1f, 0f, new Vector3(0.6f, 0.6f, 0f), new Vector3(0f, 0.7f, 0f), 6, unlit));
        data.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static void FillStatusLayer(SerializedProperty layer, Transform group, string name, int frameCount, float frameRate,
        float heightRatio, float heightOffset, float alpha, Material material, ParticleSystem particles)
    {
        Sprite[] frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = LoadSprite(StatusFxFolder + "/" + name + "_" + i.ToString("00") + ".png");
        }
        SpriteRenderer renderer = AddSprite(group, name, frames[0], 0);
        renderer.sharedMaterial = material;
        renderer.color = new Color(1f, 1f, 1f, alpha);
        renderer.enabled = false;
        layer.FindPropertyRelative("renderer").objectReferenceValue = renderer;
        SerializedProperty frameList = layer.FindPropertyRelative("frames");
        frameList.arraySize = frameCount;
        for (int i = 0; i < frameCount; i++)
        {
            frameList.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
        layer.FindPropertyRelative("frameRate").floatValue = frameRate;
        layer.FindPropertyRelative("heightRatio").floatValue = heightRatio;
        layer.FindPropertyRelative("heightOffset").floatValue = heightOffset;
        layer.FindPropertyRelative("particles").objectReferenceValue = particles;
    }

    // 2D용 작은 파티클: 상자 모양에서 위(+Y)로 내보내고, 월드 공간에 남아 움직이면 꼬리처럼 흩어진다.
    // gravity가 양수면 아래로 떨어진다(핏방울, 눈).
    private static ParticleSystem AddParticles(Transform parent, string name, Sprite sprite, Color colorA, Color colorB, float rate, float lifetime,
        Vector2 speed, float size, float gravity, Vector3 box, Vector3 position, int order, Material material)
    {
        GameObject particleObject = new GameObject(name);
        particleObject.transform.SetParent(parent, false);
        particleObject.transform.localPosition = position;
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 24;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.01f, box.x), Mathf.Max(0.01f, box.y), 0.01f);
        shape.rotation = new Vector3(-90f, 0f, 0f);
        shape.randomDirectionAmount = 0.15f;
        ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)));
        ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        if (sheet.spriteCount == 0)
        {
            sheet.AddSprite(sprite);
        }
        else
        {
            sheet.SetSprite(0, sprite);
        }
        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.sortingOrder = order;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        return particles;
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
        SetRef(controller, "statusView", BuildStatusView(root.transform));
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

        // 꾸밈: 빛무리(심 뒤), 발사 섬광(맨 위), 꼬리(심에 붙음). 빛나 보이도록 조명을 받지 않는 재질을 쓴다.
        Material unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        sprite.sharedMaterial = unlit;
        SpriteRenderer glow = AddSprite(root.transform, "Glow", LoadSprite(SpriteRoot + "/Fx/ProjGlow.png"), -1);
        glow.transform.localPosition = new Vector3(0f, Projectile.SpriteHeight, 0f);
        glow.sharedMaterial = unlit;
        glow.enabled = false;
        SpriteRenderer flash = AddSprite(root.transform, "Flash", LoadSprite(SpriteRoot + "/Fx/ProjFlash.png"), 2);
        flash.sharedMaterial = unlit;
        flash.enabled = false;
        TrailRenderer trail = sprite.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = unlit;
        trail.time = 0.15f;
        trail.minVertexDistance = 0.04f;
        trail.widthMultiplier = 0.1f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trail.numCapVertices = 2;
        trail.sortingOrder = -2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = false;

        Projectile projectile = root.AddComponent<Projectile>();
        SetRef(projectile, "spriteRenderer", sprite);
        SetRef(projectile, "shadowRenderer", shadow);
        SetRef(projectile, "glowRenderer", glow);
        SetRef(projectile, "flashRenderer", flash);
        SetRef(projectile, "trail", trail);
        SetRef(projectile, "glowSprite", LoadSprite(SpriteRoot + "/Fx/ProjGlow.png"));
        SetRef(projectile, "coreSprite", LoadSprite(SpriteRoot + "/Fx/ProjCore.png"));
        SetRef(projectile, "tracerSprite", LoadSprite(SpriteRoot + "/Fx/ProjTracer.png"));
        SetRef(projectile, "flashSprite", LoadSprite(SpriteRoot + "/Fx/ProjFlash.png"));
        SetRef(projectile, "ringSprite", LoadSprite(SpriteRoot + "/Fx/ProjRing.png"));
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
            weaponTypes[i] = balance.GetWeaponPartsType((WeaponKind)i);
        }
        SerializedObject data = new SerializedObject(balance);
        // 포션 아이콘(PotionKind 순서, 0번 None은 비움)
        string potionFolder = "Assets/Layer Lab/2D Minimal-CharacterMaker/Extenstions/Parts Pack Base/Thumbnail/Sub_Item/FA_WP_Sub_Potion_";
        string[] potionFiles = { null, "001_Red", "002_Red", "001_Blue", "001_Purple", "002_Yellow", "001_Green" };
        SerializedProperty potionIcons = Find(data, "potionIcons");
        potionIcons.arraySize = potionFiles.Length;
        for (int i = 0; i < potionFiles.Length; i++)
        {
            Sprite potionIcon = null;
            if (potionFiles[i] != null)
            {
                potionIcon = LoadSprite(potionFolder + potionFiles[i] + ".png");
            }
            potionIcons.GetArrayElementAtIndex(i).objectReferenceValue = potionIcon;
        }
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
        // 투구·갑옷 계열: 그림 모양으로 나눈 목록(번호 또는 번호_색). 세 계열은 수를 같게(투구 19, 갑옷 17) 맞추고, 목록에 없으면 공용.
        string[] helmetWarrior = { "015", "021", "032", "034", "035", "036", "040" };
        string[] helmetShooter = { "001", "003", "005", "006", "008", "011", "025", "030", "031", "037" };
        string[] helmetMage = { "002", "009", "013", "016", "017", "018", "022", "025_WhiteGold", "033", "039" };
        string[] chestWarrior = { "018", "020_GreenGold", "020_Wood", "022", "023", "024", "025", "026", "027", "028", "029" };
        string[] chestShooter = { "002", "003", "004", "005", "007", "008", "009", "011", "012_Brown", "021" };
        string[] chestMage = { "006", "013", "014", "015", "016" };
        string helmetReport = WriteGearClasses(Find(data, "helmetClasses"), partsData, PartsType.Helmet, helmetCount, helmetWarrior, helmetShooter, helmetMage);
        string chestReport = WriteGearClasses(Find(data, "armorClasses"), partsData, PartsType.Chest, armorCount, chestWarrior, chestShooter, chestMage);
        // 계열 이름(예전 에셋에 3개만 남아 있을 수 있어 매번 다시 쓴다)
        string[] gearClassNames = { "전사", "사수", "마법사", "공용" };
        SerializedProperty classNames = Find(data, "gearClassNames");
        classNames.arraySize = gearClassNames.Length;
        for (int i = 0; i < gearClassNames.Length; i++)
        {
            classNames.GetArrayElementAtIndex(i).stringValue = gearClassNames[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(balance);
        counts.Append(' ').Append(shieldCount).Append(' ').Append(helmetCount).Append(' ').Append(armorCount);
        report.AppendLine(counts.ToString());
        report.AppendLine("투구 계열(전사/사수/마법사/공용): " + helmetReport + ", 갑옷 계열: " + chestReport);
        return balance;
    }

    // 파츠 번호마다 계열을 적는다. 그림 이름 "FA_Helmet_013_Purple"에서 "013_Purple"을 먼저, 없으면 "013"을 찾는다.
    private static string WriteGearClasses(SerializedProperty classes, SerializedObject partsData, PartsType type, int count, string[] warriorKeys, string[] shooterKeys, string[] mageKeys)
    {
        string[] names = ReadPartNames(partsData, type, count);
        int[] totals = new int[ItemBalance.GearClassCount];
        classes.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            string key = names[i];
            int second = key.IndexOf('_', key.IndexOf('_') + 1);
            if (second >= 0)
            {
                key = key.Substring(second + 1);
            }
            string number = key;
            if (key.IndexOf('_') >= 0)
            {
                number = key.Substring(0, key.IndexOf('_'));
            }
            // 번호_색이 목록에 있으면 그것을, 없으면 번호로 찾는다(같은 번호의 다른 색만 계열이 다를 때).
            int gearClass = FindGearClass(key, warriorKeys, shooterKeys, mageKeys);
            if (gearClass < 0)
            {
                gearClass = FindGearClass(number, warriorKeys, shooterKeys, mageKeys);
            }
            if (gearClass < 0)
            {
                gearClass = ItemBalance.GearCommon;
            }
            classes.GetArrayElementAtIndex(i).intValue = gearClass;
            totals[gearClass]++;
        }
        return totals[0] + "/" + totals[1] + "/" + totals[2] + "/" + totals[3];
    }

    private static int FindGearClass(string key, string[] warriorKeys, string[] shooterKeys, string[] mageKeys)
    {
        int gearClass = -1;
        if (System.Array.IndexOf(warriorKeys, key) >= 0)
        {
            gearClass = ItemBalance.GearWarrior;
        }
        else if (System.Array.IndexOf(shooterKeys, key) >= 0)
        {
            gearClass = ItemBalance.GearShooter;
        }
        else if (System.Array.IndexOf(mageKeys, key) >= 0)
        {
            gearClass = ItemBalance.GearMage;
        }
        return gearClass;
    }

    // 부위의 파츠 번호별 그림 이름(가장 긴 렌더러 목록 기준, 원본 텍스처 이름)
    private static string[] ReadPartNames(SerializedObject partsData, PartsType type, int count)
    {
        string[] names = new string[count];
        SerializedProperty categories = Find(partsData, "categories");
        for (int i = 0; i < categories.arraySize; i++)
        {
            SerializedProperty category = categories.GetArrayElementAtIndex(i);
            if (category.FindPropertyRelative("type").intValue == (int)type)
            {
                SerializedProperty renderers = category.FindPropertyRelative("renderers");
                for (int r = 0; r < renderers.arraySize; r++)
                {
                    SerializedProperty sprites = renderers.GetArrayElementAtIndex(r).FindPropertyRelative("sprites");
                    if (sprites.arraySize == count)
                    {
                        for (int s = 0; s < count; s++)
                        {
                            Sprite sprite = sprites.GetArrayElementAtIndex(s).objectReferenceValue as Sprite;
                            if (names[s] == null && sprite != null)
                            {
                                names[s] = sprite.texture.name;
                            }
                        }
                    }
                }
            }
        }
        for (int s = 0; s < count; s++)
        {
            if (names[s] == null)
            {
                names[s] = string.Empty;
            }
        }
        return names;
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
        root.layer = GameLayers.WorldText;
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

    // 화면 전체를 32픽셀 격자로: 장면은 작은 렌더 텍스처에 그리는 월드 카메라가 맡고,
    // 원래 카메라는 그 그림을 붙인 판과 월드 글자(WorldText 레이어)만 그린다.
    // 화면 전체가 맞추는 공통 팔레트(Endesga 32). 던전 그림 생성기(Tools/DungeonGen)도 이 색만 쓴다.
    private static readonly string[] Endesga32 =
    {
        "be4a2f", "d77643", "ead4aa", "e4a672", "b86f50", "733e39", "3e2731", "a22633",
        "e43b44", "f77622", "feae34", "fee761", "63c74d", "3e8948", "265c42", "193c3e",
        "124e89", "0099db", "2ce8f5", "ffffff", "c0cbdc", "8b9bb4", "5a6988", "3a4466",
        "262b44", "181425", "ff0044", "68386c", "b55088", "f6757a", "e8b796", "c28569"
    };

    private const string PaletteMaterialPath = "Assets/6.Materials/PixelPalette.mat";

    private static Material LoadPaletteMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath);
        if (material == null)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/6.Materials/PixelPalette.shader");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PaletteMaterialPath);
        }
        return material;
    }

    private static void BuildPixelView(Camera viewCamera)
    {
        GameObject worldObject = new GameObject("WorldCamera");
        worldObject.transform.SetParent(viewCamera.transform, false);
        Camera worldCamera = worldObject.AddComponent<Camera>();
        worldCamera.orthographic = true;
        worldCamera.orthographicSize = viewCamera.orthographicSize;
        worldCamera.clearFlags = CameraClearFlags.SolidColor;
        worldCamera.backgroundColor = viewCamera.backgroundColor;
        worldCamera.cullingMask = ~(1 << GameLayers.WorldText);
        worldCamera.depth = viewCamera.depth - 1f;
        worldCamera.nearClipPlane = viewCamera.nearClipPlane;
        worldCamera.farClipPlane = viewCamera.farClipPlane;

        GameObject quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name = "PixelScreen";
        Object.DestroyImmediate(quadObject.GetComponent<Collider>());
        quadObject.transform.SetParent(viewCamera.transform, false);
        quadObject.transform.localPosition = new Vector3(0f, 0f, 1f);
        quadObject.layer = GameLayers.WorldText;
        MeshRenderer quadRenderer = quadObject.GetComponent<MeshRenderer>();
        quadRenderer.sharedMaterial = LoadPaletteMaterial();
        quadRenderer.sortingOrder = -32000;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        viewCamera.cullingMask = 1 << GameLayers.WorldText;
        viewCamera.clearFlags = CameraClearFlags.SolidColor;
        viewCamera.backgroundColor = Color.black;

        PixelView pixelView = viewCamera.gameObject.AddComponent<PixelView>();
        SerializedObject paletteData = new SerializedObject(pixelView);
        SerializedProperty paletteColors = Find(paletteData, "palette");
        paletteColors.arraySize = Endesga32.Length;
        for (int i = 0; i < Endesga32.Length; i++)
        {
            Color color;
            ColorUtility.TryParseHtmlString("#" + Endesga32[i], out color);
            paletteColors.GetArrayElementAtIndex(i).colorValue = color;
        }
        Find(paletteData, "paletteStrength").floatValue = 1f;
        paletteData.ApplyModifiedPropertiesWithoutUndo();
        SetRef(pixelView, "viewCamera", viewCamera);
        SetRef(pixelView, "worldCamera", worldCamera);
        SetRef(pixelView, "screenQuad", quadRenderer);
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
        BuildPixelView(mainCamera);

        GameObject background = new GameObject("Background");
        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = LoadSprite(MapSpritePath);
        backgroundRenderer.sortingOrder = -1000;
        backgroundRenderer.color = backgroundTint;

        BuildWalls(ArenaHalfSize);

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
        InventoryUi inventoryUi = BuildInventoryPanel(canvasObject.transform, hub);
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

    private static void BuildWalls(Vector2 halfSize)
    {
        GameObject walls = new GameObject("Walls");
        float thickness = 1f;
        float x = halfSize.x + thickness * 0.5f;
        float y = halfSize.y + thickness * 0.5f;
        float width = halfSize.x * 2f + thickness * 2f;
        float height = halfSize.y * 2f + thickness * 2f;
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

        // 전투 씬의 캐릭터는 조명을 받게 하고 발밑 그림자를 픽셀 그림자로 바꾼다(SceneLook).
        GameObject lookObject = new GameObject("SceneLook");
        SceneLook look = lookObject.AddComponent<SceneLook>();
        SetRef(look, "characterMaterial", AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat"));
        SetRef(look, "shadowSprite", LoadSprite(ShadowSpritePath));

        string[] definitionNames = { "Slime", "Bat", "Mushroom", "FireSlime", "FrostBat", "BombShroom" };
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
        Sprite[] chestIdle = LoadFrames(DungeonPxRoot + "/Chest_Idle", 4);
        GameObject chest = BuildSpriteMarker("Chest", Vector2.zero, chestIdle[0], 0, 1.6f, "보물 상자", font);
        SpriteRenderer chestArt = chest.transform.Find("Art").GetComponent<SpriteRenderer>();
        FrameAnimator chestAnimator = chestArt.gameObject.AddComponent<FrameAnimator>();
        SetRef(chestAnimator, "spriteRenderer", chestArt);
        AddFlickerLight(chest.transform, "ChestLight", new Vector2(0f, 0.6f), new Color(1f, 0.82f, 0.4f), 2.6f, 0.7f, 0.1f);
        GameObject chestBlocker = new GameObject("Blocker");
        chestBlocker.transform.SetParent(chest.transform, false);
        chestBlocker.layer = GameLayers.Wall;
        BoxCollider2D chestBox = chestBlocker.AddComponent<BoxCollider2D>();
        chestBox.offset = new Vector2(0f, 0.32f);
        chestBox.size = new Vector2(1f, 0.45f);
        // 다음 층 포탈(보스를 잡으면 방 위쪽에 열린다. 가까이 가면 확인 창이 뜬다). 그림은 pixelfx arcane/portal.
        Sprite[] stairFrames = LoadFrames(SkillVfxFolder + "/World_Portal/impact", 16);
        GameObject stairs = BuildSpriteMarker("Stairs", new Vector2(0f, 2.2f), stairFrames[0], 0, 1.9f, "다음 층 포탈", font);
        AddLoop(stairs.transform.Find("Art").GetComponent<SpriteRenderer>(), stairFrames, 10f, 0f);
        // 스킬 그림은 100픽셀 = 1유닛으로 들어오므로, 다른 픽셀아트(32픽셀 = 1유닛)와 같은 크기로 키운다.
        stairs.transform.Find("Art").localScale = new Vector3(100f / 32f, 100f / 32f, 1f);
        AddFlickerLight(stairs.transform, "StairsLight", Vector2.zero, new Color(0.7f, 0.55f, 1f), 3.2f, 0.9f, 0.08f);
        GameObject facility = BuildSpriteMarker("Facility", new Vector2(0f, 1.6f), LoadSprite(DungeonPxRoot + "/Fac_Altar_Idle_00.png"), 0, 3f, "시설", font);
        FacilityView facilityView = BuildFacilityView(facility);

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
        Find(floorData, "facilityRenderer").objectReferenceValue = facility.GetComponentInChildren<SpriteRenderer>(true);
        // 방 종류(RoomType 순서): 시작, 전투, 엘리트, 보물, 상점, 대장간, 보스, 낚시터, 광맥, 마구간
        string[] roomArt = { "Shrine", "StoneA", "Elite", "Vault", "Market", "Forge", "Boss", "Grotto", "Mine", "Den" };
        Object[] backgrounds = new Object[roomArt.Length];
        for (int i = 0; i < roomArt.Length; i++)
        {
            backgrounds[i] = LoadSprite(DungeonPxRoot + "/Room_" + roomArt[i] + ".png");
        }
        SetRefArray(floorData, "roomBackgrounds", backgrounds);
        SetRefArray(floorData, "facilitySprites", new Object[0]);
        SetRefArray(floorData, "combatBackgrounds", new Object[]
        {
            LoadSprite(DungeonPxRoot + "/Room_StoneA.png"), LoadSprite(DungeonPxRoot + "/Room_StoneB.png"), LoadSprite(DungeonPxRoot + "/Room_StoneC.png")
        });
        Find(floorData, "facilityView").objectReferenceValue = facilityView;
        Find(floorData, "chestAnimator").objectReferenceValue = chestAnimator;
        SetSpriteArray(Find(floorData, "chestIdleFrames"), chestIdle);
        SetSpriteArray(Find(floorData, "chestOpenFrames"), LoadFrames(DungeonPxRoot + "/Chest_Open", 6));
        SetSpriteArray(Find(floorData, "chestOpenLoopFrames"), LoadFrames(DungeonPxRoot + "/Chest_Loop", 4));
        Find(floorData, "chestLabel").objectReferenceValue = chest.transform.Find("Label").GetComponent<TextMeshPro>();
        floorData.ApplyModifiedPropertiesWithoutUndo();
        chest.SetActive(false);
        stairs.SetActive(false);
        facility.SetActive(false);
        SetRef(hub, "floorDirector", floor);

        SetRef(hub, "minimap", BuildMinimap(canvasObject.transform, hub, floor, pixel));
        SetRef(hub, "confirmUi", BuildConfirmPanel(canvasObject.transform, hub, pixel, font));

        FacilityUi facilityUi = BuildFacilityPanel(canvasObject.transform, hub);
        SetRef(hub, "facilityUi", facilityUi);
        SetRef(hub, "resultUi", BuildResultPanel(canvasObject.transform));
        SetRef(canvasObject.GetComponentInChildren<InventoryUi>(true), "facilityUi", facilityUi);

        SaveAndClose(scene, previousScene);
    }


    // ------------------------------------------------------------ 던전 픽셀아트 애니메이션

    private static Sprite[] LoadFrames(string prefix, int count)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = LoadSprite(prefix + "_" + i.ToString("00") + ".png");
        }
        return frames;
    }

    private static void SetSpriteArray(SerializedProperty array, Sprite[] values)
    {
        array.arraySize = 0;
        if (values != null)
        {
            array.arraySize = values.Length;
        }
        for (int i = 0; i < array.arraySize; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static UnityEngine.Rendering.Universal.Light2D AddFlickerLight(Transform parent, string name, Vector2 localPosition, Color color, float radius, float intensity, float flicker)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        UnityEngine.Rendering.Universal.Light2D light = lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.1f;
        light.falloffIntensity = 0.7f;
        if (flicker > 0f)
        {
            LightFlicker flick = lightObject.AddComponent<LightFlicker>();
            SerializedObject flickData = new SerializedObject(flick);
            Find(flickData, "target").objectReferenceValue = light;
            Find(flickData, "amount").floatValue = flicker;
            flickData.ApplyModifiedPropertiesWithoutUndo();
        }
        return light;
    }

    private static void AddLoop(SpriteRenderer renderer, Sprite[] frames, float fps, float offset)
    {
        SpriteLoop loop = renderer.gameObject.AddComponent<SpriteLoop>();
        SerializedObject loopData = new SerializedObject(loop);
        Find(loopData, "spriteRenderer").objectReferenceValue = renderer;
        SetRefArray(loopData, "frames", frames);
        Find(loopData, "framesPerSecond").floatValue = fps;
        Find(loopData, "timeOffset").floatValue = offset;
        loopData.ApplyModifiedPropertiesWithoutUndo();
    }

    // 시설: 방 종류마다 대기/사용/다 쓴 그림, 불빛, 이름 높이(북쪽 문 이름과 겹치지 않게 시설 발밑에 쓴다). 마구간 우리에는 고를 수 있는 탈것을 세운다.
    private static FacilityView BuildFacilityView(GameObject facility)
    {
        facility.AddComponent<SortingGroup>();
        SpriteRenderer art = facility.transform.Find("Art").GetComponent<SpriteRenderer>();
        FrameAnimator artAnimator = art.gameObject.AddComponent<FrameAnimator>();
        SetRef(artAnimator, "spriteRenderer", art);
        FacilityView view = facility.AddComponent<FacilityView>();
        UnityEngine.Rendering.Universal.Light2D light = AddFlickerLight(facility.transform, "FacilityLight", Vector2.zero, Color.white, 3f, 0.8f, 0.12f);

        SpriteRenderer mount = AddSprite(facility.transform, "Mount", null, 1);
        mount.gameObject.SetActive(false);
        string mountRoot = "Assets/Layer Lab/2D Minimal-CharacterMaker/Extenstions/Parts Pack Base/Thumbnail/Mount/";
        Sprite[] mounts =
        {
            LoadSprite(mountRoot + "FA_Mount_001_Brown.png"), LoadSprite(mountRoot + "FA_Mount_001_Black.png"), LoadSprite(mountRoot + "FA_Mount_002_Blue.png")
        };
        float mountScale = 1.3f / Mathf.Max(0.01f, mounts[0].bounds.size.x);
        mount.transform.localScale = new Vector3(mountScale, mountScale, 1f);

        // 시설 몸통 충돌. 그림 기준점(아래 가운데)에서 위로 뻗는다. 종류마다 FacilityArt.blockers로 맞춘다.
        Object[] blockers = new Object[3];
        for (int i = 0; i < blockers.Length; i++)
        {
            GameObject blockerObject = new GameObject("Blocker" + i);
            blockerObject.transform.SetParent(facility.transform, false);
            blockerObject.layer = GameLayers.Wall;
            BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
            blocker.enabled = false;
            blockers[i] = blocker;
        }

        SerializedObject data = new SerializedObject(view);
        SetRefArray(data, "blockerColliders", blockers);
        Find(data, "artAnimator").objectReferenceValue = artAnimator;
        Find(data, "sortingGroup").objectReferenceValue = facility.GetComponent<SortingGroup>();
        Find(data, "label").objectReferenceValue = facility.transform.Find("Label");
        Find(data, "facilityLight").objectReferenceValue = light;
        Find(data, "lightFlicker").objectReferenceValue = light.GetComponent<LightFlicker>();
        SetSpriteArray(Find(data, "bobberFrames"), LoadFrames(DungeonPxRoot + "/Fx_Bobber", 4));
        SetSpriteArray(Find(data, "biteFrames"), LoadFrames(DungeonPxRoot + "/Fx_Bite", 4));
        SetSpriteArray(Find(data, "splashFrames"), LoadFrames(DungeonPxRoot + "/Fx_Splash", 6));
        Find(data, "mountRenderer").objectReferenceValue = mount;
        SetSpriteArray(Find(data, "mountSprites"), mounts);
        Find(data, "mountOffset").vector2Value = new Vector2(0.05f, 0.75f);

        // RoomType 순서: 시작, 전투, 엘리트, 보물, 상점, 대장간, 보스, 낚시터, 광맥, 마구간
        SerializedProperty arts = Find(data, "arts");
        arts.arraySize = 10;
        SetFacilityArt(arts.GetArrayElementAtIndex(0), "Altar", 8, 8, 0, 8f, 14f, new Color(0.75f, 0.45f, 1f), 1.1f, 4.5f, new Vector2(0f, 1.6f), false, -0.5f);
        SetFacilityArt(arts.GetArrayElementAtIndex(4), "Shop", 6, 6, 0, 6f, 12f, new Color(1f, 0.7f, 0.4f), 0.9f, 4f, new Vector2(-1.2f, 1.6f), false, -0.5f);
        SetFacilityArt(arts.GetArrayElementAtIndex(5), "Forge", 6, 8, 0, 9f, 16f, new Color(1f, 0.5f, 0.2f), 1.5f, 5f, new Vector2(0.5f, 0.8f), false, -0.5f);
        SetFacilityArt(arts.GetArrayElementAtIndex(7), "Fishing", 8, 0, 0, 5f, 10f, new Color(0.35f, 0.9f, 0.85f), 0.7f, 5f, new Vector2(0f, -0.1f), true, -2.95f);
        SetFacilityArt(arts.GetArrayElementAtIndex(8), "Ore", 6, 6, 1, 7f, 16f, new Color(0.4f, 0.9f, 1f), 1.0f, 3.6f, new Vector2(0f, 1.2f), false, -0.5f);
        SetFacilityArt(arts.GetArrayElementAtIndex(9), "Stable", 6, 0, 0, 6f, 10f, new Color(1f, 0.75f, 0.45f), 0.8f, 3.6f, new Vector2(-1.6f, 1.4f), false, -0.5f);
        // 몸통 충돌(가운데 x, y, 가로, 세로). 사람이 서서 쓰는 쪽(아래)은 비워 둔다.
        SetBlockers(arts.GetArrayElementAtIndex(0), new Vector4(0f, 0.35f, 2f, 0.6f));
        SetBlockers(arts.GetArrayElementAtIndex(4), new Vector4(0f, 0.4f, 3.6f, 0.7f));
        SetBlockers(arts.GetArrayElementAtIndex(5), new Vector4(0.1f, 0.38f, 3.3f, 0.65f));
        // 낚시터: 잔교(가운데 폭 0.9)만 걸어 들어갈 수 있고 양옆과 앞은 물이다. 기준점은 잔교 끝.
        SetBlockers(arts.GetArrayElementAtIndex(7), new Vector4(-2.05f, -0.29f, 3.2f, 3.6f), new Vector4(2.05f, -0.29f, 3.2f, 3.6f), new Vector4(0f, 0.95f, 0.9f, 1.4f));
        SetBlockers(arts.GetArrayElementAtIndex(8), new Vector4(0f, 0.5f, 2.5f, 0.8f));
        SetBlockers(arts.GetArrayElementAtIndex(9), new Vector4(0f, 0.7f, 4.1f, 1.2f));
        data.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static void SetBlockers(SerializedProperty element, params Vector4[] boxes)
    {
        SerializedProperty list = element.FindPropertyRelative("blockers");
        list.arraySize = boxes.Length;
        for (int i = 0; i < boxes.Length; i++)
        {
            list.GetArrayElementAtIndex(i).vector4Value = boxes[i];
        }
    }

    private static void SetFacilityArt(SerializedProperty element, string name, int idleCount, int actionCount, int emptyCount, float idleFps, float actionFps,
        Color lightColor, float lightIntensity, float lightRadius, Vector2 lightOffset, bool onFloor, float labelHeight)
    {
        SetSpriteArray(element.FindPropertyRelative("idle"), LoadFrames(DungeonPxRoot + "/Fac_" + name + "_Idle", idleCount));
        Sprite[] actionFrames = null;
        if (actionCount > 0)
        {
            actionFrames = LoadFrames(DungeonPxRoot + "/Fac_" + name + "_Act", actionCount);
        }
        SetSpriteArray(element.FindPropertyRelative("action"), actionFrames);
        Sprite[] emptyFrames = null;
        if (emptyCount > 0)
        {
            emptyFrames = LoadFrames(DungeonPxRoot + "/Fac_" + name + "_Empty", emptyCount);
        }
        SetSpriteArray(element.FindPropertyRelative("empty"), emptyFrames);
        element.FindPropertyRelative("idleFps").floatValue = idleFps;
        element.FindPropertyRelative("actionFps").floatValue = actionFps;
        element.FindPropertyRelative("lightColor").colorValue = lightColor;
        element.FindPropertyRelative("lightIntensity").floatValue = lightIntensity;
        element.FindPropertyRelative("lightRadius").floatValue = lightRadius;
        element.FindPropertyRelative("lightOffset").vector2Value = lightOffset;
        element.FindPropertyRelative("onFloor").boolValue = onFloor;
        element.FindPropertyRelative("labelHeight").floatValue = labelHeight;
        // 낚시터 연못은 잔교 끝(기준점에서 아래로 0.3)에 서서 양옆 물로 던지도록 그림을 내린다.
        element.FindPropertyRelative("artOffset").vector2Value = Vector2.zero;
        if (name == "Fishing")
        {
            element.FindPropertyRelative("artOffset").vector2Value = new Vector2(0f, -2.69f);
        }
    }

    // 문(direction 0 북, 1 동, 2 남, 3 서). 북쪽은 벽 정면의 아치(벽 밑동 y=5에 세움, 0.8배 = 1.76x1.6),
    // 나머지는 벽 윗면(두께 0.7)에 뚫린 문턱(2.2x0.9, 동서는 90도 돌림). 문 너머 방 이름은 방 안쪽으로 0.55 들여 쓴다.
    private static DoorView BuildDoor(string name, Vector2 position, int direction, TMP_FontAsset font)
    {
        // 문 그림은 방향마다 따로 그렸다(북쪽 창살 아치, 동·서 옆문, 남쪽 문). 서쪽은 동쪽 그림을 좌우로 뒤집는다.
        // 문 너머가 엘리트·보스 방이면 그 모양의 그림으로 바뀐다(DoorView.Show).
        GameObject root = new GameObject(name);
        root.transform.position = position;
        string prefix;
        int frameCount;
        Vector3 artPosition;
        int order = -800;
        if (direction == 0)
        {
            prefix = "/Gate_";
            frameCount = 5;
            artPosition = new Vector3(position.x, ArenaHalfSize.y, 0f);
        }
        else if (direction == 2)
        {
            prefix = "/DoorSouth_";
            frameCount = 4;
            // 남쪽 벽 윗면(캐릭터 앞에 그린다) 가운데, 그보다 위에 그린다.
            artPosition = new Vector3(position.x, -(ArenaHalfSize.y + WallRimThickness) + 0.65f, 0f);
            order = 301;
        }
        else
        {
            prefix = "/DoorSide_";
            frameCount = 4;
            float side = direction == 1 ? 1f : -1f;
            artPosition = new Vector3(side * (ArenaHalfSize.x + WallRimThickness * 0.5f), position.y, 0f);
        }
        Sprite[] normalFrames = LoadFrames(DungeonPxRoot + prefix + "Normal", frameCount);
        Sprite[] eliteFrames = LoadFrames(DungeonPxRoot + prefix + "Elite", frameCount);
        Sprite[] bossFrames = LoadFrames(DungeonPxRoot + prefix + "Boss", frameCount);
        SpriteRenderer doorRenderer = AddSprite(root.transform, "Door", normalFrames[frameCount - 1], order);
        doorRenderer.transform.position = artPosition;
        doorRenderer.flipX = direction == 3;

        Vector2 lightPosition = (Vector2)artPosition + (direction == 0 ? new Vector2(0f, 1f) : Vector2.zero);
        UnityEngine.Rendering.Universal.Light2D styleLight = AddFlickerLight(root.transform, "StyleLight", Vector2.zero, Color.red, 2.8f, 0.9f, 0.15f);
        styleLight.transform.position = lightPosition;
        styleLight.gameObject.SetActive(false);

        DoorView door = root.AddComponent<DoorView>();
        SerializedObject doorData = new SerializedObject(door);
        Find(doorData, "doorRenderer").objectReferenceValue = doorRenderer;
        Find(doorData, "openSprite").objectReferenceValue = normalFrames[frameCount - 1];
        Find(doorData, "closedSprite").objectReferenceValue = normalFrames[0];
        SetSpriteArray(Find(doorData, "frames"), normalFrames);
        SetSpriteArray(Find(doorData, "eliteFrames"), eliteFrames);
        SetSpriteArray(Find(doorData, "bossFrames"), bossFrames);
        Find(doorData, "styleLight").objectReferenceValue = styleLight;
        doorData.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    // 던전 방 그림: 픽셀아트 한 장(바닥 18.4x10, 북쪽 벽 정면 1.4, 동·서·남 벽 윗면 0.7), 북쪽 벽 횃불 4개,
    // 장애물 8칸(RoomDecor가 방마다 놓는다). 카메라는 벽까지 보이도록 범위를 넓히고 바깥은 어둡게 한다.
    private static RoomDecor BuildDungeonRoom(GameHub hub)
    {
        // 방 그림 한 장(바닥 + 벽)을 깐다. FloorDirector가 방 종류마다 그림을 바꾼다.
        SpriteRenderer floorRenderer = GameObject.Find("Background").GetComponent<SpriteRenderer>();
        floorRenderer.sprite = LoadSprite(DungeonPxRoot + "/Room_StoneA.png");
        floorRenderer.drawMode = SpriteDrawMode.Simple;
        floorRenderer.transform.localScale = Vector3.one;
        floorRenderer.transform.position = new Vector3(0f, RoomArtCenterY, 0f);

        GameObject room = new GameObject("DungeonRoom");
        float outerX = ArenaHalfSize.x + WallRimThickness;

        Sprite[] torchFrames = LoadFrames(DungeonPxRoot + "/Torch", 6);
        float[] torchX = { -6.5f, -3.2f, 3.2f, 6.5f };
        for (int i = 0; i < torchX.Length; i++)
        {
            SpriteRenderer torch = AddSprite(room.transform, "Torch" + i, torchFrames[0], -900);
            torch.transform.position = new Vector2(torchX[i], ArenaHalfSize.y + 1.3f);
            AddLoop(torch, torchFrames, 10f, i * 0.37f);
            AddFlickerLight(room.transform, "TorchLight" + i, new Vector2(torchX[i], ArenaHalfSize.y + 0.6f), new Color(1f, 0.66f, 0.36f), 4.2f, 0.75f, 0.2f);
        }
        // 남쪽 벽 윗면은 캐릭터보다 앞에 그려, 아래쪽 벽에 붙으면 발이 벽 뒤로 가려진다(입체감).
        SpriteRenderer southWall = AddSprite(room.transform, "WallSouthFront", LoadSprite(DungeonPxRoot + "/Wall_South.png"), 300);
        southWall.transform.position = new Vector2(0f, -(ArenaHalfSize.y + WallRimThickness) + 0.65f);
        // 던전은 전역 조명을 조금 어둡고 차갑게 해 불빛이 돋보이게 한다(캐릭터 그림은 영향을 받지 않는다).
        UnityEngine.Rendering.Universal.Light2D[] sceneLights = Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneLights.Length; i++)
        {
            // 만드는 중인 전투 씬의 전역 조명만 바꾼다(같이 열린 다른 씬은 건드리지 않는다).
            if (sceneLights[i].lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global && sceneLights[i].gameObject.scene == room.scene)
            {
                sceneLights[i].color = new Color(0.88f, 0.9f, 1f);
                sceneLights[i].intensity = 0.9f;
            }
        }

        // 장애물 칸: 그림은 캐릭터와 같은 정렬 순서(0)라 Y 위치로 앞뒤가 정해진다.
        GameObject decorObject = new GameObject("RoomDecor");
        decorObject.transform.SetParent(room.transform, false);
        RoomDecor decor = decorObject.AddComponent<RoomDecor>();
        Object[] slots = new Object[8];
        Object[] colliders = new Object[slots.Length];
        Sprite rock = LoadSprite(DungeonPxRoot + "/Prop_Rock.png");
        Object[] slotLights = new Object[slots.Length];
        Object[] slotAnimators = new Object[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            SpriteRenderer slot = AddSprite(decorObject.transform, "Obstacle" + i, rock, 0);
            slot.gameObject.layer = GameLayers.Wall;
            BoxCollider2D box = slot.gameObject.AddComponent<BoxCollider2D>();
            FrameAnimator slotAnimator = slot.gameObject.AddComponent<FrameAnimator>();
            SetRef(slotAnimator, "spriteRenderer", slot);
            slotAnimator.enabled = false;
            UnityEngine.Rendering.Universal.Light2D slotLight = AddFlickerLight(slot.transform, "BrazierLight", new Vector2(0f, 0.8f), new Color(1f, 0.6f, 0.3f), 3.6f, 0.85f, 0.22f);
            slotLight.gameObject.SetActive(false);
            slot.gameObject.SetActive(false);
            slots[i] = slot;
            colliders[i] = box;
            slotLights[i] = slotLight.gameObject;
            slotAnimators[i] = slotAnimator;
        }
        Sprite[] brazierFrames = LoadFrames(DungeonPxRoot + "/Prop_Brazier", 6);
        Object[] kinds =
        {
            LoadSprite(DungeonPxRoot + "/Prop_Pillar.png"), rock,
            LoadSprite(DungeonPxRoot + "/Prop_Crate.png"), LoadSprite(DungeonPxRoot + "/Prop_Barrel.png"),
            LoadSprite(DungeonPxRoot + "/Prop_Statue.png"), LoadSprite(DungeonPxRoot + "/Prop_Urn.png"),
            LoadSprite(DungeonPxRoot + "/Prop_PillarBroken.png"), brazierFrames[0]
        };
        SerializedObject decorData = new SerializedObject(decor);
        SetRefArray(decorData, "slots", slots);
        SetRefArray(decorData, "slotColliders", colliders);
        SetRefArray(decorData, "kindSprites", kinds);
        SetSpriteArray(Find(decorData, "brazierFrames"), brazierFrames);
        SetRefArray(decorData, "slotLights", slotLights);
        SetRefArray(decorData, "slotAnimators", slotAnimators);
        decorData.ApplyModifiedPropertiesWithoutUndo();

        Camera camera = (Camera)new SerializedObject(hub).FindProperty("mainCamera").objectReferenceValue;
        camera.backgroundColor = new Color(0.07f, 0.06f, 0.08f);
        SerializedObject followData = new SerializedObject(camera.GetComponent<CameraFollow>());
        float mapTop = ArenaHalfSize.y + WallFrontHeight + WallCapHeight;
        float mapBottom = -(ArenaHalfSize.y + WallRimThickness);
        Find(followData, "mapHalfSize").vector2Value = new Vector2(outerX, (mapTop - mapBottom) * 0.5f);
        Find(followData, "mapCenter").vector2Value = new Vector2(0f, (mapTop + mapBottom) * 0.5f);
        followData.ApplyModifiedPropertiesWithoutUndo();
        return decor;
    }

    // 그림 표시(보물 상자, 계단, 시설). 그림은 발밑 피벗(계단은 가운데), 이름은 그림 위에 쓴다.
    private static GameObject BuildSpriteMarker(string name, Vector2 position, Sprite sprite, int order, float labelHeight, string text, TMP_FontAsset font)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        AddSprite(root.transform, "Art", sprite, order);
        AddWorldLabel(root.transform, "Label", font, text, new Vector2(0f, labelHeight), 3f);
        return root;
    }

    private static TextMeshPro AddWorldLabel(Transform parent, string name, TMP_FontAsset font, string text, Vector2 localPosition, float fontSize)
    {
        GameObject labelObject = new GameObject(name);
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;
        labelObject.layer = GameLayers.WorldText;
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
    private static MinimapUi BuildMinimap(Transform canvas, GameHub hub, FloorDirector floor, Sprite pixel)
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
        Object[] marks = new Object[grid * grid];
        Object[] icons = new Object[grid * grid];
        TMP_FontAsset markFont = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                Image cell = AddMinimapImage(panel, "Cell" + x + "_" + y, pixel, cellSize);
                cell.rectTransform.anchoredPosition = new Vector2(first + step * x, first + step * y);
                cell.enabled = false;
                cells[y * grid + x] = cell;

                // 방 종류 아이콘(12픽셀 그림을 2배로)
                GameObject iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(cell.transform, false);
                Image icon = iconObject.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
                icon.enabled = false;
                icons[y * grid + x] = icon;

                GameObject markObject = new GameObject("Mark");
                markObject.transform.SetParent(cell.transform, false);
                TextMeshProUGUI mark = markObject.AddComponent<TextMeshProUGUI>();
                mark.font = markFont;
                mark.fontSize = 22f;
                mark.alignment = TextAlignmentOptions.Center;
                mark.textWrappingMode = TextWrappingModes.NoWrap;
                mark.raycastTarget = false;
                mark.rectTransform.anchorMin = Vector2.zero;
                mark.rectTransform.anchorMax = Vector2.one;
                mark.rectTransform.offsetMin = Vector2.zero;
                mark.rectTransform.offsetMax = Vector2.zero;
                mark.enabled = false;
                marks[y * grid + x] = mark;
            }
        }

        // 큰 지도일 때만 보이는 안내 글(지도 아래). 지도가 3배로 커지므로 글자는 작게 둔다.
        GameObject hintObject = new GameObject("Hint");
        hintObject.transform.SetParent(panel, false);
        TextMeshProUGUI hint = hintObject.AddComponent<TextMeshProUGUI>();
        hint.font = markFont;
        hint.fontSize = 8f;
        hint.alignment = TextAlignmentOptions.Center;
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        hint.raycastTarget = false;
        hint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -3f);
        hint.rectTransform.sizeDelta = new Vector2(250f, 12f);

        Object[] roomIcons = new Object[10];
        for (int i = 0; i < roomIcons.Length; i++)
        {
            roomIcons[i] = LoadSprite(DungeonPxRoot + "/Icon_Room_" + i + ".png");
        }

        MinimapUi minimap = panelObject.AddComponent<MinimapUi>();
        SerializedObject data = new SerializedObject(minimap);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "floor").objectReferenceValue = floor;
        SetRefArray(data, "cells", cells);
        SetRefArray(data, "marks", marks);
        SetRefArray(data, "icons", icons);
        SetRefArray(data, "roomIcons", roomIcons);
        Find(data, "hintLabel").objectReferenceValue = hint;
        SetRefArray(data, "horizontalLinks", horizontalLinks);
        SetRefArray(data, "verticalLinks", verticalLinks);
        Find(data, "currentMarker").objectReferenceValue = marker.rectTransform;
        data.ApplyModifiedPropertiesWithoutUndo();
        return minimap;
    }

    // 예/아니오 확인 창: 화면 가운데 아래쪽(캐릭터를 가리지 않게) 520x210. 글(위), 예·아니오 버튼(아래, 폭 220).
    private static ConfirmUi BuildConfirmPanel(Transform canvas, GameHub hub, Sprite pixel, TMP_FontAsset font)
    {
        GameObject rootObject = new GameObject("ConfirmUi");
        rootObject.transform.SetParent(canvas, false);
        RectTransform root = rootObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        ConfirmUi confirm = rootObject.AddComponent<ConfirmUi>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0.07f, 0.07f, 0.1f, 1f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, -230f);
        panel.sizeDelta = new Vector2(520f, 210f);

        TextMeshProUGUI message = AddPanelText(panel, "Message", font, string.Empty, new Vector2(20f, 20f), new Vector2(480f, 100f), 28f, false);
        message.alignment = TextAlignmentOptions.Center;
        message.richText = true;
        TextMeshProUGUI yesLabel;
        Button yes = AddPanelButton(panel, "Yes", pixel, font, "예", new Vector2(30f, 134f), new Vector2(220f, 56f), out yesLabel);
        TextMeshProUGUI noLabel;
        Button no = AddPanelButton(panel, "No", pixel, font, "취소  (Esc)", new Vector2(270f, 134f), new Vector2(220f, 56f), out noLabel);
        no.image.color = new Color(0.4f, 0.4f, 0.45f);

        SerializedObject data = new SerializedObject(confirm);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "messageLabel").objectReferenceValue = message;
        Find(data, "yesButton").objectReferenceValue = yes;
        Find(data, "yesLabel").objectReferenceValue = yesLabel;
        Find(data, "noButton").objectReferenceValue = no;
        data.ApplyModifiedPropertiesWithoutUndo();
        return confirm;
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

        // 로비는 전투 방보다 넓은 마을(36x20)이라 바깥 벽, 카메라 범위, 시작 위치를 다시 잡는다.
        Object.DestroyImmediate(GameObject.Find("Walls"));
        BuildWalls(LobbyHalfSize);
        SerializedObject followData = new SerializedObject(hub.CameraFollow);
        Find(followData, "mapHalfSize").vector2Value = LobbyHalfSize;
        followData.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject lobbyHubData = new SerializedObject(hub);
        Find(lobbyHubData, "arenaHalfSize").vector2Value = LobbyHalfSize;
        SerializedProperty lobbySpawns = Find(lobbyHubData, "spawnPoints");
        for (int i = 0; i < lobbySpawns.arraySize; i++)
        {
            lobbySpawns.GetArrayElementAtIndex(i).vector2Value = new Vector2(-1.5f + i, -2.9f);
        }
        lobbyHubData.ApplyModifiedPropertiesWithoutUndo();

        Vector2[] stationPositions = BuildVillage();

        // 출발 지점: 언덕 위 탑 입구 앞의 옅은 빛
        Vector2 zoneCenter = new Vector2(0f, 4.95f);
        float zoneRadius = 0.85f;
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
        zoneLabelObject.layer = GameLayers.WorldText;
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
        SetRef(hub, "confirmUi", BuildConfirmPanel(canvasObject.transform, hub, LoadSprite(SpriteRoot + "/Fx/Pixel.png"), LoadRequired<TMP_FontAsset>(KoreanFontAssetPath)));
        SetRef(hub, "customizeUi", BuildCustomizePanel(canvasObject.transform, hub));

        SaveAndClose(scene, previousScene);
    }

    // ------------------------------------------------------------ 로비 마을(픽셀아트)
    // 36x20 마을(카메라가 따라감). 북쪽 언덕(높이 단차, 가운데 돌계단) 위에 탑 입구·풍차·밀밭·텃밭·모닥불,
    // 가운데 분수 광장과 노점, 서쪽 해금 상점·대장간·집, 동쪽 도서관·여관·집, 남동쪽 미용실, 남서쪽 연못.
    // 그림은 Tools/ArtGen/px(32픽셀 = 1유닛, 점 필터)로 만들고, 배치는 같은 생성기가 만든 VillageLayout.cs를 따른다.
    // 주민은 플레이어와 같은 패키지 캐릭터에 역할별 의상(4.Sprite/Costume)을 입힌다.
    // 시설(해금 상점, 도서관, 미용실)은 주인 NPC 자리를 돌려준다(그 근처에서 F).
    private static Vector2[] BuildVillage()
    {
        Object.DestroyImmediate(GameObject.Find("Background"));
        GameObject village = new GameObject("Village");
        Transform root = village.transform;

        SpriteRenderer ground = AddSprite(root, "Ground", LoadSprite(VillageRoot + "/Ground_Px.png"), -1000);
        ground.transform.position = Vector3.zero;

        // 언덕 가장자리(계단 자리만 비운다)와 언덕 옆면, 연못과 개울, 탑 입구는 못 지나간다.
        Transform walls = new GameObject("VillageWalls").transform;
        walls.SetParent(root, false);
        float cliffMiddle = (VillageCliffTop + VillageCliffBase) * 0.5f;
        float cliffHeight = VillageCliffTop - VillageCliffBase + 0.1f;
        float cliffWidth = VillagePlateauX + 0.2f - VillageStairHalf;
        float cliffCenter = (VillagePlateauX + 0.2f + VillageStairHalf) * 0.5f;
        AddWall(walls, "CliffLeft", new Vector2(-cliffCenter, cliffMiddle), new Vector2(cliffWidth, cliffHeight));
        AddWall(walls, "CliffRight", new Vector2(cliffCenter, cliffMiddle), new Vector2(cliffWidth, cliffHeight));
        float sideHeight = LobbyHalfSize.y - VillageCliffBase;
        AddWall(walls, "TerraceSideLeft", new Vector2(-VillagePlateauX - 0.1f, VillageCliffBase + sideHeight * 0.5f), new Vector2(0.3f, sideHeight));
        AddWall(walls, "TerraceSideRight", new Vector2(VillagePlateauX + 0.1f, VillageCliffBase + sideHeight * 0.5f), new Vector2(0.3f, sideHeight));
        AddWall(walls, "Pond", new Vector2(-14.2f, -8.1f), new Vector2(5.6f, 2.3f));
        AddWall(walls, "Stream", new Vector2(-17.3f, -7.0f), new Vector2(2.4f, 0.8f));
        AddWall(walls, "Gate", new Vector2(VillageGate.x, VillageGate.y + 2.25f), new Vector2(7.2f, 4.4f));

        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        Color lampColor = new Color(1f, 0.76f, 0.48f);
        Color windowColor = new Color(1f, 0.82f, 0.55f);
        Color fireColor = new Color(1f, 0.6f, 0.3f);

        Dictionary<string, SpriteRenderer> placed = new Dictionary<string, SpriteRenderer>();
        VillageLayout.Item[] items = VillageLayout.Items;
        for (int i = 0; i < items.Length; i++)
        {
            VillageLayout.Item item = items[i];
            SpriteRenderer renderer = AddSprite(root, item.name, LoadSprite(VillageRoot + "/" + item.file + ".png"), 0);
            renderer.transform.position = new Vector2(item.x, item.y);
            if (item.kind == "fence")
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(item.width, renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit);
            }
            if (item.hasCollider)
            {
                renderer.gameObject.layer = GameLayers.Wall;
                BoxCollider2D box = renderer.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(item.cw, item.ch);
                box.offset = new Vector2(0f, item.ch * 0.35f);
            }
            if (item.hasFade)
            {
                AddOcclusionFade(renderer, new Vector2(item.fx0, item.fy0), new Vector2(item.fx1, item.fy1));
            }
            if (item.loops != null)
            {
                for (int k = 0; k < item.loops.Length; k++)
                {
                    VillageLayout.Loop loop = item.loops[k];
                    AddPropLoop(renderer, loop.prefix, loop.frames, new Vector2(loop.px, loop.py), loop.fps, loop.offset);
                }
            }
            if (item.light == "lamp")
            {
                AddPointLight(root, item.name + "Light", (Vector2)renderer.transform.position + new Vector2(item.lx, item.ly), lampColor, 3f, 0.8f);
            }
            else if (item.light == "fire")
            {
                AddPointLight(root, item.name + "Light", PropPixelToWorld(renderer, new Vector2(item.lx, item.ly)), fireColor, 2.8f, 1f);
            }
            else if (item.light == "portal")
            {
                AddPointLight(root, item.name + "Light", PropPixelToWorld(renderer, new Vector2(item.lx, item.ly)), new Color(0.78f, 0.55f, 1f), 3.6f, 1f);
                AddPointLight(root, "TorchLightL", PropPixelToWorld(renderer, new Vector2(66f, 106f)), fireColor, 2.4f, 0.9f);
                AddPointLight(root, "TorchLightR", PropPixelToWorld(renderer, new Vector2(166f, 106f)), fireColor, 2.4f, 0.9f);
            }
            if (item.kind == "building")
            {
                // 창문 불빛(앞쪽 바닥을 따뜻하게)
                AddPointLight(root, item.name + "Light", new Vector2(item.x, item.y + 0.9f), windowColor, 2.6f, 0.45f);
            }
            placed[item.name] = renderer;
        }

        // 대장간 화로 불빛
        AddPointLight(root, "ForgeLight", (Vector2)placed["Smithy"].transform.position + new Vector2(0.69f, 0.4f), fireColor, 2.4f, 1.1f);

        // 지붕 위 시설 이름(주인에게 가까이 가면 화면 아래에 F 안내가 나온다)
        AddRoofLabel(root, font, placed["Shop"], "해금 상점", 3f);
        AddRoofLabel(root, font, placed["Library"], "도서관", 3f);
        AddRoofLabel(root, font, placed["Barber"], "미용실", 3f);
        AddRoofLabel(root, font, placed["Tavern"], "여관", 2.6f);
        AddRoofLabel(root, font, placed["Smithy"], "대장간", 2.6f);

        // 주민
        Vector2 shopKeeper = Vector2.zero;
        Vector2 librarian = Vector2.zero;
        Vector2 barberKeeper = Vector2.zero;
        Dictionary<string, int> chestIndices;
        Dictionary<string, int> helmetIndices;
        GameObject template = BuildNpcTemplate(out chestIndices, out helmetIndices);
        SerializedObject partsData = new SerializedObject(template.GetComponent<PartsManager>());
        VillageLayout.Npc[] npcs = VillageLayout.Npcs;
        for (int i = 0; i < npcs.Length; i++)
        {
            AddVillageNpc(root, template, partsData, npcs[i], chestIndices, helmetIndices);
            Vector2 position = new Vector2(npcs[i].x, npcs[i].y);
            if (npcs[i].name == "Merchant")
            {
                shopKeeper = position;
            }
            else if (npcs[i].name == "Librarian")
            {
                librarian = position;
            }
            else if (npcs[i].name == "Barber")
            {
                barberKeeper = position;
            }
        }
        // 전직 교관: 로비에서 시작 계열을 바꾸는 NPC(던전 시작 방의 전직 제단을 대신한다). 배치 생성기 밖에서 직접 놓는다.
        VillageLayout.Npc trainer = new VillageLayout.Npc
        {
            name = "Trainer", costume = "guard", x = 4.2f, y = 2.0f, faceLeft = true, itemType = "Spear", itemSprite = "FA_WP_Main_Spear_008_GrayGold",
            hair = 2, eye = 7, beard = 1, hairColor = 0, skinColor = 3, scale = 1f, speed = 0f, work = false, waypoints = null,
            lines = new[] { "어떤 길을 걷겠나?", "전사, 사수, 마법사... 골라 보게.", "계열은 언제든 바꿀 수 있네." }
        };
        AddVillageNpc(root, template, partsData, trainer, chestIndices, helmetIndices);
        Vector2 trainerPosition = new Vector2(trainer.x, trainer.y);
        AddWorldLabel(root, "TrainerName", font, "전직 교관", trainerPosition + new Vector2(0f, 2.1f), 3f);
        Object.DestroyImmediate(template);

        SetupDuskLight();

        Vector2[] stations = { shopKeeper, librarian, barberKeeper, trainerPosition };
        return stations;
    }

    private static void AddRoofLabel(Transform root, TMP_FontAsset font, SpriteRenderer building, string text, float fontSize)
    {
        Bounds bounds = building.bounds;
        AddWorldLabel(root, building.name + "Name", font, text, new Vector2(bounds.center.x, bounds.max.y + 0.15f), fontSize);
    }

    private static void AddOcclusionFade(SpriteRenderer renderer, Vector2 areaMin, Vector2 areaMax)
    {
        OcclusionFade fade = renderer.gameObject.AddComponent<OcclusionFade>();
        SerializedObject data = new SerializedObject(fade);
        SetRefArray(data, "renderers", new Object[] { renderer });
        Find(data, "areaMin").vector2Value = areaMin;
        Find(data, "areaMax").vector2Value = areaMax;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // 소품 그림의 픽셀 좌표(왼쪽 위 기준)를 월드 좌표로 바꾼다.
    private static Vector2 PropPixelToWorld(SpriteRenderer prop, Vector2 pixel)
    {
        Sprite sprite = prop.sprite;
        float unit = sprite.pixelsPerUnit;
        Vector2 local = new Vector2((pixel.x - sprite.pivot.x) / unit, (sprite.rect.height - pixel.y - sprite.pivot.y) / unit);
        return (Vector2)prop.transform.position + local;
    }

    // 주민 원본: 데모 캐릭터를 한 번 복제해 직접 그린 의상(상의, 모자)을 부위 목록 끝에 덧붙인다.
    // 의상 이름(Chest_merchant의 merchant)마다 덧붙인 번호를 돌려준다. 주민은 이 원본을 복제해 만든다.
    private static GameObject BuildNpcTemplate(out Dictionary<string, int> chestIndices, out Dictionary<string, int> helmetIndices)
    {
        GameObject template = CloneDemoCharacter();
        SortingGroup[] groups = template.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].sortingOrder = 0;
        }
        SerializedObject partsData = new SerializedObject(template.GetComponent<PartsManager>());
        chestIndices = AppendCostumes(partsData, PartsType.Chest, "Chest_");
        helmetIndices = AppendCostumes(partsData, PartsType.Helmet, "Helmet_");
        partsData.ApplyModifiedPropertiesWithoutUndo();
        return template;
    }

    private static Dictionary<string, int> AppendCostumes(SerializedObject partsData, PartsType type, string prefix)
    {
        List<Sprite> sprites = new List<Sprite>();
        List<string> keys = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { CostumeRoot });
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith(prefix))
            {
                sprites.Add(LoadSprite(path));
                keys.Add(fileName.Substring(prefix.Length));
            }
        }

        Dictionary<string, int> indices = new Dictionary<string, int>();
        SerializedProperty categories = Find(partsData, "categories");
        for (int c = 0; c < categories.arraySize; c++)
        {
            SerializedProperty category = categories.GetArrayElementAtIndex(c);
            if (category.FindPropertyRelative("type").intValue != (int)type)
            {
                continue;
            }
            SerializedProperty renderers = category.FindPropertyRelative("renderers");
            int count = 0;
            for (int r = 0; r < renderers.arraySize; r++)
            {
                count = Mathf.Max(count, renderers.GetArrayElementAtIndex(r).FindPropertyRelative("sprites").arraySize);
            }
            // 목록 길이가 가장 긴 렌더러(실제 부위)에만 덧붙인다. 첫 렌더러에 그림을, 나머지에는 빈칸을 넣는다.
            bool first = true;
            for (int r = 0; r < renderers.arraySize; r++)
            {
                SerializedProperty list = renderers.GetArrayElementAtIndex(r).FindPropertyRelative("sprites");
                if (list.arraySize != count)
                {
                    continue;
                }
                list.arraySize = count + sprites.Count;
                for (int s = 0; s < sprites.Count; s++)
                {
                    list.GetArrayElementAtIndex(count + s).objectReferenceValue = null;
                    if (first)
                    {
                        list.GetArrayElementAtIndex(count + s).objectReferenceValue = sprites[s];
                    }
                }
                first = false;
            }
            // 썸네일 목록이 따로 있으면 개수를 맞춘다.
            SerializedProperty thumbnails = category.FindPropertyRelative("thumbnails");
            if (thumbnails != null && thumbnails.arraySize > 0)
            {
                int thumbCount = thumbnails.arraySize;
                thumbnails.arraySize = thumbCount + sprites.Count;
                for (int s = 0; s < sprites.Count; s++)
                {
                    thumbnails.GetArrayElementAtIndex(thumbCount + s).objectReferenceValue = sprites[s];
                }
            }
            for (int s = 0; s < sprites.Count; s++)
            {
                indices[keys[s]] = count + s;
            }
            report.AppendLine(type + " 의상 " + sprites.Count + "개 추가(기존 " + count + "개, 렌더러 " + renderers.arraySize + "개)");
        }
        return indices;
    }

    // 패키지 부위 목록에서 그림 이름으로 번호를 찾는다. 없으면 -1.
    private static int FindPartIndex(SerializedObject partsData, PartsType type, string spriteName)
    {
        SerializedProperty categories = Find(partsData, "categories");
        for (int c = 0; c < categories.arraySize; c++)
        {
            SerializedProperty category = categories.GetArrayElementAtIndex(c);
            if (category.FindPropertyRelative("type").intValue != (int)type)
            {
                continue;
            }
            SerializedProperty renderers = category.FindPropertyRelative("renderers");
            for (int r = 0; r < renderers.arraySize; r++)
            {
                SerializedProperty list = renderers.GetArrayElementAtIndex(r).FindPropertyRelative("sprites");
                for (int s = 0; s < list.arraySize; s++)
                {
                    Object sprite = list.GetArrayElementAtIndex(s).objectReferenceValue;
                    if (sprite != null && sprite.name == spriteName)
                    {
                        return s;
                    }
                }
            }
        }
        return -1;
    }

    // 주민 하나: 원본 캐릭터 복제 + CharacterVisual + VillageNpc + 머리 위 말풍선.
    private static void AddVillageNpc(Transform parent, GameObject template, SerializedObject partsData, VillageLayout.Npc npc,
        Dictionary<string, int> chestIndices, Dictionary<string, int> helmetIndices)
    {
        GameObject root = new GameObject("Npc_" + npc.name);
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector2(npc.x, npc.y);

        GameObject visualObject = Object.Instantiate(template, root.transform);
        visualObject.name = "Visual";
        visualObject.transform.localPosition = Vector3.zero;
        float scale = CharacterScale * npc.scale;
        visualObject.transform.localScale = new Vector3(scale, scale, 1f);
        CharacterVisual visual = visualObject.AddComponent<CharacterVisual>();
        SetRef(visual, "partsManager", visualObject.GetComponent<PartsManager>());
        SetRef(visual, "animator", visualObject.GetComponentInChildren<Animator>(true));
        SetRef(visual, "flipRoot", visualObject.transform);
        SetRef(visual, "handRight", FindChild(visualObject.transform, "HandRight"));

        VillageNpc villager = root.AddComponent<VillageNpc>();

        // 발밑 충돌체: 플레이어가 주민을 뚫고 지나가지 못하고, 걷는 주민은 벽·건물·소품에 막힌다.
        CapsuleCollider2D feet = root.AddComponent<CapsuleCollider2D>();
        feet.direction = CapsuleDirection2D.Horizontal;
        feet.size = new Vector2(0.46f * npc.scale, 0.28f * npc.scale);
        feet.offset = new Vector2(0f, 0.12f * npc.scale);
        Rigidbody2D npcBody = null;
        if (npc.waypoints != null && npc.waypoints.Length >= 4)
        {
            npcBody = root.AddComponent<Rigidbody2D>();
            npcBody.bodyType = RigidbodyType2D.Dynamic;
            npcBody.gravityScale = 0f;
            npcBody.mass = 6f;
            npcBody.linearDamping = 0f;
            npcBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            npcBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        GameObject bubbleRoot = new GameObject("Bubble");
        bubbleRoot.transform.SetParent(root.transform, false);
        bubbleRoot.transform.localPosition = new Vector3(0f, 1.55f * npc.scale, 0f);
        SpriteRenderer bubble = bubbleRoot.AddComponent<SpriteRenderer>();
        bubble.sprite = LoadSprite(NpcRoot + "/Bubble.png");
        bubble.drawMode = SpriteDrawMode.Sliced;
        bubble.size = new Vector2(1.6f, 0.7f);
        bubble.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        bubble.sortingOrder = 360;
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(bubbleRoot.transform, false);
        textObject.layer = GameLayers.WorldText;
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        text.fontSize = 2.4f;
        text.color = new Color(0.33f, 0.24f, 0.22f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(6f, 0.5f);
        textObject.GetComponent<MeshRenderer>().sortingOrder = 361;

        SerializedObject data = new SerializedObject(villager);
        Find(data, "visual").objectReferenceValue = visual;
        Find(data, "body").objectReferenceValue = npcBody;
        int chest;
        if (!chestIndices.TryGetValue(npc.costume, out chest))
        {
            throw new System.InvalidOperationException("의상 Chest_" + npc.costume + "을(를) 찾지 못했습니다.");
        }
        Find(data, "chestIndex").intValue = chest;
        int helmet;
        Find(data, "helmetIndex").intValue = -1;
        if (helmetIndices.TryGetValue(npc.costume, out helmet))
        {
            Find(data, "helmetIndex").intValue = helmet;
        }
        bool hasItem = false;
        if (npc.itemType != null)
        {
            PartsType itemType = (PartsType)System.Enum.Parse(typeof(PartsType), npc.itemType);
            int itemIndex = FindPartIndex(partsData, itemType, npc.itemSprite);
            if (itemIndex < 0)
            {
                report.AppendLine("주민 소품을 찾지 못함: " + npc.name + " " + npc.itemSprite);
            }
            else
            {
                hasItem = true;
                Find(data, "itemType").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(PartsType)), itemType);
                Find(data, "itemIndex").intValue = itemIndex;
            }
        }
        Find(data, "hasItem").boolValue = hasItem;
        Find(data, "hair").intValue = npc.hair;
        Find(data, "eye").intValue = npc.eye;
        Find(data, "beard").intValue = npc.beard;
        Find(data, "hairColor").intValue = npc.hairColor;
        Find(data, "skinColor").intValue = npc.skinColor;
        Find(data, "startFacingLeft").boolValue = npc.faceLeft;
        Find(data, "work").boolValue = npc.work;
        SerializedProperty points = Find(data, "waypoints");
        int pointCount = 0;
        if (npc.waypoints != null)
        {
            pointCount = npc.waypoints.Length / 2;
        }
        points.arraySize = pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            points.GetArrayElementAtIndex(i).vector2Value = new Vector2(npc.waypoints[i * 2], npc.waypoints[i * 2 + 1]);
        }
        if (npc.speed > 0f)
        {
            Find(data, "walkSpeed").floatValue = npc.speed;
        }
        SerializedProperty lineArray = Find(data, "lines");
        lineArray.arraySize = npc.lines.Length;
        for (int i = 0; i < npc.lines.Length; i++)
        {
            lineArray.GetArrayElementAtIndex(i).stringValue = npc.lines[i];
        }
        Find(data, "bubbleRoot").objectReferenceValue = bubbleRoot;
        Find(data, "bubbleSprite").objectReferenceValue = bubble;
        Find(data, "bubbleText").objectReferenceValue = text;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddPointLight(Transform parent, string name, Vector2 position, Color color, float radius, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = position;
        UnityEngine.Rendering.Universal.Light2D light = lightObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = radius;
        light.pointLightInnerRadius = radius * 0.12f;
        light.falloffIntensity = 0.6f;
    }

    // 해 질 녘: 씬의 전역 조명을 따뜻하고 조금 어둡게 한다(마을 그림만 영향을 받고, 캐릭터·주민은 밝게 남는다).
    private static void SetupDuskLight()
    {
        UnityEngine.Rendering.Universal.Light2D[] lights = Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].lightType == UnityEngine.Rendering.Universal.Light2D.LightType.Global)
            {
                lights[i].color = new Color(1f, 0.9f, 0.8f);
                lights[i].intensity = 0.93f;
            }
        }
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
            frames[i] = LoadSprite(VillageRoot + "/" + prefix + "_" + i.ToString("00") + ".png");
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
        // 위쪽 가운데를 기준으로 두어 MetaUi가 화면마다 높이를 바꿔도 위치가 흔들리지 않게 한다.
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, 380f);
        panel.sizeDelta = new Vector2(560f, 720f);

        // 위에서부터: 제목(16), 본문(72, 도감은 아래까지 길게 쓴다), 능력치 줄 9개(168부터 52 간격), 초기화(648)
        TextMeshProUGUI title = AddPanelText(panel, "Title", font, string.Empty, new Vector2(20f, 16f), new Vector2(440f, 44f), 32f, false);
        TextMeshProUGUI closeLabel;
        Button closeButton = AddPanelButton(panel, "CloseButton", pixel, font, "X", new Vector2(500f, 16f), new Vector2(40f, 40f), out closeLabel);
        TextMeshProUGUI body = AddPanelText(panel, "Body", font, string.Empty, new Vector2(20f, 72f), new Vector2(520f, 660f), 22f, true);
        body.richText = true;

        Object[] buttons = new Object[MetaProgress.UnlockCount];
        Object[] labels = new Object[MetaProgress.UnlockCount];
        for (int i = 0; i < buttons.Length; i++)
        {
            TextMeshProUGUI label;
            buttons[i] = AddPanelButton(panel, "Action" + i, pixel, font, string.Empty, new Vector2(20f, 168f + 52f * i), new Vector2(520f, 44f), out label);
            labels[i] = label;
        }
        TextMeshProUGUI resetLabel;
        Button resetButton = AddPanelButton(panel, "Reset", pixel, font, string.Empty, new Vector2(20f, 648f), new Vector2(520f, 46f), out resetLabel);
        resetButton.image.color = new Color(0.6f, 0.25f, 0.25f);

        SerializedObject data = new SerializedObject(metaUi);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        Find(data, "bodyLabel").objectReferenceValue = body;
        Find(data, "closeButton").objectReferenceValue = closeButton;
        SetRefArray(data, "actionButtons", buttons);
        SetRefArray(data, "actionLabels", labels);
        Find(data, "resetButton").objectReferenceValue = resetButton;
        Find(data, "resetLabel").objectReferenceValue = resetLabel;
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
        Button keysButton = AddPanelButton(settingsPage, "Keys", pixel, font, "키 설정", new Vector2(60f, 522f), new Vector2(440f, 52f), out label);
        Button back = AddPanelButton(settingsPage, "Back", pixel, font, "뒤로", new Vector2(60f, 586f), new Vector2(440f, 52f), out label);

        // 키 설정: 두 열 x 8줄. 한 칸 = 이름(열 x+0, 폭 132) + 키 버튼(열 x+136, 폭 112, 높이 44), 열 x 20·290, 줄 간격 51(y 76부터).
        // 아래에 안내 글(y 486), 초기화·뒤로 버튼(y 530, 폭 236).
        RectTransform keysPage = AddPage(box, "KeysPage");
        Object[] keyButtons = new Object[GameInput.RebindCount];
        Object[] keyNameLabels = new Object[GameInput.RebindCount];
        Object[] keyValueLabels = new Object[GameInput.RebindCount];
        for (int i = 0; i < GameInput.RebindCount; i++)
        {
            int column = i / 8;
            int row = i % 8;
            float x = 20f + 270f * column;
            float y = 76f + 51f * row;
            TextMeshProUGUI keyName = AddPanelText(keysPage, "KeyName" + i, font, string.Empty, new Vector2(x, y), new Vector2(132f, 44f), 20f, false);
            keyName.alignment = TextAlignmentOptions.MidlineLeft;
            keyNameLabels[i] = keyName;
            TextMeshProUGUI keyValue;
            keyButtons[i] = AddPanelButton(keysPage, "Key" + i, pixel, font, string.Empty, new Vector2(x + 136f, y), new Vector2(112f, 44f), out keyValue);
            keyValue.fontSize = 18f;
            keyValueLabels[i] = keyValue;
        }
        TextMeshProUGUI keysHelp = AddPanelText(keysPage, "Help", font, "칸을 누르고 바꿀 키를 누르세요. Esc는 취소, 겹치면 서로 바뀝니다.", new Vector2(20f, 492f), new Vector2(520f, 30f), 16f, false);
        keysHelp.alignment = TextAlignmentOptions.Center;
        keysHelp.color = new Color(1f, 1f, 1f, 0.6f);
        Button resetKeys = AddPanelButton(keysPage, "ResetKeys", pixel, font, "기본값으로", new Vector2(36f, 530f), new Vector2(236f, 52f), out label);
        resetKeys.image.color = new Color(0.6f, 0.25f, 0.25f);
        Button keysBack = AddPanelButton(keysPage, "KeysBack", pixel, font, "뒤로", new Vector2(288f, 530f), new Vector2(236f, 52f), out label);

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
        Find(data, "keysButton").objectReferenceValue = keysButton;
        Find(data, "keysPage").objectReferenceValue = keysPage.gameObject;
        Find(data, "resetKeysButton").objectReferenceValue = resetKeys;
        Find(data, "keysBackButton").objectReferenceValue = keysBack;
        Find(data, "settingsHeight").floatValue = 670f;
        Find(data, "keysHeight").floatValue = 610f;
        SetRefArray(data, "keyButtons", keyButtons);
        SetRefArray(data, "keyNameLabels", keyNameLabels);
        SetRefArray(data, "keyValueLabels", keyValueLabels);
        SetRefArray(data, "rowValues", values);
        SetRefArray(data, "rowPrevButtons", prevButtons);
        SetRefArray(data, "rowNextButtons", nextButtons);
        data.ApplyModifiedPropertiesWithoutUndo();

        settingsPage.gameObject.SetActive(false);
        keysPage.gameObject.SetActive(false);
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

        // 걸린 강화(스킬 강화, 물약) 아이콘: 무기 이름 위 한 줄. 왼쪽 아래 기준 (40,184)부터 46 간격, 금색 테두리 + 남색 바탕.
        // 걸린 상태이상 아이콘: 체력 바 오른쪽 한 줄. 왼쪽 아래 기준 (412,66)부터 46 간격, 붉은 테두리 + 검붉은 바탕.
        // 아이콘 위에 지난 시간만큼 어두운 부채꼴이 시계 방향으로 덮인다(Filled Radial360, 위에서 시작).
        const int BuffSlotCount = 4;
        Object[] buffSlots = new Object[BuffSlotCount];
        Object[] buffIcons = new Object[BuffSlotCount];
        Object[] buffGauges = new Object[BuffSlotCount];
        for (int i = 0; i < BuffSlotCount; i++)
        {
            Image buffIcon;
            Image buffGauge;
            buffSlots[i] = AddEffectSlot(parent, "Buff" + i, pixel, new Vector2(40f + 46f * i, 184f), new Color(1f, 0.82f, 0.32f), new Color(0.1f, 0.14f, 0.28f, 0.9f), out buffIcon, out buffGauge);
            buffIcons[i] = buffIcon;
            buffGauges[i] = buffGauge;
        }
        const int DebuffSlotCount = PlayerController.StatusCount;
        Object[] debuffSlots = new Object[DebuffSlotCount];
        Object[] debuffIcons = new Object[DebuffSlotCount];
        Object[] debuffGauges = new Object[DebuffSlotCount];
        for (int i = 0; i < DebuffSlotCount; i++)
        {
            Image debuffIcon;
            Image debuffGauge;
            debuffSlots[i] = AddEffectSlot(parent, "Debuff" + i, pixel, new Vector2(412f + 46f * i, 66f), new Color(0.92f, 0.22f, 0.2f), new Color(0.26f, 0.05f, 0.06f, 0.9f), out debuffIcon, out debuffGauge);
            debuffIcons[i] = debuffIcon;
            debuffGauges[i] = debuffGauge;
        }
        string[] statusNames = { "Burn", "Bleed", "Slow", "Weaken", "Stun" };
        Object[] statusSprites = new Object[statusNames.Length];
        for (int i = 0; i < statusNames.Length; i++)
        {
            statusSprites[i] = LoadSprite(SpriteRoot + "/Fx/StatusIcon/Status_" + statusNames[i] + ".png");
        }

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

        // 포션 칸 2개: 아래쪽 가운데 기준, 스킬 칸(오른쪽 끝 x 108) 오른쪽. 56x56, 가운데 x 160·224, 아래에서 44.
        Object[] potionIcons = new Object[GameInput.PotionSlotCount];
        Object[] potionCounts = new Object[GameInput.PotionSlotCount];
        Object[] potionKeys = new Object[GameInput.PotionSlotCount];
        for (int i = 0; i < GameInput.PotionSlotCount; i++)
        {
            Image potionFrame = AddBottomCenterImage(parent, "Potion" + i, pixel, new Vector2(160f + 64f * i, 44f), new Vector2(56f, 56f), new Color(0f, 0f, 0f, 0.35f));
            Image potionIcon = AddChildImage(potionFrame.transform, "Icon", pixel, Vector2.zero, new Vector2(44f, 44f));
            potionIcon.preserveAspect = true;
            potionIcon.raycastTarget = false;
            potionIcon.enabled = false;
            TextMeshProUGUI count = AddText(potionFrame.transform, "Count", hudFont, string.Empty, 16f, false);
            count.alignment = TextAlignmentOptions.BottomRight;
            count.outlineWidth = 0.25f;
            count.outlineColor = new Color32(0, 0, 0, 255);
            Stretch(count.rectTransform, 3f);
            TextMeshProUGUI key = AddText(potionFrame.transform, "Key", hudFont, (i + 1).ToString(), 16f, false);
            key.alignment = TextAlignmentOptions.TopLeft;
            key.color = new Color(1f, 0.85f, 0.3f);
            Stretch(key.rectTransform, 3f);
            potionIcons[i] = potionIcon;
            potionCounts[i] = count;
            potionKeys[i] = key;
        }

        // 차징 스킬 게이지: 왼쪽 아래 기준으로 매 프레임 캐릭터 머리 위 화면 자리로 옮긴다. 80x16 테두리 + 74x10 채움.
        GameObject chargeRoot = new GameObject("ChargeGauge");
        chargeRoot.transform.SetParent(parent, false);
        RectTransform chargeRect = chargeRoot.AddComponent<RectTransform>();
        chargeRect.anchorMin = Vector2.zero;
        chargeRect.anchorMax = Vector2.zero;
        chargeRect.pivot = new Vector2(0.5f, 0f);
        chargeRect.sizeDelta = new Vector2(80f, 16f);
        AddBar(chargeRoot.transform, "Border", pixel, Vector2.zero, new Vector2(80f, 16f), new Color(0.05f, 0.05f, 0.08f, 0.95f), false);
        AddBar(chargeRoot.transform, "Back", pixel, new Vector2(3f, 3f), new Vector2(74f, 10f), new Color(0.2f, 0.22f, 0.3f, 0.9f), false);
        Image chargeFill = AddBar(chargeRoot.transform, "Fill", pixel, new Vector2(3f, 3f), new Vector2(74f, 10f), new Color(0.45f, 0.82f, 1f), true);
        chargeRoot.SetActive(false);

        HudController hud = canvasObject.AddComponent<HudController>();
        SerializedObject hudData = new SerializedObject(hud);
        Find(hudData, "chargeGaugeRoot").objectReferenceValue = chargeRect;
        Find(hudData, "chargeGaugeFill").objectReferenceValue = chargeFill;
        SetRefArray(hudData, "potionIcons", potionIcons);
        SetRefArray(hudData, "potionCounts", potionCounts);
        SetRefArray(hudData, "potionKeys", potionKeys);
        SetRefArray(hudData, "buffSlots", buffSlots);
        SetRefArray(hudData, "buffIcons", buffIcons);
        SetRefArray(hudData, "buffGauges", buffGauges);
        SetRefArray(hudData, "debuffSlots", debuffSlots);
        SetRefArray(hudData, "debuffIcons", debuffIcons);
        SetRefArray(hudData, "debuffGauges", debuffGauges);
        SetRefArray(hudData, "statusSprites", statusSprites);
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

    private static InventoryUi BuildInventoryPanel(Transform canvas, GameHub hub)
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

        TextMeshProUGUI help = AddPanelText(panel, "Help", font, "좌클릭 장착 · 해제 · 포션 마시기     끌어서 칸 옮기기\n우클릭하거나 창 밖으로 끌어 놓으면 버리기", new Vector2(20f, 360f), new Vector2(472f, 38f), 15f, false);
        help.color = new Color(1f, 1f, 1f, 0.6f);
        TextMeshProUGUI detail = AddPanelText(panel, "Detail", font, string.Empty, new Vector2(20f, 400f), new Vector2(472f, 224f), 18f, true);

        Image itemDrag = AddDragIcon(panel, pixel, 60f);

        SerializedObject data = new SerializedObject(inventoryUi);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "dragIcon").objectReferenceValue = itemDrag;
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
    // 시작 계열 목록(로비만) 1100x26은 아래에서 154(화면 x 410~1510, 왼쪽 무기 글자는 x 382까지).
    // 스킬 트리 패널: 가운데에서 위로 30, 크기 1320x820(화면 x 300~1620, y 100~920. 가방·상점과 겹치면 트리가 위에 그려진다).
    // 안쪽 요소는 패널 왼쪽 위 기준. 계열 상자 200x40은 (556, 54), 직업 가지 머리 220x44는 가운데 x 232/656/1080·y 126(아래 끝 170),
    // 노드 98x60은 (24, 196)부터 간격 106x76으로 12열 6행(오른쪽 끝 1288, 아래 끝 636). 노드 자리와 선은 SkillUi가 계열 데이터로 놓는다.
    // 장착 칸 124x52는 x 95부터 간격 130·y 666(오른쪽 끝 479), 설명 800x150은 (500, 660).
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
        Object[] keyLabels = new Object[keys.Length];
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
            keyLabels[i] = key;

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
        panel.anchoredPosition = new Vector2(0f, 30f);
        panel.sizeDelta = new Vector2(1320f, 820f);

        Color lineColor = new Color(0.3f, 0.3f, 0.32f, 1f);
        TextMeshProUGUI title = AddPanelText(panel, "Title", font, string.Empty, new Vector2(20f, 14f), new Vector2(1280f, 34f), 26f, false);
        title.richText = true;

        // 연결선을 먼저 만들어 노드 뒤에 그려지게 한다(노드 선의 자리는 SkillUi가 계열마다 놓는다).
        Object[] jobLinks = new Object[ClassDefinition.JobCount];
        for (int job = 0; job < ClassDefinition.JobCount; job++)
        {
            float center = 232f + 424f * job;
            jobLinks[job] = AddPanelLine(panel, "JobLink" + job, pixel, new Vector2(656f, 94f), new Vector2(center, 126f), lineColor);
        }
        Object[] nodeLinks = new Object[ClassDefinition.MaxNodes];
        for (int i = 0; i < ClassDefinition.MaxNodes; i++)
        {
            nodeLinks[i] = AddPanelLine(panel, "Link" + i, pixel, Vector2.zero, new Vector2(10f, 0f), lineColor);
        }

        // 뿌리: 계열 이름 상자
        Image rootBox = AddPanelImage(panel, "RootBox", pixel, new Vector2(556f, 54f), new Vector2(200f, 40f), new Color(0.35f, 0.28f, 0.12f, 1f));
        TextMeshProUGUI rootLabel = AddText(rootBox.transform, "Label", font, string.Empty, 22f, false);
        rootLabel.alignment = TextAlignmentOptions.Center;
        Stretch(rootLabel.rectTransform, 4f);

        Object[] jobViews = new Object[ClassDefinition.JobCount];
        for (int job = 0; job < ClassDefinition.JobCount; job++)
        {
            float center = 232f + 424f * job;
            jobViews[job] = AddTreeNode(panel, "Job" + job, skillUi, pixel, font, new Vector2(center - 110f, 126f), new Vector2(220f, 44f), ClassDefinition.MaxNodes + job, 0f, TextAlignmentOptions.Center, 19f);
        }
        Object[] nodeViews = new Object[ClassDefinition.MaxNodes];
        for (int i = 0; i < ClassDefinition.MaxNodes; i++)
        {
            nodeViews[i] = AddTreeNode(panel, "Node" + i, skillUi, pixel, font, new Vector2(24f, 196f), new Vector2(98f, 70f), i, 48f, TextAlignmentOptions.Bottom, 14f);
        }

        // 장착 칸(Q, E, R)과 설명
        AddPanelImage(panel, "Separator", pixel, new Vector2(20f, 652f), new Vector2(1280f, 2f), lineColor);
        TextMeshProUGUI equipLabel = AddPanelText(panel, "EquipLabel", font, "장착", new Vector2(20f, 666f), new Vector2(70f, 52f), 22f, false);
        equipLabel.alignment = TextAlignmentOptions.Left;
        Object[] slotViews = new Object[ClassDefinition.SlotCount];
        for (int i = 0; i < ClassDefinition.SlotCount; i++)
        {
            slotViews[i] = AddSkillNode(panel, "Slot" + keys[i], skillUi, pixel, font, new Vector2(95f + 160f * i, 666f), new Vector2(152f, 56f), i, true);
        }
        TextMeshProUGUI detail = AddPanelText(panel, "Detail", font, string.Empty, new Vector2(590f, 660f), new Vector2(710f, 150f), 16f, true);
        detail.richText = true;
        // 던전 스킬 트리 초기화(골드)
        TextMeshProUGUI resetLabel;
        Button resetButton = AddPanelButton(panel, "Reset", pixel, font, string.Empty, new Vector2(1090f, 12f), new Vector2(210f, 40f), out resetLabel);
        resetButton.image.color = new Color(0.6f, 0.25f, 0.25f);
        resetLabel.fontSize = 18f;

        Image skillDrag = AddDragIcon(panel, pixel, 48f);

        SerializedObject data = new SerializedObject(skillUi);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "dragIcon").objectReferenceValue = skillDrag;
        SetRefArray(data, "skillFrames", frames);
        SetRefArray(data, "skillFills", fills);
        SetRefArray(data, "skillLabels", labels);
        SetRefArray(data, "skillKeyLabels", keyLabels);
        SetRefArray(data, "skillIcons", barIcons);
        SetRefArray(data, "fxIcons", icons);
        Find(data, "levelLabel").objectReferenceValue = levelLabel;
        Find(data, "xpFill").objectReferenceValue = xpFill;
        Find(data, "jobListLabel").objectReferenceValue = jobList;
        Find(data, "treePanel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        Find(data, "rootLabel").objectReferenceValue = rootLabel;
        SetRefArray(data, "jobViews", jobViews);
        SetRefArray(data, "jobLinks", jobLinks);
        SetRefArray(data, "nodeViews", nodeViews);
        SetRefArray(data, "nodeLinks", nodeLinks);
        SetRefArray(data, "slotViews", slotViews);
        Find(data, "detailLabel").objectReferenceValue = detail;
        Find(data, "resetButton").objectReferenceValue = resetButton;
        Find(data, "resetLabel").objectReferenceValue = resetLabel;
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
    // HUD 효과 칸(40x40): 테두리 색 바깥 칸, 2픽셀 안쪽 바탕, 아이콘 32x32, 그 위 회전 게이지(지난 시간만큼 어두운 부채꼴).
    private static GameObject AddEffectSlot(Transform parent, string name, Sprite pixel, Vector2 position, Color border, Color back, out Image icon, out Image gauge)
    {
        Image frame = AddBar(parent, name, pixel, position, new Vector2(40f, 40f), border, false);
        Image inner = AddChildImage(frame.transform, "Back", pixel, Vector2.zero, new Vector2(36f, 36f));
        inner.preserveAspect = false;
        inner.color = back;
        icon = AddChildImage(frame.transform, "Icon", pixel, Vector2.zero, new Vector2(32f, 32f));
        gauge = AddChildImage(frame.transform, "Gauge", pixel, Vector2.zero, new Vector2(36f, 36f));
        gauge.preserveAspect = false;
        gauge.color = new Color(0f, 0f, 0f, 0.62f);
        gauge.type = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Radial360;
        gauge.fillOrigin = (int)Image.Origin360.Top;
        gauge.fillClockwise = true;
        gauge.fillAmount = 0f;
        frame.gameObject.SetActive(false);
        return frame.gameObject;
    }

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

    // 패널 왼쪽 위 기준(아래로 +) from에서 to까지 잇는 선(클릭을 받지 않는다). 기준점은 선의 왼쪽 가운데.
    private static Image AddPanelLine(RectTransform panel, string name, Sprite sprite, Vector2 from, Vector2 to, Color color)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(panel, false);
        Image image = lineObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        Vector2 delta = to - from;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(from.x, -from.y);
        rect.sizeDelta = new Vector2(delta.magnitude, 4f);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg);
        return image;
    }

    // 스킬 트리 노드: 선택 테두리, 바탕, 위쪽 가운데 아이콘(iconSize 0이면 없음), 칸 전체에 걸친 글자(크기 자동)
    private static SkillNodeView AddTreeNode(RectTransform panel, string name, SkillUi owner, Sprite pixel, TMP_FontAsset font, Vector2 position, Vector2 size, int index, float iconSize, TextAlignmentOptions alignment, float fontSize)
    {
        Image outline = AddPanelImage(panel, name + "Outline", pixel, position - new Vector2(4f, 4f), size + new Vector2(8f, 8f), new Color(1f, 0.85f, 0.3f, 1f));
        outline.enabled = false;

        GameObject nodeObject = new GameObject(name);
        nodeObject.transform.SetParent(panel, false);
        Image frame = nodeObject.AddComponent<Image>();
        frame.sprite = pixel;
        PlaceTopLeft(frame.rectTransform, position, size);

        Image icon = null;
        if (iconSize > 0f)
        {
            icon = AddChildImage(nodeObject.transform, "Icon", pixel, new Vector2(0f, size.y * 0.5f - 4f - iconSize * 0.5f), new Vector2(iconSize, iconSize));
            icon.enabled = false;
        }

        TextMeshProUGUI label = AddText(nodeObject.transform, "Label", font, string.Empty, fontSize, false);
        label.alignment = alignment;
        label.richText = true;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = fontSize;
        Stretch(label.rectTransform, 3f);

        SkillNodeView view = nodeObject.AddComponent<SkillNodeView>();
        SerializedObject viewData = new SerializedObject(view);
        Find(viewData, "owner").objectReferenceValue = owner;
        Find(viewData, "frame").objectReferenceValue = frame;
        Find(viewData, "outline").objectReferenceValue = outline;
        Find(viewData, "icon").objectReferenceValue = icon;
        Find(viewData, "label").objectReferenceValue = label;
        Find(viewData, "index").intValue = index;
        Find(viewData, "isSlot").boolValue = false;
        viewData.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // 끌어 놓기용 그림: 마우스를 따라다니고 입력은 받지 않는다(놓은 곳을 가리지 않게).
    private static Image AddDragIcon(RectTransform panel, Sprite pixel, float size)
    {
        GameObject iconObject = new GameObject("DragIcon");
        iconObject.transform.SetParent(panel, false);
        Image icon = iconObject.AddComponent<Image>();
        icon.sprite = pixel;
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.color = new Color(1f, 1f, 1f, 0.9f);
        icon.rectTransform.sizeDelta = new Vector2(size, size);
        icon.enabled = false;
        return icon;
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

        // 아이콘은 24픽셀 그림을 정확히 2배(48)로 보여 준다.
        float iconSize = size.y - 8f;
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
    private const int SkillFxPoolSize = 48;

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
        SkillFx[] groundKinds = { SkillFx.Shockwave, SkillFx.Heal, SkillFx.Poison, SkillFx.Frost, SkillFx.Holy, SkillFx.Wind,
            SkillFx.Pierce, SkillFx.Quake, SkillFx.IceNova, SkillFx.ArrowRain, SkillFx.Hex };
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
            // 프레임이 10장인 베기·지진은 조금 빠르게, 화살비(반복)는 천천히
            float frameRate = 18f;
            if (sprites.Count >= 10)
            {
                frameRate = 22f;
            }
            if ((SkillFx)i == SkillFx.ArrowRain)
            {
                frameRate = 12f;
            }
            clip.FindPropertyRelative("frameRate").floatValue = frameRate;
            clip.FindPropertyRelative("ground").boolValue = System.Array.IndexOf(groundKinds, (SkillFx)i) >= 0;
        }
        SetRefArray(data, "renderers", renderers);
        // 스킬별 컬러 프레임은 빛의 영향을 받지 않는 기본 스프라이트 재질로 그린다.
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        Find(data, "frameMaterial").objectReferenceValue = unlit;
        // 몬스터 폭발·보스 내려찍기 픽셀 그림(impact_NN.png)
        LoadSkillVfx(Find(data, "monsterExplosion"), "Monster_Explosion");
        LoadSkillVfx(Find(data, "bossImpact"), "Monster_BossImpact");
        LoadSkillVfx(Find(data, "teleport"), "World_Teleport");
        data.ApplyModifiedPropertiesWithoutUndo();
        report.AppendLine(loaded.ToString());
        string unlitName = "기본";
        if (unlit != null)
        {
            unlitName = unlit.name;
        }
        report.AppendLine("스킬 이펙트 재질: " + unlitName);
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

        // 상점 포션 줄(상점에서만 켠다): 버튼 자리 y 176에 60 칸 6개(간격 72), 가격은 y 240
        RectTransform potionRow = AddPage(panel, "PotionRow");
        Object[] potionSlots = new Object[6];
        Object[] potionLabels = new Object[6];
        for (int i = 0; i < potionSlots.Length; i++)
        {
            potionSlots[i] = AddInventorySlot(potionRow, "Potion" + i, pixel, new Vector2(20f + 72f * i, 176f), 60f, facilityUi, false, FacilityUi.PotionSlotBase + i);
            TextMeshProUGUI price = AddPanelText(potionRow, "PotionPrice" + i, font, string.Empty, new Vector2(20f + 72f * i, 240f), new Vector2(60f, 22f), 16f, false);
            price.alignment = TextAlignmentOptions.Center;
            potionLabels[i] = price;
        }

        SerializedObject data = new SerializedObject(facilityUi);
        Find(data, "hub").objectReferenceValue = hub;
        Find(data, "panel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        SetRefArray(data, "slots", slots);
        SetRefArray(data, "slotLabels", slotLabels);
        SetRefArray(data, "buttons", buttons);
        SetRefArray(data, "buttonLabels", buttonLabels);
        Find(data, "detailLabel").objectReferenceValue = detail;
        Find(data, "potionRow").objectReferenceValue = potionRow.gameObject;
        SetRefArray(data, "potionSlots", potionSlots);
        SetRefArray(data, "potionLabels", potionLabels);
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

        // 강화 단계 색 안쪽 테두리(등급 테두리 안 2픽셀). 강화하지 않은 장비는 끈다.
        Image tierRing = AddSlotImage(slotObject.transform, "TierRing", 2f);
        tierRing.sprite = pixel;
        tierRing.enabled = false;
        Image inner = AddSlotImage(slotObject.transform, "Inner", 4f);
        inner.sprite = pixel;
        inner.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        Image icon = AddSlotImage(slotObject.transform, "Icon", 8f);
        icon.preserveAspect = true;
        icon.enabled = false;

        // 포션 개수·강화 단계(오른쪽 아래)와 등록 칸 키(왼쪽 위). 칸 크기에 맞춰 글자 크기를 정한다.
        TMP_FontAsset slotFont = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        float labelSize = Mathf.Round(size * 0.24f);
        TextMeshProUGUI countLabel = AddText(slotObject.transform, "Count", slotFont, string.Empty, labelSize, false);
        countLabel.alignment = TextAlignmentOptions.BottomRight;
        countLabel.raycastTarget = false;
        countLabel.outlineWidth = 0.25f;
        countLabel.outlineColor = new Color32(0, 0, 0, 255);
        Stretch(countLabel.rectTransform, 5f);
        countLabel.enabled = false;
        TextMeshProUGUI markLabel = AddText(slotObject.transform, "Mark", slotFont, string.Empty, labelSize, false);
        markLabel.alignment = TextAlignmentOptions.TopLeft;
        markLabel.raycastTarget = false;
        markLabel.color = new Color(1f, 0.85f, 0.3f);
        markLabel.outlineWidth = 0.25f;
        markLabel.outlineColor = new Color32(0, 0, 0, 255);
        Stretch(markLabel.rectTransform, 5f);
        markLabel.enabled = false;

        InventorySlotView view = slotObject.AddComponent<InventorySlotView>();
        SerializedObject data = new SerializedObject(view);
        Find(data, "owner").objectReferenceValue = owner;
        Find(data, "frame").objectReferenceValue = frame;
        Find(data, "icon").objectReferenceValue = icon;
        Find(data, "equipSlot").boolValue = equipSlot;
        Find(data, "index").intValue = index;
        Find(data, "countLabel").objectReferenceValue = countLabel;
        Find(data, "markLabel").objectReferenceValue = markLabel;
        Find(data, "tierRing").objectReferenceValue = tierRing;
        Find(data, "back").objectReferenceValue = inner;
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
