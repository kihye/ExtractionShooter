public readonly struct ShopTradeResult
{
    private ShopTradeResult(bool succeeded, ShopTradeFailureReason failureReason, string message, int totalPrice)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        Message = message;
        TotalPrice = totalPrice;
    }

    public bool Succeeded { get; }
    public ShopTradeFailureReason FailureReason { get; }
    public string Message { get; }
    public int TotalPrice { get; }

    public static ShopTradeResult Success(string message, int totalPrice)
    {
        return new ShopTradeResult(true, ShopTradeFailureReason.None, message, totalPrice);
    }

    public static ShopTradeResult Fail(ShopTradeFailureReason failureReason, string message)
    {
        return new ShopTradeResult(false, failureReason, message, 0);
    }
}
