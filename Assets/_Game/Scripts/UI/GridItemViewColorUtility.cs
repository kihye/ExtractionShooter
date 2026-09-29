using UnityEngine;

public static class GridItemViewColorUtility
{
    public static Color GetItemColor(ItemDefinition definition)
    {
        if (definition == null)
        {
            return new Color(0.35f, 0.72f, 0.82f, 0.92f);
        }

        return definition.Category switch
        {
            ItemCategory.Weapon => new Color(0.8f, 0.28f, 0.22f, 0.92f),
            ItemCategory.Armor => new Color(0.25f, 0.45f, 0.85f, 0.92f),
            ItemCategory.Accessory => new Color(0.55f, 0.38f, 0.9f, 0.92f),
            ItemCategory.Bag => new Color(0.52f, 0.46f, 0.35f, 0.92f),
            ItemCategory.Consumable => new Color(0.2f, 0.65f, 0.36f, 0.92f),
            ItemCategory.Ammo => new Color(0.86f, 0.68f, 0.18f, 0.92f),
            _ => new Color(0.35f, 0.72f, 0.82f, 0.92f)
        };
    }
}
