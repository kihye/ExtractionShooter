using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class BaseLoadoutUI : MonoBehaviour
{
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private SceneFlowController sceneFlowController;
    [SerializeField] private GameplayUiModeController uiModeController;
    [SerializeField, Min(1)] private int ammoStep = 12;
    [SerializeField, Min(0)] private int desiredDefaultAmmo = 36;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text ammoStockText;
    [SerializeField] private TMP_Text selectedAmmoText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button decreaseAmmoButton;
    [SerializeField] private Button increaseAmmoButton;
    [SerializeField] private Button deployButton;

    private int selectedAmmo;
    private bool isDeploying;
    private bool isOpen;
    private GameObject modePlayer;

    private void Awake()
    {
        sceneFlowController ??= FindFirstObjectByType<SceneFlowController>();

        if (sessionState == null)
        {
            StashInventory stashInventory = FindFirstObjectByType<StashInventory>();
            if (stashInventory != null)
            {
                sessionState = stashInventory.SessionState;
            }
        }

        ValidateAuthoredView();
        InitializeSelection();
        RegisterButtons();
        CloseImmediate();
    }

    private void OnEnable()
    {
        if (sessionState != null)
        {
            sessionState.AmmoStockChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (sessionState != null)
        {
            sessionState.AmmoStockChanged -= Refresh;
        }

        if (isOpen)
        {
            ClosePanel();
        }
    }

    private void OnDestroy()
    {
        if (decreaseAmmoButton != null)
        {
            decreaseAmmoButton.onClick.RemoveListener(DecreaseAmmo);
        }

        if (increaseAmmoButton != null)
        {
            increaseAmmoButton.onClick.RemoveListener(IncreaseAmmo);
        }

        if (deployButton != null)
        {
            deployButton.onClick.RemoveListener(Deploy);
        }
    }

    private void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePanel();
        }
    }

    public void OpenPanel(GameObject player)
    {
        uiModeController ??= FindFirstObjectByType<GameplayUiModeController>();
        if (uiModeController != null && !uiModeController.TryEnterMode(GameplayUiMode.Deploy, player))
        {
            return;
        }

        modePlayer = player;
        isOpen = true;
        if (panel == null)
        {
            Debug.LogWarning("BaseLoadoutUI is missing an authored panel reference.", this);
            isOpen = false;
            if (uiModeController != null)
            {
                uiModeController.ExitMode(GameplayUiMode.Deploy);
            }

            return;
        }

        panel.SetActive(true);
        Refresh();
    }

    public void ClosePanel()
    {
        isOpen = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (uiModeController != null)
        {
            uiModeController.ExitMode(GameplayUiMode.Deploy);
        }

        modePlayer = null;
    }

    private void InitializeSelection()
    {
        int ammoStock = sessionState != null ? sessionState.AmmoStock : 0;
        selectedAmmo = Mathf.Clamp(desiredDefaultAmmo, 0, ammoStock);
    }

    private void RegisterButtons()
    {
        if (decreaseAmmoButton != null)
        {
            decreaseAmmoButton.onClick.AddListener(DecreaseAmmo);
        }

        if (increaseAmmoButton != null)
        {
            increaseAmmoButton.onClick.AddListener(IncreaseAmmo);
        }

        if (deployButton != null)
        {
            deployButton.onClick.AddListener(Deploy);
        }
    }

    private void IncreaseAmmo()
    {
        int ammoStock = sessionState != null ? sessionState.AmmoStock : 0;
        selectedAmmo = Mathf.Min(ammoStock, selectedAmmo + ammoStep);
        Refresh();
    }

    private void DecreaseAmmo()
    {
        selectedAmmo = Mathf.Max(0, selectedAmmo - ammoStep);
        Refresh();
    }

    private void Deploy()
    {
        if (deployButton != null)
        {
            deployButton.interactable = false;
        }

        isDeploying = true;

        if (sessionState == null || sceneFlowController == null)
        {
            SetStatus("Loadout is not ready.");
            isDeploying = false;
            if (deployButton != null)
            {
                deployButton.interactable = true;
            }

            return;
        }

        ClampSelection();
        if (!sessionState.TryPrepareLoadout(selectedAmmo))
        {
            SetStatus("Not enough ammo.");
            isDeploying = false;
            Refresh();
            return;
        }

        sceneFlowController.LoadRun();
    }

    private void Refresh()
    {
        ClampSelection();

        int ammoStock = sessionState != null ? sessionState.AmmoStock : 0;

        if (ammoStockText != null)
        {
            ammoStockText.text = $"Ammo Stock\n{ammoStock}";
        }

        if (selectedAmmoText != null)
        {
            selectedAmmoText.text = $"Ammo to Carry\n{selectedAmmo}";
        }

        if (decreaseAmmoButton != null)
        {
            decreaseAmmoButton.interactable = selectedAmmo > 0;
        }

        if (increaseAmmoButton != null)
        {
            increaseAmmoButton.interactable = selectedAmmo < ammoStock;
        }

        if (deployButton != null)
        {
            deployButton.interactable = !isDeploying && sessionState != null && sceneFlowController != null;
        }
    }

    private void ClampSelection()
    {
        int ammoStock = sessionState != null ? sessionState.AmmoStock : 0;
        selectedAmmo = Mathf.Clamp(selectedAmmo, 0, ammoStock);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void CloseImmediate()
    {
        isOpen = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void ValidateAuthoredView()
    {
        if (panel == null || ammoStockText == null || selectedAmmoText == null || decreaseAmmoButton == null || increaseAmmoButton == null || deployButton == null)
        {
            Debug.LogWarning("BaseLoadoutUI requires authored panel, text, and button references.", this);
        }
    }
}
