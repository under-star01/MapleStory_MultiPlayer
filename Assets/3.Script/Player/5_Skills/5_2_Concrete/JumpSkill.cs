using UnityEngine;

public class JumpSkill : PlayerSkillBase
{
    private const float DownInputThreshold = -0.5f;
    private const float UpInputThreshold = 0.5f;
    private const float HorizontalInputThreshold = 0.1f;

    public override bool CanExecute(
        SkillContext context)
    {
        if (context == null)
            return false;

        if (context.Move.IsGrounded)
        {
            if (IsDownInput(context))
                return context.Move.CanDropDown;

            return context.Move.CanJump;
        }

        return context.Move.CanAirJump;
    }

    public override void Execute(
        SkillContext context)
    {
        if (context.Move.IsGrounded)
        {
            ExecuteGroundJump(context);
            return;
        }

        ExecuteAirJump(context);
    }

    private void ExecuteGroundJump(
        SkillContext context)
    {
        if (IsDownInput(context))
        {
            context.Move.RequestDropDown();
            return;
        }

        context.Move.RequestJump();
    }

    private void ExecuteAirJump(
        SkillContext context)
    {
        Vector2 input =
            context.InputDirection;

        /*
         * 위쪽 입력을 가장 먼저 확인합니다.
         * 위 + 좌우를 함께 눌러도 윗점프로 처리됩니다.
         */
        if (input.y > UpInputThreshold)
        {
            context.Move.RequestUpJump();
            return;
        }

        float direction;

        if (Mathf.Abs(input.x) >
            HorizontalInputThreshold)
        {
            direction =
                Mathf.Sign(input.x);
        }
        else
        {
            /*
             * 좌우 입력이 없다면
             * 현재 바라보는 방향으로 더블 점프합니다.
             */
            direction =
                context.FacingDirection.x;
        }

        context.Move.RequestDoubleJump(
            direction
        );
    }

    private bool IsDownInput(
        SkillContext context)
    {
        return context.InputDirection.y <
            DownInputThreshold;
    }
}