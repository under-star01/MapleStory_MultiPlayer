public readonly struct QuickSlotBinding
{
    public QuickSlotBindingType Type { get; }
    public QuickSlotBindingSource Source { get; }

    public SkillId SkillId { get; }
    public BasicActionId BasicActionId { get; }
    public ConsumableId ConsumableId { get; }

    public bool IsEmpty =>
        Type == QuickSlotBindingType.None;

    private QuickSlotBinding(
        QuickSlotBindingType type,
        QuickSlotBindingSource source,
        SkillId skillId,
        BasicActionId basicActionId,
        ConsumableId consumableId)
    {
        Type = type;
        Source = source;
        SkillId = skillId;
        BasicActionId = basicActionId;
        ConsumableId = consumableId;
    }

    public static QuickSlotBinding Empty()
    {
        return new QuickSlotBinding(
            QuickSlotBindingType.None,
            QuickSlotBindingSource.None,
            SkillId.None,
            BasicActionId.None,
            ConsumableId.None
        );
    }

    public static QuickSlotBinding FromSkill(
        SkillId skillId,
        QuickSlotBindingSource source)
    {
        if (skillId == SkillId.None ||
            (source != QuickSlotBindingSource.SkillUI &&
             source != QuickSlotBindingSource.DefaultActionPalette))
        {
            return Empty();
        }

        return new QuickSlotBinding(
            QuickSlotBindingType.Skill,
            source,
            skillId,
            BasicActionId.None,
            ConsumableId.None
        );
    }

    public static QuickSlotBinding FromBasicAction(
        BasicActionId actionId)
    {
        if (actionId == BasicActionId.None)
            return Empty();

        return new QuickSlotBinding(
            QuickSlotBindingType.BasicAction,
            QuickSlotBindingSource.DefaultActionPalette,
            SkillId.None,
            actionId,
            ConsumableId.None
        );
    }

    public static QuickSlotBinding FromConsumable(
        ConsumableId consumableId)
    {
        if (consumableId == ConsumableId.None)
            return Empty();

        return new QuickSlotBinding(
            QuickSlotBindingType.Consumable,
            QuickSlotBindingSource.Inventory,
            SkillId.None,
            BasicActionId.None,
            consumableId
        );
    }
}