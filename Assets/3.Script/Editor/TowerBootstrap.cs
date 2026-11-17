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
    private const string PrefabFolder = "Assets/2.Model/Prefabs";
    private const string DataFolder = "Assets/2.Model/Data";
    private const string MonsterDataFolder = "Assets/2.Model/Data/Monster";
    private const string WeaponDataFolder = "Assets/2.Model/Data/Weapon";
    private const string ItemBalancePath = DataFolder + "/ItemBalance.asset";
    private const string JobDataFolder = "Assets/2.Model/Data/Job";
    private static readonly string[] JobAssetNames = { "Swordsman", "Lancer", "Archer", "Mage" };
    private static readonly string[] WeaponAssetNames = { "Sword", "Spear", "Bow", "Staff" };
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
        staff.ApplyModifiedPropertiesWithoutUndo();

        report.AppendLine("무기 데이터 4종 생성(검, 창, 활, 지팡이)");
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
    // 직업은 무기 계열마다 하나(검사, 창기사, 궁수, 마법사). 스킬 3개(Q, E, R)와 노드 8개(갈래 2개 x 4단계).
    // 노드 구성은 모두 같다: 갈래마다 [스킬 해금, 3단계 노드, 3단계 노드, 마무리 노드]. 다 찍으려면 16포인트가 든다.

    private static void BuildJobDefinitions()
    {
        Color red = new Color(1f, 0.55f, 0.35f);
        Color blue = new Color(0.5f, 0.75f, 1f);
        Color yellow = new Color(1f, 0.9f, 0.4f);
        Color purple = new Color(0.8f, 0.55f, 1f);

        // 검사
        SkillDefinition spin = MakeArea("SwordSpin", "회전 베기", 12, 5f, 0.35f, 1.6f, 2.2f, CharacterAnim.DoubleAttack, red);
        SkillDefinition charge = MakeDash("SwordCharge", "돌진 베기", 10, 6f, 0.25f, 1.3f, 4.5f, 0.8f, true, CharacterAnim.JumpAttack, blue);
        SkillDefinition roar = MakeBuff("SwordRoar", "전투 함성", 20, 18f, 0.3f, JobNodeEffect.DamagePercent, 35f, 8f, red);
        MakeJob(0, "검사", "광전", "수호", new Object[] { spin, charge, roar },
            new[] { "회전 베기", "날 세우기", "급소 노리기", "전투 함성", "돌진 베기", "단련", "버티기", "숙련" },
            new[] { JobNodeEffect.UnlockSkill, JobNodeEffect.DamagePercent, JobNodeEffect.CritChancePercent, JobNodeEffect.UnlockSkill,
                JobNodeEffect.UnlockSkill, JobNodeEffect.MaxHp, JobNodeEffect.DefensePercent, JobNodeEffect.CooldownPercent },
            new[] { 0f, 5f, 4f, 2f, 1f, 12f, 4f, 15f });

        // 창기사
        SkillDefinition pierce = MakeDash("SpearPierce", "꿰뚫기", 12, 6f, 0.28f, 1.8f, 5.5f, 0.7f, false, CharacterAnim.JumpAttack, yellow);
        SkillDefinition sweep = MakeArea("SpearSweep", "휩쓸기", 12, 5f, 0.35f, 1.3f, 2.8f, CharacterAnim.DoubleAttack, yellow);
        SkillDefinition gale = MakeBuff("SpearGale", "질풍", 18, 18f, 0.3f, JobNodeEffect.AttackSpeedPercent, 40f, 8f, blue);
        MakeJob(1, "창기사", "돌파", "연격", new Object[] { pierce, sweep, gale },
            new[] { "꿰뚫기", "창술", "힘 싣기", "질풍", "휩쓸기", "빠른 손", "발놀림", "기세" },
            new[] { JobNodeEffect.UnlockSkill, JobNodeEffect.SkillDamagePercent, JobNodeEffect.DamagePercent, JobNodeEffect.UnlockSkill,
                JobNodeEffect.UnlockSkill, JobNodeEffect.AttackSpeedPercent, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.ManaOnHit },
            new[] { 0f, 8f, 5f, 2f, 1f, 5f, 4f, 2f });

        // 궁수
        SkillDefinition fan = MakeVolley("BowFan", "부채 사격", 10, 4f, 0.3f, 0.9f, 5, 40f, 14f, 0.9f, 0.18f, 0f, ProjectileLook.Arrow, yellow);
        SkillDefinition leap = MakeDash("BowLeap", "회피 도약", 8, 5f, 0.22f, 0f, 4f, 0.5f, true, CharacterAnim.Roll, blue);
        SkillDefinition blast = MakeVolley("BowBlast", "폭발 화살", 20, 10f, 0.35f, 2.5f, 1, 0f, 14f, 1f, 0.2f, 2f, ProjectileLook.Arrow, red);
        MakeJob(2, "궁수", "속사", "사냥꾼", new Object[] { fan, leap, blast },
            new[] { "부채 사격", "빠른 시위", "약점 간파", "폭발 화살", "회피 도약", "가벼운 발", "강한 시위", "숙련" },
            new[] { JobNodeEffect.UnlockSkill, JobNodeEffect.AttackSpeedPercent, JobNodeEffect.CritChancePercent, JobNodeEffect.UnlockSkill,
                JobNodeEffect.UnlockSkill, JobNodeEffect.MoveSpeedPercent, JobNodeEffect.DamagePercent, JobNodeEffect.CooldownPercent },
            new[] { 0f, 5f, 5f, 2f, 1f, 4f, 5f, 15f });

        // 마법사
        SkillDefinition fireball = MakeVolley("StaffFireball", "화염구", 14, 4f, 0.3f, 2.2f, 1, 0f, 10f, 1.2f, 0.25f, 2.2f, ProjectileLook.Orb, red);
        SkillDefinition blink = MakeDash("StaffBlink", "점멸", 10, 6f, 0.12f, 0f, 4.5f, 0.5f, true, CharacterAnim.Skill, purple);
        SkillDefinition storm = MakeVolley("StaffStorm", "비전 폭풍", 22, 12f, 0.4f, 1.2f, 8, 315f, 9f, 1.1f, 0.22f, 0f, ProjectileLook.Orb, purple);
        MakeJob(3, "마법사", "화염", "비전", new Object[] { fireball, blink, storm },
            new[] { "화염구", "불길", "마력 그릇", "비전 폭풍", "점멸", "마력 흡수", "빠른 영창", "집중" },
            new[] { JobNodeEffect.UnlockSkill, JobNodeEffect.SkillDamagePercent, JobNodeEffect.MaxMana, JobNodeEffect.UnlockSkill,
                JobNodeEffect.UnlockSkill, JobNodeEffect.ManaOnHit, JobNodeEffect.CooldownPercent, JobNodeEffect.SkillDamagePercent },
            new[] { 0f, 8f, 10f, 2f, 1f, 1f, 5f, 20f });

        // 스킬에 붙는 상태이상(종류, 지속 시간, 화상·출혈의 0.5초당 피해 배율)
        SetSkillStatus(spin, StatusKind.Bleed, 4f, 0.12f);
        SetSkillStatus(charge, StatusKind.Stun, 0.8f, 0f);
        SetSkillStatus(pierce, StatusKind.Bleed, 4f, 0.15f);
        SetSkillStatus(sweep, StatusKind.Slow, 3f, 0f);
        SetSkillStatus(fan, StatusKind.Slow, 2.5f, 0f);
        SetSkillStatus(blast, StatusKind.Burn, 4f, 0.12f);
        SetSkillStatus(fireball, StatusKind.Burn, 4f, 0.15f);
        SetSkillStatus(storm, StatusKind.Weaken, 5f, 0f);
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

    // 노드 단계 수는 갈래마다 [1, 3, 3, 1]로 고정.
    private static void MakeJob(int jobIndex, string displayName, string branchA, string branchB, Object[] skills, string[] nodeNames, JobNodeEffect[] effects, float[] values)
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
        SerializedProperty branches = Find(data, "branchNames");
        branches.arraySize = 2;
        branches.GetArrayElementAtIndex(0).stringValue = branchA;
        branches.GetArrayElementAtIndex(1).stringValue = branchB;
        SetRefArray(data, "skills", skills);
        int[] ranks = { 1, 3, 3, 1 };
        SerializedProperty nodes = Find(data, "nodes");
        nodes.arraySize = JobDefinition.NodeCount;
        for (int i = 0; i < JobDefinition.NodeCount; i++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            node.FindPropertyRelative("displayName").stringValue = nodeNames[i];
            node.FindPropertyRelative("effect").enumValueIndex = (int)effects[i];
            node.FindPropertyRelative("value").floatValue = values[i];
            node.FindPropertyRelative("maxRank").intValue = ranks[i % JobDefinition.BranchSize];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(job);
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

        // 범위 스킬과 강화 스킬을 쓸 때 발밑에 잠깐 보이는 원
        SpriteRenderer areaFx = AddSprite(root.transform, "AreaFx", LoadSprite(SpriteRoot + "/Fx/Circle.png"), -10);
        areaFx.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        areaFx.enabled = false;
        Object[] jobs = new Object[JobAssetNames.Length];
        for (int i = 0; i < jobs.Length; i++)
        {
            jobs[i] = LoadRequired<JobDefinition>(JobDataFolder + "/" + JobAssetNames[i] + ".asset");
        }
        PlayerSkills skills = root.AddComponent<PlayerSkills>();
        SerializedObject skillsData = new SerializedObject(skills);
        Find(skillsData, "controller").objectReferenceValue = controller;
        Find(skillsData, "inventory").objectReferenceValue = inventory;
        Find(skillsData, "areaFxRenderer").objectReferenceValue = areaFx;
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
        PartsType[] weaponTypes = { PartsType.Sword, PartsType.Spear, PartsType.Bow, PartsType.Staff };
        SerializedObject data = new SerializedObject(balance);
        SerializedProperty weaponCounts = Find(data, "weaponPartsCounts");
        weaponCounts.arraySize = weaponTypes.Length;
        StringBuilder counts = new StringBuilder("장비 외형 수(검, 창, 활, 지팡이, 방패, 투구, 갑옷):");
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
            doorViews[i] = BuildDoor(doorNames[i], doorPositions[i], i % 2 == 0, pixel, font);
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

    // 문: 방 가장자리의 색 띠(길이 2.6, 두께 0.3)와 문 너머 방 이름. horizontal이면 위아래 벽의 문이다.
    private static DoorView BuildDoor(string name, Vector2 position, bool horizontal, Sprite pixel, TMP_FontAsset font)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;
        SpriteRenderer frame = AddSprite(root.transform, "Frame", pixel, -800);
        // Pixel 스프라이트는 한 변 0.04 유닛
        if (horizontal)
        {
            frame.transform.localScale = new Vector3(2.6f / 0.04f, 0.3f / 0.04f, 1f);
        }
        else
        {
            frame.transform.localScale = new Vector3(0.3f / 0.04f, 2.6f / 0.04f, 1f);
        }

        // 이름은 방 안쪽으로 0.55 들여 쓴다.
        Vector2 inward = -position.normalized * 0.55f;
        TextMeshPro label = AddWorldLabel(root.transform, "Label", font, string.Empty, inward, 3f);
        DoorView door = root.AddComponent<DoorView>();
        SetRef(door, "frameRenderer", frame);
        SetRef(door, "label", label);
        return door;
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

    // 미니맵. 기준 해상도 1920x1080에서 오른쪽 위, 가장자리에서 40. 크기 170x170(화면 x 1710~1880, y 870~1040).
    // 칸은 26x26, 간격 4, 안쪽 여백 12. 5x5 격자이며 아래 줄이 y=0이다.
    private static void BuildMinimap(Transform canvas, FloorDirector floor, Sprite pixel)
    {
        GameObject panelObject = new GameObject("Minimap");
        panelObject.transform.SetParent(canvas, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.5f);
        panelImage.raycastTarget = false;
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-40f, -40f);
        panel.sizeDelta = new Vector2(170f, 170f);

        // 현재 방 표시(칸보다 조금 큰 흰 테두리). 칸보다 먼저 만들어 칸 뒤에 그려지게 한다.
        Image marker = AddMinimapImage(panel, "Current", pixel, 34f);
        marker.color = Color.white;

        Object[] cells = new Object[FloorDirector.GridSize * FloorDirector.GridSize];
        for (int y = 0; y < FloorDirector.GridSize; y++)
        {
            for (int x = 0; x < FloorDirector.GridSize; x++)
            {
                Image cell = AddMinimapImage(panel, "Cell" + x + "_" + y, pixel, 26f);
                cell.rectTransform.anchoredPosition = new Vector2(25f + 30f * x, 25f + 30f * y);
                cell.enabled = false;
                cells[y * FloorDirector.GridSize + x] = cell;
            }
        }

        MinimapUi minimap = panelObject.AddComponent<MinimapUi>();
        SerializedObject data = new SerializedObject(minimap);
        Find(data, "floor").objectReferenceValue = floor;
        SetRefArray(data, "cells", cells);
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

        // 출발 지점
        Vector2 zoneCenter = new Vector2(0f, 2.4f);
        float zoneRadius = 1.2f;
        GameObject zone = new GameObject("StartZone");
        zone.transform.position = zoneCenter;
        SpriteRenderer zoneRenderer = zone.AddComponent<SpriteRenderer>();
        zoneRenderer.sprite = LoadSprite(SpriteRoot + "/Fx/Circle.png");
        zoneRenderer.color = new Color(1f, 0.9f, 0.4f, 0.35f);
        zoneRenderer.sortingOrder = -900;
        float zoneScale = zoneRadius * 2f / 0.64f;
        zone.transform.localScale = new Vector3(zoneScale, zoneScale, 1f);

        GameObject zoneLabelObject = new GameObject("StartZoneLabel");
        zoneLabelObject.transform.position = zoneCenter + new Vector2(0f, zoneRadius + 0.35f);
        TextMeshPro zoneLabel = zoneLabelObject.AddComponent<TextMeshPro>();
        zoneLabel.font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        zoneLabel.fontSize = 3.2f;
        zoneLabel.alignment = TextAlignmentOptions.Center;
        zoneLabel.textWrappingMode = TextWrappingModes.NoWrap;
        zoneLabel.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
        zoneLabel.text = "출발 지점 (호스트가 F)";
        zoneLabelObject.GetComponent<MeshRenderer>().sortingOrder = 400;

        SerializedObject hubData = new SerializedObject(hub);
        Find(hubData, "hasStartZone").boolValue = true;
        Find(hubData, "startZoneCenter").vector2Value = zoneCenter;
        Find(hubData, "startZoneRadius").floatValue = zoneRadius;
        hubData.ApplyModifiedPropertiesWithoutUndo();

        BuildLobbyPanel(canvasObject.transform, hub);

        // 마을 시설 세 곳(해금 상인, 도감, 설정). 아래쪽에 나란히 두고, 가까이에서 F로 연다.
        Sprite townCircle = LoadSprite(SpriteRoot + "/Fx/Circle.png");
        TMP_FontAsset townFont = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);
        Vector2[] stationPositions = { new Vector2(-4.5f, -2.8f), new Vector2(0f, -3.4f), new Vector2(4.5f, -2.8f) };
        string[] stationNames = { "해금 상인", "도감", "설정" };
        Color[] stationColors =
        {
            new Color(0.7f, 0.5f, 1f, 0.6f), new Color(0.5f, 0.85f, 0.6f, 0.6f), new Color(0.6f, 0.75f, 1f, 0.6f)
        };
        for (int i = 0; i < stationPositions.Length; i++)
        {
            BuildMarker("Station" + i, stationPositions[i], townCircle, stationColors[i], 1.6f, stationNames[i] + "  (F)", townFont);
        }
        SetRef(hub, "metaUi", BuildMetaPanel(canvasObject.transform, hub, stationPositions));

        SaveAndClose(scene, previousScene);
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
        panelImage.color = new Color(0f, 0f, 0f, 0.88f);
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
    // 노드맵 패널: 화면 가운데에서 위로 40, 크기 620x520(x 650~1270이라 가방 552와 상점 1400 사이).
    // 안쪽 요소는 패널 왼쪽 위 기준. 갈래 이름 y 60, 노드 280x62는 y 92부터 간격 70, 갈래 사이 x 간격 300. 설명은 y 380부터.
    private static SkillUi BuildSkillUi(Transform canvas, GameHub hub)
    {
        Sprite pixel = LoadSprite(SpriteRoot + "/Fx/Pixel.png");
        TMP_FontAsset font = LoadRequired<TMP_FontAsset>(KoreanFontAssetPath);

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
        for (int i = 0; i < keys.Length; i++)
        {
            Image frame = AddBottomCenterImage(root, "Skill" + keys[i], pixel, new Vector2(-76f + 76f * i, 40f), new Vector2(64f, 64f), new Color(0f, 0f, 0f, 0.35f));
            TextMeshProUGUI name = AddText(frame.transform, "Name", font, string.Empty, 14f, true);
            name.alignment = TextAlignmentOptions.Bottom;
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
        }
        AddBottomCenterImage(root, "XpBack", pixel, new Vector2(0f, 112f), new Vector2(216f, 6f), new Color(0f, 0f, 0f, 0.6f));
        Image xpFill = AddBottomCenterImage(root, "XpFill", pixel, new Vector2(0f, 112f), new Vector2(216f, 6f), new Color(0.6f, 0.9f, 0.5f));
        xpFill.type = Image.Type.Filled;
        xpFill.fillMethod = Image.FillMethod.Horizontal;
        xpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        xpFill.fillAmount = 0f;
        TextMeshProUGUI levelLabel = AddHudText(root, "LevelLabel", font, 0.5f, new Vector2(0f, 124f), new Vector2(600f, 26f), 20f);
        levelLabel.alignment = TextAlignmentOptions.Center;

        GameObject panelObject = new GameObject("TreePanel");
        panelObject.transform.SetParent(root, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = pixel;
        panelImage.color = new Color(0f, 0f, 0f, 0.85f);
        RectTransform panel = panelImage.rectTransform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = new Vector2(0f, 40f);
        panel.sizeDelta = new Vector2(620f, 520f);

        TextMeshProUGUI title = AddPanelText(panel, "Title", font, string.Empty, new Vector2(20f, 14f), new Vector2(580f, 34f), 26f, false);
        Object[] branchLabels = new Object[2];
        Object[] nodeViews = new Object[JobDefinition.NodeCount];
        for (int branch = 0; branch < 2; branch++)
        {
            float x = 20f + 300f * branch;
            branchLabels[branch] = AddPanelText(panel, "Branch" + branch, font, string.Empty, new Vector2(x, 60f), new Vector2(280f, 26f), 20f, false);
            for (int step = 0; step < JobDefinition.BranchSize; step++)
            {
                int index = branch * JobDefinition.BranchSize + step;
                GameObject nodeObject = new GameObject("Node" + index);
                nodeObject.transform.SetParent(panel, false);
                Image nodeImage = nodeObject.AddComponent<Image>();
                nodeImage.sprite = pixel;
                PlaceTopLeft(nodeImage.rectTransform, new Vector2(x, 92f + 70f * step), new Vector2(280f, 62f));
                TextMeshProUGUI nodeLabel = AddText(nodeObject.transform, "Label", font, string.Empty, 20f, false);
                nodeLabel.alignment = TextAlignmentOptions.Center;
                Stretch(nodeLabel.rectTransform, 6f);

                SkillNodeView view = nodeObject.AddComponent<SkillNodeView>();
                SerializedObject viewData = new SerializedObject(view);
                Find(viewData, "owner").objectReferenceValue = skillUi;
                Find(viewData, "frame").objectReferenceValue = nodeImage;
                Find(viewData, "label").objectReferenceValue = nodeLabel;
                Find(viewData, "index").intValue = index;
                viewData.ApplyModifiedPropertiesWithoutUndo();
                nodeViews[index] = view;
            }
        }
        TextMeshProUGUI detail = AddPanelText(panel, "Detail", font, string.Empty, new Vector2(20f, 380f), new Vector2(580f, 126f), 18f, true);

        SerializedObject data = new SerializedObject(skillUi);
        Find(data, "hub").objectReferenceValue = hub;
        SetRefArray(data, "skillFrames", frames);
        SetRefArray(data, "skillFills", fills);
        SetRefArray(data, "skillLabels", labels);
        Find(data, "levelLabel").objectReferenceValue = levelLabel;
        Find(data, "xpFill").objectReferenceValue = xpFill;
        Find(data, "treePanel").objectReferenceValue = panelObject;
        Find(data, "titleLabel").objectReferenceValue = title;
        SetRefArray(data, "branchLabels", branchLabels);
        SetRefArray(data, "nodeViews", nodeViews);
        Find(data, "detailLabel").objectReferenceValue = detail;
        data.ApplyModifiedPropertiesWithoutUndo();

        panelObject.SetActive(false);
        return skillUi;
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
