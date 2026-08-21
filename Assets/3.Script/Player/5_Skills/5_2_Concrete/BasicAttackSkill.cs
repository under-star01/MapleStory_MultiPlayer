using UnityEngine;

public class BasicAttackSkill : PlayerSkillBase
{
    private SkillContext activeContext;

    private bool isExecuting;
    private bool movementLocked;

    public override bool CanExecute(SkillContext context)
    {
        if (context == null)
            return false;

        // 공격 애니메이션이 끝나기 전에는 재실행할 수 없습니다.
        if (isExecuting)
            return false;

        return true;
    }

    public override void Execute(SkillContext context)
    {
        activeContext = context;
        isExecuting = true;

        bool isGrounded = context.Move.IsGrounded;

        /*
         * 지상 공격:
         * 이동 입력을 막고 현재 수평 속도도 정지합니다.
         *
         * 공중 공격:
         * 이동 입력만 막고 기존 수평 관성은 유지합니다.
         */
        context.Move.SetMovementEnabled(
            enabled: false,
            stopHorizontalMovement: isGrounded
        );

        movementLocked = true;

        context.Anim.AttackAnimationEnded += EndAttack;
        context.Anim.PlayAttack();
    }

    private void EndAttack()
    {
        if (!isExecuting || activeContext == null)
            return;

        activeContext.Anim.AttackAnimationEnded -= EndAttack;

        if (movementLocked)
        {
            activeContext.Move.SetMovementEnabled(true);
            movementLocked = false;
        }

        isExecuting = false;
        activeContext = null;
    }

    private void OnDisable()
    {
        if (activeContext != null)
        {
            activeContext.Anim.AttackAnimationEnded -= EndAttack;

            if (movementLocked)
            {
                activeContext.Move.SetMovementEnabled(true);
            }
        }

        movementLocked = false;
        isExecuting = false;
        activeContext = null;
    }
}