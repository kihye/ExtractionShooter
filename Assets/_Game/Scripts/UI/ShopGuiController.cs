using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ShopGuiController : MonoBehaviour
{
    private enum ShopTab
    {
        Buy,
        Sell
    }

    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private GameplayUiModeController uiModeController;
    [SerializeField] private GameSessionState sessionState;
    [SerializeField] private ShopCatalog shopCatalog;
    [SerializeField] private GameObject panel;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text selectedItemText;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text totalPriceText;
    [SerializeField] private TMP_Text resultText;

    [Header("Buttons")]
    [SerializeField] private Button buyTabButton;
    [SerializeField] private Button sellTabButton;
    [SerializeField] private Button decreaseQuantityButton;
    [SerializeField] private Button increaseQuantityButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private Button closeButton;

    [Header("Lists")]
    [SerializeField] private RectTransform buyListContainer;
    [SerializeField] private RectTransform sellListContainer;
    [SerializeField] private TMP_Text buyEmptyText;
    [SerializeField] private TMP_Text sellEmptyText;
    [SerializeField] private List<ShopTradeRowView> buyRowPool = new List<ShopTradeRowView>();
    [SerializeField] private List<ShopTradeRowView> sellRowPool = new List<ShopTradeRowView>();

    [Header("Legacy Placeholder References")]
    [SerializeField] private TMP_Text shopListText;
    [SerializeField] private TMP_Text playerInventoryText;

    private bool isOpen;
    private GameObject modePlayer;
    private PlayerInventory subscribedPlayerInventory;
    private GameSessionState subscribedSessionState;
    private ShopTradeService tradeService;
    private ShopTab currentTab = ShopTab.Buy;
    private ShopCatalog.BuyEntry selectedBuyEntry;
    private RuntimeItemInstance selectedSellItem;
    private int quantity = 1;

    private void Awake()
    {
        ResolveReferences();
        ValidateAuthoredView();
        RegisterButtons();
        CloseImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToCurrentDataSources();
    }

    private void OnDisable()
    {
        UnsubscribeFromDataSources();

        if (isOpen)
        {
            CloseShop();
        }
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    private void Update()
    {
        if (isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseShop();
        }
    }

    public void Bind(PlayerInventory inventory, GameplayUiModeController modeController)
    {
        if (inventory != null)
        {
            playerInventory = inventory;
        }

        if (modeController != null)
        {
            uiModeController = modeController;
        }

        ResolveReferences();
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

        ResolveReferences();
        SubscribeToCurrentDataSources();
        if (uiModeController != null && !uiModeController.TryEnterMode(GameplayUiMode.Shop, player))
        {
            return;
        }

        modePlayer = player;
        isOpen = true;
        quantity = 1;
        currentTab = ShopTab.Buy;

        if (panel == null)
        {
            Debug.LogWarning("Shop GUI is missing an authored panel reference.", this);
            isOpen = false;
            if (uiModeController != null)
            {
                uiModeController.ExitMode(GameplayUiMode.Shop);
            }

            return;
        }

        panel.SetActive(true);
        SetResult("거래할 품목을 선택하세요.");
        Refresh();
    }

    public void CloseShop()
    {
        isOpen = false;
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (uiModeController != null)
        {
            uiModeController.ExitMode(GameplayUiMode.Shop);
        }

        modePlayer = null;
        selectedSellItem = null;
    }

    private void SwitchToBuyTab()
    {
        currentTab = ShopTab.Buy;
        selectedSellItem = null;
        quantity = 1;
        SetResult("");
        Refresh();
    }

    private void SwitchToSellTab()
    {
        currentTab = ShopTab.Sell;
        selectedBuyEntry = null;
        quantity = 1;
        SetResult("");
        Refresh();
    }

    private void DecreaseQuantity()
    {
        quantity = Mathf.Max(1, quantity - 1);
        Refresh();
    }

    private void IncreaseQuantity()
    {
        quantity = Mathf.Min(GetMaxQuantityForSelection(), quantity + 1);
        Refresh();
    }

    private void ConfirmTrade()
    {
        ResolveReferences();
        tradeService = new ShopTradeService(shopCatalog, sessionState, playerInventory);
        ShopTradeResult result = currentTab == ShopTab.Buy
            ? tradeService.Buy(selectedBuyEntry?.Item, quantity)
            : tradeService.Sell(selectedSellItem, quantity);

        SetResult(result.Message);
        if (result.Succeeded)
        {
            quantity = 1;
            if (currentTab == ShopTab.Sell)
            {
                selectedSellItem = null;
            }
        }

        Refresh();
    }

    private void Refresh()
    {
        if (!isOpen)
        {
            return;
        }

        ResolveReferences();
        ValidateSelection();
        RefreshTexts();
        RefreshRows();
        RefreshButtons();
    }

    private void RefreshTexts()
    {
        string currencyName = shopCatalog != null ? shopCatalog.CurrencyDisplayName : "Credits";

        if (titleText != null)
        {
            titleText.text = shopCatalog != null ? shopCatalog.ShopName : "Shop";
        }

        if (creditsText != null)
        {
            int credits = sessionState != null ? sessionState.Credits : 0;
            creditsText.text = $"{currencyName}: {credits}";
        }

        ItemDefinition selectedDefinition = GetSelectedDefinition();
        if (selectedItemText != null)
        {
            selectedItemText.text = selectedDefinition != null
                ? selectedDefinition.DisplayName
                : "선택된 품목 없음";
        }

        if (quantityText != null)
        {
            quantityText.text = quantity.ToString();
        }

        if (totalPriceText != null)
        {
            int unitPrice = GetSelectedUnitPrice();
            totalPriceText.text = ShopTradeService.TryCalculateTotal(unitPrice, quantity, out int totalPrice)
                ? $"합계: {totalPrice} {currencyName}"
                : "합계: 계산 불가";
        }

        if (shopListText != null)
        {
            shopListText.text = "";
        }

        if (playerInventoryText != null)
        {
            playerInventoryText.text = "";
        }
    }

    private void RefreshRows()
    {
        HideRows(buyRowPool);
        HideRows(sellRowPool);

        if (buyListContainer != null)
        {
            buyListContainer.gameObject.SetActive(currentTab == ShopTab.Buy);
        }

        if (sellListContainer != null)
        {
            sellListContainer.gameObject.SetActive(currentTab == ShopTab.Sell);
        }

        SetEmptyText(buyEmptyText, false, "");
        SetEmptyText(sellEmptyText, false, "");

        if (shopCatalog == null)
        {
            TMP_Text activeEmptyText = currentTab == ShopTab.Buy ? buyEmptyText : sellEmptyText;
            SetEmptyText(activeEmptyText, true, "상점 데이터가 연결되지 않았습니다.");
            return;
        }

        if (currentTab == ShopTab.Buy)
        {
            RefreshBuyRows();
        }
        else
        {
            RefreshSellRows();
        }
    }

    private void RefreshBuyRows()
    {
        int rowIndex = 0;
        string currencyName = shopCatalog.CurrencyDisplayName;

        foreach (ShopCatalog.BuyEntry entry in shopCatalog.BuyEntries)
        {
            if (entry?.Item == null)
            {
                continue;
            }

            if (rowIndex >= buyRowPool.Count)
            {
                Debug.LogWarning($"Shop buy list needs more row views. Authored pool: {buyRowPool.Count}.", this);
                break;
            }

            ShopCatalog.BuyEntry capturedEntry = entry;
            buyRowPool[rowIndex].Bind(entry.Item, 1, entry.UnitPrice, currencyName, () =>
            {
                selectedBuyEntry = capturedEntry;
                selectedSellItem = null;
                quantity = 1;
                SetResult("");
                Refresh();
            });
            rowIndex++;
        }

        SetEmptyText(buyEmptyText, rowIndex == 0, "구매 가능한 품목이 없습니다.");
    }

    private void RefreshSellRows()
    {
        if (playerInventory == null)
        {
            SetEmptyText(sellEmptyText, true, "플레이어 인벤토리가 연결되지 않았습니다.");
            return;
        }

        tradeService = new ShopTradeService(shopCatalog, sessionState, playerInventory);
        int rowIndex = 0;
        string currencyName = shopCatalog.CurrencyDisplayName;
        bool poolLimitReached = false;

        foreach (GridInventory grid in playerInventory.Grids)
        {
            foreach (GridItemPlacement placement in grid.Placements)
            {
                RuntimeItemInstance item = placement.Item;
                if (!tradeService.CanSell(item) || !shopCatalog.TryGetSellEntry(item.Definition, out ShopCatalog.SellEntry entry))
                {
                    continue;
                }

                if (rowIndex >= sellRowPool.Count)
                {
                    Debug.LogWarning($"Shop sell list needs more row views. Authored pool: {sellRowPool.Count}.", this);
                    poolLimitReached = true;
                    break;
                }

                RuntimeItemInstance capturedItem = item;
                sellRowPool[rowIndex].Bind(item.Definition, item.Quantity, entry.UnitPrice, currencyName, () =>
                {
                    selectedSellItem = capturedItem;
                    selectedBuyEntry = null;
                    quantity = 1;
                    SetResult("");
                    Refresh();
                });
                rowIndex++;
            }

            if (poolLimitReached)
            {
                break;
            }
        }

        SetEmptyText(sellEmptyText, rowIndex == 0, "판매 가능한 아이템이 없습니다.");
    }

    private void RefreshButtons()
    {
        bool hasSelection = GetSelectedDefinition() != null;
        int maxQuantity = GetMaxQuantityForSelection();

        quantity = Mathf.Clamp(quantity, 1, maxQuantity);

        if (buyTabButton != null)
        {
            buyTabButton.interactable = currentTab != ShopTab.Buy;
        }

        if (sellTabButton != null)
        {
            sellTabButton.interactable = currentTab != ShopTab.Sell;
        }

        if (decreaseQuantityButton != null)
        {
            decreaseQuantityButton.interactable = hasSelection && quantity > 1;
        }

        if (increaseQuantityButton != null)
        {
            increaseQuantityButton.interactable = hasSelection && quantity < maxQuantity;
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = hasSelection;
        }

        if (confirmButtonText != null)
        {
            confirmButtonText.text = currentTab == ShopTab.Buy ? "구매" : "판매";
        }
    }

    private void ValidateSelection()
    {
        if (shopCatalog == null)
        {
            selectedBuyEntry = null;
            selectedSellItem = null;
            quantity = 1;
            return;
        }

        if (currentTab == ShopTab.Buy)
        {
            if (selectedBuyEntry?.Item != null && shopCatalog.TryGetBuyEntry(selectedBuyEntry.Item, out _))
            {
                return;
            }

            selectedBuyEntry = null;
            foreach (ShopCatalog.BuyEntry entry in shopCatalog.BuyEntries)
            {
                if (entry?.Item == null)
                {
                    continue;
                }

                selectedBuyEntry = entry;
                break;
            }
        }
        else
        {
            tradeService = new ShopTradeService(shopCatalog, sessionState, playerInventory);
            if (selectedSellItem != null
                && tradeService.CanSell(selectedSellItem)
                && playerInventory != null
                && playerInventory.TryFindPlacement(selectedSellItem, out _, out GridItemPlacement placement)
                && placement.Item.Quantity > 0)
            {
                return;
            }

            selectedSellItem = FindFirstSellableItem();
        }

        quantity = 1;
    }

    private RuntimeItemInstance FindFirstSellableItem()
    {
        if (playerInventory == null || shopCatalog == null)
        {
            return null;
        }

        tradeService = new ShopTradeService(shopCatalog, sessionState, playerInventory);
        foreach (GridInventory grid in playerInventory.Grids)
        {
            foreach (GridItemPlacement placement in grid.Placements)
            {
                if (tradeService.CanSell(placement.Item))
                {
                    return placement.Item;
                }
            }
        }

        return null;
    }

    private int GetMaxQuantityForSelection()
    {
        if (currentTab == ShopTab.Sell && selectedSellItem != null)
        {
            return Mathf.Max(1, selectedSellItem.Quantity);
        }

        return 999;
    }

    private ItemDefinition GetSelectedDefinition()
    {
        return currentTab == ShopTab.Buy ? selectedBuyEntry?.Item : selectedSellItem?.Definition;
    }

    private int GetSelectedUnitPrice()
    {
        if (shopCatalog == null)
        {
            return 0;
        }

        if (currentTab == ShopTab.Buy && selectedBuyEntry?.Item != null && shopCatalog.TryGetBuyEntry(selectedBuyEntry.Item, out ShopCatalog.BuyEntry buyEntry))
        {
            return buyEntry.UnitPrice;
        }

        if (currentTab == ShopTab.Sell && selectedSellItem?.Definition != null && shopCatalog.TryGetSellEntry(selectedSellItem.Definition, out ShopCatalog.SellEntry sellEntry))
        {
            return sellEntry.UnitPrice;
        }

        return 0;
    }

    private void ResolveReferences()
    {
        playerInventory ??= modePlayer != null ? modePlayer.GetComponent<PlayerInventory>() : FindFirstObjectByType<PlayerInventory>();
        uiModeController ??= FindFirstObjectByType<GameplayUiModeController>();

        if (sessionState == null)
        {
            StashInventory stashInventory = FindFirstObjectByType<StashInventory>();
            if (stashInventory != null)
            {
                sessionState = stashInventory.SessionState;
            }
        }
    }

    private void HandleInventoryChanged()
    {
        Refresh();
    }

    private void HandleCreditsChanged()
    {
        Refresh();
    }

    private void SubscribeToCurrentDataSources()
    {
        if (subscribedPlayerInventory != playerInventory)
        {
            if (subscribedPlayerInventory != null)
            {
                subscribedPlayerInventory.InventoryChanged -= HandleInventoryChanged;
            }

            subscribedPlayerInventory = playerInventory;
            if (subscribedPlayerInventory != null)
            {
                subscribedPlayerInventory.InventoryChanged += HandleInventoryChanged;
            }
        }

        if (subscribedSessionState != sessionState)
        {
            if (subscribedSessionState != null)
            {
                subscribedSessionState.CreditsChanged -= HandleCreditsChanged;
            }

            subscribedSessionState = sessionState;
            if (subscribedSessionState != null)
            {
                subscribedSessionState.CreditsChanged += HandleCreditsChanged;
            }
        }
    }

    private void UnsubscribeFromDataSources()
    {
        if (subscribedPlayerInventory != null)
        {
            subscribedPlayerInventory.InventoryChanged -= HandleInventoryChanged;
            subscribedPlayerInventory = null;
        }

        if (subscribedSessionState != null)
        {
            subscribedSessionState.CreditsChanged -= HandleCreditsChanged;
            subscribedSessionState = null;
        }
    }

    private void RegisterButtons()
    {
        UnregisterButtons();

        if (buyTabButton != null)
        {
            buyTabButton.onClick.AddListener(SwitchToBuyTab);
        }

        if (sellTabButton != null)
        {
            sellTabButton.onClick.AddListener(SwitchToSellTab);
        }

        if (decreaseQuantityButton != null)
        {
            decreaseQuantityButton.onClick.AddListener(DecreaseQuantity);
        }

        if (increaseQuantityButton != null)
        {
            increaseQuantityButton.onClick.AddListener(IncreaseQuantity);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(ConfirmTrade);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseShop);
        }
    }

    private void UnregisterButtons()
    {
        if (buyTabButton != null)
        {
            buyTabButton.onClick.RemoveListener(SwitchToBuyTab);
        }

        if (sellTabButton != null)
        {
            sellTabButton.onClick.RemoveListener(SwitchToSellTab);
        }

        if (decreaseQuantityButton != null)
        {
            decreaseQuantityButton.onClick.RemoveListener(DecreaseQuantity);
        }

        if (increaseQuantityButton != null)
        {
            increaseQuantityButton.onClick.RemoveListener(IncreaseQuantity);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmTrade);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseShop);
        }
    }

    private static void HideRows(List<ShopTradeRowView> rowPool)
    {
        foreach (ShopTradeRowView row in rowPool)
        {
            if (row != null)
            {
                row.Hide();
            }
        }
    }

    private static void SetEmptyText(TMP_Text emptyText, bool visible, string message)
    {
        if (emptyText == null)
        {
            return;
        }

        emptyText.text = message;
        emptyText.gameObject.SetActive(visible);
    }

    private void SetResult(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
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

    private void ValidateAuthoredView()
    {
        if (panel == null
            || shopCatalog == null
            || titleText == null
            || creditsText == null
            || selectedItemText == null
            || quantityText == null
            || totalPriceText == null
            || resultText == null
            || buyTabButton == null
            || sellTabButton == null
            || decreaseQuantityButton == null
            || increaseQuantityButton == null
            || confirmButton == null
            || closeButton == null
            || buyListContainer == null
            || sellListContainer == null
            || buyEmptyText == null
            || sellEmptyText == null
            || buyRowPool.Count == 0
            || sellRowPool.Count == 0)
        {
            Debug.LogWarning("ShopGuiController requires authored shop UI references.", this);
        }
    }
}
