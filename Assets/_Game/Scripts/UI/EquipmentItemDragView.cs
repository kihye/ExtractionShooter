using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public sealed class EquipmentItemDragView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private InventoryGuiController controller;
    private EquipmentSlotPanel slotPanel;
    private EquipmentSlot slot;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    public void Initialize(InventoryGuiController controller, EquipmentSlotPanel slotPanel, EquipmentSlot slot)
    {
        this.controller = controller;
        this.slotPanel = slotPanel;
        this.slot = slot;
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || slotPanel == null || slot?.EquippedItem == null)
        {
            return;
        }

        canvasGroup.alpha = 0.72f;
        canvasGroup.blocksRaycasts = false;
        controller.BeginEquipmentDrag(slotPanel, slot, rectTransform, eventData.position);
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
