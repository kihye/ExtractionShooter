using UnityEngine;

public sealed class BaseSceneReferences : MonoBehaviour
{
    [Header("Runtime State")]
    [SerializeField] private StashInventory stashInventory;

    [Header("Player")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("Camera")]
    [SerializeField] private Camera baseCamera;
    [SerializeField] private TopDownCamera topDownCamera;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameplayUiModeController uiModeController;
    [SerializeField] private InventoryGuiController inventoryGui;
    [SerializeField] private StorageGuiController storageGui;
    [SerializeField] private ShopGuiController shopGui;
    [SerializeField] private BaseLoadoutUI loadoutUi;
    [SerializeField] private SaveLoadUI saveLoadUi;
    [SerializeField] private InteractionPromptUI promptUi;

    [Header("Interactables")]
    [SerializeField] private StorageInteractable storageInteractable;
    [SerializeField] private ShopInteractable shopInteractable;
    [SerializeField] private DeployTerminalInteractable deployInteractable;

    public StashInventory StashInventory => stashInventory;
    public GameObject PlayerPrefab => playerPrefab;
    public Transform PlayerSpawnPoint => playerSpawnPoint;
    public Camera BaseCamera => baseCamera;
    public TopDownCamera TopDownCamera => topDownCamera;
    public Canvas Canvas => canvas;
    public GameplayUiModeController UiModeController => uiModeController;
    public InventoryGuiController InventoryGui => inventoryGui;
    public StorageGuiController StorageGui => storageGui;
    public ShopGuiController ShopGui => shopGui;
    public BaseLoadoutUI LoadoutUi => loadoutUi;
    public SaveLoadUI SaveLoadUi => saveLoadUi;
    public InteractionPromptUI PromptUi => promptUi;
    public StorageInteractable StorageInteractable => storageInteractable;
    public ShopInteractable ShopInteractable => shopInteractable;
    public DeployTerminalInteractable DeployInteractable => deployInteractable;
}
