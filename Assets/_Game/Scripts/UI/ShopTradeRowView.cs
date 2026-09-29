using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopTradeRowView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text priceText;

    private Action clicked;

    private void Awake()
    {
        ResolveReferences();
        RegisterButton();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Bind(ItemDefinition item, int quantity, int unitPrice, string currencyName, Action onClicked)
    {
        ResolveReferences();
        clicked = onClicked;

        if (item == null)
        {
            Hide();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.color = item.Icon != null ? Color.white : GridItemViewColorUtility.GetItemColor(item);
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
        {
            nameText.text = item.DisplayName;
        }

        if (detailText != null)
        {
            detailText.text = quantity > 1 ? $"x{quantity}" : string.Empty;
        }

        if (priceText != null)
        {
            priceText.text = $"{unitPrice} {currencyName}";
        }

        if (button != null)
        {
            button.interactable = true;
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        clicked = null;
        gameObject.SetActive(false);
    }

    private void HandleClicked()
    {
        clicked?.Invoke();
    }

    private void RegisterButton()
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(HandleClicked);
        button.onClick.AddListener(HandleClicked);
    }

    private void ResolveReferences()
    {
        button ??= GetComponent<Button>();
        iconImage ??= transform.Find("Icon")?.GetComponent<Image>();

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        if (nameText == null && texts.Length > 0)
        {
            nameText = texts[0];
        }

        if (detailText == null && texts.Length > 1)
        {
            detailText = texts[1];
        }

        if (priceText == null && texts.Length > 2)
        {
            priceText = texts[2];
        }
    }
}
