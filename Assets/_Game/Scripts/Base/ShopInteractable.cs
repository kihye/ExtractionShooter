using UnityEngine;

public sealed class ShopInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private ShopGuiController shopGui;
    [SerializeField] private string promptText = "[E] Trade";

    public string PromptText => promptText;
    public Transform Transform => transform;

    public void Configure(ShopGuiController shopGui)
    {
        this.shopGui = shopGui;
    }

    public bool CanInteract(GameObject player)
    {
        return player != null && shopGui != null;
    }

    public void Interact(GameObject player)
    {
        if (CanInteract(player))
        {
            shopGui.Open(player);
        }
    }
}
