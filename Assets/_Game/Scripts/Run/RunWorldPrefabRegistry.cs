using UnityEngine;

[CreateAssetMenu(menuName = "Extraction Shooter/Run World Prefab Registry", fileName = "RunWorldPrefabRegistry")]
public sealed class RunWorldPrefabRegistry : ScriptableObject
{
    [SerializeField] private ItemPickup itemPickupPrefab;
    [SerializeField] private AmmoPickup ammoPickupPrefab;

    public ItemPickup ItemPickupPrefab => itemPickupPrefab;
    public AmmoPickup AmmoPickupPrefab => ammoPickupPrefab;
}
