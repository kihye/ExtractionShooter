using TMPro;
using UnityEngine;

public sealed class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private TMP_Text promptText;

    private IInteractable displayedInteractable;

    private void Awake()
    {
        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        ValidateAuthoredView();
    }

    private void OnEnable()
    {
        Subscribe();
        UpdatePrompt(playerInteractor != null ? playerInteractor.CurrentInteractable : null);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Bind(PlayerInteractor interactor)
    {
        if (playerInteractor == interactor)
        {
            UpdatePrompt(playerInteractor != null ? playerInteractor.CurrentInteractable : null);
            return;
        }

        Unsubscribe();
        playerInteractor = interactor;
        Subscribe();
        UpdatePrompt(playerInteractor != null ? playerInteractor.CurrentInteractable : null);
    }

    private void UpdatePrompt(IInteractable interactable)
    {
        displayedInteractable = interactable;
        bool hasInteractable = displayedInteractable != null;

        if (promptText != null && hasInteractable)
        {
            promptText.text = displayedInteractable.PromptText;
        }

        if (promptText != null)
        {
            promptText.transform.parent.gameObject.SetActive(hasInteractable);
        }
    }

    private void ValidateAuthoredView()
    {
        if (promptText == null)
        {
            Debug.LogWarning("InteractionPromptUI requires an authored promptText reference.", this);
        }
    }

    private void Subscribe()
    {
        if (playerInteractor != null)
        {
            playerInteractor.CurrentInteractableChanged -= UpdatePrompt;
            playerInteractor.CurrentInteractableChanged += UpdatePrompt;
        }
    }

    private void Unsubscribe()
    {
        if (playerInteractor != null)
        {
            playerInteractor.CurrentInteractableChanged -= UpdatePrompt;
        }
    }
}
