using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GridInventory
{
    [SerializeField] private string displayName;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private List<GridItemPlacement> placements = new List<GridItemPlacement>();

    public GridInventory(string displayName, int width, int height)
    {
        this.displayName = displayName;
        this.width = Mathf.Max(1, width);
        this.height = Mathf.Max(1, height);
    }

    public string DisplayName => displayName;
    public int Width => width;
    public int Height => height;
    public IReadOnlyList<GridItemPlacement> Placements => placements;

    public InventoryAddResult AddItem(ItemDefinition definition, int amount)
    {
        if (definition == null || amount <= 0)
        {
            return new InventoryAddResult(definition, Mathf.Max(0, amount), 0);
        }

        int requestedAmount = amount;
        int remainingAmount = amount;

        if (definition.CanStack)
        {
            foreach (GridItemPlacement placement in placements)
            {
                if (!placement.Item.CanStackWith(definition))
                {
                    continue;
                }

                remainingAmount = placement.Item.AddToStack(remainingAmount);
                if (remainingAmount <= 0)
                {
                    break;
                }
            }
        }

        while (remainingAmount > 0)
        {
            int stackAmount = definition.CanStack ? Mathf.Min(definition.MaxStack, remainingAmount) : 1;
            RuntimeItemInstance instance = new RuntimeItemInstance(definition, stackAmount);

            if (!TryFindFirstSpace(instance, out int x, out int y))
            {
                break;
            }

            placements.Add(new GridItemPlacement(instance, x, y));
            remainingAmount -= stackAmount;
        }

        return new InventoryAddResult(definition, requestedAmount, requestedAmount - remainingAmount);
    }

    public bool CanPlace(RuntimeItemInstance item, int x, int y)
    {
        return CanPlace(item, x, y, null);
    }

    public bool CanPlaceIgnoring(RuntimeItemInstance item, int x, int y, GridItemPlacement ignoredPlacement)
    {
        return CanPlace(item, x, y, ignoredPlacement);
    }

    public bool TryPlace(RuntimeItemInstance item, int x, int y)
    {
        if (!CanPlace(item, x, y))
        {
            return false;
        }

        placements.Add(new GridItemPlacement(item, x, y));
        return true;
    }

    public bool TryMove(GridItemPlacement placement, int newX, int newY)
    {
        if (placement == null || !placements.Contains(placement) || !CanPlace(placement.Item, newX, newY, placement))
        {
            return false;
        }

        placement.SetPosition(newX, newY);
        return true;
    }

    public bool Contains(GridItemPlacement placement)
    {
        return placement != null && placements.Contains(placement);
    }

    public bool Remove(GridItemPlacement placement)
    {
        return placement != null && placements.Remove(placement);
    }

    public bool RemoveAmount(ItemDefinition definition, int amount)
    {
        if (definition == null || amount <= 0)
        {
            return false;
        }

        int availableAmount = Count(definition);
        if (availableAmount < amount)
        {
            return false;
        }

        int remainingAmount = amount;
        for (int i = placements.Count - 1; i >= 0 && remainingAmount > 0; i--)
        {
            GridItemPlacement placement = placements[i];
            if (placement.Definition != definition)
            {
                continue;
            }

            int removed = placement.Item.RemoveFromStack(remainingAmount);
            remainingAmount -= removed;

            if (placement.Item.Quantity <= 0)
            {
                placements.RemoveAt(i);
            }
        }

        return true;
    }

    public int Count(ItemDefinition definition)
    {
        if (definition == null)
        {
            return 0;
        }

        int count = 0;
        foreach (GridItemPlacement placement in placements)
        {
            if (placement.Definition == definition)
            {
                count += placement.Item.Quantity;
            }
        }

        return count;
    }

    public GridItemPlacement GetCellOccupant(int x, int y)
    {
        foreach (GridItemPlacement placement in placements)
        {
            if (placement.Occupies(x, y))
            {
                return placement;
            }
        }

        return null;
    }

    public bool TryFindFirstSpace(RuntimeItemInstance item, out int x, out int y)
    {
        x = 0;
        y = 0;

        if (item?.Definition == null)
        {
            return false;
        }

        for (int candidateY = 0; candidateY <= height - item.Definition.GridHeight; candidateY++)
        {
            for (int candidateX = 0; candidateX <= width - item.Definition.GridWidth; candidateX++)
            {
                if (CanPlace(item, candidateX, candidateY))
                {
                    x = candidateX;
                    y = candidateY;
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanPlace(RuntimeItemInstance item, int x, int y, GridItemPlacement ignoredPlacement)
    {
        if (item?.Definition == null || x < 0 || y < 0)
        {
            return false;
        }

        int itemWidth = item.Definition.GridWidth;
        int itemHeight = item.Definition.GridHeight;
        if (x + itemWidth > width || y + itemHeight > height)
        {
            return false;
        }

        foreach (GridItemPlacement placement in placements)
        {
            if (placement == ignoredPlacement)
            {
                continue;
            }

            if (Overlaps(x, y, itemWidth, itemHeight, placement.X, placement.Y, placement.Width, placement.Height))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Overlaps(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
    {
        return ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
    }
}
