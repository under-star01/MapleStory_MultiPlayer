using UnityEngine;

public class QuickSlot
{
    public QuickSlotBinding Binding { get; private set; }
        = QuickSlotBinding.Empty();

    public BasicActionData BasicActionData
    {
        get;
        private set;
    }

    public bool IsEmpty =>
        Binding.IsEmpty;

    public void BindSkill(
        SkillId skillId,
        QuickSlotBindingSource source)
    {
        QuickSlotBinding binding =
            QuickSlotBinding.FromSkill(
                skillId,
                source
            );

        if (binding.IsEmpty)
            return;

        Binding = binding;
        BasicActionData = null;
    }

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

        BasicActionData =
            actionData;
    }

    public void BindConsumable(
        ConsumableId consumableId)
    {
        QuickSlotBinding binding =
            QuickSlotBinding.FromConsumable(
                consumableId
            );

        if (binding.IsEmpty)
            return;

        Binding = binding;
        BasicActionData = null;
    }

    public bool ExecuteBasicAction(
        Vector2 inputDirection)
    {
        if (Binding.Type !=
                QuickSlotBindingType.BasicAction ||
            BasicActionData?.Command == null)
        {
            return false;
        }

        return BasicActionData.Command.Execute(
            inputDirection
        );
    }

    public void Clear()
    {
        Binding =
            QuickSlotBinding.Empty();

        BasicActionData = null;
    }

    public void SwapWith(
        QuickSlot other)
    {
        if (other == null ||
            ReferenceEquals(this, other))
        {
            return;
        }

        (Binding, other.Binding) =
            (other.Binding, Binding);

        (BasicActionData, other.BasicActionData) =
            (other.BasicActionData, BasicActionData);
    }
}