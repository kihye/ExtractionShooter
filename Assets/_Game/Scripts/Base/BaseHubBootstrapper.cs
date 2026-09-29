using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class BaseHubBootstrapper
{
    private const string BaseSceneName = "BaseScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapInitialScene()
    {
        BootstrapIfBaseScene(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootstrapIfBaseScene(scene);
    }

    private static void BootstrapIfBaseScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != BaseSceneName)
        {
            return;
        }

        BaseSceneReferences sceneReferences = Object.FindFirstObjectByType<BaseSceneReferences>();
        if (sceneReferences == null)
        {
            Debug.LogError("BaseScene is missing BaseSceneReferences. Run the Base fixed object migration and assign the scene references.");
            return;
        }

        RequireEventSystem();

        StashInventory stashInventory = sceneReferences.StashInventory;
        if (stashInventory == null)
        {
            Debug.LogError("BaseSceneReferences is missing StashInventory.");
            return;
        }

        GameObject player = FindExistingBasePlayer();
        if (player == null)
        {
            player = SpawnBasePlayer(sceneReferences);
        }

        if (player == null)
        {
            return;
        }

        PlayerRuntimeStateBinder binder = player.GetComponent<PlayerRuntimeStateBinder>();
        if (binder == null)
        {
            Debug.LogError("Base player is missing PlayerRuntimeStateBinder. Add it to the Player prefab instead of relying on runtime AddComponent.");
            return;
        }

        binder.Bind();
        DisableBaseCombat(player);
        ConfigureBaseCamera(sceneReferences, player.transform);
        BindUi(sceneReferences, player, stashInventory);
        BindInteractables(sceneReferences);
        Object.FindFirstObjectByType<SceneFlowController>()?.ApplyPendingRestoreForLoadedScene();
    }

    public static void RebindCurrentBaseScene()
    {
        BootstrapIfBaseScene(SceneManager.GetActiveScene());
    }

    private static GameObject FindExistingBasePlayer()
    {
        PlayerInventory existingInventory = Object.FindFirstObjectByType<PlayerInventory>();
        return existingInventory != null ? existingInventory.gameObject : null;
    }

    private static GameObject SpawnBasePlayer(BaseSceneReferences sceneReferences)
    {
        if (sceneReferences.PlayerPrefab == null)
        {
            Debug.LogError("BaseSceneReferences is missing PlayerPrefab.");
            return null;
        }

        if (sceneReferences.PlayerSpawnPoint == null)
        {
            Debug.LogError("BaseSceneReferences is missing PlayerSpawnPoint.");
            return null;
        }

        Transform spawnPoint = sceneReferences.PlayerSpawnPoint;
        GameObject player = Object.Instantiate(sceneReferences.PlayerPrefab, spawnPoint.position, spawnPoint.rotation);
        player.name = sceneReferences.PlayerPrefab.name;
        return player;
    }

    private static void DisableBaseCombat(GameObject player)
    {
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        if (weapon != null)
        {
            weapon.enabled = false;
        }
    }

    private static void ConfigureBaseCamera(BaseSceneReferences sceneReferences, Transform playerTransform)
    {
        if (sceneReferences.BaseCamera == null)
        {
            Debug.LogError("BaseSceneReferences is missing BaseCamera.");
            return;
        }

        if (sceneReferences.TopDownCamera == null)
        {
            Debug.LogError("BaseSceneReferences is missing TopDownCamera.");
            return;
        }

        sceneReferences.TopDownCamera.SetTarget(playerTransform);
    }

    private static void BindUi(BaseSceneReferences sceneReferences, GameObject player, StashInventory stashInventory)
    {
        PlayerInventory playerInventory = player.GetComponent<PlayerInventory>();
        PlayerEquipment playerEquipment = player.GetComponent<PlayerEquipment>();
        PlayerInteractor playerInteractor = player.GetComponent<PlayerInteractor>();

        if (sceneReferences.Canvas == null)
        {
            Debug.LogError("BaseSceneReferences is missing Canvas.");
        }

        if (sceneReferences.UiModeController == null)
        {
            Debug.LogError("BaseSceneReferences is missing GameplayUiModeController.");
        }

        if (playerInventory == null)
        {
            Debug.LogError("Base player is missing PlayerInventory.");
            return;
        }

        if (sceneReferences.InventoryGui != null)
        {
            sceneReferences.InventoryGui.Bind(playerInventory, playerEquipment, sceneReferences.UiModeController);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing InventoryGuiController.");
        }

        if (sceneReferences.StorageGui != null)
        {
            sceneReferences.StorageGui.Bind(playerInventory, stashInventory, sceneReferences.UiModeController);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing StorageGuiController.");
        }

        if (sceneReferences.ShopGui != null)
        {
            sceneReferences.ShopGui.Bind(playerInventory, sceneReferences.UiModeController);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing ShopGuiController.");
        }

        if (sceneReferences.SaveLoadUi != null)
        {
            sceneReferences.SaveLoadUi.Bind(
                stashInventory.SessionState,
                playerInventory,
                playerEquipment,
                stashInventory,
                sceneReferences.UiModeController);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing SaveLoadUI.");
        }

        if (sceneReferences.PromptUi != null)
        {
            sceneReferences.PromptUi.Bind(playerInteractor);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing InteractionPromptUI.");
        }
    }

    private static void BindInteractables(BaseSceneReferences sceneReferences)
    {
        if (sceneReferences.StorageInteractable != null)
        {
            sceneReferences.StorageInteractable.Configure(sceneReferences.StorageGui);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing StorageInteractable.");
        }

        if (sceneReferences.ShopInteractable != null)
        {
            sceneReferences.ShopInteractable.Configure(sceneReferences.ShopGui);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing ShopInteractable.");
        }

        if (sceneReferences.DeployInteractable != null)
        {
            sceneReferences.DeployInteractable.Configure(sceneReferences.LoadoutUi);
        }
        else
        {
            Debug.LogError("BaseSceneReferences is missing DeployTerminalInteractable.");
        }
    }

    private static void RequireEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            Debug.LogError("BaseScene is missing an authored EventSystem.");
        }
    }
}
