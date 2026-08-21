using UnityEngine;

public class JumpSkill : PlayerSkillBase
{
    private const float DownInputThreshold = -0.5f;

    public override bool CanExecute(SkillContext context)
    {
        if (context == null)
            return false;

        if (context.InputDirection.y < DownInputThreshold)
        {
            return context.Move.CanDropDown;
        }

        return context.Move.CanJump;
    }

    public override void Execute(SkillContext context)
    {
        if (context.InputDirection.y < DownInputThreshold)
        {
            context.Move.RequestDropDown();
            return;
        }

        context.Move.RequestJump();
    }
}