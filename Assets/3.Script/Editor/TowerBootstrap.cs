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
        SetupLayers();
        SetupRendererSorting();
        AssetDatabase.ImportAsset(SpriteRoot, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

        EnsureKoreanFont();
        MonsterDefinition[] definitions = BuildMonsterDefinitions();
        GameObject playerPrefab = BuildPlayerPrefab();
        GameObject monsterPrefab = BuildMonsterPrefab();
        Projectile projectilePrefab = BuildProjectilePrefab();
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

        if (previousScene.isDirty)
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

        return SavePrefab(root, "Player");
    }

    private static GameObject BuildMonsterPrefab()
    {
        GameObject root = new GameObject("Monster");
        root.layer = GameLayers.Monster;

        SpriteRenderer telegraph = AddSprite(root.transform, "Telegraph", LoadSprite(SpriteRoot + "/Fx/Circle.png"), -20);
        telegraph.enabled = false;
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
        SetRef(controller, "hpBarRoot", hpBarRoot);
        SetRef(controller, "hpBarFill", barFill.transform);

        return SavePrefab(root, "Monster");
    }

    private static Projectile BuildProjectilePrefab()
    {
        GameObject root = new GameObject("Projectile");
        SpriteRenderer sprite = AddSprite(root.transform, "Sprite", LoadSprite(SpriteRoot + "/Fx/Spore.png"), 0);
        sprite.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        SpriteRenderer shadow = AddSprite(root.transform, "Shadow", LoadSprite(ShadowSpritePath), -10);
        shadow.color = ShadowColor;
        shadow.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        root.AddComponent<Projectile>();
        return SavePrefab(root, "Projectile").GetComponent<Projectile>();
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

        hud = BuildHud(out canvasObject, out waveLabelObject);

        SerializedObject hubData = new SerializedObject(hub);
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
        SetRef(hud, "monsterDirector", director);

        SaveAndClose(scene, previousScene);
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

        // 로비는 버튼을 누르므로 UI 입력 처리가 필요하다.
        canvasObject.AddComponent<GraphicRaycaster>();
        GameObject eventSystemObject = new GameObject("EventSystem");
        EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();

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
        Find(hubData, "eventSystem").objectReferenceValue = eventSystem;
        Find(hubData, "hasStartZone").boolValue = true;
        Find(hubData, "startZoneCenter").vector2Value = zoneCenter;
        Find(hubData, "startZoneRadius").floatValue = zoneRadius;
        hubData.ApplyModifiedPropertiesWithoutUndo();

        BuildLobbyPanel(canvasObject.transform, hub);
        SaveAndClose(scene, previousScene);
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

        TextMeshProUGUI waveLabel = AddText(parent, "WaveLabel", LoadRequired<TMP_FontAsset>(KoreanFontAssetPath), "WAVE 0", 40f, false);
        waveLabel.alignment = TextAlignmentOptions.Center;
        waveLabelObject = waveLabel.gameObject;
        RectTransform waveRect = waveLabel.rectTransform;
        waveRect.anchorMin = new Vector2(0.5f, 1f);
        waveRect.anchorMax = new Vector2(0.5f, 1f);
        waveRect.pivot = new Vector2(0.5f, 1f);
        waveRect.anchoredPosition = new Vector2(0f, -30f);
        waveRect.sizeDelta = new Vector2(400f, 60f);

        HudController hud = canvasObject.AddComponent<HudController>();
        SerializedObject hudData = new SerializedObject(hud);
        Find(hudData, "hpFill").objectReferenceValue = hpFill;
        Find(hudData, "manaFill").objectReferenceValue = manaFill;
        SetRefArray(hudData, "rollPips", new Object[] { pip0, pip1 });
        Find(hudData, "waveLabel").objectReferenceValue = waveLabel;
        hudData.ApplyModifiedPropertiesWithoutUndo();
        return hud;
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
