public class MonsterDropRecord
{
    public int MonsterId
    {
        get;
    }

    public int ItemId
    {
        get;
    }

    public float DropRate
    {
        get;
    }

    public MonsterDropRecord(
        int monsterId,
        int itemId,
        float dropRate)
    {
        MonsterId = monsterId;
        ItemId = itemId;
        DropRate = dropRate;
    }
}