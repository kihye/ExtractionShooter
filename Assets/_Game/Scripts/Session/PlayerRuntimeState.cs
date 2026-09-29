using System;
using System.Collections.Generic;

[Serializable]
public sealed class PlayerRuntimeState
{
    private readonly List<GridInventory> inventoryGrids = new List<GridInventory>();
    private readonly List<EquipmentSlot> equipmentSlots = new List<EquipmentSlot>();
    private readonly Dictionary<EquipmentSlot, GridInventory> equippedBagGrids = new Dictionary<EquipmentSlot, GridInventory>();

    public List<GridInventory> InventoryGrids => inventoryGrids;
    public List<EquipmentSlot> EquipmentSlots => equipmentSlots;
    public Dictionary<EquipmentSlot, GridInventory> EquippedBagGrids => equippedBagGrids;

    public void InitializeIfNeeded(int defaultBagWidth, int defaultBagHeight)
    {
        if (inventoryGrids.Count == 0)
        {
            inventoryGrids.Add(new GridInventory("Bag 1 - Basic Bag", defaultBagWidth, defaultBagHeight));
        }

        if (equipmentSlots.Count > 0)
        {
            return;
        }

        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.PrimaryWeapon, "Primary Weapon", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.SecondaryWeapon, "Secondary Weapon", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Armor, "Armor", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Accessory, "Accessory 1", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Accessory, "Accessory 2", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Accessory, "Accessory 3", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Contractor, "Contractor", EquipmentSlotState.Locked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 1 - Basic Bag", EquipmentSlotState.Fixed));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 2", EquipmentSlotState.Unlocked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 3", EquipmentSlotState.Locked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 4", EquipmentSlotState.Locked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 5", EquipmentSlotState.Locked));
        equipmentSlots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 6", EquipmentSlotState.Locked));
    }
}
