using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(100)]
public sealed class SceneFlowController : MonoBehaviour
{
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private SceneMapRegistry sceneMapRegistry;
    [SerializeField] private ItemDefinitionRegistry itemRegistry;
    [SerializeField] private RunWorldPrefabRegistry runWorldPrefabRegistry;
    [SerializeField] private string baseSceneName = "BaseScene";
    [SerializeField] private string runSceneName = "PrototypeScene";
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool isLoading;
    private bool sceneBindingsReady;

    public bool IsLoading => isLoading;

    public bool LoadBase()
    {
        if (TryGetMap(GameSceneKind.Base, out SceneMapRegistry.SceneMapEntry map))
        {
            sessionState?.SetCurrentScene(GameSceneKind.Base, map.MapId);
            return LoadScene(map.SceneName);
        }

        sessionState?.SetCurrentScene(GameSceneKind.Base, "base");
        return LoadScene(baseSceneName);
    }

    public bool LoadRun()
    {
        if (TryGetMap(GameSceneKind.Run, out SceneMapRegistry.SceneMapEntry map))
        {
            sessionState?.BeginRun(map.MapId);
            return LoadScene(map.SceneName);
        }

        sessionState?.BeginRun("prototype_run");
        return LoadScene(runSceneName);
    }

    public bool StartNewGame()
    {
        if (TryGetMap(GameSceneKind.Base, out SceneMapRegistry.SceneMapEntry map))
        {
            sessionState?.BeginNewGame(map.MapId);
            return LoadScene(map.SceneName);
        }

        sessionState?.BeginNewGame("base");
        return LoadScene(baseSceneName);
    }

    public bool LoadTitle()
    {
        return LoadScene(titleSceneName);
    }

    public bool LoadSavedProgress()
    {
        if (sessionState == null)
        {
            Debug.LogWarning("Saved progress load requested but SceneFlowController has no GameSessionState.", this);
            return false;
        }

        if (sceneMapRegistry != null && sceneMapRegistry.TryGetById(sessionState.CurrentMapId, out SceneMapRegistry.SceneMapEntry map))
        {
            return LoadScene(map.SceneName);
        }

        return LoadScene(sessionState.CurrentSceneKind == GameSceneKind.Run ? runSceneName : baseSceneName);
    }

    public void ApplyPendingRestoreForLoadedScene()
    {
        if (!sceneBindingsReady)
        {
            return;
        }
        isLoading = true;
        try
        {
            SceneProgressApplier.ApplyPendingRestoreForCurrentScene(sessionState, sceneMapRegistry, itemRegistry, runWorldPrefabRegistry);
        }
        finally
        {
            isLoading = false;
        }
    }

    private void Start()
    {
        // All authored Awake/OnEnable and normal Start initialization precede this phase.
        foreach (PlayerRuntimeStateBinder binder in FindObjectsByType<PlayerRuntimeStateBinder>(FindObjectsSortMode.None))
        {
            binder.Bind();
        }

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        GameplayUiModeController modes = FindFirstObjectByType<GameplayUiModeController>();
        foreach (InventoryGuiController gui in FindObjectsByType<InventoryGuiController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            gui.Bind(inventory, inventory != null ? inventory.GetComponent<PlayerEquipment>() : null, modes);
        }

        sceneBindingsReady = true;
        ApplyPendingRestoreForLoadedScene();
    }

    private bool LoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning($"Scene load '{sceneName}' was ignored because another scene load is already in progress.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("Scene load requested with an empty scene name.", this);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Scene '{sceneName}' is not available in Build Settings.", this);
            return false;
        }

        isLoading = true;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    private void Awake()
    {
        if (sessionState == null)
        {
            StashInventory stashInventory = FindFirstObjectByType<StashInventory>();
            if (stashInventory != null)
            {
                sessionState = stashInventory.SessionState;
            }
        }
    }

    private bool TryGetMap(GameSceneKind kind, out SceneMapRegistry.SceneMapEntry map)
    {
        if (sceneMapRegistry != null && sceneMapRegistry.TryGetFirstByKind(kind, out map))
        {
            return true;
        }

        map = null;
        return false;
    }
}
