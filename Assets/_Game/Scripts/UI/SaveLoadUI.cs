using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SaveLoadUI : MonoBehaviour
{
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private ItemDefinitionRegistry itemRegistry;
    [SerializeField] private SceneMapRegistry sceneMapRegistry;
    [SerializeField] private RunWorldPrefabRegistry runWorldPrefabRegistry;
    [SerializeField] private GameplayUiModeController uiModeController;
    [SerializeField] private SceneFlowController sceneFlowController;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text lastSaveText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private PlayerInventory playerInventory;
    private PlayerEquipment playerEquipment;
    private StashInventory stashInventory;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;
    private GameProgressSaveService saveService;
    private GameObject modePlayer;
    private PendingAction pendingAction = PendingAction.None;
    private bool isOpen;
    private bool isBusy;
    private bool isPausedByMenu;
    private float previousTimeScale = 1f;

    private enum PendingAction
    {
        None,
        SaveOverwrite,
        LoadMain,
        LoadBackup,
        ReturnToTitle
    }

    private void Awake()
    {
        ResolveReferences();
        RegisterButtons();
        CloseImmediate();
    }

    private void OnEnable()
    {
        RegisterButtons();
        Refresh();
    }

    private void OnDisable()
    {
        UnregisterButtons();

        if (isOpen)
        {
            ClosePanel();
        }
    }

    private void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (pendingAction != PendingAction.None)
            {
                ClearConfirmation();
            }
            else
            {
                ClosePanel();
            }
        }
    }

    public void Bind(
        GameSessionState state,
        PlayerInventory inventory,
        PlayerEquipment equipment,
        StashInventory stash,
        GameplayUiModeController modeController)
    {
        if (state != null)
        {
            sessionState = state;
        }

        if (inventory != null)
        {
            playerInventory = inventory;
            modePlayer = inventory.gameObject;
            playerWeapon = inventory.GetComponent<PlayerWeapon>();
            playerHealth = inventory.GetComponent<PlayerHealth>();
        }

        if (equipment != null)
        {
            playerEquipment = equipment;
        }

        if (stash != null)
        {
            stashInventory = stash;
        }

        if (modeController != null)
        {
            uiModeController = modeController;
        }

        ResolveReferences();
        Refresh();
    }

    public void OpenPanel(GameObject player)
    {
        if (player != null)
        {
            modePlayer = player;
            playerInventory = player.GetComponent<PlayerInventory>();
            playerEquipment = player.GetComponent<PlayerEquipment>();
        }

        ResolveReferences();
        if (uiModeController != null && !uiModeController.TryEnterMode(GameplayUiMode.SaveLoad, modePlayer))
        {
            return;
        }

        isOpen = true;
        ClearConfirmation();
        PauseIfRunScene();

        if (panel == null)
        {
            Debug.LogWarning("SaveLoadUI is missing an authored panel reference.", this);
            isOpen = false;
            uiModeController?.ExitMode(GameplayUiMode.SaveLoad);
            return;
        }

        panel.SetActive(true);
        SetStatus("");
        Refresh();
    }

    public void ClosePanel()
    {
        isOpen = false;
        ClearConfirmation();
        RestorePauseIfNeeded();
        if (panel != null)
        {
            panel.SetActive(false);
        }

        uiModeController?.ExitMode(GameplayUiMode.SaveLoad);
        RefreshButtons();
    }

    private void HandleOpenClicked()
    {
        OpenPanel(playerInventory != null ? playerInventory.gameObject : modePlayer);
    }

    private void HandleSaveClicked()
    {
        if (isBusy)
        {
            return;
        }

        ResolveReferences();
        if (!HasRequiredReferences())
        {
            SetStatus("저장 참조가 누락되었습니다.");
            RefreshButtons();
            return;
        }

        if (!CanSaveNow(out string blockedReason))
        {
            SetStatus(blockedReason);
            Debug.Log($"Save request rejected: {blockedReason}", this);
            RefreshButtons();
            return;
        }

        if (saveService.HasSaveFile())
        {
            ShowConfirmation(PendingAction.SaveOverwrite, "기존 저장 파일을 덮어씁니다.\n계속할까요?");
            return;
        }

        ExecuteSave();
    }

    private void HandleLoadClicked()
    {
        if (isBusy)
        {
            return;
        }

        ResolveReferences();
        if (!HasRequiredReferences())
        {
            SetStatus("불러오기 참조가 누락되었습니다.");
            RefreshButtons();
            return;
        }

        if (!saveService.HasSaveFile())
        {
            SetStatus("저장 파일이 없습니다.");
            Refresh();
            return;
        }

        ShowConfirmation(PendingAction.LoadMain, "현재 진행 상태가 저장 파일의 상태로 교체됩니다.\n계속할까요?");
    }

    private void HandleTitleClicked()
    {
        if (isBusy)
        {
            return;
        }

        ResolveReferences();
        if (sceneFlowController == null)
        {
            SetStatus("Scene 전환 참조가 누락되었습니다.");
            RefreshButtons();
            return;
        }

        ShowConfirmation(PendingAction.ReturnToTitle, "저장하지 않은 진행이 사라질 수 있습니다.\n타이틀로 돌아갈까요?");
    }

    private void HandleConfirmClicked()
    {
        if (isBusy)
        {
            return;
        }

        PendingAction action = pendingAction;
        ClearConfirmation();

        switch (action)
        {
            case PendingAction.SaveOverwrite:
                ExecuteSave();
                break;
            case PendingAction.LoadMain:
                ExecuteLoad(false);
                break;
            case PendingAction.LoadBackup:
                ExecuteLoad(true);
                break;
            case PendingAction.ReturnToTitle:
                ExecuteReturnToTitle();
                break;
        }
    }

    private void ExecuteSave()
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;

        try
        {
            RefreshButtons();
            Debug.Log("Save requested.", this);
            GameProgressSaveResult result = saveService.Save(sessionState);
            ReportResult(result);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save failed with an unexpected exception: {exception}", this);
            SetStatus("저장 중 오류가 발생했습니다.");
        }
        finally
        {
            isBusy = false;
            Debug.Log("Save operation finished. Busy state released.", this);
            SafeRefreshAfterOperation();
        }
    }

    private void ExecuteLoad(bool useBackup)
    {
        if (isBusy || (sceneFlowController != null && sceneFlowController.IsLoading))
        {
            return;
        }

        isBusy = true;
        bool startedSceneLoad = false;

        try
        {
            RefreshButtons();
            GameProgressSaveResult result = saveService.Load(sessionState, useBackup);
            ReportResult(result);

            if (result.Succeeded)
            {
                RestorePauseIfNeeded();
                if (sceneFlowController == null)
                {
                    sceneFlowController = FindFirstObjectByType<SceneFlowController>();
                }

                if (sceneFlowController != null)
                {
                    startedSceneLoad = sceneFlowController.LoadSavedProgress();
                    if (!startedSceneLoad)
                    {
                        BaseHubBootstrapper.RebindCurrentBaseScene();
                        SetStatus("불러올 Scene을 시작할 수 없습니다.");
                    }
                }
                else
                {
                    BaseHubBootstrapper.RebindCurrentBaseScene();
                }
            }
            else if (result.BackupAvailable)
            {
                ShowConfirmation(PendingAction.LoadBackup, "저장 파일을 불러올 수 없습니다.\n백업 저장을 불러올까요?");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Load failed with an unexpected exception: {exception}", this);
            SetStatus("불러오기 중 오류가 발생했습니다.");
        }
        finally
        {
            isBusy = false;
            Debug.Log("Load operation finished. Busy state released.", this);
            if (!startedSceneLoad && this != null)
            {
                SafeRefreshAfterOperation();
            }
        }
    }

    private void ExecuteReturnToTitle()
    {
        isBusy = true;
        RefreshButtons();
        bool startedSceneLoad = false;

        try
        {
            RestorePauseIfNeeded();
            startedSceneLoad = sceneFlowController != null && sceneFlowController.LoadTitle();
            if (!startedSceneLoad)
            {
                SetStatus("타이틀 Scene을 시작할 수 없습니다.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Return to title failed with an unexpected exception: {exception}", this);
            SetStatus("타이틀로 돌아가는 중 오류가 발생했습니다.");
        }
        finally
        {
            isBusy = false;
            if (!startedSceneLoad && this != null)
            {
                SafeRefreshAfterOperation();
            }
        }
    }

    private void SafeRefreshAfterOperation()
    {
        if (this == null)
        {
            return;
        }

        try
        {
            Refresh();
        }
        catch (Exception exception)
        {
            Debug.LogError($"Save/load UI refresh failed after operation: {exception}", this);
            RefreshButtons();
        }
    }

    private void ReportResult(GameProgressSaveResult result)
    {
        if (result == null)
        {
            SetStatus("처리 결과를 확인할 수 없습니다.");
            return;
        }

        SetStatus(result.Message);

        if (!string.IsNullOrWhiteSpace(result.DiagnosticMessage))
        {
            Debug.LogWarning(result.DiagnosticMessage, this);
        }
    }

    private void ShowConfirmation(PendingAction action, string message)
    {
        pendingAction = action;
        if (confirmationText != null)
        {
            confirmationText.text = message;
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
        }

        RefreshButtons();
    }

    private void ClearConfirmation()
    {
        bool hadPendingAction = pendingAction != PendingAction.None;
        pendingAction = PendingAction.None;
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        if (hadPendingAction)
        {
            Debug.Log("Save/load confirmation cleared.", this);
        }

        RefreshButtons();
    }

    private void ResolveReferences()
    {
        if (stashInventory == null)
        {
            stashInventory = FindFirstObjectByType<StashInventory>();
        }

        if (sessionState == null && stashInventory != null)
        {
            sessionState = stashInventory.SessionState;
        }

        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerInventory != null)
        {
            modePlayer = playerInventory.gameObject;
            if (playerEquipment == null)
            {
                playerEquipment = playerInventory.GetComponent<PlayerEquipment>();
            }

            if (playerWeapon == null)
            {
                playerWeapon = playerInventory.GetComponent<PlayerWeapon>();
            }

            if (playerHealth == null)
            {
                playerHealth = playerInventory.GetComponent<PlayerHealth>();
            }
        }

        if (uiModeController == null)
        {
            uiModeController = FindFirstObjectByType<GameplayUiModeController>();
        }

        if (sceneFlowController == null)
        {
            sceneFlowController = FindFirstObjectByType<SceneFlowController>();
        }

        saveService = itemRegistry != null ? new GameProgressSaveService(itemRegistry, sceneMapRegistry) : null;
    }

    private bool HasRequiredReferences()
    {
        return sessionState != null && itemRegistry != null && saveService != null;
    }

    private void Refresh()
    {
        try
        {
            ResolveReferences();

            if (lastSaveText != null)
            {
                if (saveService != null && saveService.TryGetLastSaveTime(out DateTime savedAtUtc))
                {
                    lastSaveText.text = "마지막 저장: " + savedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    lastSaveText.text = "저장 파일 없음";
                }
            }
        }
        finally
        {
            RefreshButtons();
        }
    }

    private void RefreshButtons()
    {
        bool hasReferences = HasRequiredReferences();
        bool hasSave = saveService != null && saveService.HasSaveFile();
        bool waitingConfirmation = pendingAction != PendingAction.None;
        bool transitioning = sceneFlowController != null && sceneFlowController.IsLoading;
        bool canOperate = isOpen && !isBusy && !waitingConfirmation && !transitioning && hasReferences;

        if (openButton != null)
        {
            openButton.interactable = !isBusy && !transitioning && (uiModeController == null || uiModeController.IsGameplay);
        }

        if (saveButton != null)
        {
            saveButton.interactable = canOperate && CanSaveNow(out _);
        }

        if (loadButton != null)
        {
            loadButton.interactable = canOperate && hasSave;
        }

        if (titleButton != null)
        {
            titleButton.interactable = isOpen && !isBusy && !waitingConfirmation && sceneFlowController != null;
        }

        if (closeButton != null)
        {
            closeButton.interactable = !isBusy;
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = !isBusy && waitingConfirmation;
        }

        if (cancelButton != null)
        {
            cancelButton.interactable = !isBusy && waitingConfirmation;
        }
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

        if (openButton != null)
        {
            openButton.onClick.AddListener(HandleOpenClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(HandleSaveClicked);
        }

        if (loadButton != null)
        {
            loadButton.onClick.AddListener(HandleLoadClicked);
        }

        if (titleButton != null)
        {
            titleButton.onClick.AddListener(HandleTitleClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(ClearConfirmation);
        }
    }

    private void UnregisterButtons()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(HandleOpenClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(HandleSaveClicked);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(HandleLoadClicked);
        }

        if (titleButton != null)
        {
            titleButton.onClick.RemoveListener(HandleTitleClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(ClearConfirmation);
        }
    }

    private void CloseImmediate()
    {
        isOpen = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }
    }

    private bool CanSaveNow(out string reason)
    {
        reason = null;

        if (sceneFlowController != null && sceneFlowController.IsLoading)
        {
            reason = "Scene 전환 중에는 저장할 수 없습니다.";
            return false;
        }

        RunSessionController runSession = FindFirstObjectByType<RunSessionController>();
        if (runSession != null && !runSession.IsRunning)
        {
            reason = "Run 결과 정산 중에는 저장할 수 없습니다.";
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            reason = "사망 처리 중에는 저장할 수 없습니다.";
            return false;
        }

        if (playerWeapon != null && playerWeapon.IsReloading)
        {
            reason = "재장전 중에는 저장할 수 없습니다.";
            return false;
        }

        return true;
    }

    private void PauseIfRunScene()
    {
        if (isPausedByMenu || FindFirstObjectByType<RunSessionController>() == null)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPausedByMenu = true;
    }

    private void RestorePauseIfNeeded()
    {
        if (!isPausedByMenu)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        isPausedByMenu = false;
    }
}
