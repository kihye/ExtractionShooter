using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class StorageGuiController : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private StashInventory stashInventory;
    [SerializeField] private GameplayUiModeController uiModeController;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform playerGridViewport;
    [SerializeField] private RectTransform playerGridContainer;
    [SerializeField] private RectTransform stashGridViewport;
    [SerializeField] private RectTransform stashGridContainer;
    [SerializeField] private List<StorageGridPanel> playerGridPanelPool = new List<StorageGridPanel>();
    [SerializeField] private StorageGridPanel stashGridPanel;
    [SerializeField, Min(24f)] private float cellSize = 42f;

    private readonly List<StorageGridPanel> gridPanels = new List<StorageGridPanel>();
    private Image placementPreview;
    private RectTransform placementPreviewRect;
    private GameObject modePlayer;
    private bool isOpen;
    private PlayerInventory subscribedPlayerInventory;
    private StashInventory subscribedStashInventory;
    private bool isApplyingMove;
    private bool viewInitialized;

    private StorageGridPanel dragSourcePanel;
    private GridInventory dragSourceGrid;
    private GridItemPlacement dragPlacement;
    private RuntimeItemInstance dragItem;
    private Vector2Int dragGrabOffset;
    private RectTransform dragRect;

    private void Awake()
    {
        InitializeView();
        CloseImmediate();
    }

    private void OnEnable()
    {
        SubscribeToCurrentDataSources();
    }

    private void OnDisable()
    {
        UnsubscribeFromDataSources();

        if (isOpen)
        {
            CloseStorage();
        }
    }

    private void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseStorage();
        }
    }

    public void Bind(PlayerInventory inventory, StashInventory stash, GameplayUiModeController modeController)
    {
        if (inventory != null)
        {
            playerInventory = inventory;
        }

        if (stash != null)
        {
            stashInventory = stash;
        }

        if (modeController != null)
        {
            uiModeController = modeController;
        }

        InitializeView();
        SubscribeToCurrentDataSources();

        if (isOpen)
        {
            Refresh();
        }
    }

    public void Open(GameObject player)
    {
        if (player != null)
        {
            playerInventory = player.GetComponent<PlayerInventory>();
        }

        ResolveRuntimeReferences();
        InitializeView();
        SubscribeToCurrentDataSources();

        if (playerInventory == null || stashInventory == null)
        {
            Debug.LogWarning("Storage UI is missing PlayerInventory or StashInventory.", this);
            return;
        }

        if (uiModeController != null && !uiModeController.TryEnterMode(GameplayUiMode.Storage, player))
        {
            return;
        }

        modePlayer = player;
        isOpen = true;
        if (!HasRequiredView())
        {
            Debug.LogWarning("Storage GUI is missing authored Scene/Prefab references.", this);
            isOpen = false;
            if (uiModeController != null)
            {
                uiModeController.ExitMode(GameplayUiMode.Storage);
            }

            return;
        }

        panel.SetActive(true);
        Refresh();
    }

    public void CloseStorage()
    {
        isOpen = false;
        ClearDrag();
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (uiModeController != null)
        {
            uiModeController.ExitMode(GameplayUiMode.Storage);
        }

        modePlayer = null;
    }

    public void BeginDrag(StorageGridPanel sourcePanel, GridInventory sourceGrid, GridItemPlacement placement, Vector2Int grabOffset, RectTransform itemRect, Vector2 screenPosition)
    {
        if (!isOpen)
        {
            return;
        }

        dragSourcePanel = sourcePanel;
        dragSourceGrid = sourceGrid;
        dragPlacement = placement;
        dragItem = placement?.Item;
        dragGrabOffset = grabOffset;
        dragRect = itemRect;

        if (dragRect != null)
        {
            dragRect.SetAsLastSibling();
        }

        UpdateDrag(screenPosition);
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (dragItem?.Definition == null)
        {
            return;
        }

        if (dragRect != null)
        {
            dragRect.position = new Vector3(
                screenPosition.x - dragGrabOffset.x * cellSize,
                screenPosition.y + dragGrabOffset.y * cellSize,
                dragRect.position.z);
        }

        StorageGridPanel targetPanel = FindTargetPanel(screenPosition, out int targetX, out int targetY);
        bool canDrop = targetPanel != null && CanDropOn(targetPanel, targetX, targetY);
        UpdatePlacementPreview(targetPanel, targetX, targetY, canDrop);
    }

    public void EndDrag(Vector2 screenPosition)
    {
        if (dragItem?.Definition == null)
        {
            return;
        }

        StorageGridPanel targetPanel = FindTargetPanel(screenPosition, out int targetX, out int targetY);
        RuntimeItemInstance item = dragItem;
        GridInventory sourceGrid = dragSourceGrid;
        GridItemPlacement placement = dragPlacement;
        StorageGridSource sourceKind = dragSourcePanel != null ? dragSourcePanel.Source : StorageGridSource.PlayerInventory;
        int originalX = placement != null ? placement.X : 0;
        int originalY = placement != null ? placement.Y : 0;

        ClearDrag();

        if (targetPanel == null || placement == null)
        {
            Refresh();
            return;
        }

        bool moved;
        isApplyingMove = true;
        try
        {
            moved = TryMoveItem(sourceKind, sourceGrid, placement, item, originalX, originalY, targetPanel.Source, targetPanel.Grid, targetX, targetY);
        }
        finally
        {
            isApplyingMove = false;
        }

        if (!moved)
        {
            Debug.Log("Storage move failed.", this);
        }

        Refresh();
    }

    private bool TryMoveItem(
        StorageGridSource sourceKind,
        GridInventory sourceGrid,
        GridItemPlacement placement,
        RuntimeItemInstance item,
        int originalX,
        int originalY,
        StorageGridSource targetKind,
        GridInventory targetGrid,
        int targetX,
        int targetY)
    {
        if (sourceGrid == null || targetGrid == null || placement == null || item?.Definition == null)
        {
            return false;
        }

        if (sourceKind == StorageGridSource.PlayerInventory && targetKind == StorageGridSource.PlayerInventory)
        {
            return playerInventory != null && playerInventory.TryMoveItem(sourceGrid, placement, targetGrid, targetX, targetY);
        }

        if (sourceKind == StorageGridSource.Stash && targetKind == StorageGridSource.Stash)
        {
            return sourceGrid.TryMove(placement, targetX, targetY);
        }

        if (sourceKind == StorageGridSource.PlayerInventory && targetKind == StorageGridSource.Stash)
        {
            if (playerInventory == null || stashInventory == null || !stashInventory.CanPlaceRuntimeItem(item, targetX, targetY))
            {
                return false;
            }

            if (!playerInventory.RemovePlacement(sourceGrid, placement, false))
            {
                return false;
            }

            if (stashInventory.TryPlaceRuntimeItem(item, targetX, targetY))
            {
                playerInventory.NotifyInventoryChanged();
                return true;
            }

            playerInventory.TryPlaceItem(sourceGrid, item, originalX, originalY, false);
            playerInventory.NotifyInventoryChanged();
            return false;
        }

        if (sourceKind == StorageGridSource.Stash && targetKind == StorageGridSource.PlayerInventory)
        {
            if (playerInventory == null || stashInventory == null || !playerInventory.CanPlaceItem(targetGrid, item, targetX, targetY))
            {
                return false;
            }

            if (!stashInventory.RemovePlacement(placement))
            {
                return false;
            }

            if (playerInventory.TryPlaceItem(targetGrid, item, targetX, targetY))
            {
                return true;
            }

            stashInventory.TryPlaceRuntimeItem(item, originalX, originalY);
            return false;
        }

        return false;
    }

    private bool CanDropOn(StorageGridPanel targetPanel, int targetX, int targetY)
    {
        if (targetPanel == null || dragSourcePanel == null || dragItem?.Definition == null || dragPlacement == null)
        {
            return false;
        }

        if (dragSourcePanel.Source == StorageGridSource.PlayerInventory && targetPanel.Source == StorageGridSource.PlayerInventory)
        {
            return playerInventory != null && playerInventory.CanMoveItem(dragSourceGrid, dragPlacement, targetPanel.Grid, targetX, targetY);
        }

        if (dragSourcePanel.Source == StorageGridSource.Stash && targetPanel.Source == StorageGridSource.Stash)
        {
            return targetPanel.Grid.CanPlaceIgnoring(dragItem, targetX, targetY, dragPlacement);
        }

        if (targetPanel.Source == StorageGridSource.Stash)
        {
            return stashInventory != null && stashInventory.CanPlaceRuntimeItem(dragItem, targetX, targetY);
        }

        return playerInventory != null && playerInventory.CanPlaceItem(targetPanel.Grid, dragItem, targetX, targetY);
    }

    private StorageGridPanel FindTargetPanel(Vector2 screenPosition, out int targetX, out int targetY)
    {
        foreach (StorageGridPanel gridPanel in gridPanels)
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

    private void Refresh()
    {
        InitializeView();

        if (!isOpen || playerInventory == null || stashInventory == null)
        {
            return;
        }

        if (!HasRequiredView())
        {
            Debug.LogWarning("Storage GUI is missing authored Scene/Prefab references.", this);
            return;
        }

        HideGridPanels();

        float x = 0f;
        float maxPlayerGridPanelHeight = 0f;
        int gridIndex = 0;
        foreach (GridInventory grid in playerInventory.Grids)
        {
            if (gridIndex >= playerGridPanelPool.Count)
            {
                Debug.LogWarning($"Storage UI needs {playerInventory.Grids.Count} player inventory panels, but the authored pool has {playerGridPanelPool.Count}.", this);
                break;
            }

            StorageGridPanel gridPanel = playerGridPanelPool[gridIndex];
            if (gridPanel == null)
            {
                gridIndex++;
                x += grid.Width * cellSize + 64f;
                continue;
            }

            gridPanel.Initialize(this, grid, StorageGridSource.PlayerInventory, playerGridContainer, new Vector2(x, 0f), cellSize, null);
            gridPanels.Add(gridPanel);

            maxPlayerGridPanelHeight = Mathf.Max(maxPlayerGridPanelHeight, grid.Height * cellSize + 82f);
            x += grid.Width * cellSize + 64f;
            gridIndex++;
        }

        playerGridContainer.sizeDelta = new Vector2(Mathf.Max(820f, x), Mathf.Max(760f, maxPlayerGridPanelHeight));
        playerGridContainer.anchoredPosition = Vector2.zero;

        if (stashGridPanel != null)
        {
            stashGridPanel.Initialize(this, stashInventory.GetGrid(), StorageGridSource.Stash, stashGridContainer, Vector2.zero, cellSize, null);
            gridPanels.Add(stashGridPanel);
        }
        else
        {
            Debug.LogWarning("Storage UI is missing the authored stash grid panel.", this);
        }

        GridInventory stashGrid = stashInventory.GetGrid();
        stashGridContainer.sizeDelta = new Vector2(
            Mathf.Max(720f, stashGrid.Width * cellSize + 36f),
            Mathf.Max(760f, stashGrid.Height * cellSize + 82f));
        stashGridContainer.anchoredPosition = Vector2.zero;
    }

    private void HandleDataChanged()
    {
        if (isApplyingMove)
        {
            return;
        }

        Refresh();
    }

    private void UpdatePlacementPreview(StorageGridPanel targetPanel, int targetX, int targetY, bool canPlace)
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

    private void ClearDrag()
    {
        dragSourcePanel = null;
        dragSourceGrid = null;
        dragPlacement = null;
        dragItem = null;
        dragGrabOffset = Vector2Int.zero;
        dragRect = null;

        if (placementPreview != null)
        {
            placementPreview.transform.SetParent(panel.transform, false);
            placementPreview.gameObject.SetActive(false);
        }
    }

    private void HideGridPanels()
    {
        foreach (StorageGridPanel gridPanel in gridPanels)
        {
            if (gridPanel != null)
            {
                gridPanel.Hide();
            }
        }

        gridPanels.Clear();

        foreach (StorageGridPanel gridPanel in playerGridPanelPool)
        {
            if (gridPanel != null)
            {
                gridPanel.Hide();
            }
        }

        if (stashGridPanel != null)
        {
            stashGridPanel.Hide();
        }
    }

    private void ResolveRuntimeReferences()
    {
        playerInventory ??= modePlayer != null ? modePlayer.GetComponent<PlayerInventory>() : FindFirstObjectByType<PlayerInventory>();
        stashInventory ??= FindFirstObjectByType<StashInventory>();
        uiModeController ??= FindFirstObjectByType<GameplayUiModeController>();
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

        if (subscribedStashInventory != stashInventory)
        {
            if (subscribedStashInventory != null)
            {
                subscribedStashInventory.StashChanged -= HandleDataChanged;
            }

            subscribedStashInventory = stashInventory;
            if (subscribedStashInventory != null)
            {
                subscribedStashInventory.StashChanged += HandleDataChanged;
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

        if (subscribedStashInventory != null)
        {
            subscribedStashInventory.StashChanged -= HandleDataChanged;
            subscribedStashInventory = null;
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

    private void InitializeView()
    {
        if (viewInitialized && HasRequiredView())
        {
            return;
        }

        panel ??= gameObject;

        Transform panelTransform = panel != null ? panel.transform : transform;
        playerGridViewport = ResolveChildRect(playerGridViewport, panelTransform, "PlayerGridViewport");
        stashGridViewport = ResolveChildRect(stashGridViewport, panelTransform, "StashGridViewport");
        playerGridContainer = ResolveChildRect(playerGridContainer, playerGridViewport, "PlayerGridContent");
        stashGridContainer = ResolveChildRect(stashGridContainer, stashGridViewport, "StashGridContent");

        if ((playerGridPanelPool.Count == 0 || ContainsPanelOutside(playerGridPanelPool, playerGridContainer)) && playerGridContainer != null)
        {
            playerGridPanelPool.Clear();
            playerGridPanelPool.AddRange(playerGridContainer.GetComponentsInChildren<StorageGridPanel>(true));
        }

        if ((stashGridPanel == null || (stashGridContainer != null && !stashGridPanel.transform.IsChildOf(stashGridContainer))) && stashGridContainer != null)
        {
            StorageGridPanel[] panels = stashGridContainer.GetComponentsInChildren<StorageGridPanel>(true);
            if (panels.Length > 0)
            {
                stashGridPanel = panels[0];
            }
        }

        if (placementPreview == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image != null && image.name == "PlacementPreview")
                {
                    placementPreview = image;
                    break;
                }
            }
        }

        placementPreviewRect = placementPreview != null ? placementPreview.rectTransform : placementPreviewRect;
        if (placementPreview != null)
        {
            placementPreview.raycastTarget = false;
            placementPreview.gameObject.SetActive(false);
        }

        viewInitialized = HasRequiredView();
    }

    private bool HasRequiredView()
    {
        return panel != null
            && playerGridContainer != null
            && stashGridContainer != null
            && playerGridPanelPool.Count > 0
            && stashGridPanel != null
            && !ContainsPanelOutside(playerGridPanelPool, playerGridContainer)
            && stashGridPanel.transform.IsChildOf(stashGridContainer);
    }

    private static RectTransform ResolveChildRect(RectTransform current, Transform parent, string childName)
    {
        if (current != null || parent == null)
        {
            return current;
        }

        Transform child = parent.Find(childName);
        return child as RectTransform;
    }

    private static bool ContainsPanelOutside(List<StorageGridPanel> panels, RectTransform expectedParent)
    {
        if (expectedParent == null)
        {
            return false;
        }

        foreach (StorageGridPanel panel in panels)
        {
            if (panel != null && !panel.transform.IsChildOf(expectedParent))
            {
                return true;
            }
        }

        return false;
    }
}
