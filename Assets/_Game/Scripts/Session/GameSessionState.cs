using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Extraction Shooter/Game Session State", fileName = "GameSessionState")]
public sealed class GameSessionState : ScriptableObject
{
    [Serializable]
    public sealed class InitialStashEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int amount = 1;

        public ItemDefinition Item => item;
        public int Amount => Mathf.Max(1, amount);
    }

    public event Action StashChanged;
    public event Action AmmoStockChanged;
    public event Action CreditsChanged;

    [SerializeField, Min(0)] private int initialCredits;
    [SerializeField, Min(0)] private int initialAmmoStock = 72;
    [SerializeField] private List<InitialStashEntry> initialStashItems = new List<InitialStashEntry>();
    [SerializeField, Min(1)] private int stashGridWidth = 12;
    [SerializeField, Min(1)] private int stashGridHeight = 12;

    [NonSerialized] private Dictionary<ItemDefinition, int> stashItems;
    [NonSerialized] private GridInventory stashGrid;
    [NonSerialized] private PlayerRuntimeState playerRuntimeState;
    [NonSerialized] private int credits;
    [NonSerialized] private int ammoStock;
    [NonSerialized] private int pendingAmmo;
    [NonSerialized] private bool initialized;
    [NonSerialized] private bool hasPendingLoadout;
    [NonSerialized] private GameSceneKind currentSceneKind = GameSceneKind.Base;
    [NonSerialized] private string currentMapId = "base";
    [NonSerialized] private string currentRunId;
    [NonSerialized] private bool hasPendingSceneRestore;
    [NonSerialized] private PlayerSceneSaveData pendingPlayerScene;
    [NonSerialized] private RunWorldSaveData pendingRunWorld;
    [NonSerialized] private bool suppressNextRunLoadoutApply;

    public GameSceneKind CurrentSceneKind => currentSceneKind;
    public string CurrentMapId => currentMapId;
    public string CurrentRunId => currentRunId;
    public bool HasPendingSceneRestore => hasPendingSceneRestore;

    public int AmmoStock
    {
        get
        {
            InitializeIfNeeded();
            return ammoStock;
        }
    }

    public PlayerRuntimeState PlayerRuntimeState
    {
        get
        {
            InitializeIfNeeded();
            return playerRuntimeState;
        }
    }

    public GridInventory StashGrid
    {
        get
        {
            InitializeIfNeeded();
            return stashGrid;
        }
    }

    public int Credits
    {
        get
        {
            InitializeIfNeeded();
            return credits;
        }
    }

    public void InitializeIfNeeded()
    {
        if (initialized && stashItems != null && stashGrid != null && playerRuntimeState != null)
        {
            return;
        }

        stashItems = new Dictionary<ItemDefinition, int>();
        stashGrid = new GridInventory("Base Stash", stashGridWidth, stashGridHeight);
        playerRuntimeState = new PlayerRuntimeState();
        playerRuntimeState.InitializeIfNeeded(6, 5);
        credits = Mathf.Max(0, initialCredits);
        ammoStock = Mathf.Max(0, initialAmmoStock);
        pendingAmmo = 0;
        hasPendingLoadout = false;
        hasPendingSceneRestore = false;
        pendingPlayerScene = null;
        pendingRunWorld = null;
        suppressNextRunLoadoutApply = false;
        GrantInitialStashItems();
        initialized = true;
    }

    public void BeginNewGame(string baseMapId)
    {
        ClearSession();
        SetCurrentScene(GameSceneKind.Base, baseMapId);
    }

    public void SetCurrentScene(GameSceneKind sceneKind, string mapId)
    {
        currentSceneKind = sceneKind;
        currentMapId = string.IsNullOrWhiteSpace(mapId) ? sceneKind.ToString() : mapId;
        hasPendingSceneRestore = false;
        pendingPlayerScene = null;
        pendingRunWorld = null;
        suppressNextRunLoadoutApply = false;

        if (sceneKind == GameSceneKind.Run && string.IsNullOrWhiteSpace(currentRunId))
        {
            currentRunId = Guid.NewGuid().ToString("N");
        }
        else if (sceneKind == GameSceneKind.Base)
        {
            currentRunId = null;
        }
    }

    public void BeginRun(string runMapId)
    {
        currentRunId = Guid.NewGuid().ToString("N");
        SetCurrentScene(GameSceneKind.Run, runMapId);
    }

    public bool ConsumeSuppressNextRunLoadoutApply()
    {
        if (!suppressNextRunLoadoutApply)
        {
            return false;
        }

        suppressNextRunLoadoutApply = false;
        return true;
    }

    public bool ConsumePendingSceneRestore(out PlayerSceneSaveData playerScene, out RunWorldSaveData runWorld)
    {
        playerScene = pendingPlayerScene;
        runWorld = pendingRunWorld;

        if (!hasPendingSceneRestore)
        {
            return false;
        }

        hasPendingSceneRestore = false;
        pendingPlayerScene = null;
        pendingRunWorld = null;
        return true;
    }

    public bool TryGetPendingSceneRestore(out PlayerSceneSaveData playerScene, out RunWorldSaveData runWorld)
    {
        playerScene = pendingPlayerScene;
        runWorld = pendingRunWorld;
        return hasPendingSceneRestore;
    }

    public bool AddItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();
        InventoryAddResult result = stashGrid.AddItem(item, amount);
        if (!result.AddedAny)
        {
            return false;
        }

        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        return result.WasFullyAdded;
    }

    public bool AddItems(IReadOnlyList<InventoryItemStack> itemStacks)
    {
        if (itemStacks == null || itemStacks.Count == 0)
        {
            return false;
        }

        InitializeIfNeeded();
        bool changed = false;

        foreach (InventoryItemStack stack in itemStacks)
        {
            if (stack.Definition == null || stack.Amount <= 0)
            {
                continue;
            }

            InventoryAddResult result = stashGrid.AddItem(stack.Definition, stack.Amount);
            changed |= result.AddedAny;
        }

        if (changed)
        {
            RebuildLegacyStashTotals();
            StashChanged?.Invoke();
        }

        return changed;
    }

    public bool RemoveItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();
        if (!stashGrid.RemoveAmount(item, amount))
        {
            return false;
        }

        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        return true;
    }

    public bool RemoveItems(IReadOnlyList<InventoryItemStack> itemStacks)
    {
        if (itemStacks == null || itemStacks.Count == 0)
        {
            return false;
        }

        InitializeIfNeeded();
        Dictionary<ItemDefinition, int> requestedAmounts = new Dictionary<ItemDefinition, int>();

        foreach (InventoryItemStack stack in itemStacks)
        {
            if (stack.Definition == null || stack.Amount <= 0)
            {
                return false;
            }

            requestedAmounts.TryGetValue(stack.Definition, out int currentRequestedAmount);
            requestedAmounts[stack.Definition] = currentRequestedAmount + stack.Amount;
        }

        RebuildLegacyStashTotals();

        foreach (KeyValuePair<ItemDefinition, int> pair in requestedAmounts)
        {
            if (!stashItems.TryGetValue(pair.Key, out int currentAmount) || currentAmount < pair.Value)
            {
                return false;
            }
        }

        foreach (KeyValuePair<ItemDefinition, int> pair in requestedAmounts)
        {
            stashGrid.RemoveAmount(pair.Key, pair.Value);
        }

        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        return true;
    }

    public bool TryAddRuntimeItemToStash(RuntimeItemInstance item)
    {
        if (item?.Definition == null)
        {
            return false;
        }

        InitializeIfNeeded();
        if (!stashGrid.TryFindFirstSpace(item, out int x, out int y) || !stashGrid.TryPlace(item, x, y))
        {
            return false;
        }

        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        return true;
    }

    public bool CanPlaceRuntimeItemInStash(RuntimeItemInstance item, int x, int y)
    {
        InitializeIfNeeded();
        return stashGrid.CanPlace(item, x, y);
    }

    public bool TryPlaceRuntimeItemInStash(RuntimeItemInstance item, int x, int y)
    {
        if (item?.Definition == null)
        {
            return false;
        }

        InitializeIfNeeded();
        if (!stashGrid.TryPlace(item, x, y))
        {
            return false;
        }

        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        return true;
    }

    public bool RemoveStashPlacement(GridItemPlacement placement)
    {
        InitializeIfNeeded();
        if (!stashGrid.Remove(placement))
        {
            return false;
        }

        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        return true;
    }

    public void AddCredits(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        InitializeIfNeeded();
        credits = amount > int.MaxValue - credits
            ? int.MaxValue
            : credits + amount;
        CreditsChanged?.Invoke();
    }

    public bool CanAddCredits(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();
        return amount <= int.MaxValue - credits;
    }

    public bool TryAddCredits(int amount)
    {
        if (!CanAddCredits(amount))
        {
            return false;
        }

        credits += amount;
        CreditsChanged?.Invoke();
        return true;
    }

    public bool TrySpendCredits(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();
        if (amount > credits)
        {
            return false;
        }

        credits -= amount;
        CreditsChanged?.Invoke();
        return true;
    }

    public bool TrySpendAmmo(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        InitializeIfNeeded();
        if (amount > ammoStock)
        {
            return false;
        }

        ammoStock -= amount;
        AmmoStockChanged?.Invoke();
        return true;
    }

    public void AddAmmo(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        InitializeIfNeeded();
        ammoStock += amount;
        AmmoStockChanged?.Invoke();
    }

    public bool TryPrepareLoadout(int ammoAmount)
    {
        InitializeIfNeeded();

        int requestedAmount = Mathf.Max(0, ammoAmount);
        if (requestedAmount > ammoStock)
        {
            return false;
        }

        if (requestedAmount > 0 && !TrySpendAmmo(requestedAmount))
        {
            return false;
        }

        pendingAmmo = requestedAmount;
        hasPendingLoadout = true;
        return true;
    }

    public bool TryConsumePendingLoadout(out int ammoAmount)
    {
        InitializeIfNeeded();

        if (!hasPendingLoadout)
        {
            ammoAmount = 0;
            return false;
        }

        ammoAmount = Mathf.Max(0, pendingAmmo);
        pendingAmmo = 0;
        hasPendingLoadout = false;
        return true;
    }

    public List<InventoryItemStack> GetStashEntries()
    {
        InitializeIfNeeded();
        RebuildLegacyStashTotals();
        List<InventoryItemStack> result = new List<InventoryItemStack>();

        foreach (KeyValuePair<ItemDefinition, int> pair in stashItems)
        {
            if (pair.Key == null || pair.Value <= 0)
            {
                continue;
            }

            result.Add(new InventoryItemStack(pair.Key, pair.Value));
        }

        return result;
    }

    public void ClearSession()
    {
        InitializeIfNeeded();
        bool hadStash = stashItems.Count > 0;
        stashItems.Clear();
        stashGrid = new GridInventory("Base Stash", stashGridWidth, stashGridHeight);
        playerRuntimeState = new PlayerRuntimeState();
        playerRuntimeState.InitializeIfNeeded(6, 5);
        credits = Mathf.Max(0, initialCredits);
        ammoStock = Mathf.Max(0, initialAmmoStock);
        pendingAmmo = 0;
        hasPendingLoadout = false;
        GrantInitialStashItems();

        if (hadStash || stashItems.Count > 0)
        {
            StashChanged?.Invoke();
        }

        AmmoStockChanged?.Invoke();
        CreditsChanged?.Invoke();
    }

    public void ApplyLoadedProgress(
        int loadedCredits,
        int loadedAmmoStock,
        GridInventory loadedStashGrid,
        PlayerRuntimeState loadedPlayerRuntimeState,
        SceneProgressSaveData loadedScene = null,
        PlayerSceneSaveData loadedPlayerScene = null,
        RunWorldSaveData loadedRunWorld = null)
    {
        if (loadedStashGrid == null || loadedPlayerRuntimeState == null)
        {
            Debug.LogError("Cannot apply loaded progress because the restored runtime state is incomplete.", this);
            return;
        }

        stashItems ??= new Dictionary<ItemDefinition, int>();
        stashGrid = loadedStashGrid;
        playerRuntimeState = loadedPlayerRuntimeState;
        credits = Mathf.Max(0, loadedCredits);
        ammoStock = Mathf.Max(0, loadedAmmoStock);
        pendingAmmo = 0;
        hasPendingLoadout = false;
        if (loadedScene != null && Enum.TryParse(loadedScene.sceneKind, out GameSceneKind sceneKind))
        {
            currentSceneKind = sceneKind;
            currentMapId = loadedScene.mapId;
        }
        else
        {
            currentSceneKind = GameSceneKind.Base;
            currentMapId = "base";
        }

        currentRunId = loadedRunWorld != null ? loadedRunWorld.runId : null;
        hasPendingSceneRestore = loadedPlayerScene != null;
        pendingPlayerScene = loadedPlayerScene;
        pendingRunWorld = loadedRunWorld;
        suppressNextRunLoadoutApply = currentSceneKind == GameSceneKind.Run;
        initialized = true;
        RebuildLegacyStashTotals();
        StashChanged?.Invoke();
        AmmoStockChanged?.Invoke();
        CreditsChanged?.Invoke();
    }

    private void GrantInitialStashItems()
    {
        if (initialStashItems == null)
        {
            return;
        }

        foreach (InitialStashEntry entry in initialStashItems)
        {
            if (entry?.Item == null || entry.Amount <= 0)
            {
                continue;
            }

            stashItems.TryGetValue(entry.Item, out int currentAmount);
            stashItems[entry.Item] = currentAmount + entry.Amount;
            stashGrid.AddItem(entry.Item, entry.Amount);
        }

        RebuildLegacyStashTotals();
    }

    private void RebuildLegacyStashTotals()
    {
        stashItems.Clear();
        if (stashGrid == null)
        {
            return;
        }

        foreach (GridItemPlacement placement in stashGrid.Placements)
        {
            if (placement.Definition == null || placement.Item.Quantity <= 0)
            {
                continue;
            }

            stashItems.TryGetValue(placement.Definition, out int currentAmount);
            stashItems[placement.Definition] = currentAmount + placement.Item.Quantity;
        }
    }
}
