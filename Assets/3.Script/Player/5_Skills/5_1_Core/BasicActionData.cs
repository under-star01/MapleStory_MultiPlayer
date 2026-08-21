using UnityEngine;

public class BasicActionData
{
    public BasicActionId ActionId { get; }
    public Sprite Icon { get; }
    public IQuickSlotCommand Command { get; }

    public BasicActionData(
        BasicActionId actionId,
        Sprite icon,
        IQuickSlotCommand command)
    {
        ActionId = actionId;
        Icon = icon;
        Command = command;
    }
}