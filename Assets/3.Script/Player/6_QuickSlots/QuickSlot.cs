using UnityEngine;

public class QuickSlot
{
    private IQuickSlotCommand command;

    /// <summary>
    /// 현재 슬롯에 연결된 스킬의 ID입니다.
    /// 일반 Command이거나 빈 슬롯이면 None입니다.
    /// </summary>
    public SkillId SkillId { get; private set; }
        = SkillId.None;

    public bool IsEmpty => command == null;

    /// <summary>
    /// 스킬 ID와 해당 스킬을 실행할 Command를 함께 연결합니다.
    /// </summary>
    public void BindSkill(
        SkillId skillId,
        IQuickSlotCommand command)
    {
        if (skillId == SkillId.None ||
            command == null)
        {
            return;
        }

        SkillId = skillId;
        this.command = command;
    }

    /// <summary>
    /// 스킬이 아닌 일반 Command를 연결합니다.
    /// </summary>
    public void BindCommand(
        IQuickSlotCommand command)
    {
        if (command == null)
            return;

        SkillId = SkillId.None;
        this.command = command;
    }

    /// <summary>
    /// 현재 슬롯에 연결된 기능을 실행합니다.
    /// </summary>
    public bool Execute(Vector2 inputDirection)
    {
        if (command == null)
            return false;

        return command.Execute(inputDirection);
    }

    /// <summary>
    /// 현재 슬롯의 바인딩을 제거합니다.
    /// </summary>
    public void Clear()
    {
        SkillId = SkillId.None;
        command = null;
    }

    /// <summary>
    /// 다른 슬롯과 전체 바인딩 내용을 교환합니다.
    /// 상대 슬롯이 비어 있으면 이동처럼 동작합니다.
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

        SkillId tempSkillId =
            SkillId;

        command =
            other.command;

        SkillId =
            other.SkillId;

        other.command =
            tempCommand;

        other.SkillId =
            tempSkillId;
    }
}