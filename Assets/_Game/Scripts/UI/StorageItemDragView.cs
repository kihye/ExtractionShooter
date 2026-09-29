using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public sealed class StorageItemDragView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private StorageGuiController controller;
    private StorageGridPanel gridPanel;
    private GridInventory grid;
    private GridItemPlacement placement;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    public void Initialize(StorageGuiController controller, StorageGridPanel gridPanel, GridInventory grid, GridItemPlacement placement)
    {
        this.controller = controller;
        this.gridPanel = gridPanel;
        this.grid = grid;
        this.placement = placement;
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || gridPanel == null || placement == null)
        {
            return;
        }

        gridPanel.TryGetRawCell(eventData.position, out int rawX, out int rawY);
        Vector2Int grabOffset = new Vector2Int(
            Mathf.Clamp(rawX - placement.X, 0, placement.Width - 1),
            Mathf.Clamp(rawY - placement.Y, 0, placement.Height - 1));

        canvasGroup.alpha = 0.72f;
        canvasGroup.blocksRaycasts = false;
        controller.BeginDrag(gridPanel, grid, placement, grabOffset, rectTransform, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        controller?.EndDrag(eventData.position);
    }
}
