using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerEquipment : MonoBehaviour
{
    public event Action EquipmentChanged;

    private List<EquipmentSlot> slots = new List<EquipmentSlot>();
    private Dictionary<EquipmentSlot, GridInventory> equippedBagGrids = new Dictionary<EquipmentSlot, GridInventory>();
    private PlayerHealth playerHealth;
    private PlayerInventory playerInventory;
    private PlayerWeapon playerWeapon;

    public IReadOnlyList<EquipmentSlot> Slots => slots;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerInventory = GetComponent<PlayerInventory>();
        playerWeapon = GetComponent<PlayerWeapon>();
        InitializeSlots();
        ReapplyEquipmentEffects();
        SyncActiveWeapon();
    }

    public void BindRuntimeState(PlayerRuntimeState runtimeState)
    {
        if (runtimeState == null)
        {
            return;
        }

        playerHealth ??= GetComponent<PlayerHealth>();
        playerInventory ??= GetComponent<PlayerInventory>();
        playerWeapon ??= GetComponent<PlayerWeapon>();
        runtimeState.InitializeIfNeeded(
            playerInventory != null ? playerInventory.DefaultBagWidth : 6,
            playerInventory != null ? playerInventory.DefaultBagHeight : 5);

        slots = runtimeState.EquipmentSlots;
        equippedBagGrids = runtimeState.EquippedBagGrids;
        ReapplyEquipmentEffects();
        SyncActiveWeapon();
        EquipmentChanged?.Invoke();
    }

    public bool TryEquip(EquipmentSlot slot, RuntimeItemInstance item)
    {
        if (slot == null || item?.Definition == null || !slot.Equip(item))
        {
            return false;
        }

        ReapplyEquipmentEffects();
        SyncActiveWeapon();
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool TryUnequip(EquipmentSlot slot, out RuntimeItemInstance removedItem)
    {
        removedItem = null;
        if (slot == null || !slot.Unequip(out removedItem))
        {
            return false;
        }

        ReapplyEquipmentEffects();
        SyncActiveWeapon();
        EquipmentChanged?.Invoke();
        return true;
    }

    public bool CanEquipFromInventory(GridInventory sourceGrid, GridItemPlacement placement, EquipmentSlot targetSlot)
    {
        if (playerInventory == null || sourceGrid == null || placement?.Item?.Definition == null || targetSlot == null)
        {
            return false;
        }

        if (!sourceGrid.Contains(placement) || !targetSlot.CanEquip(placement.Definition))
        {
            return false;
        }

        RuntimeItemInstance replacedItem = targetSlot.EquippedItem;
        if (!CanRemoveEquippedItemForReplacement(targetSlot))
        {
            return false;
        }

        if (replacedItem == null)
        {
            return true;
        }

        GridInventory excludedGrid = GetBagGridIfOwnedBySlot(targetSlot);
        return playerInventory.TryFindSpaceForItem(
            replacedItem,
            sourceGrid,
            placement.X,
            placement.Y,
            placement,
            excludedGrid,
            out _,
            out _,
            out _);
    }

    public bool TryEquipFromInventory(GridInventory sourceGrid, GridItemPlacement placement, EquipmentSlot targetSlot)
    {
        if (!CanEquipFromInventory(sourceGrid, placement, targetSlot))
        {
            return false;
        }

        RuntimeItemInstance itemToEquip = placement.Item;
        RuntimeItemInstance replacedItem = targetSlot.EquippedItem;
        GridInventory returnGrid = null;
        int returnX = 0;
        int returnY = 0;

        if (replacedItem != null)
        {
            GridInventory excludedGrid = GetBagGridIfOwnedBySlot(targetSlot);
            if (!playerInventory.TryFindSpaceForItem(
                replacedItem,
                sourceGrid,
                placement.X,
                placement.Y,
                placement,
                excludedGrid,
                out returnGrid,
                out returnX,
                out returnY))
            {
                return false;
            }
        }

        if (!playerInventory.RemovePlacement(sourceGrid, placement, false))
        {
            return false;
        }

        bool removedBagGrid = DeactivateBagGrid(targetSlot, false);
        if (IsBagItem(replacedItem) && !removedBagGrid)
        {
            sourceGrid.TryPlace(itemToEquip, placement.X, placement.Y);
            playerInventory.NotifyInventoryChanged();
            return false;
        }

        if (!targetSlot.SetEquippedItem(itemToEquip, out _))
        {
            sourceGrid.TryPlace(itemToEquip, placement.X, placement.Y);
            playerInventory.NotifyInventoryChanged();
            return false;
        }

        ActivateBagGrid(targetSlot, false);

        if (replacedItem != null && !playerInventory.TryPlaceItem(returnGrid, replacedItem, returnX, returnY, false))
        {
            targetSlot.Restore(replacedItem);
            DeactivateBagGrid(targetSlot, false);
            sourceGrid.TryPlace(itemToEquip, placement.X, placement.Y);
            playerInventory.NotifyInventoryChanged();
            NotifyEquipmentChanged();
            return false;
        }

        playerInventory.NotifyInventoryChanged();
        NotifyEquipmentChanged();
        return true;
    }

    public bool CanUnequipToInventory(EquipmentSlot sourceSlot, GridInventory targetGrid, int targetX, int targetY)
    {
        if (playerInventory == null || sourceSlot == null || targetGrid == null || sourceSlot.EquippedItem?.Definition == null)
        {
            return false;
        }

        if (!sourceSlot.CanUnequip || !CanRemoveEquippedItemForReplacement(sourceSlot))
        {
            return false;
        }

        if (targetGrid == GetBagGridIfOwnedBySlot(sourceSlot))
        {
            return false;
        }

        return playerInventory.CanPlaceItem(targetGrid, sourceSlot.EquippedItem, targetX, targetY);
    }

    public bool TryUnequipToInventory(EquipmentSlot sourceSlot, GridInventory targetGrid, int targetX, int targetY)
    {
        if (!CanUnequipToInventory(sourceSlot, targetGrid, targetX, targetY))
        {
            return false;
        }

        RuntimeItemInstance item = sourceSlot.EquippedItem;
        if (!sourceSlot.Unequip(out RuntimeItemInstance removedItem))
        {
            return false;
        }

        bool removedBagGrid = DeactivateBagGrid(sourceSlot, false);
        if (IsBagItem(item) && !removedBagGrid)
        {
            sourceSlot.Restore(item);
            return false;
        }

        if (!playerInventory.TryPlaceItem(targetGrid, removedItem, targetX, targetY, false))
        {
            sourceSlot.Restore(item);
            ActivateBagGrid(sourceSlot, false);
            return false;
        }

        playerInventory.NotifyInventoryChanged();
        NotifyEquipmentChanged();
        return true;
    }

    public bool CanMoveEquipment(EquipmentSlot sourceSlot, EquipmentSlot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || sourceSlot == targetSlot || sourceSlot.EquippedItem?.Definition == null)
        {
            return false;
        }

        RuntimeItemInstance sourceItem = sourceSlot.EquippedItem;
        RuntimeItemInstance targetItem = targetSlot.EquippedItem;

        if (!sourceSlot.CanUnequip || !targetSlot.CanEquip(sourceItem.Definition))
        {
            return false;
        }

        if (IsBagItem(sourceItem) || IsBagItem(targetItem))
        {
            return false;
        }

        return targetItem == null || sourceSlot.CanEquip(targetItem.Definition);
    }

    public bool TryMoveEquipment(EquipmentSlot sourceSlot, EquipmentSlot targetSlot)
    {
        if (!CanMoveEquipment(sourceSlot, targetSlot))
        {
            return false;
        }

        RuntimeItemInstance sourceItem = sourceSlot.EquippedItem;
        RuntimeItemInstance targetItem = targetSlot.EquippedItem;

        sourceSlot.Restore(targetItem);
        targetSlot.Restore(sourceItem);
        NotifyEquipmentChanged();
        return true;
    }

    public EquipmentSlot GetFirstCompatibleSlot(ItemDefinition definition)
    {
        if (definition == null || !definition.CanEquip)
        {
            return null;
        }

        foreach (EquipmentSlot slot in slots)
        {
            if (slot.CanEquip(definition))
            {
                return slot;
            }
        }

        return null;
    }

    private void InitializeSlots()
    {
        if (slots.Count > 0)
        {
            return;
        }

        slots.Add(new EquipmentSlot(EquipmentSlotType.PrimaryWeapon, "Primary Weapon", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.SecondaryWeapon, "Secondary Weapon", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Armor, "Armor", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Accessory, "Accessory 1", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Accessory, "Accessory 2", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Accessory, "Accessory 3", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Contractor, "Contractor", EquipmentSlotState.Locked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 1 - Basic Bag", EquipmentSlotState.Fixed));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 2", EquipmentSlotState.Unlocked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 3", EquipmentSlotState.Locked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 4", EquipmentSlotState.Locked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 5", EquipmentSlotState.Locked));
        slots.Add(new EquipmentSlot(EquipmentSlotType.Bag, "Bag 6", EquipmentSlotState.Locked));
    }

    private void ReapplyEquipmentEffects()
    {
        float maxHealthBonus = 0f;

        foreach (EquipmentSlot slot in slots)
        {
            if (slot.EquippedItem?.Definition == null)
            {
                continue;
            }

            maxHealthBonus += slot.EquippedItem.Definition.MaxHealthBonus;
        }

        if (playerHealth != null)
        {
            playerHealth.SetEquipmentMaxHealthBonus(maxHealthBonus);
        }
    }

    private void NotifyEquipmentChanged()
    {
        ReapplyEquipmentEffects();
        SyncActiveWeapon();
        EquipmentChanged?.Invoke();
    }

    private void SyncActiveWeapon()
    {
        if (playerWeapon == null)
        {
            return;
        }

        EquipmentSlot primarySlot = GetFirstSlot(EquipmentSlotType.PrimaryWeapon);
        if (primarySlot?.EquippedItem?.Definition != null && primarySlot.EquippedItem.Definition.Category == ItemCategory.Weapon)
        {
            playerWeapon.EquipRuntimeWeapon(primarySlot.EquippedItem);
            return;
        }

        playerWeapon.ClearRuntimeWeapon();
    }

    private EquipmentSlot GetFirstSlot(EquipmentSlotType slotType)
    {
        foreach (EquipmentSlot slot in slots)
        {
            if (slot.SlotType == slotType)
            {
                return slot;
            }
        }

        return null;
    }

    private bool CanRemoveEquippedItemForReplacement(EquipmentSlot slot)
    {
        RuntimeItemInstance item = slot?.EquippedItem;
        if (item == null)
        {
            return true;
        }

        if (!slot.CanUnequip)
        {
            return false;
        }

        if (!IsBagItem(item))
        {
            return true;
        }

        GridInventory bagGrid = GetBagGridIfOwnedBySlot(slot);
        return bagGrid == null || bagGrid.Placements.Count == 0;
    }

    private void ActivateBagGrid(EquipmentSlot slot, bool notify)
    {
        if (playerInventory == null || slot?.EquippedItem?.Definition == null || !IsBagItem(slot.EquippedItem))
        {
            return;
        }

        if (equippedBagGrids.ContainsKey(slot))
        {
            return;
        }

        ItemDefinition bagDefinition = slot.EquippedItem.Definition;
        GridInventory grid = playerInventory.AddGrid(slot.DisplayName + " - " + bagDefinition.DisplayName, bagDefinition.BagGridWidth, bagDefinition.BagGridHeight, notify);
        equippedBagGrids[slot] = grid;
    }

    private bool DeactivateBagGrid(EquipmentSlot slot, bool notify)
    {
        if (playerInventory == null || slot == null || !equippedBagGrids.TryGetValue(slot, out GridInventory grid))
        {
            return !IsBagItem(slot?.EquippedItem);
        }

        if (grid.Placements.Count > 0)
        {
            return false;
        }

        if (!playerInventory.RemoveGrid(grid, notify))
        {
            return false;
        }

        equippedBagGrids.Remove(slot);
        return true;
    }

    private GridInventory GetBagGridIfOwnedBySlot(EquipmentSlot slot)
    {
        return slot != null && equippedBagGrids.TryGetValue(slot, out GridInventory grid) ? grid : null;
    }

    private static bool IsBagItem(RuntimeItemInstance item)
    {
        return item?.Definition != null && item.Definition.Category == ItemCategory.Bag;
    }
}
