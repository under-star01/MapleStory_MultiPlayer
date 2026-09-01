public class PlayerQuickSlotRecord
{
    public int QuickKey { get; }
    public int BindingType { get; }
    public int BindingSource { get; }
    public int TargetId { get; }

    public PlayerQuickSlotRecord(
        int quickKey,
        int bindingType,
        int bindingSource,
        int targetId)
    {
        QuickKey = quickKey;
        BindingType = bindingType;
        BindingSource = bindingSource;
        TargetId = targetId;
    }
}