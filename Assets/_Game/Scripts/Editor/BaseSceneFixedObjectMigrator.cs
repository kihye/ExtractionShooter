using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BaseSceneFixedObjectMigrator
{
    private const string BaseScenePath = "Assets/_Game/Scenes/BaseScene.unity";
    private const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/Player.prefab";
    private const string BasePrefabFolder = "Assets/_Game/Prefabs/Base";
    private const string BaseMaterialFolder = "Assets/_Game/Materials/Base";

    private const string StorageTerminalPrefabPath = BasePrefabFolder + "/StorageTerminal.prefab";
    private const string ShopNpcPrefabPath = BasePrefabFolder + "/ShopNpc.prefab";
    private const string DeployTerminalPrefabPath = BasePrefabFolder + "/DeployTerminal.prefab";
    private const string BaseWallPrefabPath = BasePrefabFolder + "/BaseWall.prefab";

    [MenuItem("Tools/Extraction Shooter/Migrate Base Scene Fixed Objects")]
    public static void MigrateBaseSceneFixedObjectsAndSave()
    {
        EnsureFolders();
        EnsurePlayerPrefabBinder();

        Scene scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);

        Material floorMaterial = EnsureMaterial("BaseFloor", new Color(0.28f, 0.30f, 0.30f, 1f));
        Material wallMaterial = EnsureMaterial("BaseWall", new Color(0.18f, 0.18f, 0.20f, 1f));
        Material storageMaterial = EnsureMaterial("BaseStorage", new Color(0.20f, 0.65f, 0.95f, 1f));
        Material shopMaterial = EnsureMaterial("BaseShop", new Color(0.25f, 0.80f, 0.45f, 1f));
        Material deployMaterial = EnsureMaterial("BaseDeploy", new Color(0.95f, 0.72f, 0.22f, 1f));

        GameObject storagePrefab = EnsureInteractablePrefab<StorageInteractable>(StorageTerminalPrefabPath, "Storage Terminal", PrimitiveType.Cube, storageMaterial);
        GameObject shopPrefab = EnsureInteractablePrefab<ShopInteractable>(ShopNpcPrefabPath, "Shop NPC", PrimitiveType.Capsule, shopMaterial);
        GameObject deployPrefab = EnsureInteractablePrefab<DeployTerminalInteractable>(DeployTerminalPrefabPath, "Deploy Terminal", PrimitiveType.Cube, deployMaterial);
        GameObject wallPrefab = EnsureWallPrefab(wallMaterial);

        GameObject environmentRoot = EnsureSceneRoot("BaseEnvironment");
        GameObject facilitiesRoot = EnsureSceneRoot("BaseFacilities");
        GameObject spawnRoot = EnsureSceneRoot("BaseSpawn");

        EnsureFloor(environmentRoot.transform, floorMaterial);
        EnsureWall("BaseWall_North", new Vector3(0f, 1f, 6f), new Vector3(12f, 2f, 0.5f), environmentRoot.transform, wallPrefab);
        EnsureWall("BaseWall_South", new Vector3(0f, 1f, -6f), new Vector3(12f, 2f, 0.5f), environmentRoot.transform, wallPrefab);
        EnsureWall("BaseWall_East", new Vector3(6f, 1f, 0f), new Vector3(0.5f, 2f, 12f), environmentRoot.transform, wallPrefab);
        EnsureWall("BaseWall_West", new Vector3(-6f, 1f, 0f), new Vector3(0.5f, 2f, 12f), environmentRoot.transform, wallPrefab);

        GameObject storageTerminal = EnsurePrefabInstance("Storage Terminal", storagePrefab, facilitiesRoot.transform, new Vector3(-3f, 0.75f, 2f), Quaternion.identity);
        GameObject shopNpc = EnsurePrefabInstance("Shop NPC", shopPrefab, facilitiesRoot.transform, new Vector3(0f, 1f, 2.5f), Quaternion.identity);
        GameObject deployTerminal = EnsurePrefabInstance("Deploy Terminal", deployPrefab, facilitiesRoot.transform, new Vector3(3f, 0.75f, 2f), Quaternion.identity);

        Transform playerSpawnPoint = EnsureTransform("BasePlayerSpawn", spawnRoot.transform, new Vector3(0f, 1f, -3f), Quaternion.identity);
        Camera baseCamera = EnsureSceneCamera();
        EnsureEventSystem();

        BaseSceneReferences sceneReferences = EnsureBaseSceneReferences();
        AssignSceneReferences(
            sceneReferences,
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
            playerSpawnPoint,
            baseCamera,
            storageTerminal.GetComponent<StorageInteractable>(),
            shopNpc.GetComponent<ShopInteractable>(),
            deployTerminal.GetComponent<DeployTerminalInteractable>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BaseScene fixed object migration completed.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/_Game/Prefabs", "Base");
        EnsureFolder("Assets/_Game/Materials", "Base");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Material EnsureMaterial(string name, Color color)
    {
        string path = $"{BaseMaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader)
        {
            name = name,
            color = color
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject EnsureInteractablePrefab<T>(string path, string name, PrimitiveType visualType, Material material) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            return prefab;
        }

        GameObject root = new GameObject(name);
        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.6f, 1.8f, 1.6f);
        trigger.center = new Vector3(0f, 0.9f, 0f);
        root.AddComponent<T>();

        GameObject visual = GameObject.CreatePrimitive(visualType);
        visual.name = "Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, visualType == PrimitiveType.Capsule ? 1f : 0.75f, 0f);
        visual.transform.localScale = visualType == PrimitiveType.Capsule ? Vector3.one : new Vector3(1.1f, 1.5f, 1.1f);
        SetMaterial(visual, material);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return savedPrefab;
    }

    private static GameObject EnsureWallPrefab(Material material)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseWallPrefabPath);
        if (prefab != null)
        {
            return prefab;
        }

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "BaseWall";
        SetMaterial(wall, material);
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(wall, BaseWallPrefabPath);
        Object.DestroyImmediate(wall);
        return savedPrefab;
    }

    private static void EnsurePlayerPrefabBinder()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null || playerPrefab.GetComponent<PlayerRuntimeStateBinder>() != null)
        {
            return;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (contents.GetComponent<PlayerRuntimeStateBinder>() == null)
        {
            contents.AddComponent<PlayerRuntimeStateBinder>();
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        }

        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static GameObject EnsureSceneRoot(string name)
    {
        GameObject root = GameObject.Find(name);
        return root != null ? root : new GameObject(name);
    }

    private static void EnsureFloor(Transform parent, Material material)
    {
        GameObject floor = GameObject.Find("BaseFloor");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BaseFloor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        floor.transform.SetParent(parent, true);
        SetMaterialIfEmpty(floor, material);
    }

    private static void EnsureWall(string name, Vector3 position, Vector3 scale, Transform parent, GameObject prefab)
    {
        GameObject wall = GameObject.Find(name);
        if (wall == null)
        {
            wall = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
        }

        wall.transform.SetParent(parent, true);
    }

    private static GameObject EnsurePrefabInstance(string name, GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
    {
        GameObject instance = GameObject.Find(name);
        if (instance == null)
        {
            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
        }

        instance.transform.SetParent(parent, true);
        return instance;
    }

    private static Transform EnsureTransform(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        GameObject gameObject = GameObject.Find(name);
        if (gameObject == null)
        {
            gameObject = new GameObject(name);
            gameObject.transform.position = position;
            gameObject.transform.rotation = rotation;
        }

        gameObject.transform.SetParent(parent, true);
        return gameObject.transform;
    }

    private static Camera EnsureSceneCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera = cameraObject.AddComponent<Camera>();
        }

        if (camera.GetComponent<TopDownCamera>() == null)
        {
            camera.gameObject.AddComponent<TopDownCamera>();
        }

        return camera;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static BaseSceneReferences EnsureBaseSceneReferences()
    {
        BaseSceneReferences sceneReferences = Object.FindFirstObjectByType<BaseSceneReferences>();
        if (sceneReferences != null)
        {
            return sceneReferences;
        }

        GameObject baseSession = GameObject.Find("BaseSession");
        if (baseSession == null)
        {
            baseSession = new GameObject("BaseSession");
        }

        return baseSession.AddComponent<BaseSceneReferences>();
    }

    private static void AssignSceneReferences(
        BaseSceneReferences sceneReferences,
        GameObject playerPrefab,
        Transform playerSpawnPoint,
        Camera baseCamera,
        StorageInteractable storageInteractable,
        ShopInteractable shopInteractable,
        DeployTerminalInteractable deployInteractable)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameplayUiModeController uiModeController = canvas != null ? canvas.GetComponent<GameplayUiModeController>() : null;
        StorageGuiController storageGui = Object.FindFirstObjectByType<StorageGuiController>();
        ShopGuiController shopGui = Object.FindFirstObjectByType<ShopGuiController>();
        BaseLoadoutUI loadoutUi = Object.FindFirstObjectByType<BaseLoadoutUI>();
        SaveLoadUI saveLoadUi = Object.FindFirstObjectByType<SaveLoadUI>();

        SetReference(sceneReferences, "stashInventory", Object.FindFirstObjectByType<StashInventory>());
        SetReference(sceneReferences, "playerPrefab", playerPrefab);
        SetReference(sceneReferences, "playerSpawnPoint", playerSpawnPoint);
        SetReference(sceneReferences, "baseCamera", baseCamera);
        SetReference(sceneReferences, "topDownCamera", baseCamera != null ? baseCamera.GetComponent<TopDownCamera>() : null);
        SetReference(sceneReferences, "canvas", canvas);
        SetReference(sceneReferences, "uiModeController", uiModeController);
        SetReference(sceneReferences, "inventoryGui", Object.FindFirstObjectByType<InventoryGuiController>());
        SetReference(sceneReferences, "storageGui", storageGui);
        SetReference(sceneReferences, "shopGui", shopGui);
        SetReference(sceneReferences, "loadoutUi", loadoutUi);
        SetReference(sceneReferences, "saveLoadUi", saveLoadUi);
        SetReference(sceneReferences, "promptUi", Object.FindFirstObjectByType<InteractionPromptUI>());
        SetReference(sceneReferences, "storageInteractable", storageInteractable);
        SetReference(sceneReferences, "shopInteractable", shopInteractable);
        SetReference(sceneReferences, "deployInteractable", deployInteractable);

        SetReference(storageInteractable, "storageGui", storageGui);
        SetReference(shopInteractable, "shopGui", shopGui);
        SetReference(deployInteractable, "loadoutUi", loadoutUi);
    }

    private static void SetReference(Object target, string fieldName, Object value)
    {
        if (target == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"{target.name} is missing serialized field '{fieldName}'.");
            return;
        }

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetMaterial(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void SetMaterialIfEmpty(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial == null)
        {
            renderer.sharedMaterial = material;
        }
    }
}
