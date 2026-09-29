using System;
using UnityEngine;

[Serializable]
public sealed class EquipmentSlot
{
    [SerializeField] private EquipmentSlotType slotType;
    [SerializeField] private string displayName;
    [SerializeField] private EquipmentSlotState state;
    [SerializeField] private RuntimeItemInstance equippedItem;

    public EquipmentSlot(EquipmentSlotType slotType, string displayName, EquipmentSlotState state)
    {
        this.slotType = slotType;
        this.displayName = displayName;
        this.state = state;
    }

    public EquipmentSlotType SlotType => slotType;
    public string DisplayName => displayName;
    public EquipmentSlotState State => state;
    public RuntimeItemInstance EquippedItem => equippedItem;
    public bool IsLocked => state == EquipmentSlotState.Locked;
    public bool IsFixed => state == EquipmentSlotState.Fixed;
    public bool CanUnequip => state == EquipmentSlotState.Unlocked;

    public string GetDisplayValue()
    {
        if (slotType == EquipmentSlotType.Contractor && IsLocked)
        {
            return "?";
        }

        if (equippedItem?.Definition != null)
        {
            return equippedItem.Definition.DisplayName;
        }

        if (slotType == EquipmentSlotType.Bag && IsFixed)
        {
            return "Basic Bag";
        }

        return IsLocked ? "Locked" : "Empty";
    }

    public bool CanEquip(ItemDefinition definition)
    {
        if (definition == null || IsLocked || IsFixed || !definition.CanEquip)
        {
            return false;
        }

        return definition.Category switch
        {
            ItemCategory.Weapon => slotType == EquipmentSlotType.PrimaryWeapon || slotType == EquipmentSlotType.SecondaryWeapon,
            ItemCategory.Armor => slotType == EquipmentSlotType.Armor,
            ItemCategory.Accessory => slotType == EquipmentSlotType.Accessory,
            ItemCategory.Bag => slotType == EquipmentSlotType.Bag,
            _ => definition.EquipmentSlot == slotType
        };
    }

    public bool Equip(RuntimeItemInstance item)
    {
        if (item?.Definition == null || !CanEquip(item.Definition))
        {
            return false;
        }

        equippedItem = item;
        return true;
    }

    public bool SetEquippedItem(RuntimeItemInstance item, out RuntimeItemInstance previousItem)
    {
        previousItem = equippedItem;
        if (item != null && !CanEquip(item.Definition))
        {
            return false;
        }

        equippedItem = item;
        return true;
    }

    public void Restore(RuntimeItemInstance item)
    {
        equippedItem = item;
    }

    public bool Unequip(out RuntimeItemInstance removedItem)
    {
        removedItem = null;
        if (!CanUnequip || equippedItem == null)
        {
            return false;
        }

        removedItem = equippedItem;
        equippedItem = null;
        return true;
    }
}
