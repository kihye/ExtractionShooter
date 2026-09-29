using UnityEngine;

public sealed class AmmoPickup : MonoBehaviour, IPlayerPickup
{
    [SerializeField] private ItemDefinition ammoItem;
    [SerializeField] private AmmoType ammoType = AmmoType.LightRifle;
    [SerializeField, Min(1)] private int ammoAmount = 12;

    public ItemDefinition AmmoItem => ammoItem;
    public AmmoType AmmoType => ammoType;
    public int AmmoAmount => ammoAmount;
    public string DisplayName => ammoItem != null ? $"{ammoItem.DisplayName} x{ammoAmount}" : $"Ammo Box (+{ammoAmount})";
    public string PromptText => $"[E] Pick up {DisplayName}";
    public Transform Transform => transform;

    public void Configure(int amount)
    {
        Configure(ammoItem, amount);
    }

    public void Configure(ItemDefinition item, int amount)
    {
        if (item != null)
        {
            ammoItem = item;
            ammoType = item.AmmoType;
        }

        ammoAmount = Mathf.Max(1, amount);
    }

    public void RestoreAmount(int amount)
    {
        ammoAmount = Mathf.Max(1, amount);
    }

    public bool TryPickup(GameObject player)
    {
        if (player == null || ammoAmount <= 0)
        {
            return false;
        }

        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        ItemDefinition targetAmmoItem = ammoItem != null ? ammoItem : weapon?.ReserveAmmoItem;

        if (inventory != null && targetAmmoItem != null)
        {
            if (targetAmmoItem.Category != ItemCategory.Ammo || targetAmmoItem.AmmoType != ammoType)
            {
                Debug.LogWarning($"{targetAmmoItem.DisplayName} is not compatible with {ammoType}.", this);
                return false;
            }

            InventoryAddResult result = inventory.TryAddItem(targetAmmoItem, ammoAmount);
            if (!result.AddedAny)
            {
                return false;
            }

            ammoAmount = result.RemainingAmount;
            if (ammoAmount <= 0)
            {
                Destroy(gameObject);
            }
            else
            {
                Debug.Log($"Partially picked up {targetAmmoItem.DisplayName}. Remaining: {ammoAmount}", this);
            }

            return true;
        }

        if (weapon == null)
        {
            return false;
        }

        weapon.AddReserveAmmo(ammoAmount);
        Debug.Log($"Picked up Ammo Box +{ammoAmount}", this);
        Destroy(gameObject);
        return true;
    }

    public bool CanInteract(GameObject player)
    {
        return player != null && ammoAmount > 0;
    }

    public void Interact(GameObject player)
    {
        TryPickup(player);
    }
}
