using System;
using UnityEngine;

[Serializable]
public sealed class GridItemPlacement
{
    [SerializeField] private RuntimeItemInstance item;
    [SerializeField] private int x;
    [SerializeField] private int y;

    public GridItemPlacement(RuntimeItemInstance item, int x, int y)
    {
        this.item = item;
        this.x = x;
        this.y = y;
    }

    public RuntimeItemInstance Item => item;
    public ItemDefinition Definition => item?.Definition;
    public int X => x;
    public int Y => y;
    public int Width => Definition != null ? Definition.GridWidth : 1;
    public int Height => Definition != null ? Definition.GridHeight : 1;

    public void SetPosition(int newX, int newY)
    {
        x = newX;
        y = newY;
    }

    public bool Occupies(int cellX, int cellY)
    {
        return cellX >= x && cellX < x + Width && cellY >= y && cellY < y + Height;
    }
}
