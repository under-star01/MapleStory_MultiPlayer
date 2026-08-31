public class PlayerInventoryRecord
{
    public int ItemId { get; }
    public int Quantity { get; }

    public PlayerInventoryRecord(
        int itemId,
        int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}