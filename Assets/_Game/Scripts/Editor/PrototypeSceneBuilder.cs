using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class PrototypeSceneBuilder
{
    private const string AutoBuildSessionKey = "ExtractionShooter.PrototypeSceneBuilder.AutoBuildComplete";
    private const string GameRoot = "Assets/_Game";
    private const string InputPath = GameRoot + "/Input/PlayerInputActions.inputactions";
    private const string ScenePath = GameRoot + "/Scenes/PrototypeScene.unity";
    private const string GameSessionStatePath = GameRoot + "/Data/Runtime/GameSessionState.asset";
    private const string PlayerPrefabPath = GameRoot + "/Prefabs/Player/Player.prefab";
    private const string PlayerMaterialPath = GameRoot + "/Materials/PrototypePlayer.mat";
    private const string FloorMaterialPath = GameRoot + "/Materials/PrototypeFloor.mat";
    private const string WallMaterialPath = GameRoot + "/Materials/PrototypeWall.mat";
    private const string EnemyMaterialPath = GameRoot + "/Materials/PrototypeEnemy.mat";
    private const string PickupMaterialPath = GameRoot + "/Materials/PrototypePickup.mat";
    private const string AmmoMaterialPath = GameRoot + "/Materials/PrototypeAmmo.mat";
    private const string ItemPickupPrefabPath = GameRoot + "/Prefabs/Items/ItemPickup.prefab";
    private const string AmmoPickupPrefabPath = GameRoot + "/Prefabs/Items/AmmoPickup.prefab";
    private const string ScrapItemPath = GameRoot + "/Items/Scrap.asset";
    private const string MedicalSupplyItemPath = GameRoot + "/Items/MedicalSupply.asset";
    private const string StrangeArtifactItemPath = GameRoot + "/Items/StrangeArtifact.asset";
    private const string LightRifleAmmoItemPath = GameRoot + "/Items/LightRifleAmmo.asset";
    private const string TrainingRifleItemPath = GameRoot + "/Items/TrainingRifle.asset";
    private const string TrainingSidearmItemPath = GameRoot + "/Items/TrainingSidearm.asset";
    private const string TrainingArmorItemPath = GameRoot + "/Items/TrainingArmor.asset";
    private const string TrainingAccessoryItemPath = GameRoot + "/Items/TrainingAccessory.asset";
    private const string FieldBagItemPath = GameRoot + "/Items/FieldBag.asset";

    [InitializeOnLoadMethod]
    private static void CreatePrototypeSceneOnLoad()
    {
        if (SessionState.GetBool(AutoBuildSessionKey, false))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(AutoBuildSessionKey, false) || PrototypeSceneHasCombatObjects())
            {
                return;
            }

            SessionState.SetBool(AutoBuildSessionKey, true);
            CreatePrototypeScene();
        };
    }

    [MenuItem("Tools/Prototype/Create Player Controller Scene")]
    public static void CreatePrototypeScene()
    {
        EnsureFolders();
        AssetDatabase.ImportAsset(InputPath, ImportAssetOptions.ForceUpdate);

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        if (inputActions == null)
        {
            Debug.LogError($"Input actions asset not found at {InputPath}");
            return;
        }

        Material playerMaterial = CreateMaterial(PlayerMaterialPath, new Color(0.1f, 0.45f, 0.85f));
        Material floorMaterial = CreateMaterial(FloorMaterialPath, new Color(0.32f, 0.34f, 0.32f));
        Material wallMaterial = CreateMaterial(WallMaterialPath, new Color(0.55f, 0.52f, 0.46f));
        Material enemyMaterial = CreateMaterial(EnemyMaterialPath, new Color(0.75f, 0.18f, 0.12f));
        Material pickupMaterial = CreateMaterial(PickupMaterialPath, new Color(0.18f, 0.8f, 0.42f));
        Material ammoMaterial = CreateMaterial(AmmoMaterialPath, new Color(0.95f, 0.78f, 0.18f));
        ItemData scrap = LoadItem(ScrapItemPath);
        ItemData medicalSupply = LoadItem(MedicalSupplyItemPath);
        ItemData strangeArtifact = LoadItem(StrangeArtifactItemPath);
        ItemData lightRifleAmmo = LoadItem(LightRifleAmmoItemPath);
        ItemDefinition trainingRifle = LoadItemDefinition(TrainingRifleItemPath);
        ItemDefinition trainingSidearm = LoadItemDefinition(TrainingSidearmItemPath);
        ItemDefinition trainingArmor = LoadItemDefinition(TrainingArmorItemPath);
        ItemDefinition trainingAccessory = LoadItemDefinition(TrainingAccessoryItemPath);
        ItemDefinition fieldBag = LoadItemDefinition(FieldBagItemPath);
        GameSessionState gameSessionState = CreateGameSessionState();
        ItemPickup pickupPrefab = CreateItemPickupPrefab(pickupMaterial, scrap);
        AmmoPickup ammoPickupPrefab = CreateAmmoPickupPrefab(ammoMaterial, lightRifleAmmo);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "PrototypeScene";

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(3f, 1f, 3f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

        CreateWall("Wall_North", new Vector3(0f, 1f, 14f), new Vector3(28f, 2f, 1f), wallMaterial);
        CreateWall("Wall_South", new Vector3(0f, 1f, -14f), new Vector3(28f, 2f, 1f), wallMaterial);
        CreateWall("Wall_West", new Vector3(-14f, 1f, 0f), new Vector3(1f, 2f, 28f), wallMaterial);
        CreateWall("Wall_East", new Vector3(14f, 1f, 0f), new Vector3(1f, 2f, 28f), wallMaterial);
        CreateWall("Cover_Block_A", new Vector3(-4f, 0.75f, 3f), new Vector3(4f, 1.5f, 1f), wallMaterial);
        CreateWall("Cover_Block_B", new Vector3(5f, 0.75f, -3f), new Vector3(1f, 1.5f, 5f), wallMaterial);
        CreateWall("Enemy_Blocking_Wall", new Vector3(0f, 1f, 6f), new Vector3(3f, 2f, 0.5f), wallMaterial);

        GameObject player = CreatePlayer(inputActions, playerMaterial, lightRifleAmmo);
        CreatePlayerPrefab(player);
        RunRestartController runRestartController;
        SceneFlowController sceneFlowController;
        RunSessionController runSessionController = CreateGameSession(player, gameSessionState, lightRifleAmmo, out runRestartController, out sceneFlowController);

        CreateDummyEnemy("DummyEnemy_Front", new Vector3(0f, 1f, 4f), enemyMaterial, pickupPrefab, scrap, 2, ammoPickupPrefab, true, 18);
        CreateDummyEnemy("DummyEnemy_Left", new Vector3(-5f, 1f, 2f), enemyMaterial, pickupPrefab, medicalSupply, 1, ammoPickupPrefab, false, 18);
        CreateDummyEnemy("DummyEnemy_BehindWall", new Vector3(0f, 1f, 8.5f), enemyMaterial, pickupPrefab, strangeArtifact, 1, ammoPickupPrefab, false, 18);
        CreatePlacedPickup("Pickup_Scrap", new Vector3(2.5f, 0.25f, 1.5f), pickupMaterial, scrap, 1);
        CreatePlacedPickup("Pickup_MedicalSupply", new Vector3(-2.5f, 0.25f, -1.5f), pickupMaterial, medicalSupply, 1);
        CreatePlacedPickup("Pickup_StrangeArtifact", new Vector3(6f, 0.25f, 1.5f), pickupMaterial, strangeArtifact, 1);
        CreatePlacedPickup("Pickup_TrainingRifle", new Vector3(-6f, 0.25f, -2.5f), pickupMaterial, trainingRifle, 1);
        CreatePlacedPickup("Pickup_TrainingSidearm", new Vector3(-7f, 0.25f, -2.5f), pickupMaterial, trainingSidearm, 1);
        CreatePlacedPickup("Pickup_TrainingArmor", new Vector3(-8f, 0.25f, -2.5f), pickupMaterial, trainingArmor, 1);
        CreatePlacedPickup("Pickup_TrainingAccessory", new Vector3(-9f, 0.25f, -2.5f), pickupMaterial, trainingAccessory, 1);
        CreatePlacedPickup("Pickup_FieldBag", new Vector3(-10f, 0.25f, -2.5f), pickupMaterial, fieldBag, 1);
        CreatePlacedAmmoPickup("Pickup_AmmoBox", new Vector3(1.5f, 0.2f, -2.5f), ammoMaterial, lightRifleAmmo, 12);
        ExtractionZone extractionZone = CreateExtractionZone(new Vector3(10f, 0.15f, 10f), pickupMaterial, runSessionController);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.transform.position = new Vector3(0f, 12f, -8f);
        camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

        TopDownCamera topDownCamera = cameraObject.AddComponent<TopDownCamera>();
        SerializedObject cameraSerializedObject = new SerializedObject(topDownCamera);
        cameraSerializedObject.FindProperty("target").objectReferenceValue = player.transform;
        cameraSerializedObject.FindProperty("offset").vector3Value = new Vector3(0f, 12f, -8f);
        cameraSerializedObject.FindProperty("smoothTime").floatValue = 0.08f;
        cameraSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject aimSerializedObject = new SerializedObject(player.GetComponent<PlayerAim>());
        aimSerializedObject.FindProperty("aimCamera").objectReferenceValue = camera;
        aimSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        CreatePrototypeUI(player, inputActions, runSessionController, runRestartController, sceneFlowController, extractionZone);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created prototype scene at {ScenePath}");
    }

    private static void EnsureFolders()
    {
        string[] folders =
        {
            GameRoot,
            GameRoot + "/Scripts",
            GameRoot + "/Scripts/Player",
            GameRoot + "/Scripts/Camera",
            GameRoot + "/Scripts/Combat",
            GameRoot + "/Scripts/Enemies",
            GameRoot + "/Scripts/Economy",
            GameRoot + "/Scripts/Equipment",
            GameRoot + "/Scripts/Inventory",
            GameRoot + "/Scripts/Run",
            GameRoot + "/Scripts/Session",
            GameRoot + "/Scripts/UI",
            GameRoot + "/Prefabs",
            GameRoot + "/Prefabs/Player",
            GameRoot + "/Prefabs/Items",
            GameRoot + "/Prefabs/UI",
            GameRoot + "/Input",
            GameRoot + "/Scenes",
            GameRoot + "/Materials",
            GameRoot + "/Items",
            GameRoot + "/Data",
            GameRoot + "/Data/Runtime"
        };

        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
    }

    private static Material CreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreatePlayer(InputActionAsset inputActions, Material playerMaterial, ItemData defaultAmmoItem)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = playerMaterial;

        Rigidbody body = player.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        PlayerMovement movement = player.AddComponent<PlayerMovement>();
        SerializedObject movementSerializedObject = new SerializedObject(movement);
        movementSerializedObject.FindProperty("inputActions").objectReferenceValue = inputActions;
        movementSerializedObject.FindProperty("moveSpeed").floatValue = 5f;
        movementSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerAim aim = player.AddComponent<PlayerAim>();
        SerializedObject aimSerializedObject = new SerializedObject(aim);
        aimSerializedObject.FindProperty("inputActions").objectReferenceValue = inputActions;
        aimSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject firePoint = new GameObject("FirePoint");
        firePoint.transform.SetParent(player.transform);
        firePoint.transform.localPosition = new Vector3(0f, 0.2f, 0.7f);
        firePoint.transform.localRotation = Quaternion.identity;

        PlayerWeapon weapon = player.AddComponent<PlayerWeapon>();
        SerializedObject weaponSerializedObject = new SerializedObject(weapon);
        weaponSerializedObject.FindProperty("inputActions").objectReferenceValue = inputActions;
        weaponSerializedObject.FindProperty("firePoint").objectReferenceValue = firePoint.transform;
        weaponSerializedObject.FindProperty("reloadActionName").stringValue = "Reload";
        weaponSerializedObject.FindProperty("fireRate").floatValue = 4f;
        weaponSerializedObject.FindProperty("damage").floatValue = 25f;
        weaponSerializedObject.FindProperty("range").floatValue = 18f;
        weaponSerializedObject.FindProperty("hitMask").intValue = ~0;
        weaponSerializedObject.FindProperty("magazineSize").intValue = 12;
        weaponSerializedObject.FindProperty("currentAmmo").intValue = 12;
        weaponSerializedObject.FindProperty("reserveAmmo").intValue = 48;
        weaponSerializedObject.FindProperty("reloadDuration").floatValue = 1.5f;
        weaponSerializedObject.FindProperty("requiredAmmoType").enumValueIndex = 0;
        weaponSerializedObject.FindProperty("reserveAmmoItem").objectReferenceValue = defaultAmmoItem;
        weaponSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        player.AddComponent<PlayerHealth>();
        PlayerInventory playerInventory = player.AddComponent<PlayerInventory>();
        player.AddComponent<PlayerEquipment>();

        SerializedObject weaponInventorySerializedObject = new SerializedObject(weapon);
        weaponInventorySerializedObject.FindProperty("playerInventory").objectReferenceValue = playerInventory;
        weaponInventorySerializedObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerInteractor interactor = player.AddComponent<PlayerInteractor>();
        SerializedObject interactorSerializedObject = new SerializedObject(interactor);
        interactorSerializedObject.FindProperty("inputActions").objectReferenceValue = inputActions;
        interactorSerializedObject.FindProperty("interactionRange").floatValue = 1.75f;
        interactorSerializedObject.FindProperty("pickupMask").intValue = ~0;
        interactorSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    private static GameSessionState CreateGameSessionState()
    {
        GameSessionState state = AssetDatabase.LoadAssetAtPath<GameSessionState>(GameSessionStatePath);
        if (state != null)
        {
            return state;
        }

        state = ScriptableObject.CreateInstance<GameSessionState>();
        AssetDatabase.CreateAsset(state, GameSessionStatePath);
        return state;
    }

    private static RunSessionController CreateGameSession(GameObject player, GameSessionState gameSessionState, ItemData defaultAmmoItem, out RunRestartController runRestartController, out SceneFlowController sceneFlowController)
    {
        GameObject sessionObject = new GameObject("GameSession");
        StashInventory stashInventory = sessionObject.AddComponent<StashInventory>();
        RunSessionController runSessionController = sessionObject.AddComponent<RunSessionController>();
        runRestartController = sessionObject.AddComponent<RunRestartController>();
        sceneFlowController = sessionObject.AddComponent<SceneFlowController>();
        RunLoadoutApplier runLoadoutApplier = sessionObject.AddComponent<RunLoadoutApplier>();

        SerializedObject stashSerializedObject = new SerializedObject(stashInventory);
        stashSerializedObject.FindProperty("sessionState").objectReferenceValue = gameSessionState;
        stashSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        PlayerWeapon playerWeapon = player.GetComponent<PlayerWeapon>();

        SerializedObject runSerializedObject = new SerializedObject(runSessionController);
        runSerializedObject.FindProperty("playerInventory").objectReferenceValue = playerInventory;
        runSerializedObject.FindProperty("stashInventory").objectReferenceValue = stashInventory;
        runSerializedObject.FindProperty("sessionState").objectReferenceValue = gameSessionState;
        runSerializedObject.FindProperty("playerMovement").objectReferenceValue = player.GetComponent<PlayerMovement>();
        runSerializedObject.FindProperty("playerAim").objectReferenceValue = player.GetComponent<PlayerAim>();
        runSerializedObject.FindProperty("playerWeapon").objectReferenceValue = playerWeapon;
        runSerializedObject.FindProperty("playerInteractor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
        runSerializedObject.FindProperty("playerBody").objectReferenceValue = player.GetComponent<Rigidbody>();
        runSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject loadoutSerializedObject = new SerializedObject(runLoadoutApplier);
        loadoutSerializedObject.FindProperty("sessionState").objectReferenceValue = gameSessionState;
        loadoutSerializedObject.FindProperty("playerWeapon").objectReferenceValue = playerWeapon;
        loadoutSerializedObject.FindProperty("defaultAmmoItem").objectReferenceValue = defaultAmmoItem;
        loadoutSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject healthSerializedObject = new SerializedObject(playerHealth);
        healthSerializedObject.FindProperty("runSessionController").objectReferenceValue = runSessionController;
        healthSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        return runSessionController;
    }

    private static void CreateWall(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreatePlayerPrefab(GameObject player)
    {
        PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
    }

    private static void CreateDummyEnemy(string name, Vector3 position, Material material, ItemPickup pickupPrefab, ItemData dropItem, int amount, AmmoPickup ammoPickupPrefab, bool dropAmmo, int ammoAmount)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = name;
        enemy.transform.position = position;
        enemy.GetComponent<Renderer>().sharedMaterial = material;
        enemy.AddComponent<DummyEnemy>();

        EnemyDrop enemyDrop = enemy.AddComponent<EnemyDrop>();
        SerializedObject dropSerializedObject = new SerializedObject(enemyDrop);
        dropSerializedObject.FindProperty("pickupPrefab").objectReferenceValue = pickupPrefab;
        dropSerializedObject.FindProperty("itemData").objectReferenceValue = dropItem;
        dropSerializedObject.FindProperty("amount").intValue = amount;
        dropSerializedObject.FindProperty("ammoPickupPrefab").objectReferenceValue = ammoPickupPrefab;
        dropSerializedObject.FindProperty("dropAmmo").boolValue = dropAmmo;
        dropSerializedObject.FindProperty("ammoAmount").intValue = ammoAmount;
        dropSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        Rigidbody body = enemy.AddComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        enemy.AddComponent<EnemyAI>();
    }

    private static ItemData LoadItem(string path)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            return null;
        }

        SerializedObject itemSerializedObject = new SerializedObject(item);
        if (path == ScrapItemPath)
        {
            itemSerializedObject.FindProperty("sellValue").intValue = 10;
        }
        else if (path == MedicalSupplyItemPath)
        {
            itemSerializedObject.FindProperty("sellValue").intValue = 20;
        }
        else if (path == StrangeArtifactItemPath)
        {
            itemSerializedObject.FindProperty("sellValue").intValue = 50;
        }

        itemSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static ItemDefinition LoadItemDefinition(string path)
    {
        return AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
    }

    private static ItemPickup CreateItemPickupPrefab(Material material, ItemData defaultItem)
    {
        ItemPickup existingPrefab = AssetDatabase.LoadAssetAtPath<ItemPickup>(ItemPickupPrefabPath);
        if (existingPrefab != null)
        {
            return existingPrefab;
        }

        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.name = "ItemPickup";
        pickup.transform.localScale = Vector3.one * 0.45f;
        pickup.GetComponent<Renderer>().sharedMaterial = material;
        pickup.GetComponent<Collider>().isTrigger = true;

        ItemPickup itemPickup = pickup.AddComponent<ItemPickup>();
        SerializedObject pickupSerializedObject = new SerializedObject(itemPickup);
        pickupSerializedObject.FindProperty("itemData").objectReferenceValue = defaultItem;
        pickupSerializedObject.FindProperty("amount").intValue = 1;
        pickupSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pickup, ItemPickupPrefabPath);
        Object.DestroyImmediate(pickup);
        return prefab.GetComponent<ItemPickup>();
    }

    private static AmmoPickup CreateAmmoPickupPrefab(Material material, ItemData ammoItem)
    {
        AmmoPickup existingPrefab = AssetDatabase.LoadAssetAtPath<AmmoPickup>(AmmoPickupPrefabPath);
        if (existingPrefab != null)
        {
            SerializedObject existingSerializedObject = new SerializedObject(existingPrefab);
            existingSerializedObject.FindProperty("ammoItem").objectReferenceValue = ammoItem;
            existingSerializedObject.FindProperty("ammoType").enumValueIndex = 0;
            existingSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existingPrefab);
            return existingPrefab;
        }

        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.name = "AmmoPickup";
        pickup.transform.localScale = new Vector3(0.7f, 0.35f, 0.45f);
        pickup.GetComponent<Renderer>().sharedMaterial = material;
        pickup.GetComponent<Collider>().isTrigger = true;

        AmmoPickup ammoPickup = pickup.AddComponent<AmmoPickup>();
        SerializedObject pickupSerializedObject = new SerializedObject(ammoPickup);
        pickupSerializedObject.FindProperty("ammoItem").objectReferenceValue = ammoItem;
        pickupSerializedObject.FindProperty("ammoType").enumValueIndex = 0;
        pickupSerializedObject.FindProperty("ammoAmount").intValue = 12;
        pickupSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pickup, AmmoPickupPrefabPath);
        Object.DestroyImmediate(pickup);
        return prefab.GetComponent<AmmoPickup>();
    }

    private static void CreatePlacedPickup(string name, Vector3 position, Material material, ItemDefinition itemData, int amount)
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.name = name;
        pickup.transform.position = position;
        pickup.transform.localScale = Vector3.one * 0.45f;
        pickup.GetComponent<Renderer>().sharedMaterial = material;
        pickup.GetComponent<Collider>().isTrigger = true;

        ItemPickup itemPickup = pickup.AddComponent<ItemPickup>();
        itemPickup.Configure(itemData, amount);
    }

    private static void CreatePlacedAmmoPickup(string name, Vector3 position, Material material, ItemData ammoItem, int ammoAmount)
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.name = name;
        pickup.transform.position = position;
        pickup.transform.localScale = new Vector3(0.7f, 0.35f, 0.45f);
        pickup.GetComponent<Renderer>().sharedMaterial = material;
        pickup.GetComponent<Collider>().isTrigger = true;

        AmmoPickup ammoPickup = pickup.AddComponent<AmmoPickup>();
        ammoPickup.Configure(ammoItem, ammoAmount);
    }

    private static ExtractionZone CreateExtractionZone(Vector3 position, Material material, RunSessionController runSessionController)
    {
        GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        zone.name = "ExtractionZone";
        zone.transform.position = position;
        zone.transform.localScale = new Vector3(3f, 0.3f, 3f);
        zone.GetComponent<Renderer>().sharedMaterial = material;
        zone.GetComponent<Collider>().isTrigger = true;

        ExtractionZone extractionZone = zone.AddComponent<ExtractionZone>();
        SerializedObject zoneSerializedObject = new SerializedObject(extractionZone);
        zoneSerializedObject.FindProperty("extractionDuration").floatValue = 3f;
        zoneSerializedObject.FindProperty("runSessionController").objectReferenceValue = runSessionController;
        zoneSerializedObject.ApplyModifiedPropertiesWithoutUndo();

        return extractionZone;
    }

    private static void CreatePrototypeUI(GameObject player, InputActionAsset inputActions, RunSessionController runSessionController, RunRestartController runRestartController, SceneFlowController sceneFlowController, ExtractionZone extractionZone)
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        AuthoredGameplayUiBuilder.BuildPrototypeSceneUi(canvasObject, player, inputActions, runSessionController, runRestartController, sceneFlowController, extractionZone);
    }

    private static bool PrototypeSceneHasCombatObjects()
    {
        return File.Exists(ScenePath) && File.ReadAllText(ScenePath).Contains("DummyEnemy");
    }
}
