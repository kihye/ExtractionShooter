using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AuthoredGameplayUiBuilder
{
    private const string GameRoot = "Assets/_Game";
    private const string InputPath = GameRoot + "/Input/PlayerInputActions.inputactions";
    private const string PrototypeScenePath = GameRoot + "/Scenes/PrototypeScene.unity";
    private const string BaseScenePath = GameRoot + "/Scenes/BaseScene.unity";
    private const string GameSessionStatePath = GameRoot + "/Data/Runtime/GameSessionState.asset";
    private const string PretendardFontPath = "Assets/_Resources/Fonts/Pretendard-Medium SDF.asset";

    private static readonly Color WindowColor = new Color(0.05f, 0.06f, 0.07f, 0.88f);
    private static readonly Color HeaderColor = new Color(0.13f, 0.15f, 0.17f, 0.94f);
    private static readonly Color CellColor = new Color(0.18f, 0.2f, 0.22f, 0.72f);
    private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
    private static readonly Color MutedTextColor = new Color(0.72f, 0.78f, 0.84f, 1f);

    [MenuItem("Tools/Prototype/Repair Authored UI In Scenes")]
    public static void RepairAuthoredUiInScenes()
    {
        RepairPrototypeScene();
        RepairBaseScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Repaired authored UI hierarchy and serialized references in PrototypeScene and BaseScene.");
    }

    public static void BuildPrototypeSceneUi(
        GameObject canvasObject,
        GameObject player,
        InputActionAsset inputActions,
        RunSessionController runSessionController,
        RunRestartController runRestartController,
        SceneFlowController sceneFlowController,
        ExtractionZone extractionZone)
    {
        if (canvasObject == null)
        {
            return;
        }

        Canvas canvas = EnsureCanvas(canvasObject);
        Transform gameplayRoot = EnsureGameplayRoot(canvas.transform);
        SetReference(canvas.GetComponent<GameplayUiModeController>(), "playerInventory", player != null ? player.GetComponent<PlayerInventory>() : null);

        PlayerHUD playerHud = EnsureController<PlayerHUD>(gameplayRoot, "PlayerHUD");
        BuildPlayerHud(playerHud, player != null ? player.GetComponent<PlayerHealth>() : null);

        AmmoHUD ammoHud = EnsureController<AmmoHUD>(gameplayRoot, "AmmoHUD");
        BuildAmmoHud(ammoHud, player != null ? player.GetComponent<PlayerWeapon>() : null);

        RunInventoryUI runInventoryUi = EnsureController<RunInventoryUI>(gameplayRoot, "RunInventoryPanel");
        BuildRunInventoryUi(runInventoryUi, player != null ? player.GetComponent<PlayerInventory>() : null, player != null ? player.GetComponent<PlayerEquipment>() : null);

        InteractionPromptUI promptUi = EnsureController<InteractionPromptUI>(gameplayRoot, "InteractionPrompt");
        BuildInteractionPrompt(promptUi, player != null ? player.GetComponent<PlayerInteractor>() : null);

        ExtractionUI extractionUi = EnsureController<ExtractionUI>(gameplayRoot, "ExtractionPanel");
        BuildExtractionUi(extractionUi, extractionZone);

        RunResultUI runResultUi = EnsureController<RunResultUI>(gameplayRoot, "RunResultPanel");
        BuildRunResultUi(runResultUi, runSessionController, runInventoryUi, promptUi, extractionUi, ammoHud, runRestartController, sceneFlowController);

        InventoryGuiController inventoryGui = EnsureController<InventoryGuiController>(gameplayRoot, "InventoryGUI");
        BuildInventoryGui(inventoryGui, inputActions, player);

        EnsureEventSystem();
    }

    public static void BuildBaseSceneUi(
        GameObject canvasObject,
        GameObject player,
        InputActionAsset inputActions,
        GameSessionState sessionState,
        StashInventory stashInventory,
        SceneFlowController sceneFlowController,
        BaseEconomyController economyController)
    {
        if (canvasObject == null)
        {
            return;
        }

        Canvas canvas = EnsureCanvas(canvasObject);
        Transform gameplayRoot = EnsureGameplayRoot(canvas.transform);
        SetReference(canvas.GetComponent<GameplayUiModeController>(), "playerInventory", player != null ? player.GetComponent<PlayerInventory>() : null);

        BaseStashUI stashUi = EnsureController<BaseStashUI>(gameplayRoot, "BaseStashUI");
        BuildBaseStashUi(stashUi, stashInventory);

        BaseEconomyUI economyUi = EnsureController<BaseEconomyUI>(gameplayRoot, "BaseEconomyUI");
        economyUi.gameObject.SetActive(false);
        EditorUtility.SetDirty(economyUi.gameObject);

        InteractionPromptUI promptUi = EnsureController<InteractionPromptUI>(gameplayRoot, "InteractionPrompt");
        BuildInteractionPrompt(promptUi, player != null ? player.GetComponent<PlayerInteractor>() : null);

        InventoryGuiController inventoryGui = EnsureController<InventoryGuiController>(gameplayRoot, "InventoryGUI");
        BuildInventoryGui(inventoryGui, inputActions, player);

        StorageGuiController storageGui = EnsureController<StorageGuiController>(gameplayRoot, "StorageGUI");
        BuildStorageGui(storageGui, player != null ? player.GetComponent<PlayerInventory>() : null, stashInventory);

        ShopGuiController shopGui = EnsureController<ShopGuiController>(gameplayRoot, "ShopGUI");
        BuildShopGui(shopGui, player != null ? player.GetComponent<PlayerInventory>() : null);

        BaseLoadoutUI loadoutUi = EnsureController<BaseLoadoutUI>(gameplayRoot, "BaseLoadoutUI");
        BuildBaseLoadoutUi(loadoutUi, sessionState, sceneFlowController);

        EnsureEventSystem();
    }

    private static void RepairPrototypeScene()
    {
        Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        GameObject canvasObject = FindOrCreateRootObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GameObject player = GameObject.Find("Player");
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);

        BuildPrototypeSceneUi(
            canvasObject,
            player,
            inputActions,
            Object.FindFirstObjectByType<RunSessionController>(),
            Object.FindFirstObjectByType<RunRestartController>(),
            Object.FindFirstObjectByType<SceneFlowController>(),
            Object.FindFirstObjectByType<ExtractionZone>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void RepairBaseScene()
    {
        Scene scene = EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
        GameObject canvasObject = FindOrCreateRootObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GameObject player = FindPlayerObject();
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
        GameSessionState sessionState = AssetDatabase.LoadAssetAtPath<GameSessionState>(GameSessionStatePath);
        StashInventory stashInventory = Object.FindFirstObjectByType<StashInventory>();
        SceneFlowController sceneFlowController = Object.FindFirstObjectByType<SceneFlowController>();
        BaseEconomyController economyController = Object.FindFirstObjectByType<BaseEconomyController>();

        BuildBaseSceneUi(canvasObject, player, inputActions, sessionState, stashInventory, sceneFlowController, economyController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildPlayerHud(PlayerHUD playerHud, PlayerHealth playerHealth)
    {
        RectTransform root = EnsureRect(playerHud.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "HealthPanel");
        SetAnchored(panel, new Vector2(24f, -24f), new Vector2(280f, 54f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.58f));

        RectTransform background = EnsureChildRect(panel, "HealthBackground");
        SetStretch(background, new Vector2(14f, 12f), new Vector2(-14f, -28f));
        EnsureImage(background.gameObject, new Color(0.18f, 0.08f, 0.08f, 0.94f));

        RectTransform fill = EnsureChildRect(background, "HealthFill");
        SetStretch(fill);
        Image fillImage = EnsureImage(fill.gameObject, new Color(0.78f, 0.14f, 0.14f, 1f));
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;

        TMP_Text label = EnsureText(panel, "HealthText", "100 / 100", 18f, TextAlignmentOptions.Left, TextColor);
        SetStretch(label.rectTransform, new Vector2(14f, 26f), new Vector2(-14f, -4f));

        SerializedObject serializedObject = new SerializedObject(playerHud);
        SetProperty(serializedObject, "playerHealth", playerHealth);
        SetProperty(serializedObject, "healthFill", fillImage);
        SetProperty(serializedObject, "healthText", label);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(playerHud);
    }

    private static void BuildAmmoHud(AmmoHUD ammoHud, PlayerWeapon playerWeapon)
    {
        RectTransform root = EnsureRect(ammoHud.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "AmmoPanel");
        SetAnchored(panel, new Vector2(-24f, 24f), new Vector2(230f, 76f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.58f));

        TMP_Text ammoText = EnsureText(panel, "AmmoText", "12 / 108", 28f, TextAlignmentOptions.Right, TextColor);
        SetStretch(ammoText.rectTransform, new Vector2(14f, 26f), new Vector2(-14f, -8f));

        TMP_Text reloadText = EnsureText(panel, "ReloadText", "Reloading", 16f, TextAlignmentOptions.Right, new Color(0.96f, 0.74f, 0.28f, 1f));
        SetStretch(reloadText.rectTransform, new Vector2(14f, 6f), new Vector2(-14f, -46f));
        reloadText.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(ammoHud);
        SetProperty(serializedObject, "playerWeapon", playerWeapon);
        SetProperty(serializedObject, "ammoText", ammoText);
        SetProperty(serializedObject, "reloadText", reloadText);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ammoHud);
    }

    private static void BuildRunInventoryUi(RunInventoryUI runInventoryUi, PlayerInventory playerInventory, PlayerEquipment playerEquipment)
    {
        RectTransform root = EnsureRect(runInventoryUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "RunInventorySummary");
        SetAnchored(panel, new Vector2(24f, -96f), new Vector2(330f, 430f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.46f));

        TMP_Text itemListText = EnsureText(panel, "ItemListText", "RUN INVENTORY\nEmpty", 15f, TextAlignmentOptions.TopLeft, TextColor);
        SetStretch(itemListText.rectTransform, new Vector2(14f, 14f), new Vector2(-14f, -14f));

        SerializedObject serializedObject = new SerializedObject(runInventoryUi);
        SetProperty(serializedObject, "playerInventory", playerInventory);
        SetProperty(serializedObject, "playerEquipment", playerEquipment);
        SetProperty(serializedObject, "itemListText", itemListText);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runInventoryUi);
    }

    private static void BuildInteractionPrompt(InteractionPromptUI promptUi, PlayerInteractor playerInteractor)
    {
        RectTransform root = EnsureRect(promptUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "PromptPanel");
        SetAnchored(panel, new Vector2(0f, 96f), new Vector2(360f, 44f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.7f));

        TMP_Text promptText = EnsureText(panel, "PromptText", "[E] Interact", 18f, TextAlignmentOptions.Center, TextColor);
        SetStretch(promptText.rectTransform, new Vector2(10f, 6f), new Vector2(-10f, -6f));
        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(promptUi);
        SetProperty(serializedObject, "playerInteractor", playerInteractor);
        SetProperty(serializedObject, "promptText", promptText);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(promptUi);
    }

    private static void BuildExtractionUi(ExtractionUI extractionUi, ExtractionZone extractionZone)
    {
        RectTransform root = EnsureRect(extractionUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "ExtractionWindow");
        SetAnchored(panel, new Vector2(0f, -88f), new Vector2(420f, 78f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.7f));

        TMP_Text label = EnsureText(panel, "ExtractionText", "EXTRACTING", 16f, TextAlignmentOptions.Left, TextColor);
        SetStretch(label.rectTransform, new Vector2(14f, 42f), new Vector2(-110f, -10f));

        TMP_Text remainingTimeText = EnsureText(panel, "RemainingTimeText", "0.0s", 18f, TextAlignmentOptions.Right, TextColor);
        SetStretch(remainingTimeText.rectTransform, new Vector2(300f, 42f), new Vector2(-14f, -10f));

        RectTransform background = EnsureChildRect(panel, "ProgressBackground");
        SetStretch(background, new Vector2(14f, 12f), new Vector2(-14f, -46f));
        EnsureImage(background.gameObject, new Color(0.16f, 0.18f, 0.2f, 0.95f));

        RectTransform fill = EnsureChildRect(background, "ProgressFill");
        SetStretch(fill);
        Image fillImage = EnsureImage(fill.gameObject, new Color(0.24f, 0.72f, 0.9f, 1f));
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 0f;
        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(extractionUi);
        SetProperty(serializedObject, "extractionZone", extractionZone);
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "progressFill", fillImage);
        SetProperty(serializedObject, "remainingTimeText", remainingTimeText);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(extractionUi);
    }

    private static void BuildRunResultUi(
        RunResultUI runResultUi,
        RunSessionController runSessionController,
        RunInventoryUI runInventoryUi,
        InteractionPromptUI promptUi,
        ExtractionUI extractionUi,
        AmmoHUD ammoHud,
        RunRestartController restartController,
        SceneFlowController sceneFlowController)
    {
        RectTransform root = EnsureRect(runResultUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "RunResultWindow");
        SetAnchored(panel, Vector2.zero, new Vector2(560f, 420f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        TMP_Text titleText = EnsureText(panel, "ResultTitleText", "RUN RESULT", 32f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(titleText.rectTransform, new Vector2(0f, -34f), new Vector2(500f, 52f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        TMP_Text itemListText = EnsureText(panel, "ItemResultListText", "Secured:\nEmpty", 18f, TextAlignmentOptions.TopLeft, TextColor);
        SetStretch(itemListText.rectTransform, new Vector2(36f, 96f), new Vector2(-36f, -118f));

        Button retryButton = EnsureButton(panel, "RetryButton", "RETRY", new Vector2(-118f, 46f), new Vector2(190f, 46f));
        Button returnButton = EnsureButton(panel, "ReturnToBaseButton", "RETURN TO BASE", new Vector2(118f, 46f), new Vector2(190f, 46f));
        TMP_Text retryButtonText = retryButton.GetComponentInChildren<TMP_Text>(true);
        TMP_Text returnButtonText = returnButton.GetComponentInChildren<TMP_Text>(true);
        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(runResultUi);
        SetProperty(serializedObject, "runSessionController", runSessionController);
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "resultTitleText", titleText);
        SetProperty(serializedObject, "itemResultListText", itemListText);
        SetProperty(serializedObject, "runInventoryUI", runInventoryUi);
        SetProperty(serializedObject, "interactionPromptUI", promptUi);
        SetProperty(serializedObject, "extractionUI", extractionUi);
        SetProperty(serializedObject, "ammoHUD", ammoHud);
        SetProperty(serializedObject, "restartController", restartController);
        SetProperty(serializedObject, "sceneFlowController", sceneFlowController);
        SetProperty(serializedObject, "retryButton", retryButton);
        SetProperty(serializedObject, "retryButtonText", retryButtonText);
        SetProperty(serializedObject, "returnToBaseButton", returnButton);
        SetProperty(serializedObject, "returnToBaseButtonText", returnButtonText);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runResultUi);
    }

    private static void BuildInventoryGui(InventoryGuiController inventoryGui, InputActionAsset inputActions, GameObject player)
    {
        RectTransform root = EnsureRect(inventoryGui.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "InventoryWindow");
        SetAnchored(panel, Vector2.zero, new Vector2(1560f, 860f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        TMP_Text title = EnsureText(panel, "Title", "INVENTORY / EQUIPMENT", 26f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(title.rectTransform, new Vector2(26f, -24f), new Vector2(620f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        RectTransform equipmentViewport = EnsureChildRect(panel, "EquipmentViewport");
        SetAnchored(equipmentViewport, new Vector2(28f, -84f), new Vector2(330f, 730f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(equipmentViewport.gameObject, new Color(0.02f, 0.025f, 0.03f, 0.72f));
        EnsureComponent<RectMask2D>(equipmentViewport.gameObject);

        RectTransform equipmentContainer = EnsureChildRect(equipmentViewport, "EquipmentContent");
        SetAnchored(equipmentContainer, Vector2.zero, new Vector2(290f, 1200f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        RectTransform bagViewport = EnsureChildRect(panel, "BagViewport");
        SetAnchored(bagViewport, new Vector2(386f, -84f), new Vector2(1146f, 730f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(bagViewport.gameObject, new Color(0.02f, 0.025f, 0.03f, 0.72f));
        EnsureComponent<RectMask2D>(bagViewport.gameObject);

        RectTransform bagContainer = EnsureChildRect(bagViewport, "BagContent");
        SetAnchored(bagContainer, Vector2.zero, new Vector2(1120f, 760f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        List<EquipmentSlotPanel> slotPanels = EnsureEquipmentSlotPool(equipmentContainer, 13);
        List<InventoryGridPanel> bagPanels = EnsureInventoryGridPanelPool(bagContainer, 6, 60, 32);

        Image placementPreview = EnsureImage(EnsureChildRect(panel, "PlacementPreview").gameObject, new Color(0.2f, 1f, 0.35f, 0.28f));
        placementPreview.raycastTarget = false;
        placementPreview.gameObject.SetActive(false);

        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(inventoryGui);
        SetProperty(serializedObject, "inputActions", inputActions);
        SetProperty(serializedObject, "playerInventory", player != null ? player.GetComponent<PlayerInventory>() : null);
        SetProperty(serializedObject, "playerEquipment", player != null ? player.GetComponent<PlayerEquipment>() : null);
        SetProperty(serializedObject, "playerMovement", player != null ? player.GetComponent<PlayerMovement>() : null);
        SetProperty(serializedObject, "playerAim", player != null ? player.GetComponent<PlayerAim>() : null);
        SetProperty(serializedObject, "playerWeapon", player != null ? player.GetComponent<PlayerWeapon>() : null);
        SetProperty(serializedObject, "playerInteractor", player != null ? player.GetComponent<PlayerInteractor>() : null);
        SetProperty(serializedObject, "uiModeController", Object.FindFirstObjectByType<GameplayUiModeController>());
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "equipmentViewport", equipmentViewport);
        SetProperty(serializedObject, "equipmentContainer", equipmentContainer);
        SetProperty(serializedObject, "bagViewport", bagViewport);
        SetProperty(serializedObject, "bagContainer", bagContainer);
        SetList(serializedObject.FindProperty("equipmentSlotPanelPool"), slotPanels);
        SetList(serializedObject.FindProperty("bagPanelPool"), bagPanels);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(inventoryGui);
    }

    private static void BuildStorageGui(StorageGuiController storageGui, PlayerInventory playerInventory, StashInventory stashInventory)
    {
        RectTransform root = EnsureRect(storageGui.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "StorageWindow");
        SetAnchored(panel, Vector2.zero, new Vector2(1600f, 860f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        TMP_Text title = EnsureText(panel, "Title", "STORAGE", 26f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(title.rectTransform, new Vector2(26f, -24f), new Vector2(360f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        RectTransform playerViewport = EnsureChildRect(panel, "PlayerGridViewport");
        SetAnchored(playerViewport, new Vector2(28f, -84f), new Vector2(760f, 730f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(playerViewport.gameObject, new Color(0.02f, 0.025f, 0.03f, 0.72f));
        EnsureComponent<RectMask2D>(playerViewport.gameObject);

        RectTransform playerContainer = EnsureChildRect(playerViewport, "PlayerGridContent");
        SetAnchored(playerContainer, Vector2.zero, new Vector2(820f, 760f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        RectTransform stashViewport = EnsureChildRect(panel, "StashGridViewport");
        SetAnchored(stashViewport, new Vector2(820f, -84f), new Vector2(752f, 730f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(stashViewport.gameObject, new Color(0.02f, 0.025f, 0.03f, 0.72f));
        EnsureComponent<RectMask2D>(stashViewport.gameObject);

        RectTransform stashContainer = EnsureChildRect(stashViewport, "StashGridContent");
        SetAnchored(stashContainer, Vector2.zero, new Vector2(720f, 760f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        List<StorageGridPanel> playerPanels = EnsureStorageGridPanelPool(playerContainer, "PlayerBagPanel", 6, 60, 32);
        StorageGridPanel stashPanel = EnsureStorageGridPanel(stashContainer, "StashGridPanel", 144, 80);

        Image placementPreview = EnsureImage(EnsureChildRect(panel, "PlacementPreview").gameObject, new Color(0.2f, 1f, 0.35f, 0.28f));
        placementPreview.raycastTarget = false;
        placementPreview.gameObject.SetActive(false);

        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(storageGui);
        SetProperty(serializedObject, "playerInventory", playerInventory);
        SetProperty(serializedObject, "stashInventory", stashInventory);
        SetProperty(serializedObject, "uiModeController", Object.FindFirstObjectByType<GameplayUiModeController>());
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "playerGridViewport", playerViewport);
        SetProperty(serializedObject, "playerGridContainer", playerContainer);
        SetProperty(serializedObject, "stashGridViewport", stashViewport);
        SetProperty(serializedObject, "stashGridContainer", stashContainer);
        SetList(serializedObject.FindProperty("playerGridPanelPool"), playerPanels);
        SetProperty(serializedObject, "stashGridPanel", stashPanel);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(storageGui);
    }

    private static void BuildShopGui(ShopGuiController shopGui, PlayerInventory playerInventory)
    {
        RectTransform root = EnsureRect(shopGui.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "ShopWindow");
        panel.gameObject.SetActive(false);

        DisableChild(panel, "Title");
        DisableChild(panel, "ShopListText");
        DisableChild(panel, "PlayerInventoryText");

        SerializedObject serializedObject = new SerializedObject(shopGui);
        SetProperty(serializedObject, "playerInventory", playerInventory);
        SetProperty(serializedObject, "uiModeController", Object.FindFirstObjectByType<GameplayUiModeController>());
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "shopListText", panel.Find("ShopListText")?.GetComponent<TMP_Text>());
        SetProperty(serializedObject, "playerInventoryText", panel.Find("PlayerInventoryText")?.GetComponent<TMP_Text>());
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shopGui);
    }

    private static void BuildBaseLoadoutUi(BaseLoadoutUI loadoutUi, GameSessionState sessionState, SceneFlowController sceneFlowController)
    {
        RectTransform root = EnsureRect(loadoutUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "DeployWindow");
        SetAnchored(panel, Vector2.zero, new Vector2(620f, 410f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        EnsureImage(panel.gameObject, WindowColor);

        TMP_Text title = EnsureText(panel, "Title", "DEPLOY LOADOUT", 30f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(title.rectTransform, new Vector2(0f, -30f), new Vector2(560f, 46f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        TMP_Text ammoStockText = EnsureText(panel, "AmmoStockText", "Ammo Stock\n0", 22f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(ammoStockText.rectTransform, new Vector2(-150f, -118f), new Vector2(220f, 80f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        TMP_Text selectedAmmoText = EnsureText(panel, "SelectedAmmoText", "Ammo to Carry\n0", 22f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(selectedAmmoText.rectTransform, new Vector2(150f, -118f), new Vector2(220f, 80f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        Button decreaseButton = EnsureButton(panel, "DecreaseAmmoButton", "-", new Vector2(-86f, -220f), new Vector2(70f, 48f));
        Button increaseButton = EnsureButton(panel, "IncreaseAmmoButton", "+", new Vector2(86f, -220f), new Vector2(70f, 48f));
        Button deployButton = EnsureButton(panel, "DeployButton", "DEPLOY", new Vector2(0f, -300f), new Vector2(220f, 52f));

        TMP_Text statusText = EnsureText(panel, "StatusText", "", 16f, TextAlignmentOptions.Center, MutedTextColor);
        SetAnchored(statusText.rectTransform, new Vector2(0f, -354f), new Vector2(520f, 30f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        panel.gameObject.SetActive(false);

        SerializedObject serializedObject = new SerializedObject(loadoutUi);
        SetProperty(serializedObject, "sessionState", sessionState);
        SetProperty(serializedObject, "sceneFlowController", sceneFlowController);
        SetProperty(serializedObject, "uiModeController", Object.FindFirstObjectByType<GameplayUiModeController>());
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "ammoStockText", ammoStockText);
        SetProperty(serializedObject, "selectedAmmoText", selectedAmmoText);
        SetProperty(serializedObject, "statusText", statusText);
        SetProperty(serializedObject, "decreaseAmmoButton", decreaseButton);
        SetProperty(serializedObject, "increaseAmmoButton", increaseButton);
        SetProperty(serializedObject, "deployButton", deployButton);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loadoutUi);
    }

    private static void BuildBaseStashUi(BaseStashUI stashUi, StashInventory stashInventory)
    {
        RectTransform root = EnsureRect(stashUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "BaseStashSummaryPanel");
        SetAnchored(panel, new Vector2(24f, -24f), new Vector2(360f, 310f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.48f));

        TMP_Text itemListText = EnsureText(panel, "ItemListText", "Base Stash\nEmpty", 16f, TextAlignmentOptions.TopLeft, TextColor);
        SetStretch(itemListText.rectTransform, new Vector2(16f, 14f), new Vector2(-16f, -14f));

        SerializedObject serializedObject = new SerializedObject(stashUi);
        SetProperty(serializedObject, "stashInventory", stashInventory);
        SetProperty(serializedObject, "panel", panel.gameObject);
        SetProperty(serializedObject, "itemListText", itemListText);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stashUi);
    }

    private static void BuildBaseEconomyUi(BaseEconomyUI economyUi, BaseEconomyController economyController, GameSessionState sessionState)
    {
        RectTransform root = EnsureRect(economyUi.gameObject);
        SetStretch(root);

        RectTransform panel = EnsureChildRect(root, "BaseEconomyStatusPanel");
        SetAnchored(panel, new Vector2(-24f, -24f), new Vector2(420f, 260f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        EnsureImage(panel.gameObject, new Color(0f, 0f, 0f, 0.48f));

        TMP_Text creditsText = EnsureText(panel, "CreditsText", "Credits\n0 C", 19f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(creditsText.rectTransform, new Vector2(-100f, -48f), new Vector2(180f, 62f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        TMP_Text ammoStockText = EnsureText(panel, "AmmoStockText", "Ammo Stock\n0", 19f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(ammoStockText.rectTransform, new Vector2(100f, -48f), new Vector2(180f, 62f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        TMP_Text sellValueText = EnsureText(panel, "SellValueText", "Sell Value\n0 C", 18f, TextAlignmentOptions.Center, TextColor);
        SetAnchored(sellValueText.rectTransform, new Vector2(0f, -118f), new Vector2(280f, 58f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        Button sellAllButton = EnsureButton(panel, "SellAllButton", "SELL ALL LOOT", new Vector2(-100f, -188f), new Vector2(178f, 44f));
        Button buyAmmoButton = EnsureButton(panel, "BuyAmmoButton", "BUY AMMO", new Vector2(100f, -188f), new Vector2(178f, 44f));

        TMP_Text statusText = EnsureText(panel, "StatusText", "", 15f, TextAlignmentOptions.Center, MutedTextColor);
        SetAnchored(statusText.rectTransform, new Vector2(0f, -238f), new Vector2(360f, 24f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        SerializedObject serializedObject = new SerializedObject(economyUi);
        SetProperty(serializedObject, "economyController", economyController);
        SetProperty(serializedObject, "sessionState", sessionState);
        SetProperty(serializedObject, "creditsText", creditsText);
        SetProperty(serializedObject, "sellValueText", sellValueText);
        SetProperty(serializedObject, "ammoStockText", ammoStockText);
        SetProperty(serializedObject, "statusText", statusText);
        SetProperty(serializedObject, "sellAllButton", sellAllButton);
        SetProperty(serializedObject, "sellAllButtonText", sellAllButton.GetComponentInChildren<TMP_Text>(true));
        SetProperty(serializedObject, "buyAmmoButton", buyAmmoButton);
        SetProperty(serializedObject, "buyAmmoButtonText", buyAmmoButton.GetComponentInChildren<TMP_Text>(true));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(economyUi);
    }

    private static List<EquipmentSlotPanel> EnsureEquipmentSlotPool(RectTransform parent, int count)
    {
        List<EquipmentSlotPanel> panels = new List<EquipmentSlotPanel>(count);
        for (int i = 0; i < count; i++)
        {
            RectTransform slotRect = EnsureChildRect(parent, $"EquipmentSlot_{i:00}");
            EnsureImage(slotRect.gameObject, CellColor);
            EquipmentSlotPanel panel = EnsureComponent<EquipmentSlotPanel>(slotRect.gameObject);

            TMP_Text titleText = EnsureText(slotRect, "TitleText", "Slot", 13f, TextAlignmentOptions.Left, TextColor);
            SetStretch(titleText.rectTransform, new Vector2(10f, 8f), new Vector2(-10f, -46f));

            TMP_Text valueText = EnsureText(slotRect, "ValueText", "Empty", 13f, TextAlignmentOptions.Left, MutedTextColor);
            SetStretch(valueText.rectTransform, new Vector2(10f, 28f), new Vector2(-10f, -26f));

            Image dropPreview = EnsureImage(EnsureChildRect(slotRect, "DropPreview").gameObject, new Color(0.2f, 1f, 0.35f, 0.28f));
            SetStretch(dropPreview.rectTransform);
            dropPreview.gameObject.SetActive(false);
            dropPreview.raycastTarget = false;

            RectTransform itemRect = EnsureChildRect(slotRect, "ItemView");
            EnsureImage(itemRect.gameObject, new Color(0.24f, 0.32f, 0.42f, 0.96f));
            CanvasGroup itemCanvasGroup = EnsureComponent<CanvasGroup>(itemRect.gameObject);
            itemCanvasGroup.alpha = 1f;
            itemCanvasGroup.blocksRaycasts = true;
            itemCanvasGroup.interactable = true;
            EquipmentItemDragView dragView = EnsureComponent<EquipmentItemDragView>(itemRect.gameObject);
            TMP_Text itemLabel = EnsureText(itemRect, "ItemLabelText", "", 12f, TextAlignmentOptions.Center, TextColor);
            SetStretch(itemLabel.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            itemRect.gameObject.SetActive(false);

            SerializedObject serializedObject = new SerializedObject(panel);
            SetProperty(serializedObject, "rootRect", slotRect);
            SetProperty(serializedObject, "background", slotRect.GetComponent<Image>());
            SetProperty(serializedObject, "dropPreview", dropPreview);
            SetProperty(serializedObject, "titleText", titleText);
            SetProperty(serializedObject, "valueText", valueText);
            SetProperty(serializedObject, "itemRect", itemRect);
            SetProperty(serializedObject, "itemImage", itemRect.GetComponent<Image>());
            SetProperty(serializedObject, "itemLabelText", itemLabel);
            SetProperty(serializedObject, "dragView", dragView);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            slotRect.gameObject.SetActive(false);
            panels.Add(panel);
        }

        return panels;
    }

    private static List<InventoryGridPanel> EnsureInventoryGridPanelPool(RectTransform parent, int panelCount, int cellCount, int itemViewCount)
    {
        List<InventoryGridPanel> panels = new List<InventoryGridPanel>(panelCount);
        for (int i = 0; i < panelCount; i++)
        {
            panels.Add(EnsureInventoryGridPanel(parent, $"BagGridPanel_{i:00}", cellCount, itemViewCount));
        }

        return panels;
    }

    private static InventoryGridPanel EnsureInventoryGridPanel(RectTransform parent, string name, int cellCount, int itemViewCount)
    {
        RectTransform panelRect = EnsureChildRect(parent, name);
        EnsureImage(panelRect.gameObject, new Color(0f, 0f, 0f, 0.42f));
        InventoryGridPanel panel = EnsureComponent<InventoryGridPanel>(panelRect.gameObject);

        TMP_Text titleText = EnsureText(panelRect, "TitleText", "Bag", 15f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(titleText.rectTransform, new Vector2(12f, -10f), new Vector2(260f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        RectTransform gridRect = EnsureChildRect(panelRect, "Grid");
        SetAnchored(gridRect, new Vector2(18f, -46f), new Vector2(280f, 280f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        List<RectTransform> cells = EnsureCellPool(gridRect, cellCount);
        List<Image> cellImages = new List<Image>(cells.Count);
        foreach (RectTransform cell in cells)
        {
            cellImages.Add(cell.GetComponent<Image>());
        }

        List<InventoryGridItemView> itemViews = EnsureInventoryItemViewPool(gridRect, itemViewCount);

        SerializedObject serializedObject = new SerializedObject(panel);
        SetProperty(serializedObject, "rootRect", panelRect);
        SetProperty(serializedObject, "titleText", titleText);
        SetProperty(serializedObject, "gridRect", gridRect);
        SetList(serializedObject.FindProperty("cellPool"), cells);
        SetList(serializedObject.FindProperty("cellImages"), cellImages);
        SetList(serializedObject.FindProperty("itemViewPool"), itemViews);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        panelRect.gameObject.SetActive(false);
        return panel;
    }

    private static List<StorageGridPanel> EnsureStorageGridPanelPool(RectTransform parent, string prefix, int panelCount, int cellCount, int itemViewCount)
    {
        List<StorageGridPanel> panels = new List<StorageGridPanel>(panelCount);
        for (int i = 0; i < panelCount; i++)
        {
            panels.Add(EnsureStorageGridPanel(parent, $"{prefix}_{i:00}", cellCount, itemViewCount));
        }

        return panels;
    }

    private static StorageGridPanel EnsureStorageGridPanel(RectTransform parent, string name, int cellCount, int itemViewCount)
    {
        RectTransform panelRect = EnsureChildRect(parent, name);
        EnsureImage(panelRect.gameObject, new Color(0f, 0f, 0f, 0.42f));
        StorageGridPanel panel = EnsureComponent<StorageGridPanel>(panelRect.gameObject);

        TMP_Text titleText = EnsureText(panelRect, "TitleText", "Grid", 15f, TextAlignmentOptions.Left, TextColor);
        SetAnchored(titleText.rectTransform, new Vector2(12f, -10f), new Vector2(260f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        RectTransform gridRect = EnsureChildRect(panelRect, "Grid");
        SetAnchored(gridRect, new Vector2(18f, -46f), new Vector2(520f, 520f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        List<RectTransform> cells = EnsureCellPool(gridRect, cellCount);
        List<Image> cellImages = new List<Image>(cells.Count);
        foreach (RectTransform cell in cells)
        {
            cellImages.Add(cell.GetComponent<Image>());
        }

        List<StorageGridItemView> itemViews = EnsureStorageItemViewPool(gridRect, itemViewCount);

        SerializedObject serializedObject = new SerializedObject(panel);
        SetProperty(serializedObject, "rootRect", panelRect);
        SetProperty(serializedObject, "titleText", titleText);
        SetProperty(serializedObject, "gridRect", gridRect);
        SetList(serializedObject.FindProperty("cellPool"), cells);
        SetList(serializedObject.FindProperty("cellImages"), cellImages);
        SetList(serializedObject.FindProperty("itemViewPool"), itemViews);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        panelRect.gameObject.SetActive(false);
        return panel;
    }

    private static List<RectTransform> EnsureCellPool(RectTransform gridRect, int count)
    {
        List<RectTransform> cells = new List<RectTransform>(count);
        for (int i = 0; i < count; i++)
        {
            RectTransform cell = EnsureChildRect(gridRect, $"Cell_{i:000}");
            Image cellImage = EnsureImage(cell.gameObject, CellColor);
            cellImage.raycastTarget = false;
            cell.gameObject.SetActive(false);
            cells.Add(cell);
        }

        return cells;
    }

    private static List<InventoryGridItemView> EnsureInventoryItemViewPool(RectTransform parent, int count)
    {
        List<InventoryGridItemView> views = new List<InventoryGridItemView>(count);
        for (int i = 0; i < count; i++)
        {
            RectTransform itemRect = EnsureChildRect(parent, $"ItemView_{i:00}");
            Image image = EnsureImage(itemRect.gameObject, new Color(0.24f, 0.32f, 0.42f, 0.96f));
            image.raycastTarget = true;
            CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(itemRect.gameObject);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            InventoryItemDragView dragView = EnsureComponent<InventoryItemDragView>(itemRect.gameObject);
            InventoryGridItemView itemView = EnsureComponent<InventoryGridItemView>(itemRect.gameObject);

            TMP_Text label = EnsureText(itemRect, "LabelText", "", 12f, TextAlignmentOptions.Center, TextColor);
            SetStretch(label.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            TMP_Text stack = EnsureText(itemRect, "StackText", "", 11f, TextAlignmentOptions.BottomRight, TextColor);
            SetStretch(stack.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            SerializedObject serializedObject = new SerializedObject(itemView);
            SetProperty(serializedObject, "background", image);
            SetProperty(serializedObject, "labelText", label);
            SetProperty(serializedObject, "stackText", stack);
            SetProperty(serializedObject, "dragView", dragView);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            itemRect.gameObject.SetActive(false);
            views.Add(itemView);
        }

        return views;
    }

    private static List<StorageGridItemView> EnsureStorageItemViewPool(RectTransform parent, int count)
    {
        List<StorageGridItemView> views = new List<StorageGridItemView>(count);
        for (int i = 0; i < count; i++)
        {
            RectTransform itemRect = EnsureChildRect(parent, $"ItemView_{i:00}");
            Image image = EnsureImage(itemRect.gameObject, new Color(0.24f, 0.32f, 0.42f, 0.96f));
            image.raycastTarget = true;
            CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(itemRect.gameObject);
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            StorageItemDragView dragView = EnsureComponent<StorageItemDragView>(itemRect.gameObject);
            StorageGridItemView itemView = EnsureComponent<StorageGridItemView>(itemRect.gameObject);

            TMP_Text label = EnsureText(itemRect, "LabelText", "", 12f, TextAlignmentOptions.Center, TextColor);
            SetStretch(label.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            TMP_Text stack = EnsureText(itemRect, "StackText", "", 11f, TextAlignmentOptions.BottomRight, TextColor);
            SetStretch(stack.rectTransform, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            SerializedObject serializedObject = new SerializedObject(itemView);
            SetProperty(serializedObject, "background", image);
            SetProperty(serializedObject, "labelText", label);
            SetProperty(serializedObject, "stackText", stack);
            SetProperty(serializedObject, "dragView", dragView);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            itemRect.gameObject.SetActive(false);
            views.Add(itemView);
        }

        return views;
    }

    private static Canvas EnsureCanvas(GameObject canvasObject)
    {
        canvasObject.SetActive(true);
        RectTransform rectTransform = EnsureRect(canvasObject);
        SetStretch(rectTransform);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localPosition = Vector3.zero;

        Canvas canvas = EnsureComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureComponent<GraphicRaycaster>(canvasObject);
        EnsureComponent<PrototypeCanvasSetup>(canvasObject);
        EnsureComponent<GameplayUiModeController>(canvasObject);
        SetUiLayerRecursive(canvasObject);
        EditorUtility.SetDirty(canvasObject);
        return canvas;
    }

    private static Transform EnsureGameplayRoot(Transform canvasTransform)
    {
        RectTransform gameplayRoot = EnsureChildRect(canvasTransform, "GameplayUI");
        SetStretch(gameplayRoot);
        gameplayRoot.gameObject.SetActive(true);

        string[] controllerNames =
        {
            "PlayerHUD",
            "AmmoHUD",
            "RunInventoryPanel",
            "InteractionPrompt",
            "ExtractionPanel",
            "RunResultPanel",
            "InventoryGUI",
            "BaseStashUI",
            "BaseEconomyUI",
            "StorageGUI",
            "ShopGUI",
            "BaseLoadoutUI",
            "SaveLoadUI"
        };

        foreach (string controllerName in controllerNames)
        {
            Transform existing = FindDeep(canvasTransform, controllerName);
            if (existing != null && existing.parent != gameplayRoot)
            {
                existing.SetParent(gameplayRoot, false);
            }
        }

        return gameplayRoot;
    }

    private static T EnsureController<T>(Transform parent, string name) where T : Component
    {
        Transform existing = FindDeep(parent, name);
        RectTransform rectTransform = existing != null ? existing as RectTransform : null;
        if (rectTransform == null)
        {
            rectTransform = EnsureChildRect(parent, name);
        }

        rectTransform.SetParent(parent, false);
        SetStretch(rectTransform);
        rectTransform.gameObject.SetActive(true);
        SetUiLayerRecursive(rectTransform.gameObject);
        return EnsureComponent<T>(rectTransform.gameObject);
    }

    private static Button EnsureButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rectTransform = EnsureChildRect(parent, name);
        SetAnchored(rectTransform, anchoredPosition, size, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        Image image = EnsureImage(rectTransform.gameObject, new Color(0.2f, 0.28f, 0.36f, 1f));
        Button button = EnsureComponent<Button>(rectTransform.gameObject);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        TMP_Text text = EnsureText(rectTransform, "Text", label, 16f, TextAlignmentOptions.Center, TextColor);
        SetStretch(text.rectTransform, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    private static TMP_Text EnsureText(RectTransform parent, string name, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rectTransform = EnsureChildRect(parent, name);
        TextMeshProUGUI label = EnsureComponent<TextMeshProUGUI>(rectTransform.gameObject);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        ApplyProjectFont(label);
        return label;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = EnsureComponent<Image>(gameObject);
        image.color = color;
        return image;
    }

    private static RectTransform EnsureRect(GameObject gameObject)
    {
        return EnsureComponent<RectTransform>(gameObject);
    }

    private static RectTransform EnsureChildRect(Transform parent, string name)
    {
        Transform existing = FindDirectChild(parent, name);
        RectTransform rectTransform;
        if (existing == null)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            rectTransform = child.GetComponent<RectTransform>();
        }
        else
        {
            rectTransform = existing as RectTransform;
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

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static GameObject FindOrCreateRootObject(string name, params System.Type[] components)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            foreach (System.Type componentType in components)
            {
                if (existing.GetComponent(componentType) == null)
                {
                    existing.AddComponent(componentType);
                }
            }

            return existing;
        }

        return new GameObject(name, components);
    }

    private static GameObject FindPlayerObject()
    {
        PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
        {
            return inventory.gameObject;
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        return taggedPlayer != null ? taggedPlayer : GameObject.Find("BasePlayer");
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void DisableChild(RectTransform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        if (child != null)
        {
            child.gameObject.SetActive(false);
            EditorUtility.SetDirty(child.gameObject);
        }
    }

    private static void ApplyProjectFont(TMP_Text text)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardFontPath);
        if (font == null)
        {
            return;
        }

        text.font = font;
        if (font.material != null)
        {
            text.fontSharedMaterial = font.material;
        }
    }

    private static void SetStretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private static void SetStretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void SetAnchored(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static void SetReference(Component component, string propertyName, Object value)
    {
        if (component == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(component);
        SetProperty(serializedObject, propertyName, value);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(component);
    }

    private static void SetProperty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetList<T>(SerializedProperty property, IReadOnlyList<T> values) where T : Object
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static void SetUiLayerRecursive(GameObject gameObject)
    {
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
        {
            return;
        }

        gameObject.layer = uiLayer;
        foreach (Transform child in gameObject.transform)
        {
            SetUiLayerRecursive(child.gameObject);
        }
    }
}
