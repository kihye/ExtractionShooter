using UnityEngine;

public sealed class DeployTerminalInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private BaseLoadoutUI loadoutUi;
    [SerializeField] private string promptText = "[E] Deploy";

    public string PromptText => promptText;
    public Transform Transform => transform;

    public void Configure(BaseLoadoutUI loadoutUi)
    {
        this.loadoutUi = loadoutUi;
    }

    public bool CanInteract(GameObject player)
    {
        return player != null && loadoutUi != null;
    }

    public void Interact(GameObject player)
    {
        if (CanInteract(player))
        {
            loadoutUi.OpenPanel(player);
        }
    }
}
