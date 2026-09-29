using UnityEngine;

public interface IInteractable
{
    string PromptText { get; }
    Transform Transform { get; }
    bool CanInteract(GameObject player);
    void Interact(GameObject player);
}
