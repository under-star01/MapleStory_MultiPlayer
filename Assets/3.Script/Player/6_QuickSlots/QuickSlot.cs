using UnityEngine;

public class QuickSlot
{
    private IQuickSlotCommand command;

    public QuickSlotBinding Binding { get; private set; }
        = QuickSlotBinding.Empty();

    /*
     * 기본 기능이 등록된 경우에만 값을 가집니다.
     * 스킬, 소비 아이템 또는 빈 슬롯이면 null입니다.
     */
    public BasicActionData BasicActionData
    {
        get;
        private set;
    }

    public bool IsEmpty => command == null;

    public void BindSkill(
        SkillId skillId,
        IQuickSlotCommand command)
    {
        if (skillId == SkillId.None ||
            command == null)
        {
            return;
        }

        Binding =
            QuickSlotBinding.FromSkill(skillId);

        BasicActionData = null;
        this.command = command;
    }

    /// <summary>
    /// 기본 기능 데이터 전체를 슬롯에 연결합니다.
    /// </summary>
    public void BindBasicAction(
        BasicActionData actionData)
    {
        if (actionData == null ||
            actionData.ActionId == BasicActionId.None ||
            actionData.Command == null)
        {
            return;
        }

        Binding =
            QuickSlotBinding.FromBasicAction(
                actionData.ActionId
            );

        BasicActionData = actionData;
        command = actionData.Command;
    }

    /// <summary>
    /// 소비 아이템 데이터 전체를 슬롯에 연결합니다.
    /// </summary>
    public void BindConsumable(
        ConsumableId consumableId,
        IQuickSlotCommand command)
    {
        if (consumableId == ConsumableId.None ||
            command == null)
        {
            return;
        }

        Binding =
            QuickSlotBinding.FromConsumable(
                consumableId
            );

        BasicActionData = null;
        this.command = command;
    }

    public bool Execute(Vector2 inputDirection)
    {
        if (command == null)
            return false;

        return command.Execute(inputDirection);
    }

    public void Clear()
    {
        Binding = QuickSlotBinding.Empty();
        BasicActionData = null;
        command = null;
    }

    /// <summary>
    /// Command, Binding, BasicActionData를 함께 교환합니다.
    /// 대상이 비어 있으면 이동처럼 동작합니다.
    /// </summary>
    public void SwapWith(QuickSlot other)
    {
        if (other == null ||
            ReferenceEquals(this, other))
        {
            return;
        }

        IQuickSlotCommand tempCommand =
            command;

        QuickSlotBinding tempBinding =
            Binding;

        BasicActionData tempActionData =
            BasicActionData;

        command = other.command;
        Binding = other.Binding;
        BasicActionData = other.BasicActionData;

        other.command = tempCommand;
        other.Binding = tempBinding;
        other.BasicActionData = tempActionData;
    }
}