using UnityEngine;

public sealed class StorageInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private StorageGuiController storageGui;
    [SerializeField] private string promptText = "[E] Open Storage";

    public string PromptText => promptText;
    public Transform Transform => transform;

    public void Configure(StorageGuiController storageGui)
    {
        this.storageGui = storageGui;
    }

    public bool CanInteract(GameObject player)
    {
        return player != null && storageGui != null;
    }

    public void Interact(GameObject player)
    {
        if (CanInteract(player))
        {
            storageGui.Open(player);
        }
    }
}
