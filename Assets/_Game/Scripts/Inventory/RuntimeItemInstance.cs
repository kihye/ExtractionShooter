using System;
using UnityEngine;

[Serializable]
public sealed class RuntimeItemInstance : ISerializationCallbackReceiver
{
    [SerializeField] private string instanceId;
    [SerializeField] private ItemDefinition definition;
    [SerializeField] private int quantity;
    [SerializeField] private int upgradeLevel;
    [SerializeField, Min(0)] private int currentMagazineAmmo;

    public RuntimeItemInstance(ItemDefinition definition, int quantity, int upgradeLevel = 0, string instanceId = null)
    {
        this.instanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
        this.definition = definition;
        this.quantity = Mathf.Clamp(quantity, 1, definition != null ? definition.MaxStack : 1);
        this.upgradeLevel = definition != null
            ? Mathf.Clamp(upgradeLevel, 0, definition.MaxUpgradeLevel)
            : 0;
        currentMagazineAmmo = definition != null && definition.Category == ItemCategory.Weapon
            ? definition.InitialMagazineAmmo
            : 0;
    }

    public string InstanceId => EnsureInstanceId();
    internal string StoredInstanceId => instanceId;
    public ItemDefinition Definition => definition;
    public int Quantity => quantity;
    public int UpgradeLevel => upgradeLevel;
    public int CurrentMagazineAmmo => definition != null && definition.Category == ItemCategory.Weapon
        ? Mathf.Clamp(currentMagazineAmmo, 0, definition.WeaponMagazineSize)
        : 0;
    public bool CanStack => definition != null && definition.CanStack && quantity < definition.MaxStack;

    public string EnsureInstanceId()
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            instanceId = Guid.NewGuid().ToString("N");
        }

        return instanceId;
    }

    public void SetCurrentMagazineAmmo(int amount, int magazineSize)
    {
        currentMagazineAmmo = definition != null && definition.Category == ItemCategory.Weapon
            ? Mathf.Clamp(amount, 0, Mathf.Max(1, magazineSize))
            : 0;
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        EnsureInstanceId();
    }

    public bool CanStackWith(ItemDefinition otherDefinition)
    {
        return definition != null && otherDefinition != null && definition == otherDefinition && CanStack;
    }

    public int AddToStack(int amount)
    {
        if (definition == null || amount <= 0 || !definition.CanStack)
        {
            return amount;
        }

        int added = Mathf.Min(definition.MaxStack - quantity, amount);
        quantity += added;
        return amount - added;
    }

    public int RemoveFromStack(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int removed = Mathf.Min(quantity, amount);
        quantity -= removed;
        return removed;
    }
}
