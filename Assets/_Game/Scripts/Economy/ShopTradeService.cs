using UnityEngine;

public sealed class ShopTradeService
{
    private readonly ShopCatalog catalog;
    private readonly GameSessionState sessionState;
    private readonly PlayerInventory playerInventory;

    public ShopTradeService(ShopCatalog catalog, GameSessionState sessionState, PlayerInventory playerInventory)
    {
        this.catalog = catalog;
        this.sessionState = sessionState;
        this.playerInventory = playerInventory;
    }

    public ShopTradeResult Buy(ItemDefinition item, int quantity)
    {
        if (!HasRequiredReferences())
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.MissingReferences, "상점 참조가 누락되었습니다.");
        }

        if (catalog.ValidateCatalog().Count > 0)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.InvalidCatalog, "상점 설정이 올바르지 않습니다.");
        }

        if (item == null || !catalog.TryGetBuyEntry(item, out ShopCatalog.BuyEntry entry))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.ItemUnavailable, "구매할 수 없는 아이템입니다.");
        }

        if (quantity <= 0)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.InvalidQuantity, "수량이 올바르지 않습니다.");
        }

        if (!TryCalculateTotal(entry.UnitPrice, quantity, out int totalPrice))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.PriceOverflow, "총가격이 너무 큽니다.");
        }

        if (sessionState.Credits < totalPrice)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.NotEnoughCredits, "화폐가 부족합니다.");
        }

        if (!playerInventory.CanFullyAddItem(item, quantity))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.NotEnoughSpace, "가방 공간이 부족합니다.");
        }

        if (!sessionState.TrySpendCredits(totalPrice))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.NotEnoughCredits, "화폐가 부족합니다.");
        }

        InventoryAddResult addResult = playerInventory.TryAddItemAtomic(item, quantity);
        if (!addResult.WasFullyAdded)
        {
            sessionState.AddCredits(totalPrice);
            return ShopTradeResult.Fail(ShopTradeFailureReason.NotEnoughSpace, "가방 공간이 부족합니다.");
        }

        return ShopTradeResult.Success($"{item.DisplayName} x{quantity} 구매 완료.", totalPrice);
    }

    public ShopTradeResult Sell(RuntimeItemInstance item, int quantity)
    {
        if (!HasRequiredReferences())
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.MissingReferences, "상점 참조가 누락되었습니다.");
        }

        if (catalog.ValidateCatalog().Count > 0)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.InvalidCatalog, "상점 설정이 올바르지 않습니다.");
        }

        if (item?.Definition == null || !catalog.TryGetSellEntry(item.Definition, out ShopCatalog.SellEntry entry))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.CannotSellItem, "판매할 수 없는 아이템입니다.");
        }

        if (item.Definition.Category == ItemCategory.Bag || item.Definition.Category == ItemCategory.Weapon || item.Definition.Category == ItemCategory.Armor || item.Definition.Category == ItemCategory.Accessory)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.CannotSellItem, "판매할 수 없는 아이템입니다.");
        }

        if (quantity <= 0)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.InvalidQuantity, "수량이 올바르지 않습니다.");
        }

        if (!TryCalculateTotal(entry.UnitPrice, quantity, out int totalPrice))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.PriceOverflow, "총가격이 너무 큽니다.");
        }

        if (!sessionState.CanAddCredits(totalPrice))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.PriceOverflow, "보유 화폐 한도를 초과합니다.");
        }

        if (!playerInventory.TryFindPlacement(item, out GridInventory sourceGrid, out GridItemPlacement placement))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.ItemChanged, "아이템 수량이 변경되었습니다.");
        }

        if (placement.Item.Quantity < quantity)
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.ItemChanged, "아이템 수량이 변경되었습니다.");
        }

        if (!playerInventory.TryRemoveFromPlacement(sourceGrid, placement, quantity))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.ItemChanged, "아이템 수량이 변경되었습니다.");
        }

        if (!sessionState.TryAddCredits(totalPrice))
        {
            return ShopTradeResult.Fail(ShopTradeFailureReason.PriceOverflow, "보유 화폐 한도를 초과합니다.");
        }

        return ShopTradeResult.Success($"{item.Definition.DisplayName} x{quantity} 판매 완료.", totalPrice);
    }

    public bool CanSell(RuntimeItemInstance item)
    {
        return item?.Definition != null
            && item.Definition.Category != ItemCategory.Bag
            && item.Definition.Category != ItemCategory.Weapon
            && item.Definition.Category != ItemCategory.Armor
            && item.Definition.Category != ItemCategory.Accessory
            && catalog != null
            && catalog.TryGetSellEntry(item.Definition, out _);
    }

    public static bool TryCalculateTotal(int unitPrice, int quantity, out int totalPrice)
    {
        totalPrice = 0;
        if (unitPrice <= 0 || quantity <= 0)
        {
            return false;
        }

        long total = (long)unitPrice * quantity;
        if (total > int.MaxValue)
        {
            return false;
        }

        totalPrice = (int)total;
        return true;
    }

    private bool HasRequiredReferences()
    {
        return catalog != null && sessionState != null && playerInventory != null;
    }
}
