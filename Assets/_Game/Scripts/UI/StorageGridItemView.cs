using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class StorageGridItemView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private StorageItemDragView dragView;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : rectTransform = GetComponent<RectTransform>();

    private void Awake()
    {
        ResolveReferences();
    }

    public void Bind(StorageGuiController controller, StorageGridPanel gridPanel, GridInventory grid, GridItemPlacement placement, float cellSize)
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
            dragView.Initialize(controller, gridPanel, grid, placement);
        }
        else
        {
            Debug.LogWarning($"{nameof(StorageGridItemView)} is missing {nameof(StorageItemDragView)}.", this);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
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
        canvasGroup ??= GetComponent<CanvasGroup>();
        background ??= GetComponent<Image>();
        dragView ??= GetComponent<StorageItemDragView>();

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
