public struct PlayerQuickSlotLoadData
{
    public int QuickKey;
    public int BindingType;
    public int BindingSource;
    public int TargetId;

    public PlayerQuickSlotLoadData(
        PlayerQuickSlotRecord record)
    {
        QuickKey = record.QuickKey;
        BindingType = record.BindingType;
        BindingSource = record.BindingSource;
        TargetId = record.TargetId;
    }
}