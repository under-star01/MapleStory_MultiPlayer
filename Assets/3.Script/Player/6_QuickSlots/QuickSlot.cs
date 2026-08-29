using UnityEngine;

public class QuickSlot
{
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

    public bool IsEmpty =>
        Binding.IsEmpty;

    public void BindSkill(
        SkillId skillId)
    {
        if (skillId == SkillId.None)
            return;

        Binding =
            QuickSlotBinding.FromSkill(
                skillId
            );

        BasicActionData = null;
    }

    /// <summary>
    /// 기본 기능 데이터 전체를 슬롯에 연결합니다.
    /// </summary>
    public void BindBasicAction(
        BasicActionData actionData)
    {
        if (actionData == null ||
            actionData.ActionId ==
                BasicActionId.None ||
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
        if (consumableId ==
            ConsumableId.None)
        {
            return;
        }

        Binding =
            QuickSlotBinding.FromConsumable(
                consumableId
            );

        BasicActionData = null;
    }

    /// <summary>
    /// 슬롯에 등록된 기본 기능을 실행합니다.
    /// 스킬과 소비 아이템은 서버 실행 경로를 따로 사용합니다.
    /// </summary>
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

    /// <summary>
    /// Binding과 BasicActionData를 함께 교환합니다.
    /// 대상이 비어 있으면 이동처럼 동작합니다.
    /// </summary>
    public void SwapWith(
        QuickSlot other)
    {
        if (other == null ||
            ReferenceEquals(this, other))
        {
            return;
        }

        QuickSlotBinding tempBinding =
            Binding;

        BasicActionData tempActionData =
            BasicActionData;

        Binding =
            other.Binding;

        BasicActionData =
            other.BasicActionData;

        other.Binding =
            tempBinding;

        other.BasicActionData =
            tempActionData;
    }
}