using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryGridPanel : MonoBehaviour
{
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform gridRect;
    [SerializeField] private List<RectTransform> cellPool = new List<RectTransform>();
    [SerializeField] private List<Image> cellImages = new List<Image>();
    [SerializeField] private List<InventoryGridItemView> itemViewPool = new List<InventoryGridItemView>();

    private InventoryGuiController controller;
    private PlayerInventory inventory;
    private GridInventory grid;
    private Camera eventCamera;
    private float cellSize;
    private int warnedCellCapacity;
    private int warnedItemCapacity;

    public GridInventory Grid => grid;
    public RectTransform GridRect => gridRect;

    public void Initialize(InventoryGuiController controller, PlayerInventory inventory, GridInventory grid, RectTransform parent, Vector2 anchoredPosition, float cellSize, Camera eventCamera)
    {
        this.controller = controller;
        this.inventory = inventory;
        this.grid = grid;
        this.cellSize = cellSize;
        this.eventCamera = eventCamera;

        ResolveReferences();
        if (rootRect == null || gridRect == null)
        {
            Debug.LogWarning($"{nameof(InventoryGridPanel)} is missing authored RectTransform references.", this);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (transform.parent != parent)
        {
            transform.SetParent(parent, false);
        }

        Vector2 panelSize = new Vector2(grid.Width * cellSize + 36f, grid.Height * cellSize + 82f);
        SetTopLeft(rootRect, anchoredPosition, panelSize);
        SetTopLeft(gridRect, gridRect.anchoredPosition, new Vector2(grid.Width * cellSize, grid.Height * cellSize));

        if (titleText != null)
        {
            titleText.text = grid.DisplayName;
        }

        RefreshCells();
        RefreshItems();
    }

    public void RefreshItems()
    {
        foreach (InventoryGridItemView itemView in itemViewPool)
        {
            if (itemView != null)
            {
                itemView.Hide();
            }
        }

        int index = 0;
        foreach (GridItemPlacement placement in grid.Placements)
        {
            if (index >= itemViewPool.Count)
            {
                WarnItemCapacity();
                return;
            }

            InventoryGridItemView itemView = itemViewPool[index];
            if (itemView == null)
            {
                index++;
                continue;
            }

            itemView.Bind(controller, this, inventory, grid, placement, cellSize);
            index++;
        }
    }

    public bool TryGetCell(Vector2 screenPosition, Vector2Int grabOffset, out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = gridRect.rect;
        if (localPoint.x < rect.xMin || localPoint.x > rect.xMax || localPoint.y < rect.yMin || localPoint.y > rect.yMax)
        {
            return false;
        }

        int rawX = Mathf.FloorToInt((localPoint.x - rect.xMin) / cellSize);
        int rawY = Mathf.FloorToInt((rect.yMax - localPoint.y) / cellSize);
        cellX = rawX - grabOffset.x;
        cellY = rawY - grabOffset.y;
        return true;
    }

    public bool TryGetRawCell(Vector2 screenPosition, out int cellX, out int cellY)
    {
        cellX = 0;
        cellY = 0;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPosition, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = gridRect.rect;
        if (localPoint.x < rect.xMin || localPoint.x > rect.xMax || localPoint.y < rect.yMin || localPoint.y > rect.yMax)
        {
            return false;
        }

        cellX = Mathf.FloorToInt((localPoint.x - rect.xMin) / cellSize);
        cellY = Mathf.FloorToInt((rect.yMax - localPoint.y) / cellSize);
        return true;
    }

    public void PositionPreview(RectTransform preview, int x, int y, int width, int height)
    {
        preview.SetParent(gridRect, false);
        preview.anchorMin = new Vector2(0f, 1f);
        preview.anchorMax = new Vector2(0f, 1f);
        preview.pivot = new Vector2(0f, 1f);
        preview.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);
        preview.sizeDelta = new Vector2(width * cellSize, height * cellSize);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void RefreshCells()
    {
        int requiredCells = grid.Width * grid.Height;
        if (requiredCells > cellPool.Count)
        {
            WarnCellCapacity(requiredCells);
        }

        for (int index = 0; index < cellPool.Count; index++)
        {
            RectTransform cell = cellPool[index];
            if (cell == null)
            {
                continue;
            }

            bool active = index < requiredCells;
            cell.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            int x = index % grid.Width;
            int y = index / grid.Width;
            SetTopLeft(cell, new Vector2(x * cellSize, -y * cellSize), new Vector2(cellSize - 2f, cellSize - 2f));
        }
    }

    private void ResolveReferences()
    {
        rootRect ??= GetComponent<RectTransform>();

        if (gridRect == null)
        {
            Transform gridTransform = transform.Find("Grid");
            if (gridTransform != null)
            {
                gridRect = gridTransform as RectTransform;
            }
        }

        if (titleText == null)
        {
            titleText = GetComponentInChildren<TMP_Text>(true);
        }

        if (gridRect != null && cellPool.Count == 0)
        {
            foreach (RectTransform child in gridRect.GetComponentsInChildren<RectTransform>(true))
            {
                if (child != gridRect && child.name.StartsWith("Cell"))
                {
                    cellPool.Add(child);
                    Image image = child.GetComponent<Image>();
                    if (image != null)
                    {
                        cellImages.Add(image);
                    }
                }
            }
        }

        if (itemViewPool.Count == 0)
        {
            itemViewPool.AddRange(GetComponentsInChildren<InventoryGridItemView>(true));
        }
    }

    private void WarnCellCapacity(int requiredCells)
    {
        if (warnedCellCapacity == requiredCells)
        {
            return;
        }

        warnedCellCapacity = requiredCells;
        Debug.LogWarning($"{nameof(InventoryGridPanel)} '{name}' needs {requiredCells} cells, but the authored pool has {cellPool.Count}. Increase the prefab pool capacity.", this);
    }

    private void WarnItemCapacity()
    {
        if (warnedItemCapacity == itemViewPool.Count)
        {
            return;
        }

        warnedItemCapacity = itemViewPool.Count;
        Debug.LogWarning($"{nameof(InventoryGridPanel)} '{name}' has more item placements than the authored item view pool ({itemViewPool.Count}). Increase the prefab pool capacity.", this);
    }

    private static void SetTopLeft(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
    }
}
