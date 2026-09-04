public struct PlayerQuickSlotLoadData
{
    public int QuickKey;
    public int BindingType;
    public int TargetId;

    public PlayerQuickSlotLoadData(
        PlayerQuickSlotRecord record)
    {
        QuickKey = record.QuickKey;
        BindingType = record.BindingType;
        TargetId = record.TargetId;
    }
}