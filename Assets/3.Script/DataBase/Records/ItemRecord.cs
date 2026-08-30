public class ItemRecord
{
    public int ItemId
    {
        get;
    }

    public string ItemName
    {
        get;
    }

    public int HealAmount
    {
        get;
    }

    public int MaxStack
    {
        get;
    }

    public ItemRecord(
        int itemId,
        string itemName,
        int healAmount,
        int maxStack)
    {
        ItemId = itemId;
        ItemName = itemName;
        HealAmount = healAmount;
        MaxStack = maxStack;
    }
}