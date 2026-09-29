using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BaseEconomyUI : MonoBehaviour
{
    [SerializeField] private BaseEconomyController economyController;
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text sellValueText;
    [SerializeField] private TMP_Text ammoStockText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button sellAllButton;
    [SerializeField] private TMP_Text sellAllButtonText;
    [SerializeField] private Button buyAmmoButton;
    [SerializeField] private TMP_Text buyAmmoButtonText;

    private void Awake()
    {
        ResolveReferences();
        ValidateAuthoredView();
        RegisterButtons();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (sessionState != null)
        {
            sessionState.CreditsChanged += Refresh;
            sessionState.AmmoStockChanged += Refresh;
        }

        if (economyController != null && economyController.StashInventory != null)
        {
            economyController.StashInventory.StashChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (sessionState != null)
        {
            sessionState.CreditsChanged -= Refresh;
            sessionState.AmmoStockChanged -= Refresh;
        }

        if (economyController != null && economyController.StashInventory != null)
        {
            economyController.StashInventory.StashChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        if (sellAllButton != null)
        {
            sellAllButton.onClick.RemoveListener(SellAllLoot);
        }

        if (buyAmmoButton != null)
        {
            buyAmmoButton.onClick.RemoveListener(BuyAmmo);
        }
    }

    private void RegisterButtons()
    {
        if (sellAllButton != null)
        {
            sellAllButton.onClick.AddListener(SellAllLoot);
        }

        if (buyAmmoButton != null)
        {
            buyAmmoButton.onClick.AddListener(BuyAmmo);
        }
    }

    private void SellAllLoot()
    {
        if (economyController == null || !economyController.TrySellAllLoot())
        {
            SetStatus("No sellable loot.");
        }
        else
        {
            SetStatus("Loot sold.");
        }

        Refresh();
    }

    private void BuyAmmo()
    {
        if (economyController == null || !economyController.TryBuyAmmo())
        {
            SetStatus("Not enough credits.");
        }
        else
        {
            SetStatus("Ammo purchased.");
        }

        Refresh();
    }

    private void Refresh()
    {
        ResolveReferences();

        int credits = sessionState != null ? sessionState.Credits : 0;
        int ammoStock = sessionState != null ? sessionState.AmmoStock : 0;
        int sellValue = economyController != null ? economyController.CalculateSellAllValue() : 0;
        int ammoAmount = economyController != null ? economyController.AmmoPurchaseAmount : 12;
        int ammoPrice = economyController != null ? economyController.AmmoPurchasePrice : 20;

        if (creditsText != null)
        {
            creditsText.text = $"Credits\n{credits} C";
        }

        if (sellValueText != null)
        {
            sellValueText.text = $"Sell Value\n{sellValue} C";
        }

        if (ammoStockText != null)
        {
            ammoStockText.text = $"Ammo Stock\n{ammoStock}";
        }

        if (sellAllButton != null)
        {
            sellAllButton.interactable = sellValue > 0;
        }

        if (buyAmmoButton != null)
        {
            buyAmmoButton.interactable = economyController != null && economyController.CanBuyAmmo();
        }

        if (sellAllButtonText != null)
        {
            sellAllButtonText.text = "SELL ALL LOOT";
        }

        if (buyAmmoButtonText != null)
        {
            buyAmmoButtonText.text = $"BUY {ammoAmount} AMMO - {ammoPrice} C";
        }
    }

    private void ResolveReferences()
    {
        economyController ??= FindFirstObjectByType<BaseEconomyController>();

        if (sessionState == null && economyController != null)
        {
            sessionState = economyController.SessionState;
        }

        if (sessionState == null)
        {
            StashInventory stashInventory = FindFirstObjectByType<StashInventory>();
            if (stashInventory != null)
            {
                sessionState = stashInventory.SessionState;
            }
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void ValidateAuthoredView()
    {
        if (creditsText == null || sellValueText == null || ammoStockText == null || sellAllButton == null || buyAmmoButton == null)
        {
            Debug.LogWarning("BaseEconomyUI requires authored text and button references.", this);
        }
    }
}
