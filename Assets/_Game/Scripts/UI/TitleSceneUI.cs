using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleSceneUI : MonoBehaviour
{
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private ItemDefinitionRegistry itemRegistry;
    [SerializeField] private SceneMapRegistry sceneMapRegistry;
    [SerializeField] private SceneFlowController sceneFlowController;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private TMP_Text saveInfoText;
    [SerializeField] private TMP_Text statusText;

    private GameProgressSaveService saveService;
    private bool isBusy;

    private void Awake()
    {
        ResolveReferences();
        RegisterButtons();
        Refresh();
    }

    private void OnEnable()
    {
        RegisterButtons();
        Refresh();
    }

    private void OnDisable()
    {
        UnregisterButtons();
    }

    private void HandleNewGameClicked()
    {
        if (isBusy || sceneFlowController == null)
        {
            return;
        }

        isBusy = true;
        RefreshButtons();
        try
        {
            if (!sceneFlowController.StartNewGame())
            {
                isBusy = false;
                SetStatus("Base Scene을 시작할 수 없습니다.");
                RefreshButtons();
            }
        }
        catch (Exception exception)
        {
            isBusy = false;
            Debug.LogError($"New game failed with an unexpected exception: {exception}", this);
            SetStatus("새 게임 시작 중 오류가 발생했습니다.");
            RefreshButtons();
        }
    }

    private void HandleLoadClicked()
    {
        if (isBusy || saveService == null || sessionState == null)
        {
            return;
        }

        isBusy = true;
        RefreshButtons();

        try
        {
            GameProgressSaveResult result = saveService.Load(sessionState, false);
            if (!result.Succeeded)
            {
                isBusy = false;
                SetStatus(result.Message);
                if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
                {
                    Debug.LogWarning(result.DiagnosticMessage, this);
                }

                Refresh();
                return;
            }

            if (!sceneFlowController.LoadSavedProgress())
            {
                isBusy = false;
                SetStatus("저장된 Scene을 시작할 수 없습니다.");
                RefreshButtons();
            }
        }
        catch (Exception exception)
        {
            isBusy = false;
            Debug.LogError($"Title load failed with an unexpected exception: {exception}", this);
            SetStatus("불러오기 중 오류가 발생했습니다.");
            Refresh();
        }
    }

    private void HandleQuitClicked()
    {
        Application.Quit();
    }

    private void Refresh()
    {
        ResolveReferences();
        RefreshSaveInfo();
        RefreshButtons();
    }

    private void RefreshSaveInfo()
    {
        if (saveInfoText == null)
        {
            return;
        }

        if (saveService == null || !saveService.HasSaveFile())
        {
            saveInfoText.text = "저장 파일 없음";
            return;
        }

        if (!saveService.TryReadSummary(false, out GameProgressSaveSummary summary, out string issue))
        {
            saveInfoText.text = "저장 파일을 불러올 수 없습니다.";
            SetStatus("불러오기 불가: 저장 파일 검증 실패");
            Debug.LogWarning(issue, this);
            return;
        }

        string savedAt = DateTime.TryParse(summary.savedAtUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime savedAtUtc)
            ? savedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            : "알 수 없음";

        saveInfoText.text =
            $"마지막 저장: {savedAt}\n" +
            $"위치: {summary.sceneKind} / {summary.mapName}\n" +
            $"좌표: {summary.positionX:0.0}, {summary.positionY:0.0}, {summary.positionZ:0.0}\n" +
            $"화폐: {summary.credits}\n" +
            $"소지품: {summary.inventoryItemCount} / Stash: {summary.stashItemCount}";
    }

    private void RefreshButtons()
    {
        bool canUse = !isBusy && sessionState != null && sceneFlowController != null;
        if (newGameButton != null)
        {
            newGameButton.interactable = canUse;
        }

        if (loadButton != null)
        {
            loadButton.interactable = canUse && saveService != null && saveService.HasSaveFile();
        }

        if (quitButton != null)
        {
            quitButton.interactable = !isBusy;
        }
    }

    private void ResolveReferences()
    {
        if (sceneFlowController == null)
        {
            sceneFlowController = FindFirstObjectByType<SceneFlowController>();
        }
        saveService = itemRegistry != null ? new GameProgressSaveService(itemRegistry, sceneMapRegistry) : null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void RegisterButtons()
    {
        UnregisterButtons();
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(HandleNewGameClicked);
        }

        if (loadButton != null)
        {
            loadButton.onClick.AddListener(HandleLoadClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(HandleQuitClicked);
        }
    }

    private void UnregisterButtons()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(HandleNewGameClicked);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(HandleLoadClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(HandleQuitClicked);
        }
    }
}
