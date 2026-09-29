using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunResultUI : MonoBehaviour
{
    [SerializeField] private RunSessionController runSessionController;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text itemResultListText;
    [SerializeField] private RunInventoryUI runInventoryUI;
    [SerializeField] private InteractionPromptUI interactionPromptUI;
    [SerializeField] private ExtractionUI extractionUI;
    [SerializeField] private AmmoHUD ammoHUD;
    [SerializeField] private RunRestartController restartController;
    [SerializeField] private SceneFlowController sceneFlowController;
    [SerializeField] private bool showDebugRetryButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryButtonText;
    [SerializeField] private Button returnToBaseButton;
    [SerializeField] private TMP_Text returnToBaseButtonText;

    private void Awake()
    {
        if (runSessionController == null)
        {
            runSessionController = FindFirstObjectByType<RunSessionController>();
        }

        runInventoryUI ??= FindFirstObjectByType<RunInventoryUI>();
        interactionPromptUI ??= FindFirstObjectByType<InteractionPromptUI>();
        extractionUI ??= FindFirstObjectByType<ExtractionUI>();
        ammoHUD ??= FindFirstObjectByType<AmmoHUD>();
        restartController ??= FindFirstObjectByType<RunRestartController>();
        sceneFlowController ??= FindFirstObjectByType<SceneFlowController>();

        ValidateAuthoredView();

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(HandleRetryClicked);
        }

        if (returnToBaseButton != null)
        {
            returnToBaseButton.onClick.AddListener(HandleReturnToBaseClicked);
        }

        UpdateButtonVisibility();
    }

    private void OnEnable()
    {
        if (runSessionController != null)
        {
            runSessionController.RunEnded += ShowResult;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (retryButton != null)
        {
            retryButton.interactable = true;
        }

        if (returnToBaseButton != null)
        {
            returnToBaseButton.interactable = true;
        }

        UpdateButtonVisibility();
    }

    private void OnDisable()
    {
        if (runSessionController != null)
        {
            runSessionController.RunEnded -= ShowResult;
        }
    }

    private void OnDestroy()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(HandleRetryClicked);
        }

        if (returnToBaseButton != null)
        {
            returnToBaseButton.onClick.RemoveListener(HandleReturnToBaseClicked);
        }
    }

    private void ShowResult(RunSessionController.RunState state, IReadOnlyList<InventoryItemStack> itemStacks)
    {
        runInventoryUI?.Hide();
        interactionPromptUI?.Hide();
        extractionUI?.Hide();
        ammoHUD?.Hide();

        if (panel != null)
        {
            panel.SetActive(true);
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = state == RunSessionController.RunState.Extracted
                ? "EXTRACTION SUCCESS"
                : "RUN FAILED";
        }

        if (itemResultListText != null)
        {
            string label = state == RunSessionController.RunState.Extracted ? "Secured:" : "Lost:";
            string ammoLabel = state == RunSessionController.RunState.Extracted ? "Ammo Recovered:" : "Ammo Lost:";
            int ammoAmount = state == RunSessionController.RunState.Extracted
                ? (runSessionController != null ? runSessionController.LastRunAmmoRecovered : 0)
                : (runSessionController != null ? runSessionController.LastRunAmmoLost : 0);

            itemResultListText.text = label
                + "\n"
                + InventoryLogFormatter.Format(itemStacks)
                + "\n\n"
                + ammoLabel
                + "\n"
                + ammoAmount;
        }

        if (retryButton != null)
        {
            retryButton.interactable = true;
        }

        if (returnToBaseButton != null)
        {
            returnToBaseButton.interactable = true;
        }

        UpdateButtonVisibility();
    }

    private void ValidateAuthoredView()
    {
        if (panel == null || resultTitleText == null || itemResultListText == null || retryButton == null || returnToBaseButton == null)
        {
            Debug.LogWarning("RunResultUI requires authored panel, title/list text, and button references.", this);
        }
    }

    private void HandleRetryClicked()
    {
        if (retryButton != null)
        {
            retryButton.interactable = false;
        }

        if (returnToBaseButton != null)
        {
            returnToBaseButton.interactable = false;
        }

        if (restartController == null)
        {
            Debug.LogWarning("Run retry requested but no RunRestartController was found.", this);
            if (retryButton != null)
            {
                retryButton.interactable = true;
            }

            if (returnToBaseButton != null)
            {
                returnToBaseButton.interactable = true;
            }

            return;
        }

        restartController.RestartRun();
    }

    private void HandleReturnToBaseClicked()
    {
        if (retryButton != null)
        {
            retryButton.interactable = false;
        }

        if (returnToBaseButton != null)
        {
            returnToBaseButton.interactable = false;
        }

        if (sceneFlowController == null)
        {
            Debug.LogWarning("Return to base requested but no SceneFlowController was found.", this);
            if (retryButton != null)
            {
                retryButton.interactable = true;
            }

            if (returnToBaseButton != null)
            {
                returnToBaseButton.interactable = true;
            }

            return;
        }

        sceneFlowController.LoadBase();
    }

    private void UpdateButtonVisibility()
    {
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(showDebugRetryButton);
        }

        if (returnToBaseButton != null)
        {
            RectTransform rectTransform = returnToBaseButton.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = showDebugRetryButton
                    ? new Vector2(125f, 46f)
                    : new Vector2(0f, 46f);
            }
        }
    }

}
