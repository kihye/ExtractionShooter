using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class InventoryGuiController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string inventoryActionName = "Inventory";
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerEquipment playerEquipment;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAim playerAim;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private PlayerInteractor playerInteractor;
    [SerializeField] private GameplayUiModeController uiModeController;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform equipmentViewport;
    [SerializeField] private RectTransform equipmentContainer;
    [SerializeField] private RectTransform bagViewport;
    [SerializeField] private RectTransform bagContainer;
    [SerializeField] private List<EquipmentSlotPanel> equipmentSlotPanelPool = new List<EquipmentSlotPanel>();
    [SerializeField] private List<InventoryGridPanel> bagPanelPool = new List<InventoryGridPanel>();
    [SerializeField, Min(24f)] private float cellSize = 46f;

    private readonly List<InventoryGridPanel> gridPanels = new List<InventoryGridPanel>();
    private readonly List<EquipmentSlotPanel> equipmentSlotPanels = new List<EquipmentSlotPanel>();
    private InputAction inventoryAction;
    private Image placementPreview;
    private RectTransform placementPreviewRect;
    private bool isOpen;
    private bool wasMovementEnabled;
    private bool wasAimEnabled;
    private bool wasWeaponEnabled;
    private bool wasInteractorEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool usesUiModeController;
    private PlayerInventory subscribedPlayerInventory;
    private PlayerEquipment subscribedPlayerEquipment;

    private PlayerInventory dragInventory;
    private InventoryGridPanel dragSourcePanel;
    private GridInventory dragSourceGrid;
    private GridItemPlacement dragPlacement;
    private EquipmentSlotPanel dragSourceSlotPanel;
    private EquipmentSlot dragSourceSlot;
    private RuntimeItemInstance dragItem;
    private Vector2Int dragGrabOffset;
    private RectTransform dragRect;
    private DragSourceKind dragSourceKind;

    private enum DragSourceKind
    {
        None,
        Inventory,
        Equipment
    }

    private void Awake()
    {
        ResolveReferences();
        inventoryAction = FindAction();
        ResolveAuthoredView();
        CloseImmediate();
    }

    private void OnEnable()
    {
        inventoryAction?.Enable();
        ResolveReferences();
        SubscribeToCurrentDataSources();
    }

    private void OnDisable()
    {
        inventoryAction?.Disable();
        UnsubscribeFromDataSources();

        if (isOpen)
        {
            CloseInventory();
        }
    }

    private void Update()
    {
        if (inventoryAction != null && inventoryAction.WasPressedThisFrame())
        {
            ToggleInventory();
        }

        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseInventory();
        }
    }

    public void Bind(PlayerInventory inventory, PlayerEquipment equipment, GameplayUiModeController modeController)
    {
        if (inventory != null)
        {
            playerInventory = inventory;
            playerMovement = inventory.GetComponent<PlayerMovement>();
            playerAim = inventory.GetComponent<PlayerAim>();
            playerWeapon = inventory.GetComponent<PlayerWeapon>();
            playerInteractor = inventory.GetComponent<PlayerInteractor>();
        }

        if (equipment != null)
        {
            playerEquipment = equipment;
        }
        else if (playerInventory != null)
        {
            playerEquipment = playerInventory.GetComponent<PlayerEquipment>();
        }

        if (modeController != null)
        {
            uiModeController = modeController;
        }

        ResolveAuthoredView();
        SubscribeToCurrentDataSources();

        if (isOpen)
        {
            Refresh();
        }
    }

    public void BeginDrag(PlayerInventory inventory, InventoryGridPanel sourcePanel, GridInventory sourceGrid, GridItemPlacement placement, Vector2Int grabOffset, RectTransform itemRect, Vector2 screenPosition)
    {
        if (!isOpen)
        {
            return;
        }

        dragInventory = inventory;
        dragSourcePanel = sourcePanel;
        dragSourceGrid = sourceGrid;
        dragPlacement = placement;
        dragSourceSlotPanel = null;
        dragSourceSlot = null;
        dragItem = placement?.Item;
        dragGrabOffset = grabOffset;
        dragRect = itemRect;
        dragSourceKind = DragSourceKind.Inventory;

        if (dragRect != null)
        {
            dragRect.SetAsLastSibling();
        }

        UpdateDrag(screenPosition);
    }

    public void BeginEquipmentDrag(EquipmentSlotPanel sourcePanel, EquipmentSlot sourceSlot, RectTransform itemRect, Vector2 screenPosition)
    {
        if (!isOpen || sourceSlot?.EquippedItem == null)
        {
            return;
        }

        dragInventory = null;
        dragSourcePanel = null;
        dragSourceGrid = null;
        dragPlacement = null;
        dragSourceSlotPanel = sourcePanel;
        dragSourceSlot = sourceSlot;
        dragItem = sourceSlot.EquippedItem;
        dragGrabOffset = Vector2Int.zero;
        dragRect = itemRect;
        dragSourceKind = DragSourceKind.Equipment;

        if (dragRect != null)
        {
            dragRect.SetAsLastSibling();
        }

        UpdateDrag(screenPosition);
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragSourceKind == DragSourceKind.None || dragItem?.Definition == null)
        {
            return;
        }

        if (dragRect != null)
        {
            if (dragSourceKind == DragSourceKind.Inventory)
            {
                dragRect.position = new Vector3(
                    screenPosition.x - dragGrabOffset.x * cellSize,
                    screenPosition.y + dragGrabOffset.y * cellSize,
                    dragRect.position.z);
            }
            else
            {
                dragRect.position = new Vector3(screenPosition.x, screenPosition.y, dragRect.position.z);
            }
        }

        EquipmentSlotPanel targetSlotPanel = FindTargetEquipmentSlot(screenPosition);
        if (targetSlotPanel != null)
        {
            bool canDrop = dragSourceKind == DragSourceKind.Inventory
                ? playerEquipment != null && playerEquipment.CanEquipFromInventory(dragSourceGrid, dragPlacement, targetSlotPanel.Slot)
                : playerEquipment != null && playerEquipment.CanMoveEquipment(dragSourceSlot, targetSlotPanel.Slot);

            UpdatePlacementPreview(null, 0, 0, false);
            UpdateEquipmentPreviews(targetSlotPanel, canDrop);
            return;
        }

        ClearEquipmentPreviews();
        InventoryGridPanel targetPanel = FindTargetPanel(screenPosition, out int targetX, out int targetY);
        bool canPlace = false;

        if (targetPanel != null)
        {
            canPlace = dragSourceKind == DragSourceKind.Inventory
                ? dragInventory != null && dragInventory.CanMoveItem(dragSourceGrid, dragPlacement, targetPanel.Grid, targetX, targetY)
                : playerEquipment != null && playerEquipment.CanUnequipToInventory(dragSourceSlot, targetPanel.Grid, targetX, targetY);
        }

        UpdatePlacementPreview(targetPanel, targetX, targetY, canPlace);
    }

    public void EndDrag(Vector2 screenPosition)
    {
        if (dragSourceKind == DragSourceKind.None || dragItem?.Definition == null)
        {
            return;
        }

        EquipmentSlotPanel targetSlotPanel = FindTargetEquipmentSlot(screenPosition);
        InventoryGridPanel targetPanel = FindTargetPanel(screenPosition, out int targetX, out int targetY);
        PlayerInventory inventory = dragInventory;
        GridInventory sourceGrid = dragSourceGrid;
        GridItemPlacement placement = dragPlacement;
        EquipmentSlot sourceSlot = dragSourceSlot;
        GridInventory targetGrid = targetPanel != null ? targetPanel.Grid : null;
        EquipmentSlot targetSlot = targetSlotPanel != null ? targetSlotPanel.Slot : null;
        DragSourceKind sourceKind = dragSourceKind;

        ClearDrag();

        if (targetSlot != null && sourceKind == DragSourceKind.Inventory && playerEquipment != null)
        {
            playerEquipment.TryEquipFromInventory(sourceGrid, placement, targetSlot);
        }
        else if (targetSlot != null && sourceKind == DragSourceKind.Equipment && playerEquipment != null)
        {
            playerEquipment.TryMoveEquipment(sourceSlot, targetSlot);
        }
        else if (targetGrid != null && sourceKind == DragSourceKind.Inventory && inventory != null)
        {
            inventory.TryMoveItem(sourceGrid, placement, targetGrid, targetX, targetY);
        }
        else if (targetGrid != null && sourceKind == DragSourceKind.Equipment && playerEquipment != null)
        {
            playerEquipment.TryUnequipToInventory(sourceSlot, targetGrid, targetX, targetY);
        }

        Refresh();
    }

    private void ToggleInventory()
    {
        if (isOpen)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    private void OpenInventory()
    {
        ResolveReferences();
        UnsubscribeFromDataSources();
        playerInventory?.GetComponent<PlayerRuntimeStateBinder>()?.Bind();
        ResolveAuthoredView();
        SubscribeToCurrentDataSources();
        usesUiModeController = uiModeController != null;
        if (usesUiModeController && !uiModeController.TryEnterMode(GameplayUiMode.Inventory, playerInventory != null ? playerInventory.gameObject : null))
        {
            return;
        }

        isOpen = true;
        if (!HasRequiredView())
        {
            Debug.LogWarning("Inventory GUI is missing authored Scene/Prefab references.", this);
            isOpen = false;
            if (usesUiModeController && uiModeController != null)
            {
                uiModeController.ExitMode(GameplayUiMode.Inventory);
                usesUiModeController = false;
            }

            return;
        }

        panel.SetActive(true);
        Refresh();

        if (usesUiModeController)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        wasMovementEnabled = playerMovement != null && playerMovement.enabled;
        wasAimEnabled = playerAim != null && playerAim.enabled;
        wasWeaponEnabled = playerWeapon != null && playerWeapon.enabled;
        wasInteractorEnabled = playerInteractor != null && playerInteractor.enabled;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (playerAim != null)
        {
            playerAim.enabled = false;
        }

        if (playerWeapon != null)
        {
            playerWeapon.enabled = false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = false;
        }
    }

    private void CloseInventory()
    {
        isOpen = false;
        ClearDrag();
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (usesUiModeController && uiModeController != null)
        {
            uiModeController.ExitMode(GameplayUiMode.Inventory);
            usesUiModeController = false;
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = wasMovementEnabled;
        }

        if (playerAim != null)
        {
            playerAim.enabled = wasAimEnabled;
        }

        if (playerWeapon != null)
        {
            playerWeapon.enabled = wasWeaponEnabled;
        }

        if (playerInteractor != null)
        {
            playerInteractor.enabled = wasInteractorEnabled;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private void CloseImmediate()
    {
        isOpen = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void Refresh()
    {
        if (!isOpen || playerInventory == null || bagContainer == null || equipmentContainer == null)
        {
            return;
        }

        HideEquipmentSlotPanels();
        HideGridPanels();

        if (playerEquipment != null)
        {
            float slotY = 0f;
            int slotIndex = 0;
            foreach (EquipmentSlot slot in playerEquipment.Slots)
            {
                if (slotIndex >= equipmentSlotPanelPool.Count)
                {
                    Debug.LogWarning($"Inventory equipment UI needs {playerEquipment.Slots.Count} slots, but the authored pool has {equipmentSlotPanelPool.Count}.", this);
                    break;
                }

                EquipmentSlotPanel slotPanel = equipmentSlotPanelPool[slotIndex];
                if (slotPanel == null)
                {
                    slotIndex++;
                    slotY -= 92f;
                    continue;
                }

                slotPanel.Initialize(this, slot, equipmentContainer, new Vector2(0f, slotY), new Vector2(250f, 82f), null);
                equipmentSlotPanels.Add(slotPanel);

                slotIndex++;
                slotY -= 92f;
            }

            equipmentContainer.sizeDelta = new Vector2(280f, Mathf.Max(820f, Mathf.Abs(slotY) + 12f));
            equipmentContainer.anchoredPosition = Vector2.zero;
        }

        float x = 0f;
        float maxGridPanelHeight = 0f;
        int gridIndex = 0;
        foreach (GridInventory grid in playerInventory.Grids)
        {
            if (gridIndex >= bagPanelPool.Count)
            {
                Debug.LogWarning($"Inventory UI needs {playerInventory.Grids.Count} bag panels, but the authored pool has {bagPanelPool.Count}.", this);
                break;
            }

            InventoryGridPanel gridPanel = bagPanelPool[gridIndex];
            if (gridPanel == null)
            {
                gridIndex++;
                x += grid.Width * cellSize + 76f;
                continue;
            }

            gridPanel.Initialize(this, playerInventory, grid, bagContainer, new Vector2(x, 0f), cellSize, null);
            gridPanels.Add(gridPanel);

            maxGridPanelHeight = Mathf.Max(maxGridPanelHeight, grid.Height * cellSize + 82f);
            x += grid.Width * cellSize + 76f;
            gridIndex++;
        }

        bagContainer.sizeDelta = new Vector2(Mathf.Max(1000f, x), Mathf.Max(760f, maxGridPanelHeight));
        bagContainer.anchoredPosition = Vector2.zero;
        LayoutRebuilder.MarkLayoutForRebuild(bagContainer);
        LayoutRebuilder.MarkLayoutForRebuild(equipmentContainer);
    }

    private void HandleDataChanged()
    {
        Refresh();
    }

    private InventoryGridPanel FindTargetPanel(Vector2 screenPosition, out int targetX, out int targetY)
    {
        foreach (InventoryGridPanel gridPanel in gridPanels)
        {
            if (gridPanel.TryGetCell(screenPosition, dragGrabOffset, out targetX, out targetY))
            {
                return gridPanel;
            }
        }

        targetX = 0;
        targetY = 0;
        return null;
    }

    private EquipmentSlotPanel FindTargetEquipmentSlot(Vector2 screenPosition)
    {
        foreach (EquipmentSlotPanel slotPanel in equipmentSlotPanels)
        {
            if (slotPanel != null && slotPanel.ContainsScreenPoint(screenPosition))
            {
                return slotPanel;
            }
        }

        return null;
    }

    private void UpdatePlacementPreview(InventoryGridPanel targetPanel, int targetX, int targetY, bool canPlace)
    {
        if (placementPreview == null || placementPreviewRect == null || dragItem?.Definition == null)
        {
            return;
        }

        if (targetPanel == null)
        {
            placementPreview.gameObject.SetActive(false);
            return;
        }

        placementPreview.gameObject.SetActive(true);
        placementPreview.color = canPlace
            ? new Color(0.2f, 1f, 0.35f, 0.28f)
            : new Color(1f, 0.2f, 0.2f, 0.28f);
        targetPanel.PositionPreview(placementPreviewRect, targetX, targetY, dragItem.Definition.GridWidth, dragItem.Definition.GridHeight);
    }

    private void UpdateEquipmentPreviews(EquipmentSlotPanel targetSlotPanel, bool canDrop)
    {
        foreach (EquipmentSlotPanel slotPanel in equipmentSlotPanels)
        {
            if (slotPanel == null)
            {
                continue;
            }

            slotPanel.SetDropPreview(slotPanel == targetSlotPanel, canDrop);
        }
    }

    private void ClearEquipmentPreviews()
    {
        foreach (EquipmentSlotPanel slotPanel in equipmentSlotPanels)
        {
            if (slotPanel != null)
            {
                slotPanel.SetDropPreview(false, false);
            }
        }
    }

    private void ClearDrag()
    {
        dragInventory = null;
        dragSourcePanel = null;
        dragSourceGrid = null;
        dragPlacement = null;
        dragSourceSlotPanel = null;
        dragSourceSlot = null;
        dragItem = null;
        dragGrabOffset = Vector2Int.zero;
        dragRect = null;
        dragSourceKind = DragSourceKind.None;
        ClearEquipmentPreviews();

        if (placementPreview != null)
        {
            if (panel != null)
            {
                placementPreview.transform.SetParent(panel.transform, false);
            }

            placementPreview.gameObject.SetActive(false);
        }
    }

    private void HideGridPanels()
    {
        foreach (InventoryGridPanel gridPanel in gridPanels)
        {
            if (gridPanel != null)
            {
                gridPanel.Hide();
            }
        }

        gridPanels.Clear();

        foreach (InventoryGridPanel gridPanel in bagPanelPool)
        {
            if (gridPanel != null)
            {
                gridPanel.Hide();
            }
        }
    }

    private void HideEquipmentSlotPanels()
    {
        foreach (EquipmentSlotPanel slotPanel in equipmentSlotPanels)
        {
            if (slotPanel != null)
            {
                slotPanel.Hide();
            }
        }

        equipmentSlotPanels.Clear();

        foreach (EquipmentSlotPanel slotPanel in equipmentSlotPanelPool)
        {
            if (slotPanel != null)
            {
                slotPanel.Hide();
            }
        }
    }

    private void ResolveReferences()
    {
        if (playerInventory == null)
        {
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        if (playerInventory != null)
        {
            playerEquipment = playerInventory.GetComponent<PlayerEquipment>();
            playerMovement = playerInventory.GetComponent<PlayerMovement>();
            playerAim = playerInventory.GetComponent<PlayerAim>();
            playerWeapon = playerInventory.GetComponent<PlayerWeapon>();
            playerInteractor = playerInventory.GetComponent<PlayerInteractor>();
        }

        if (uiModeController == null)
        {
            uiModeController = FindFirstObjectByType<GameplayUiModeController>();
        }
    }

    private void SubscribeToCurrentDataSources()
    {
        if (subscribedPlayerInventory != playerInventory)
        {
            if (subscribedPlayerInventory != null)
            {
                subscribedPlayerInventory.InventoryChanged -= HandleDataChanged;
            }

            subscribedPlayerInventory = playerInventory;
            if (subscribedPlayerInventory != null)
            {
                subscribedPlayerInventory.InventoryChanged += HandleDataChanged;
            }
        }

        if (subscribedPlayerEquipment != playerEquipment)
        {
            if (subscribedPlayerEquipment != null)
            {
                subscribedPlayerEquipment.EquipmentChanged -= HandleDataChanged;
            }

            subscribedPlayerEquipment = playerEquipment;
            if (subscribedPlayerEquipment != null)
            {
                subscribedPlayerEquipment.EquipmentChanged += HandleDataChanged;
            }
        }
    }

    private void UnsubscribeFromDataSources()
    {
        if (subscribedPlayerInventory != null)
        {
            subscribedPlayerInventory.InventoryChanged -= HandleDataChanged;
            subscribedPlayerInventory = null;
        }

        if (subscribedPlayerEquipment != null)
        {
            subscribedPlayerEquipment.EquipmentChanged -= HandleDataChanged;
            subscribedPlayerEquipment = null;
        }
    }

    private InputAction FindAction()
    {
        if (inputActions == null)
        {
            return new InputAction(inventoryActionName, InputActionType.Button, "<Keyboard>/tab");
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
        InputAction action = actionMap?.FindAction(inventoryActionName, false);
        return action ?? new InputAction(inventoryActionName, InputActionType.Button, "<Keyboard>/tab");
    }

    private void ResolveAuthoredView()
    {
        panel ??= gameObject;

        if (equipmentSlotPanelPool.Count == 0 && equipmentContainer != null)
        {
            equipmentSlotPanelPool.AddRange(equipmentContainer.GetComponentsInChildren<EquipmentSlotPanel>(true));
        }

        if (bagPanelPool.Count == 0 && bagContainer != null)
        {
            bagPanelPool.AddRange(bagContainer.GetComponentsInChildren<InventoryGridPanel>(true));
        }

        if (placementPreview == null)
        {
            placementPreview = GetComponentInChildren<Image>(true);
            if (placementPreview != null && placementPreview.name != "PlacementPreview")
            {
                placementPreview = null;
            }
        }

        placementPreviewRect = placementPreview != null ? placementPreview.rectTransform : placementPreviewRect;
        if (placementPreview != null)
        {
            placementPreview.raycastTarget = false;
            placementPreview.gameObject.SetActive(false);
        }
    }

    private bool HasRequiredView()
    {
        return panel != null
            && equipmentContainer != null
            && bagContainer != null
            && equipmentSlotPanelPool.Count > 0
            && bagPanelPool.Count > 0;
    }
}
