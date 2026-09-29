using System.Collections.Generic;
using UnityEngine;

public sealed class BaseEconomyController : MonoBehaviour
{
    [SerializeField] private StashInventory stashInventory;
    [SerializeField] private GameSessionState sessionState;
    [SerializeField, Min(1)] private int ammoPurchaseAmount = 12;
    [SerializeField, Min(0)] private int ammoPurchasePrice = 20;

    public StashInventory StashInventory => stashInventory;
    public GameSessionState SessionState => sessionState;
    public int AmmoPurchaseAmount => Mathf.Max(1, ammoPurchaseAmount);
    public int AmmoPurchasePrice => Mathf.Max(0, ammoPurchasePrice);

    private void Awake()
    {
        EnsureReferences();
    }

    public int CalculateSellAllValue()
    {
        if (!EnsureReferences())
        {
            return 0;
        }

        int totalValue = 0;
        List<InventoryItemStack> entries = stashInventory.GetEntries();

        foreach (InventoryItemStack entry in entries)
        {
            if (entry.Definition == null || entry.Amount <= 0 || entry.Definition.SellValue <= 0)
            {
                continue;
            }

            totalValue += entry.Definition.SellValue * entry.Amount;
        }

        return totalValue;
    }

    public bool CanBuyAmmo()
    {
        if (!EnsureReferences())
        {
            return false;
        }

        return AmmoPurchaseAmount > 0 && sessionState.Credits >= AmmoPurchasePrice;
    }

    public bool TryBuyAmmo()
    {
        if (!CanBuyAmmo())
        {
            return false;
        }

        int price = AmmoPurchasePrice;
        if (price > 0 && !sessionState.TrySpendCredits(price))
        {
            return false;
        }

        sessionState.AddAmmo(AmmoPurchaseAmount);
        Debug.Log($"Bought {AmmoPurchaseAmount} ammo for {price} credits.", this);
        return true;
    }

    public bool TrySellAllLoot()
    {
        if (!EnsureReferences())
        {
            return false;
        }

        List<InventoryItemStack> sellableItems = new List<InventoryItemStack>();
        int totalValue = 0;

        foreach (InventoryItemStack entry in stashInventory.GetEntries())
        {
            if (entry.Definition == null || entry.Amount <= 0 || entry.Definition.SellValue <= 0)
            {
                continue;
            }

            sellableItems.Add(entry);
            totalValue += entry.Definition.SellValue * entry.Amount;
        }

        if (sellableItems.Count == 0 || totalValue <= 0)
        {
            return false;
        }

        if (!stashInventory.RemoveItems(sellableItems))
        {
            Debug.LogWarning("Sell All Loot failed because stash contents changed before removal.", this);
            return false;
        }

        sessionState.AddCredits(totalValue);
        Debug.Log($"Sold loot for {totalValue} credits.", this);
        return true;
    }

    private bool EnsureReferences()
    {
        stashInventory ??= FindFirstObjectByType<StashInventory>();

        if (sessionState == null && stashInventory != null)
        {
            sessionState = stashInventory.SessionState;
        }

        return stashInventory != null && sessionState != null;
    }
}
