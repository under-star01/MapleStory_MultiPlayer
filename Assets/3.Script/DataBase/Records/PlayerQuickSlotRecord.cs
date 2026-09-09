public class PlayerQuickSlotRecord
{
    public int QuickKey { get; }
    public int BindingType { get; }
    public int TargetId { get; }

    public PlayerQuickSlotRecord(
        int quickKey,
        int bindingType,
        int targetId)
    {
        QuickKey = quickKey;
        BindingType = bindingType;
        TargetId = targetId;
    }
}