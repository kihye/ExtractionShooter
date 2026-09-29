using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Extraction Shooter/Shop Catalog", fileName = "ShopCatalog")]
public sealed class ShopCatalog : ScriptableObject
{
    [Serializable]
    public sealed class BuyEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int unitPrice = 1;

        public ItemDefinition Item => item;
        public int UnitPrice => Mathf.Max(1, unitPrice);
        internal bool HasValidUnitPrice => unitPrice > 0;
    }

    [Serializable]
    public sealed class SellEntry
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField, Min(1)] private int unitPrice = 1;

        public ItemDefinition Item => item;
        public int UnitPrice => Mathf.Max(1, unitPrice);
        internal bool HasValidUnitPrice => unitPrice > 0;
    }

    [SerializeField] private string shopName = "Base Trader";
    [SerializeField] private string currencyDisplayName = "Credits";
    [SerializeField] private List<BuyEntry> buyEntries = new List<BuyEntry>();
    [SerializeField] private List<SellEntry> sellEntries = new List<SellEntry>();

    public string ShopName => string.IsNullOrWhiteSpace(shopName) ? name : shopName;
    public string CurrencyDisplayName => string.IsNullOrWhiteSpace(currencyDisplayName) ? "Credits" : currencyDisplayName;
    public IReadOnlyList<BuyEntry> BuyEntries => buyEntries;
    public IReadOnlyList<SellEntry> SellEntries => sellEntries;

    public bool TryGetBuyEntry(ItemDefinition item, out BuyEntry entry)
    {
        entry = null;
        if (item == null)
        {
            return false;
        }

        foreach (BuyEntry candidate in buyEntries)
        {
            if (candidate?.Item == item)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetSellEntry(ItemDefinition item, out SellEntry entry)
    {
        entry = null;
        if (item == null)
        {
            return false;
        }

        foreach (SellEntry candidate in sellEntries)
        {
            if (candidate?.Item == item)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    public List<string> ValidateCatalog()
    {
        List<string> issues = new List<string>();
        HashSet<ItemDefinition> buyItems = new HashSet<ItemDefinition>();
        HashSet<ItemDefinition> sellItems = new HashSet<ItemDefinition>();

        foreach (BuyEntry entry in buyEntries)
        {
            if (entry == null || entry.Item == null)
            {
                issues.Add("Buy entry has no item.");
                continue;
            }

            if (!buyItems.Add(entry.Item))
            {
                issues.Add($"Duplicate buy entry: {entry.Item.DisplayName}.");
            }

            if (!entry.HasValidUnitPrice)
            {
                issues.Add($"Buy price must be positive: {entry.Item.DisplayName}.");
            }
        }

        foreach (SellEntry entry in sellEntries)
        {
            if (entry == null || entry.Item == null)
            {
                issues.Add("Sell entry has no item.");
                continue;
            }

            if (!sellItems.Add(entry.Item))
            {
                issues.Add($"Duplicate sell entry: {entry.Item.DisplayName}.");
            }

            if (!entry.HasValidUnitPrice)
            {
                issues.Add($"Sell price must be positive: {entry.Item.DisplayName}.");
            }
        }

        foreach (BuyEntry buyEntry in buyEntries)
        {
            if (buyEntry?.Item == null || !TryGetSellEntry(buyEntry.Item, out SellEntry sellEntry))
            {
                continue;
            }

            if (sellEntry.UnitPrice > buyEntry.UnitPrice)
            {
                issues.Add($"Sell price exceeds buy price: {buyEntry.Item.DisplayName}.");
            }
        }

        return issues;
    }

    private void OnValidate()
    {
        foreach (string issue in ValidateCatalog())
        {
            Debug.LogWarning($"{name}: {issue}", this);
        }
    }
}
