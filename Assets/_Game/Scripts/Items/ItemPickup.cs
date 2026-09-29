using UnityEngine;

public sealed class ItemPickup : MonoBehaviour, IPlayerPickup
{
    [SerializeField] private ItemDefinition itemData;
    [SerializeField, Min(1)] private int amount = 1;

    public ItemDefinition Definition => itemData;
    public ItemData ItemData => itemData as ItemData;
    public int Amount => amount;
    public string DisplayName => itemData != null ? itemData.DisplayName : name;
    public string PromptText => $"[E] Pick up {DisplayName} x{amount}";
    public Transform Transform => transform;

    public void Configure(ItemDefinition item, int itemAmount)
    {
        itemData = item;
        amount = Mathf.Max(1, itemAmount);
    }

    public void RestoreAmount(int itemAmount)
    {
        amount = Mathf.Max(1, itemAmount);
    }

    public bool TryPickup(PlayerInventory inventory)
    {
        if (inventory == null || itemData == null || amount <= 0)
        {
            return false;
        }

        InventoryAddResult result = inventory.TryAddItem(itemData, amount);
        if (!result.AddedAny)
        {
            return false;
        }

        amount = result.RemainingAmount;
        if (amount <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            Debug.Log($"Partially picked up {DisplayName}. Remaining: {amount}", this);
        }

        return true;
    }

    public bool TryPickup(GameObject player)
    {
        if (player == null)
        {
            return false;
        }

        return TryPickup(player.GetComponent<PlayerInventory>());
    }

    public bool CanInteract(GameObject player)
    {
        return player != null && itemData != null && amount > 0;
    }

    public void Interact(GameObject player)
    {
        TryPickup(player);
    }
}
