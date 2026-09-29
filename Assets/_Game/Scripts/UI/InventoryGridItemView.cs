using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class InventoryGridItemView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private InventoryItemDragView dragView;

    private RectTransform rectTransform;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = GetComponent<RectTransform>();

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(InventoryGuiController controller, InventoryGridPanel gridPanel, PlayerInventory inventory, GridInventory grid, GridItemPlacement placement, float cellSize)
    {
        ResolveReferences();

        if (placement?.Definition == null)
        {
            Hide();
            return;
        }

        RectTransform rect = RectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(placement.X * cellSize, -placement.Y * cellSize);
        rect.sizeDelta = new Vector2(placement.Width * cellSize - 4f, placement.Height * cellSize - 4f);

        if (background != null)
        {
            background.color = GridItemViewColorUtility.GetItemColor(placement.Definition);
            background.sprite = placement.Definition.Icon;
            background.preserveAspect = placement.Definition.Icon != null;
            if (placement.Definition.Icon != null)
            {
                background.color = Color.white;
            }
        }

        if (labelText != null)
        {
            labelText.text = placement.Definition.DisplayName;
        }

        if (stackText != null)
        {
            bool showStack = placement.Definition.CanStack && placement.Item.Quantity > 1;
            stackText.gameObject.SetActive(showStack);
            if (showStack)
            {
                stackText.text = placement.Item.Quantity.ToString();
            }
        }

        if (dragView != null)
        {
            dragView.Initialize(controller, gridPanel, inventory, grid, placement);
        }
        else
        {
            Debug.LogWarning($"{nameof(InventoryGridItemView)} is missing {nameof(InventoryItemDragView)}.", this);
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        rectTransform ??= GetComponent<RectTransform>();
        background ??= GetComponent<Image>();
        dragView ??= GetComponent<InventoryItemDragView>();

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (labelText == null && texts.Length > 0)
        {
            labelText = texts[0];
        }

        if (stackText == null && texts.Length > 1)
        {
            stackText = texts[1];
        }
    }
}
