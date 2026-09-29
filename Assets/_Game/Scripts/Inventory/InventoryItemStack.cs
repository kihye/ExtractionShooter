public readonly struct InventoryItemStack
{
    public InventoryItemStack(ItemDefinition definition, int amount)
    {
        Definition = definition;
        Amount = amount;
    }

    public ItemDefinition Definition { get; }
    public ItemData ItemData => Definition as ItemData;
    public int Amount { get; }
}
