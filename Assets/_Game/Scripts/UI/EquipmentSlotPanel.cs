using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class EquipmentSlotPanel : MonoBehaviour
{
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private Image background;
    [SerializeField] private Image dropPreview;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private RectTransform itemRect;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemLabelText;
    [SerializeField] private EquipmentItemDragView dragView;

    private InventoryGuiController controller;
    private EquipmentSlot slot;
    private Camera eventCamera;

    public EquipmentSlot Slot => slot;

    public void Initialize(InventoryGuiController controller, EquipmentSlot slot, RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta, Camera eventCamera)
    {
        this.controller = controller;
        this.slot = slot;
        this.eventCamera = eventCamera;

        ResolveReferences();
        if (rootRect == null)
        {
            Debug.LogWarning($"{nameof(EquipmentSlotPanel)} is missing an authored root RectTransform.", this);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (transform.parent != parent)
        {
            transform.SetParent(parent, false);
        }

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = sizeDelta;

        if (titleText != null)
        {
            titleText.text = slot.DisplayName;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (background != null)
        {
            background.color = GetSlotColor();
        }

        if (valueText != null)
        {
            valueText.text = slot.GetDisplayValue();
        }

        if (slot.EquippedItem?.Definition != null)
        {
            BindItemView(slot.EquippedItem);
        }
        else if (itemRect != null)
        {
            itemRect.gameObject.SetActive(false);
        }
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        return rootRect != null && RectTransformUtility.RectangleContainsScreenPoint(rootRect, screenPosition, eventCamera);
    }

    public void SetDropPreview(bool visible, bool canDrop)
    {
        if (dropPreview == null)
        {
            return;
        }

        dropPreview.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        dropPreview.transform.SetAsLastSibling();
        dropPreview.color = slot.IsLocked
            ? new Color(0.7f, 0.7f, 0.7f, 0.26f)
            : canDrop
                ? new Color(0.2f, 1f, 0.35f, 0.28f)
                : new Color(1f, 0.2f, 0.2f, 0.28f);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BindItemView(RuntimeItemInstance item)
    {
        ResolveReferences();

        if (itemRect == null)
        {
            Debug.LogWarning($"{nameof(EquipmentSlotPanel)} '{name}' is missing an authored item view.", this);
            return;
        }

        itemRect.gameObject.SetActive(true);
        itemRect.anchorMin = new Vector2(0f, 0f);
        itemRect.anchorMax = new Vector2(1f, 0f);
        itemRect.pivot = new Vector2(0f, 0f);
        itemRect.anchoredPosition = new Vector2(10f, 10f);
        itemRect.sizeDelta = new Vector2(-20f, 42f);

        if (itemImage != null)
        {
            itemImage.color = GridItemViewColorUtility.GetItemColor(item.Definition);
            itemImage.sprite = item.Definition.Icon;
            itemImage.preserveAspect = item.Definition.Icon != null;
            if (item.Definition.Icon != null)
            {
                itemImage.color = Color.white;
            }
        }

        if (itemLabelText != null)
        {
            itemLabelText.text = item.Definition.DisplayName;
        }

        if (dragView != null && slot.CanUnequip)
        {
            dragView.Initialize(controller, this, slot);
        }
    }

    private void ResolveReferences()
    {
        rootRect ??= GetComponent<RectTransform>();
        background ??= GetComponent<Image>();

        if (dropPreview == null)
        {
            Transform dropPreviewTransform = transform.Find("DropPreview");
            if (dropPreviewTransform != null)
            {
                dropPreview = dropPreviewTransform.GetComponent<Image>();
            }
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (titleText == null && texts.Length > 0)
        {
            titleText = texts[0];
        }

        if (valueText == null && texts.Length > 1)
        {
            valueText = texts[1];
        }

        if (itemRect == null)
        {
            Transform itemTransform = transform.Find("ItemView");
            if (itemTransform != null)
            {
                itemRect = itemTransform as RectTransform;
            }
        }

        if (itemRect != null)
        {
            itemImage ??= itemRect.GetComponent<Image>();
            dragView ??= itemRect.GetComponent<EquipmentItemDragView>();
            if (itemLabelText == null)
            {
                itemLabelText = itemRect.GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private Color GetSlotColor()
    {
        if (slot.IsLocked)
        {
            return new Color(0.12f, 0.12f, 0.12f, 0.78f);
        }

        if (slot.IsFixed)
        {
            return new Color(0.22f, 0.22f, 0.22f, 0.78f);
        }

        return new Color(0f, 0f, 0f, 0.62f);
    }

}
