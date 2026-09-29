using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class PlayerInventory : MonoBehaviour
{
    public event Action InventoryChanged;

    [Serializable]
    public sealed class InventoryEntry
    {
        [SerializeField] private ItemDefinition itemData;
        [SerializeField] private int amount;

        public ItemDefinition Definition => itemData;
        public ItemData ItemData => itemData as ItemData;
        public int Amount => amount;
        public bool CanStack(ItemDefinition item) => itemData != null && itemData == item && amount < itemData.MaxStack;

        public InventoryEntry(ItemDefinition itemData, int amount)
        {
            this.itemData = itemData;
            this.amount = amount;
        }

        public int Add(int amountToAdd)
        {
            int space = itemData.MaxStack - amount;
            int added = Mathf.Min(space, amountToAdd);
            amount += added;
            return amountToAdd - added;
        }
    }

    [SerializeField, Min(1)] private int defaultBagWidth = 6;
    [SerializeField, Min(1)] private int defaultBagHeight = 5;

    private List<GridInventory> grids = new List<GridInventory>();
    private readonly List<InventoryEntry> compatibilityEntries = new List<InventoryEntry>();

    public IReadOnlyList<InventoryEntry> Items
    {
        get
        {
            RebuildCompatibilityEntries();
            return compatibilityEntries;
        }
    }

    public IReadOnlyList<GridInventory> Grids => grids;

    public int DefaultBagWidth => Mathf.Max(1, defaultBagWidth);
    public int DefaultBagHeight => Mathf.Max(1, defaultBagHeight);

    private void Awake()
    {
        EnsureDefaultGrid();
    }

    public bool AddItem(ItemData item, int amount)
    {
        return AddItem((ItemDefinition)item, amount);
    }

    public void BindRuntimeGrids(List<GridInventory> runtimeGrids)
    {
        if (runtimeGrids == null)
        {
            return;
        }

        grids = runtimeGrids;
        EnsureDefaultGrid();
        InventoryChanged?.Invoke();
    }

    public List<GridInventory> GetRuntimeGrids()
    {
        EnsureDefaultGrid();
        return grids;
    }

    public bool AddItem(ItemDefinition item, int amount)
    {
        InventoryAddResult result = TryAddItem(item, amount);
        return result.WasFullyAdded;
    }

    public InventoryAddResult TryAddItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return new InventoryAddResult(item, Mathf.Max(0, amount), 0);
        }

        EnsureDefaultGrid();
        int remaining = amount;
        int added = 0;

        foreach (GridInventory grid in grids)
        {
            InventoryAddResult result = grid.AddItem(item, remaining);
            added += result.AddedAmount;
            remaining = result.RemainingAmount;

            if (remaining <= 0)
            {
                break;
            }
        }

        if (added > 0)
        {
            Debug.Log($"Picked up {item.DisplayName} x{added}", this);
            InventoryChanged?.Invoke();
            LogInventory();
        }

        return new InventoryAddResult(item, amount, added);
    }

    public bool CanFullyAddItem(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return false;
        }

        EnsureDefaultGrid();
        int remaining = amount;
        List<GridInventory> simulatedGrids = BuildSimulatedGrids();

        foreach (GridInventory grid in simulatedGrids)
        {
            InventoryAddResult result = grid.AddItem(item, remaining);
            remaining = result.RemainingAmount;

            if (remaining <= 0)
            {
                return true;
            }
        }

        return false;
    }

    public InventoryAddResult TryAddItemAtomic(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return new InventoryAddResult(item, Mathf.Max(0, amount), 0);
        }

        if (!CanFullyAddItem(item, amount))
        {
            return new InventoryAddResult(item, amount, 0);
        }

        EnsureDefaultGrid();
        int remaining = amount;
        int added = 0;
        List<StackAddition> stackAdditions = new List<StackAddition>();
        List<PlacedItemAddition> placedItems = new List<PlacedItemAddition>();

        if (item.CanStack)
        {
            foreach (GridInventory grid in grids)
            {
                foreach (GridItemPlacement placement in grid.Placements)
                {
                    if (!placement.Item.CanStackWith(item))
                    {
                        continue;
                    }

                    int before = placement.Item.Quantity;
                    remaining = placement.Item.AddToStack(remaining);
                    int stackAdded = placement.Item.Quantity - before;

                    if (stackAdded > 0)
                    {
                        stackAdditions.Add(new StackAddition(placement.Item, stackAdded));
                        added += stackAdded;
                    }

                    if (remaining <= 0)
                    {
                        NotifyAtomicAddSucceeded(item, added);
                        return new InventoryAddResult(item, amount, added);
                    }
                }
            }
        }

        while (remaining > 0)
        {
            int stackAmount = item.CanStack ? Mathf.Min(item.MaxStack, remaining) : 1;
            RuntimeItemInstance instance = new RuntimeItemInstance(item, stackAmount);

            if (!TryPlaceNewItemInAnyGrid(instance, out GridInventory targetGrid, out GridItemPlacement placement))
            {
                RollbackAtomicAdd(stackAdditions, placedItems);
                return new InventoryAddResult(item, amount, 0);
            }

            placedItems.Add(new PlacedItemAddition(targetGrid, placement));
            remaining -= stackAmount;
            added += stackAmount;
        }

        NotifyAtomicAddSucceeded(item, added);
        return new InventoryAddResult(item, amount, added);
    }

    public GridInventory AddGrid(string displayName, int width, int height, bool notify = true)
    {
        GridInventory grid = new GridInventory(displayName, width, height);
        grids.Add(grid);
        if (notify)
        {
            InventoryChanged?.Invoke();
        }

        return grid;
    }

    public bool RemoveGrid(GridInventory grid, bool notify = true)
    {
        if (grid == null || !grids.Contains(grid) || grid.Placements.Count > 0 || grids.Count <= 1)
        {
            return false;
        }

        bool removed = grids.Remove(grid);
        if (removed && notify)
        {
            InventoryChanged?.Invoke();
        }

        return removed;
    }

    public bool CanMoveItem(GridInventory sourceGrid, GridItemPlacement placement, GridInventory targetGrid, int targetX, int targetY)
    {
        if (sourceGrid == null || targetGrid == null || placement?.Item?.Definition == null || !sourceGrid.Contains(placement))
        {
            return false;
        }

        GridItemPlacement targetPlacement = targetGrid.GetCellOccupant(targetX, targetY);
        if (CanMergeStacks(placement, targetPlacement))
        {
            return true;
        }

        return sourceGrid == targetGrid
            ? targetGrid.CanPlaceIgnoring(placement.Item, targetX, targetY, placement)
            : targetGrid.CanPlace(placement.Item, targetX, targetY);
    }

    public bool CanPlaceItem(GridInventory targetGrid, RuntimeItemInstance item, int targetX, int targetY, GridItemPlacement ignoredPlacement = null)
    {
        if (targetGrid == null || item?.Definition == null)
        {
            return false;
        }

        return ignoredPlacement != null
            ? targetGrid.CanPlaceIgnoring(item, targetX, targetY, ignoredPlacement)
            : targetGrid.CanPlace(item, targetX, targetY);
    }

    public bool TryMoveItem(GridInventory sourceGrid, GridItemPlacement placement, GridInventory targetGrid, int targetX, int targetY)
    {
        if (sourceGrid == null || targetGrid == null || placement?.Item?.Definition == null || !sourceGrid.Contains(placement))
        {
            return false;
        }

        GridItemPlacement targetPlacement = targetGrid.GetCellOccupant(targetX, targetY);
        if (CanMergeStacks(placement, targetPlacement))
        {
            MergeStacks(placement, targetPlacement, sourceGrid);
            InventoryChanged?.Invoke();
            return true;
        }

        bool moved = sourceGrid == targetGrid
            ? sourceGrid.TryMove(placement, targetX, targetY)
            : TryMoveBetweenGrids(sourceGrid, placement, targetGrid, targetX, targetY);

        if (moved)
        {
            InventoryChanged?.Invoke();
        }

        return moved;
    }

    public bool TryPlaceItem(GridInventory targetGrid, RuntimeItemInstance item, int targetX, int targetY, bool notify = true)
    {
        if (targetGrid == null || item?.Definition == null || !targetGrid.TryPlace(item, targetX, targetY))
        {
            return false;
        }

        if (notify)
        {
            InventoryChanged?.Invoke();
        }

        return true;
    }

    public bool RemovePlacement(GridInventory sourceGrid, GridItemPlacement placement, bool notify = true)
    {
        if (sourceGrid == null || placement == null || !sourceGrid.Remove(placement))
        {
            return false;
        }

        if (notify)
        {
            InventoryChanged?.Invoke();
        }

        return true;
    }

    public bool TryFindPlacement(RuntimeItemInstance item, out GridInventory sourceGrid, out GridItemPlacement placement)
    {
        sourceGrid = null;
        placement = null;

        if (item == null)
        {
            return false;
        }

        EnsureDefaultGrid();
        foreach (GridInventory grid in grids)
        {
            foreach (GridItemPlacement candidate in grid.Placements)
            {
                if (ReferenceEquals(candidate.Item, item))
                {
                    sourceGrid = grid;
                    placement = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryRemoveFromPlacement(GridInventory sourceGrid, GridItemPlacement placement, int amount, bool notify = true)
    {
        if (sourceGrid == null
            || placement?.Item == null
            || amount <= 0
            || !sourceGrid.Contains(placement)
            || placement.Item.Quantity < amount)
        {
            return false;
        }

        placement.Item.RemoveFromStack(amount);
        if (placement.Item.Quantity <= 0)
        {
            sourceGrid.Remove(placement);
        }

        if (notify)
        {
            InventoryChanged?.Invoke();
        }

        return true;
    }

    public bool TryFindSpaceForItem(
        RuntimeItemInstance item,
        GridInventory preferredGrid,
        int preferredX,
        int preferredY,
        GridItemPlacement ignoredPlacement,
        GridInventory excludedGrid,
        out GridInventory targetGrid,
        out int targetX,
        out int targetY)
    {
        targetGrid = null;
        targetX = 0;
        targetY = 0;

        if (item?.Definition == null)
        {
            return false;
        }

        EnsureDefaultGrid();

        if (preferredGrid != null
            && preferredGrid != excludedGrid
            && grids.Contains(preferredGrid)
            && CanPlaceItem(preferredGrid, item, preferredX, preferredY, ignoredPlacement))
        {
            targetGrid = preferredGrid;
            targetX = preferredX;
            targetY = preferredY;
            return true;
        }

        foreach (GridInventory grid in grids)
        {
            if (grid == null || grid == excludedGrid)
            {
                continue;
            }

            for (int y = 0; y <= grid.Height - item.Definition.GridHeight; y++)
            {
                for (int x = 0; x <= grid.Width - item.Definition.GridWidth; x++)
                {
                    GridItemPlacement ignoredForGrid = grid == preferredGrid ? ignoredPlacement : null;
                    if (CanPlaceItem(grid, item, x, y, ignoredForGrid))
                    {
                        targetGrid = grid;
                        targetX = x;
                        targetY = y;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }

    public List<InventoryItemStack> GetEntries()
    {
        Dictionary<ItemDefinition, int> totals = BuildTotals();
        return BuildEntriesFromTotals(totals);
    }

    public List<InventoryItemStack> GetEntriesWithoutAmmo()
    {
        Dictionary<ItemDefinition, int> totals = BuildTotals();
        List<InventoryItemStack> result = new List<InventoryItemStack>();

        foreach (KeyValuePair<ItemDefinition, int> pair in totals)
        {
            if (pair.Key == null || pair.Key.Category == ItemCategory.Ammo)
            {
                continue;
            }

            result.Add(new InventoryItemStack(pair.Key, pair.Value));
        }

        return result;
    }

    public int GetItemCount(ItemDefinition item)
    {
        if (item == null)
        {
            return 0;
        }

        EnsureDefaultGrid();
        int count = 0;

        foreach (GridInventory grid in grids)
        {
            count += grid.Count(item);
        }

        return count;
    }

    public int GetCompatibleAmmoCount(AmmoType ammoType)
    {
        EnsureDefaultGrid();
        int count = 0;

        foreach (GridInventory grid in grids)
        {
            foreach (GridItemPlacement placement in grid.Placements)
            {
                if (IsCompatibleAmmo(placement.Definition, ammoType))
                {
                    count += placement.Item.Quantity;
                }
            }
        }

        return count;
    }

    public int ConsumeCompatibleAmmo(AmmoType ammoType, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        EnsureDefaultGrid();
        int remainingAmount = amount;
        int consumedAmount = 0;

        foreach (GridInventory grid in grids)
        {
            List<GridItemPlacement> placements = new List<GridItemPlacement>(grid.Placements);
            foreach (GridItemPlacement placement in placements)
            {
                if (!IsCompatibleAmmo(placement.Definition, ammoType))
                {
                    continue;
                }

                int removed = placement.Item.RemoveFromStack(remainingAmount);
                consumedAmount += removed;
                remainingAmount -= removed;

                if (placement.Item.Quantity <= 0)
                {
                    grid.Remove(placement);
                }

                if (remainingAmount <= 0)
                {
                    break;
                }
            }

            if (remainingAmount <= 0)
            {
                break;
            }
        }

        if (consumedAmount > 0)
        {
            InventoryChanged?.Invoke();
            LogInventory();
        }

        return consumedAmount;
    }

    private static List<InventoryItemStack> BuildEntriesFromTotals(Dictionary<ItemDefinition, int> totals)
    {
        List<InventoryItemStack> result = new List<InventoryItemStack>();

        foreach (KeyValuePair<ItemDefinition, int> pair in totals)
        {
            result.Add(new InventoryItemStack(pair.Key, pair.Value));
        }

        return result;
    }

    public void Clear()
    {
        foreach (GridInventory grid in grids)
        {
            List<GridItemPlacement> placements = new List<GridItemPlacement>(grid.Placements);
            foreach (GridItemPlacement placement in placements)
            {
                grid.Remove(placement);
            }
        }

        InventoryChanged?.Invoke();
    }

    public List<InventoryItemStack> TransferAllTo(StashInventory targetInventory)
    {
        List<InventoryItemStack> transferred = GetEntriesWithoutAmmo();

        if (targetInventory == null)
        {
            return transferred;
        }

        foreach (GridInventory grid in grids)
        {
            List<GridItemPlacement> placements = new List<GridItemPlacement>(grid.Placements);
            foreach (GridItemPlacement placement in placements)
            {
                if (placement.Item?.Definition == null || placement.Definition.Category == ItemCategory.Ammo)
                {
                    continue;
                }

                if (targetInventory.TryAddRuntimeItem(placement.Item))
                {
                    grid.Remove(placement);
                }
            }
        }

        foreach (GridInventory grid in grids)
        {
            List<GridItemPlacement> placements = new List<GridItemPlacement>(grid.Placements);
            foreach (GridItemPlacement placement in placements)
            {
                if (placement.Definition != null && placement.Definition.Category == ItemCategory.Ammo)
                {
                    grid.Remove(placement);
                }
            }
        }

        InventoryChanged?.Invoke();
        return transferred;
    }

    private static bool IsCompatibleAmmo(ItemDefinition definition, AmmoType ammoType)
    {
        return definition != null
            && definition.Category == ItemCategory.Ammo
            && definition.AmmoType == ammoType;
    }

    private void LogInventory()
    {
        Debug.Log("Inventory:\n" + InventoryLogFormatter.Format(GetEntries()), this);
    }

    private static bool CanMergeStacks(GridItemPlacement sourcePlacement, GridItemPlacement targetPlacement)
    {
        if (sourcePlacement == null || targetPlacement == null || sourcePlacement == targetPlacement)
        {
            return false;
        }

        RuntimeItemInstance sourceItem = sourcePlacement.Item;
        RuntimeItemInstance targetItem = targetPlacement.Item;
        return sourceItem?.Definition != null
            && targetItem?.Definition != null
            && sourceItem.Definition == targetItem.Definition
            && sourceItem.Definition.CanStack
            && targetItem.Quantity < targetItem.Definition.MaxStack;
    }

    private static void MergeStacks(GridItemPlacement sourcePlacement, GridItemPlacement targetPlacement, GridInventory sourceGrid)
    {
        int remaining = targetPlacement.Item.AddToStack(sourcePlacement.Item.Quantity);
        int movedAmount = sourcePlacement.Item.Quantity - remaining;
        sourcePlacement.Item.RemoveFromStack(movedAmount);

        if (sourcePlacement.Item.Quantity <= 0)
        {
            sourceGrid.Remove(sourcePlacement);
        }
    }

    private static bool TryMoveBetweenGrids(GridInventory sourceGrid, GridItemPlacement placement, GridInventory targetGrid, int targetX, int targetY)
    {
        if (!targetGrid.CanPlace(placement.Item, targetX, targetY))
        {
            return false;
        }

        sourceGrid.Remove(placement);
        if (targetGrid.TryPlace(placement.Item, targetX, targetY))
        {
            return true;
        }

        sourceGrid.TryPlace(placement.Item, placement.X, placement.Y);
        return false;
    }

    public string FormatGridDebugText()
    {
        EnsureDefaultGrid();
        StringBuilder builder = new StringBuilder();

        foreach (GridInventory grid in grids)
        {
            builder.AppendLine($"{grid.DisplayName} {grid.Width}x{grid.Height}");

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    builder.Append(grid.GetCellOccupant(x, y) == null ? "." : "#");
                }

                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private Dictionary<ItemDefinition, int> BuildTotals()
    {
        EnsureDefaultGrid();
        Dictionary<ItemDefinition, int> totals = new Dictionary<ItemDefinition, int>();

        foreach (GridInventory grid in grids)
        {
            foreach (GridItemPlacement placement in grid.Placements)
            {
                if (placement.Definition == null || placement.Item.Quantity <= 0)
                {
                    continue;
                }

                totals.TryGetValue(placement.Definition, out int currentAmount);
                totals[placement.Definition] = currentAmount + placement.Item.Quantity;
            }
        }

        return totals;
    }

    private List<GridInventory> BuildSimulatedGrids()
    {
        List<GridInventory> simulatedGrids = new List<GridInventory>(grids.Count);

        foreach (GridInventory grid in grids)
        {
            GridInventory simulatedGrid = new GridInventory(grid.DisplayName, grid.Width, grid.Height);
            foreach (GridItemPlacement placement in grid.Placements)
            {
                if (placement.Item?.Definition == null)
                {
                    continue;
                }

                RuntimeItemInstance simulatedItem = new RuntimeItemInstance(
                    placement.Item.Definition,
                    placement.Item.Quantity,
                    placement.Item.UpgradeLevel);
                simulatedItem.SetCurrentMagazineAmmo(
                    placement.Item.CurrentMagazineAmmo,
                    placement.Item.Definition.WeaponMagazineSize);
                simulatedGrid.TryPlace(simulatedItem, placement.X, placement.Y);
            }

            simulatedGrids.Add(simulatedGrid);
        }

        return simulatedGrids;
    }

    private bool TryPlaceNewItemInAnyGrid(RuntimeItemInstance item, out GridInventory targetGrid, out GridItemPlacement placement)
    {
        targetGrid = null;
        placement = null;

        foreach (GridInventory grid in grids)
        {
            if (!grid.TryFindFirstSpace(item, out int x, out int y) || !grid.TryPlace(item, x, y))
            {
                continue;
            }

            foreach (GridItemPlacement candidate in grid.Placements)
            {
                if (ReferenceEquals(candidate.Item, item))
                {
                    targetGrid = grid;
                    placement = candidate;
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private void NotifyAtomicAddSucceeded(ItemDefinition item, int added)
    {
        if (added <= 0)
        {
            return;
        }

        Debug.Log($"Picked up {item.DisplayName} x{added}", this);
        InventoryChanged?.Invoke();
        LogInventory();
    }

    private static void RollbackAtomicAdd(List<StackAddition> stackAdditions, List<PlacedItemAddition> placedItems)
    {
        foreach (StackAddition stackAddition in stackAdditions)
        {
            stackAddition.Item.RemoveFromStack(stackAddition.Amount);
        }

        foreach (PlacedItemAddition placedItem in placedItems)
        {
            placedItem.Grid.Remove(placedItem.Placement);
        }
    }

    private readonly struct StackAddition
    {
        public StackAddition(RuntimeItemInstance item, int amount)
        {
            Item = item;
            Amount = amount;
        }

        public RuntimeItemInstance Item { get; }
        public int Amount { get; }
    }

    private readonly struct PlacedItemAddition
    {
        public PlacedItemAddition(GridInventory grid, GridItemPlacement placement)
        {
            Grid = grid;
            Placement = placement;
        }

        public GridInventory Grid { get; }
        public GridItemPlacement Placement { get; }
    }

    private void RebuildCompatibilityEntries()
    {
        compatibilityEntries.Clear();

        foreach (InventoryItemStack stack in GetEntries())
        {
            compatibilityEntries.Add(new InventoryEntry(stack.Definition, stack.Amount));
        }
    }

    private void EnsureDefaultGrid()
    {
        if (grids.Count > 0)
        {
            return;
        }

        grids.Add(new GridInventory("Bag 1 - Basic Bag", defaultBagWidth, defaultBagHeight));
    }
}
