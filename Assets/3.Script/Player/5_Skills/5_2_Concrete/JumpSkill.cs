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
        }
        else
        {
            context.Move.RequestJump();
        }

        context.Anim.PlaySkillSound(
            SkillSoundId.Jump
        );
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
            ExecuteUpJump(context);
            return;
        }

        ExecuteDoubleJump(
            context,
            GetDoubleJumpDirection(context)
        );
    }

    private void ExecuteUpJump(
        SkillContext context)
    {
        if (!context.Move.RequestUpJump())
            return;

        context.Effect.PlayUpJumpEffect();

        context.Anim.PlaySkillSound(
            SkillSoundId.UpJump
        );
    }

    private void ExecuteDoubleJump(
        SkillContext context,
        float direction)
    {
        if (!context.Move.RequestDoubleJump(
                direction))
        {
            return;
        }

        context.Effect.PlayDoubleJumpEffect(
            direction
        );

        context.Anim.PlaySkillSound(
            SkillSoundId.DoubleJump
        );
    }

    private float GetDoubleJumpDirection(
        SkillContext context)
    {
        float horizontalInput =
            context.InputDirection.x;

        if (Mathf.Abs(horizontalInput) >
            HorizontalInputThreshold)
        {
            return Mathf.Sign(
                horizontalInput
            );
        }

        /*
         * 좌우 입력이 없다면
         * 현재 바라보는 방향으로 더블 점프합니다.
         */
        return context.FacingDirection.x;
    }

    private bool IsDownInput(
        SkillContext context)
    {
        return context.InputDirection.y <
               DownInputThreshold;
    }
}