using UnityEngine;

public interface IPlayerPickup : IInteractable
{
    string DisplayName { get; }
    bool TryPickup(GameObject player);
}
