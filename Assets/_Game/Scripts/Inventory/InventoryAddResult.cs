public readonly struct InventoryAddResult
{
    public InventoryAddResult(ItemDefinition definition, int requestedAmount, int addedAmount)
    {
        Definition = definition;
        RequestedAmount = requestedAmount;
        AddedAmount = addedAmount;
    }

    public ItemDefinition Definition { get; }
    public int RequestedAmount { get; }
    public int AddedAmount { get; }
    public int RemainingAmount => RequestedAmount - AddedAmount;
    public bool WasFullyAdded => RequestedAmount > 0 && RemainingAmount <= 0;
    public bool AddedAny => AddedAmount > 0;
}
