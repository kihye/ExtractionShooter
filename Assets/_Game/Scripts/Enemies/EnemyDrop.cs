using UnityEngine;

public sealed class EnemyDrop : MonoBehaviour
{
    [SerializeField] private ItemPickup pickupPrefab;
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private AmmoPickup ammoPickupPrefab;
    [SerializeField] private bool dropAmmo;
    [SerializeField, Min(1)] private int ammoAmount = 18;
    [SerializeField, Min(0f)] private float dropRadius = 0.6f;

    public void DropItems()
    {
        DropItem();
        DropAmmo();
    }

    private void DropItem()
    {
        if (pickupPrefab == null || itemData == null)
        {
            return;
        }

        Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
        Vector3 dropPosition = transform.position + new Vector3(randomOffset.x, 0.25f, randomOffset.y);
        ItemPickup pickup = Instantiate(pickupPrefab, dropPosition, Quaternion.identity);
        pickup.Configure(itemData, amount);
        pickup.gameObject.AddComponent<RuntimeSpawnId>().EnsureId();
    }

    private void DropAmmo()
    {
        if (!dropAmmo || ammoPickupPrefab == null)
        {
            return;
        }

        Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
        Vector3 dropPosition = transform.position + new Vector3(randomOffset.x, 0.25f, randomOffset.y);
        AmmoPickup pickup = Instantiate(ammoPickupPrefab, dropPosition, Quaternion.identity);
        pickup.Configure(ammoAmount);
        pickup.gameObject.AddComponent<RuntimeSpawnId>().EnsureId();
    }
}
