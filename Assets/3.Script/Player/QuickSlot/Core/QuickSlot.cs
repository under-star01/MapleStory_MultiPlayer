using UnityEngine;

public class QuickSlot
{
    public QuickSlotBinding Binding { get; private set; }
        = QuickSlotBinding.Empty();

    public bool IsEmpty =>
        Binding.IsEmpty;

    public void BindSkill(
        SkillId skillId)
    {
        QuickSlotBinding newBinding =
            QuickSlotBinding.FromSkill(
                skillId
            );

        if (newBinding.IsEmpty)
            return;

        Binding = newBinding;
    }

    public void BindBasicAction(
        BasicActionId actionId)
    {
        QuickSlotBinding newBinding =
            QuickSlotBinding.FromBasicAction(
                actionId
            );

        if (newBinding.IsEmpty)
            return;

        Binding = newBinding;
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
    }

    public void Clear()
    {
        Binding =
            QuickSlotBinding.Empty();
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
    }
}