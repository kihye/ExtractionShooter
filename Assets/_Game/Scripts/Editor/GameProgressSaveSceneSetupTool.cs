using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameProgressSaveSceneSetupTool
{
    private const string BaseScenePath = "Assets/_Game/Scenes/BaseScene.unity";
    private const string RunScenePath = "Assets/_Game/Scenes/PrototypeScene.unity";
    private const string TitleScenePath = "Assets/_Game/Scenes/TitleScene.unity";
    private const string RegistryPath = "Assets/_Game/Data/Items/ItemDefinitionRegistry.asset";
    private const string SceneMapRegistryPath = "Assets/_Game/Data/Runtime/SceneMapRegistry.asset";
    private const string RunWorldPrefabRegistryPath = "Assets/_Game/Data/Runtime/RunWorldPrefabRegistry.asset";
    private const string GameSessionStatePath = "Assets/_Game/Data/Runtime/GameSessionState.asset";
    private const string ItemPickupPrefabPath = "Assets/_Game/Prefabs/Items/ItemPickup.prefab";
    private const string AmmoPickupPrefabPath = "Assets/_Game/Prefabs/Items/AmmoPickup.prefab";
    private const string ItemSearchFolder = "Assets/_Game/Items";
    private const string PretendardFontPath = "Assets/_Resources/Fonts/Pretendard-Medium SDF.asset";

    private static readonly Color WindowColor = new Color(0.05f, 0.06f, 0.07f, 0.94f);
    private static readonly Color PanelColor = new Color(0.10f, 0.12f, 0.14f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.24f, 0.30f, 1f);
    private static readonly Color DangerButtonColor = new Color(0.42f, 0.20f, 0.18f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.70f, 0.76f, 0.82f, 1f);

    [MenuItem("Tools/Extraction Shooter/Setup Save Load UI")]
    public static void SetupSaveLoadUiAndSave()
    {
        ItemDefinitionRegistry registry = EnsureRegistry();
        SceneMapRegistry sceneMapRegistry = EnsureSceneMapRegistry();
        RunWorldPrefabRegistry runWorldPrefabRegistry = EnsureRunWorldPrefabRegistry();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardFontPath);

        Scene scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("BaseScene is missing Canvas.");
            return;
        }

        RectTransform gameplayRoot = EnsureGameplayRoot(canvas.transform);
        SaveLoadUI saveLoadUi = EnsureController<SaveLoadUI>(gameplayRoot, "SaveLoadUI");
        BuildSaveLoadUi(saveLoadUi, registry, sceneMapRegistry, runWorldPrefabRegistry, font);
        AssignBaseSceneReference(saveLoadUi);
        AssignSceneFlowReferences(Object.FindFirstObjectByType<SceneFlowController>(), registry, sceneMapRegistry, runWorldPrefabRegistry);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        registry = AssetDatabase.LoadAssetAtPath<ItemDefinitionRegistry>(RegistryPath);
        sceneMapRegistry = AssetDatabase.LoadAssetAtPath<SceneMapRegistry>(SceneMapRegistryPath);
        runWorldPrefabRegistry = AssetDatabase.LoadAssetAtPath<RunWorldPrefabRegistry>(RunWorldPrefabRegistryPath);

        Scene runScene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
        AssignRunSceneIds();
        SceneFlowController runSceneFlowController = Object.FindFirstObjectByType<SceneFlowController>();
        AssignSceneFlowReferences(runSceneFlowController, registry, sceneMapRegistry, runWorldPrefabRegistry);
        AssignRunRestartReferences(runSceneFlowController);
        EnsureRunSaveLoadUi(registry, sceneMapRegistry, runWorldPrefabRegistry, font);
        EditorSceneManager.MarkSceneDirty(runScene);
        EditorSceneManager.SaveScene(runScene);

        registry = AssetDatabase.LoadAssetAtPath<ItemDefinitionRegistry>(RegistryPath);
        sceneMapRegistry = AssetDatabase.LoadAssetAtPath<SceneMapRegistry>(SceneMapRegistryPath);
        Scene titleScene = EnsureTitleScene(registry, sceneMapRegistry, font);
        EditorSceneManager.MarkSceneDirty(titleScene);
        EditorSceneManager.SaveScene(titleScene);

        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Save/Load and Title scene setup completed.");
    }

    private static ItemDefinitionRegistry EnsureRegistry()
    {
        EnsureFolder("Assets/_Game/Data", "Items");

        ItemDefinitionRegistry registry = AssetDatabase.LoadAssetAtPath<ItemDefinitionRegistry>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<ItemDefinitionRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemSearchFolder });
        List<ItemDefinition> definitions = new List<ItemDefinition>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (definition != null && !definitions.Contains(definition))
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort((left, right) => string.CompareOrdinal(left.ItemId, right.ItemId));

        SerializedObject serializedObject = new SerializedObject(registry);
        SerializedProperty items = serializedObject.FindProperty("itemDefinitions");
        items.arraySize = definitions.Count;
        for (int i = 0; i < definitions.Count; i++)
        {
            items.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);

        List<string> issues = registry.ValidateRegistry();
        foreach (string issue in issues)
        {
            Debug.LogWarning($"ItemDefinitionRegistry: {issue}", registry);
        }

        return registry;
    }

    private static SceneMapRegistry EnsureSceneMapRegistry()
    {
        EnsureFolder("Assets/_Game/Data", "Runtime");

        SceneMapRegistry registry = AssetDatabase.LoadAssetAtPath<SceneMapRegistry>(SceneMapRegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<SceneMapRegistry>();
            AssetDatabase.CreateAsset(registry, SceneMapRegistryPath);
        }

        SerializedObject serializedObject = new SerializedObject(registry);
        SerializedProperty maps = serializedObject.FindProperty("maps");
        maps.arraySize = 3;
        SetMapEntry(maps.GetArrayElementAtIndex(0), "title", "Title", "TitleScene", GameSceneKind.Title, Vector3.zero, 0f);
        SetMapEntry(maps.GetArrayElementAtIndex(1), "base", "Base", "BaseScene", GameSceneKind.Base, Vector3.zero, 0f);
        SetMapEntry(maps.GetArrayElementAtIndex(2), "prototype_run", "Prototype Run", "PrototypeScene", GameSceneKind.Run, Vector3.up, 0f);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
        return registry;
    }

    private static void SetMapEntry(SerializedProperty property, string mapId, string displayName, string sceneName, GameSceneKind sceneKind, Vector3 fallbackPosition, float fallbackYaw)
    {
        property.FindPropertyRelative("mapId").stringValue = mapId;
        property.FindPropertyRelative("displayName").stringValue = displayName;
        property.FindPropertyRelative("sceneName").stringValue = sceneName;
        property.FindPropertyRelative("sceneKind").enumValueIndex = (int)sceneKind;
        property.FindPropertyRelative("fallbackSpawnPosition").vector3Value = fallbackPosition;
        property.FindPropertyRelative("fallbackSpawnYaw").floatValue = fallbackYaw;
    }

    private static RunWorldPrefabRegistry EnsureRunWorldPrefabRegistry()
    {
        EnsureFolder("Assets/_Game/Data", "Runtime");

        RunWorldPrefabRegistry registry = AssetDatabase.LoadAssetAtPath<RunWorldPrefabRegistry>(RunWorldPrefabRegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<RunWorldPrefabRegistry>();
            AssetDatabase.CreateAsset(registry, RunWorldPrefabRegistryPath);
        }

        SerializedObject serializedObject = new SerializedObject(registry);
        SetProperty(serializedObject, "itemPickupPrefab", AssetDatabase.LoadAssetAtPath<ItemPickup>(ItemPickupPrefabPath));
        SetProperty(serializedObject, "ammoPickupPrefab", AssetDatabase.LoadAssetAtPath<AmmoPickup>(AmmoPickupPrefabPath));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
        return registry;
    }

    private static void BuildSaveLoadUi(
        SaveLoadUI saveLoadUi,
        ItemDefinitionRegistry registry,
        SceneMapRegistry sceneMapRegistry,
        RunWorldPrefabRegistry runWorldPrefabRegistry,
        TMP_FontAsset font)
    {
        if (registry == null)
        {
            registry = LoadItemRegistry();
        }

        if (sceneMapRegistry == null)
        {
            sceneMapRegistry = LoadSceneMapRegistry();
        }

        if (runWorldPrefabRegistry == null)
        {
            runWorldPrefabRegistry = LoadRunWorldPrefabRegistry();
        }

        RectTransform root = EnsureRect(saveLoadUi.gameObject);
        SetStretch(root);
        saveLoadUi.gameObject.SetActive(true);
        SetUiLayerRecursive(saveLoadUi.gameObject);

        Button openButton = EnsureButton(root, "OpenSaveLoadButton", "저장/불러오기", new Vector2(-160f, -24f), new Vector2(190f, 42f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), ButtonColor, font);

        RectTransform panel = EnsureChildRect(root, "SaveLoadWindow");
        SetAnchored(panel, Vector2.zero, new Vector2(560f, 390f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        TMP_Text title = EnsureText(panel, "TitleText", "저장 / 불러오기", 28f, TextAlignmentOptions.MidlineLeft, TextColor, font);
        SetAnchored(title.rectTransform, new Vector2(28f, -34f), new Vector2(380f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

        Button closeButton = EnsureButton(panel, "CloseButton", "X", new Vector2(-28f, -34f), new Vector2(42f, 34f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), ButtonColor, font);

        TMP_Text lastSaveText = EnsureText(panel, "LastSaveText", "저장 파일 없음", 18f, TextAlignmentOptions.MidlineLeft, MutedTextColor, font);
        SetAnchored(lastSaveText.rectTransform, new Vector2(30f, -96f), new Vector2(500f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));

        Button saveButton = EnsureButton(panel, "SaveButton", "저장", new Vector2(-180f, -168f), new Vector2(150f, 52f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), ButtonColor, font);
        Button loadButton = EnsureButton(panel, "LoadButton", "불러오기", new Vector2(0f, -168f), new Vector2(150f, 52f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), ButtonColor, font);
        Button titleButton = EnsureButton(panel, "TitleButton", "타이틀", new Vector2(180f, -168f), new Vector2(150f, 52f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), ButtonColor, font);

        TMP_Text statusText = EnsureText(panel, "StatusText", "", 17f, TextAlignmentOptions.TopLeft, MutedTextColor, font);
        SetAnchored(statusText.rectTransform, new Vector2(30f, -248f), new Vector2(500f, 72f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        statusText.textWrappingMode = TextWrappingModes.Normal;
        statusText.overflowMode = TextOverflowModes.Truncate;

        RectTransform confirmationPanel = EnsureChildRect(panel, "ConfirmationPanel");
        SetAnchored(confirmationPanel, new Vector2(0f, -250f), new Vector2(500f, 150f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
        EnsureImage(confirmationPanel.gameObject, PanelColor);

        TMP_Text confirmationText = EnsureText(confirmationPanel, "ConfirmationText", "", 17f, TextAlignmentOptions.TopLeft, TextColor, font);
        SetStretch(confirmationText.rectTransform, new Vector2(22f, 62f), new Vector2(-22f, -18f));
        confirmationText.textWrappingMode = TextWrappingModes.Normal;
        confirmationText.overflowMode = TextOverflowModes.Truncate;

        Button confirmButton = EnsureButton(confirmationPanel, "ConfirmButton", "확인", new Vector2(-72f, 25f), new Vector2(120f, 40f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), DangerButtonColor, font);
        Button cancelButton = EnsureButton(confirmationPanel, "CancelButton", "취소", new Vector2(72f, 25f), new Vector2(120f, 40f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), ButtonColor, font);
        confirmationPanel.gameObject.SetActive(false);
        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(saveLoadUi);
        SetProperty(serializedObject, "sessionState", FindSessionState());
        SetProperty(serializedObject, "itemRegistry", registry);
        SetProperty(serializedObject, "sceneMapRegistry", sceneMapRegistry);
        SetProperty(serializedObject, "runWorldPrefabRegistry", runWorldPrefabRegistry);
        SetProperty(serializedObject, "uiModeController", canvasModeController);
        SetProperty(serializedObject, "sceneFlowController", Object.FindFirstObjectByType<SceneFlowController>());
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "openButton", openButton);
        SetProperty(serializedObject, "saveButton", saveButton);
        SetProperty(serializedObject, "loadButton", loadButton);
        SetProperty(serializedObject, "titleButton", titleButton);
        SetProperty(serializedObject, "closeButton", closeButton);
        SetProperty(serializedObject, "lastSaveText", lastSaveText);
        SetProperty(serializedObject, "statusText", statusText);
        SetProperty(serializedObject, "confirmationPanel", confirmationPanel.gameObject);
        SetProperty(serializedObject, "confirmationText", confirmationText);
        SetProperty(serializedObject, "confirmButton", confirmButton);
        SetProperty(serializedObject, "cancelButton", cancelButton);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(saveLoadUi);
    }

    private static GameplayUiModeController canvasModeController => Object.FindFirstObjectByType<GameplayUiModeController>();

    private static GameSessionState FindSessionState()
    {
        StashInventory stashInventory = Object.FindFirstObjectByType<StashInventory>();
        return stashInventory != null ? stashInventory.SessionState : LoadSessionState();
    }

    private static void AssignBaseSceneReference(SaveLoadUI saveLoadUi)
    {
        BaseSceneReferences sceneReferences = Object.FindFirstObjectByType<BaseSceneReferences>();
        if (sceneReferences == null)
        {
            Debug.LogError("BaseScene is missing BaseSceneReferences.");
            return;
        }

        SerializedObject serializedObject = new SerializedObject(sceneReferences);
        SetProperty(serializedObject, "saveLoadUi", saveLoadUi);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sceneReferences);
    }

    private static void AssignSceneFlowReferences(
        SceneFlowController sceneFlowController,
        ItemDefinitionRegistry itemRegistry,
        SceneMapRegistry sceneMapRegistry,
        RunWorldPrefabRegistry runWorldPrefabRegistry)
    {
        if (sceneFlowController == null)
        {
            return;
        }

        if (itemRegistry == null)
        {
            itemRegistry = LoadItemRegistry();
        }

        if (sceneMapRegistry == null)
        {
            sceneMapRegistry = LoadSceneMapRegistry();
        }

        if (runWorldPrefabRegistry == null)
        {
            runWorldPrefabRegistry = LoadRunWorldPrefabRegistry();
        }

        SerializedObject serializedObject = new SerializedObject(sceneFlowController);
        SetProperty(serializedObject, "sessionState", FindSessionState());
        SetProperty(serializedObject, "itemRegistry", itemRegistry);
        SetProperty(serializedObject, "sceneMapRegistry", sceneMapRegistry);
        SetProperty(serializedObject, "runWorldPrefabRegistry", runWorldPrefabRegistry);
        serializedObject.FindProperty("baseSceneName").stringValue = "BaseScene";
        serializedObject.FindProperty("runSceneName").stringValue = "PrototypeScene";
        serializedObject.FindProperty("titleSceneName").stringValue = "TitleScene";
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sceneFlowController);
    }

    private static void AssignRunSceneIds()
    {
        AssignIds<DummyEnemy>("enemy");
        AssignIds<ItemPickup>("item_pickup");
        AssignIds<AmmoPickup>("ammo_pickup");
    }

    private static void AssignRunRestartReferences(SceneFlowController sceneFlowController)
    {
        foreach (RunRestartController restartController in Object.FindObjectsByType<RunRestartController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedObject serializedObject = new SerializedObject(restartController);
            SetProperty(serializedObject, "sceneFlowController", sceneFlowController);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(restartController);
        }
    }

    private static void AssignIds<T>(string prefix) where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            ScenePersistentId sceneId = component.GetComponent<ScenePersistentId>();
            if (sceneId == null)
            {
                sceneId = component.gameObject.AddComponent<ScenePersistentId>();
            }

            sceneId.EnsureId($"{prefix}_{SanitizeId(component.gameObject.name)}_{i + 1:00}");
            EditorUtility.SetDirty(sceneId);
        }
    }

    private static void EnsureRunSaveLoadUi(
        ItemDefinitionRegistry registry,
        SceneMapRegistry sceneMapRegistry,
        RunWorldPrefabRegistry runWorldPrefabRegistry,
        TMP_FontAsset font)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        RectTransform gameplayRoot = EnsureGameplayRoot(canvas.transform);
        SaveLoadUI saveLoadUi = EnsureController<SaveLoadUI>(gameplayRoot, "SaveLoadUI");
        BuildSaveLoadUi(saveLoadUi, registry, sceneMapRegistry, runWorldPrefabRegistry, font);
    }

    private static Scene EnsureTitleScene(ItemDefinitionRegistry itemRegistry, SceneMapRegistry sceneMapRegistry, TMP_FontAsset font)
    {
        EnsureFolder("Assets/_Game", "Scenes");
        if (itemRegistry == null)
        {
            itemRegistry = LoadItemRegistry();
        }

        if (sceneMapRegistry == null)
        {
            sceneMapRegistry = LoadSceneMapRegistry();
        }

        RunWorldPrefabRegistry runWorldPrefabRegistry = LoadRunWorldPrefabRegistry();

        Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) != null
            ? EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = GameObject.Find("TitleSceneRoot");
        if (root == null)
        {
            root = new GameObject("TitleSceneRoot");
        }

        SceneFlowController sceneFlowController = EnsureComponent<SceneFlowController>(root);
        AssignSceneFlowReferences(sceneFlowController, itemRegistry, sceneMapRegistry, runWorldPrefabRegistry);

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        RectTransform canvasRect = EnsureRect(canvas.gameObject);
        RectTransform panel = EnsureChildRect(canvasRect, "TitlePanel");
        SetAnchored(panel, Vector2.zero, new Vector2(820f, 620f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        TMP_Text title = EnsureText(panel, "TitleText", "EXTRACTION SHOOTER", 44f, TextAlignmentOptions.Center, TextColor, font);
        SetAnchored(title.rectTransform, new Vector2(0f, -64f), new Vector2(720f, 70f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

        Button newGameButton = EnsureButton(panel, "NewGameButton", "새 게임", new Vector2(-230f, -176f), new Vector2(220f, 56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), ButtonColor, font);
        Button loadButton = EnsureButton(panel, "LoadButton", "불러오기", new Vector2(0f, -176f), new Vector2(220f, 56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), ButtonColor, font);
        Button quitButton = EnsureButton(panel, "QuitButton", "종료", new Vector2(230f, -176f), new Vector2(220f, 56f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), DangerButtonColor, font);

        TMP_Text saveInfoText = EnsureText(panel, "SaveInfoText", "저장 파일 없음", 22f, TextAlignmentOptions.TopLeft, TextColor, font);
        SetAnchored(saveInfoText.rectTransform, new Vector2(0f, -350f), new Vector2(700f, 190f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
        saveInfoText.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text statusText = EnsureText(panel, "StatusText", "", 18f, TextAlignmentOptions.TopLeft, MutedTextColor, font);
        SetAnchored(statusText.rectTransform, new Vector2(0f, -514f), new Vector2(700f, 54f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));

        TitleSceneUI titleUi = EnsureComponent<TitleSceneUI>(panel.gameObject);
        SerializedObject titleSerialized = new SerializedObject(titleUi);
        SetProperty(titleSerialized, "sessionState", LoadSessionState());
        SetProperty(titleSerialized, "itemRegistry", LoadItemRegistry());
        SetProperty(titleSerialized, "sceneMapRegistry", LoadSceneMapRegistry());
        SetProperty(titleSerialized, "sceneFlowController", sceneFlowController);
        SetProperty(titleSerialized, "newGameButton", newGameButton);
        SetProperty(titleSerialized, "loadButton", loadButton);
        SetProperty(titleSerialized, "quitButton", quitButton);
        SetProperty(titleSerialized, "saveInfoText", saveInfoText);
        SetProperty(titleSerialized, "statusText", statusText);
        titleSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(titleUi);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) == null)
        {
            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        return scene;
    }

    private static ItemDefinitionRegistry LoadItemRegistry()
    {
        return AssetDatabase.LoadAssetAtPath<ItemDefinitionRegistry>(RegistryPath);
    }

    private static SceneMapRegistry LoadSceneMapRegistry()
    {
        return AssetDatabase.LoadAssetAtPath<SceneMapRegistry>(SceneMapRegistryPath);
    }

    private static RunWorldPrefabRegistry LoadRunWorldPrefabRegistry()
    {
        return AssetDatabase.LoadAssetAtPath<RunWorldPrefabRegistry>(RunWorldPrefabRegistryPath);
    }

    private static GameSessionState LoadSessionState()
    {
        return AssetDatabase.LoadAssetAtPath<GameSessionState>(GameSessionStatePath);
    }

    private static void EnsureBuildSettings()
    {
        string[] desiredScenes =
        {
            TitleScenePath,
            BaseScenePath,
            RunScenePath
        };

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in desiredScenes)
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            bool alreadyAdded = false;
            foreach (string path in desiredScenes)
            {
                if (existing.path == path)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                scenes.Add(existing);
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static string SanitizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "object";
        }

        return value.Replace(" ", "_").Replace("(", "").Replace(")", "").ToLowerInvariant();
    }

    private static RectTransform EnsureGameplayRoot(Transform canvasTransform)
    {
        RectTransform gameplayRoot = EnsureChildRect(canvasTransform, "GameplayUI");
        SetStretch(gameplayRoot);
        gameplayRoot.gameObject.SetActive(true);
        return gameplayRoot;
    }

    private static T EnsureController<T>(Transform parent, string name) where T : Component
    {
        RectTransform rectTransform = EnsureChildRect(parent, name);
        rectTransform.SetParent(parent, false);
        SetStretch(rectTransform);
        rectTransform.gameObject.SetActive(true);
        return EnsureComponent<T>(rectTransform.gameObject);
    }

    private static Button EnsureButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Color color, TMP_FontAsset font)
    {
        RectTransform rect = EnsureChildRect(parent, name);
        SetAnchored(rect, anchoredPosition, size, anchorMin, anchorMax, pivot);
        Image image = EnsureImage(rect.gameObject, color);
        Button button = EnsureComponent<Button>(rect.gameObject);
        button.targetGraphic = image;

        TMP_Text labelText = EnsureText(rect, "Label", label, 16f, TextAlignmentOptions.Center, TextColor, font);
        SetStretch(labelText.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
    {
        RectTransform rect = EnsureChildRect(parent, name);
        TextMeshProUGUI textComponent = EnsureComponent<TextMeshProUGUI>(rect.gameObject);
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.raycastTarget = false;
        if (font != null)
        {
            textComponent.font = font;
            if (font.material != null)
            {
                textComponent.fontSharedMaterial = font.material;
            }
        }

        EditorUtility.SetDirty(textComponent);
        return textComponent;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = EnsureComponent<Image>(gameObject);
        image.color = color;
        image.raycastTarget = true;
        EditorUtility.SetDirty(image);
        return image;
    }

    private static RectTransform EnsureRect(GameObject gameObject)
    {
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        return rectTransform != null ? rectTransform : gameObject.AddComponent<RectTransform>();
    }

    private static RectTransform EnsureChildRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        RectTransform rectTransform;
        if (existing == null)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            rectTransform = child.GetComponent<RectTransform>();
        }
        else
        {
            rectTransform = existing.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = existing.gameObject.AddComponent<RectTransform>();
            }
        }

        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        SetUiLayerRecursive(rectTransform.gameObject);
        return rectTransform;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        EditorUtility.SetDirty(rect);
    }

    private static void SetStretch(RectTransform rect)
    {
        SetStretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        EditorUtility.SetDirty(rect);
    }

    private static void SetProperty(SerializedObject serializedObject, string fieldName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"{serializedObject.targetObject.name} is missing serialized field '{fieldName}'.");
            return;
        }

        if (value == null && IsRequiredAssetReference(fieldName))
        {
            Debug.LogWarning($"{serializedObject.targetObject.name}.{fieldName} was assigned null.", serializedObject.targetObject);
        }

        property.objectReferenceValue = value;
    }

    private static bool IsRequiredAssetReference(string fieldName)
    {
        return fieldName == "sessionState"
            || fieldName == "itemRegistry"
            || fieldName == "sceneMapRegistry"
            || fieldName == "runWorldPrefabRegistry";
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void SetUiLayerRecursive(GameObject gameObject)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            gameObject.layer = uiLayer;
        }

        foreach (Transform child in gameObject.transform)
        {
            SetUiLayerRecursive(child.gameObject);
        }
    }
}
