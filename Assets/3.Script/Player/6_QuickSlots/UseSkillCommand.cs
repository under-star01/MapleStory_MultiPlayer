using System;
using UnityEngine;

public class UseSkillCommand : IQuickSlotCommand
{
    private readonly PlayerSkillController skillController;
    private readonly IPlayerSkill skill;

    public UseSkillCommand(
        PlayerSkillController skillController,
        IPlayerSkill skill)
    {
        this.skillController = skillController
            ? skillController
            : throw new ArgumentNullException(
                nameof(skillController)
            );

        this.skill = skill
            ?? throw new ArgumentNullException(
                nameof(skill)
            );
    }

    public bool Execute(Vector2 inputDirection)
    {
        return skillController.TryExecute(
            skill,
            inputDirection
        );
    }
}