using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StashInventory : MonoBehaviour
{
    public event Action StashChanged;

    [SerializeField] private GameSessionState sessionState;

    private GameSessionState runtimeFallbackState;

    public GameSessionState SessionState => EnsureSessionState();

    private void Awake()
    {
        EnsureSessionState();
    }

    private void OnEnable()
    {
        GameSessionState state = EnsureSessionState();
        state.StashChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (sessionState != null)
        {
            sessionState.StashChanged -= HandleStateChanged;
        }
    }

    public bool AddItem(ItemDefinition item, int amount)
    {
        return EnsureSessionState().AddItem(item, amount);
    }

    public bool TryAddRuntimeItem(RuntimeItemInstance item)
    {
        return EnsureSessionState().TryAddRuntimeItemToStash(item);
    }

    public bool CanPlaceRuntimeItem(RuntimeItemInstance item, int x, int y)
    {
        return EnsureSessionState().CanPlaceRuntimeItemInStash(item, x, y);
    }

    public bool TryPlaceRuntimeItem(RuntimeItemInstance item, int x, int y)
    {
        return EnsureSessionState().TryPlaceRuntimeItemInStash(item, x, y);
    }

    public bool RemovePlacement(GridItemPlacement placement)
    {
        return EnsureSessionState().RemoveStashPlacement(placement);
    }

    public bool RemoveItem(ItemDefinition item, int amount)
    {
        return EnsureSessionState().RemoveItem(item, amount);
    }

    public bool RemoveItems(IReadOnlyList<InventoryItemStack> itemStacks)
    {
        return EnsureSessionState().RemoveItems(itemStacks);
    }

    public List<InventoryItemStack> GetEntries()
    {
        return EnsureSessionState().GetStashEntries();
    }

    public GridInventory GetGrid()
    {
        return EnsureSessionState().StashGrid;
    }

    public void LogContents()
    {
        Debug.Log("Stash:\n" + InventoryLogFormatter.Format(GetEntries()), this);
    }

    private GameSessionState EnsureSessionState()
    {
        if (sessionState == null)
        {
            runtimeFallbackState ??= ScriptableObject.CreateInstance<GameSessionState>();
            runtimeFallbackState.name = "Runtime Game Session State";
            sessionState = runtimeFallbackState;
        }

        sessionState.InitializeIfNeeded();
        return sessionState;
    }

    private void HandleStateChanged()
    {
        StashChanged?.Invoke();
    }
}
