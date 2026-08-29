public readonly struct QuickSlotBinding
{
    public QuickSlotBindingType Type { get; }
    public SkillId SkillId { get; }
    public BasicActionId BasicActionId { get; }
    public ConsumableId ConsumableId { get; }

    public bool IsEmpty =>
        Type == QuickSlotBindingType.None;

    private QuickSlotBinding(
        QuickSlotBindingType type,
        SkillId skillId,
        BasicActionId basicActionId,
        ConsumableId consumableId)
    {
        Type = type;
        SkillId = skillId;
        BasicActionId = basicActionId;
        ConsumableId = consumableId;
    }

    /// <summary>
    /// 빈 바인딩을 생성합니다.
    /// </summary>
    public static QuickSlotBinding Empty()
    {
        return new QuickSlotBinding(
            QuickSlotBindingType.None,
            SkillId.None,
            BasicActionId.None,
            ConsumableId.None
        );
    }

    /// <summary>
    /// 스킬 바인딩을 생성합니다.
    /// </summary>
    public static QuickSlotBinding FromSkill(
        SkillId skillId)
    {
        if (skillId == SkillId.None)
            return Empty();

        return new QuickSlotBinding(
            QuickSlotBindingType.Skill,
            skillId,
            BasicActionId.None,
            ConsumableId.None
        );
    }

    /// <summary>
    /// 기본 기능 바인딩을 생성합니다.
    /// </summary>
    public static QuickSlotBinding FromBasicAction(
        BasicActionId actionId)
    {
        if (actionId == BasicActionId.None)
            return Empty();

        return new QuickSlotBinding(
            QuickSlotBindingType.BasicAction,
            SkillId.None,
            actionId,
            ConsumableId.None
        );
    }

    /// <summary>
    /// 소비 아이템 바인딩을 생성합니다.
    /// </summary>
    public static QuickSlotBinding FromConsumable(
    ConsumableId consumableId)
    {
        if (consumableId == ConsumableId.None)
            return Empty();

        return new QuickSlotBinding(
            QuickSlotBindingType.Consumable,
            SkillId.None,
            BasicActionId.None,
            consumableId
        );
    }
}