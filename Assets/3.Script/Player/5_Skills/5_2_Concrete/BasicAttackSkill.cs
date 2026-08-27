using Mirror;
using UnityEngine;

public class BasicAttackSkill : AttackSkillBase
{
    [Header("Attack")]
    [SerializeField]
    private int baseDamage = 10;

    [SerializeField]
    private int hitCount = 1;

    [SerializeField]
    private int maxTargets = 1;

    [SerializeField]
    private Vector2 attackSize =
        new Vector2(0.8f, 0.6f);

    [SerializeField]
    private Vector2 attackOffset =
        new Vector2(0.5f, 0.05f);

    private SkillContext activeContext;

    private bool isExecuting;
    private bool movementLocked;
    private bool hasAppliedHit;

    public override bool CanExecute(
        SkillContext context)
    {
        if (context == null)
            return false;

        if (!NetworkServer.active)
            return false;

        if (isExecuting)
            return false;

        if (context.SkillController
            .IsAttackExecuting)
        {
            return false;
        }

        return true;
    }

    public override void Execute(
        SkillContext context)
    {
        if (!context.SkillController
            .TryBeginAttack())
        {
            return;
        }

        activeContext = context;
        isExecuting = true;
        hasAppliedHit = false;

        bool isGrounded =
            context.Move.IsGrounded;

        /*
         * 지상 공격에서는 수평 이동을 정지하고,
         * 공중 공격에서는 기존 수평 관성을 유지합니다.
         */
        context.Move.SetMovementEnabled(
            enabled: false,
            stopHorizontalMovement: isGrounded
        );

        movementLocked = true;

        context.Anim.AttackHitFrame +=
            ApplyAttackHit;

        context.Anim.AttackAnimationEnded +=
            EndAttack;

        context.Anim.PlayAttack();
    }

    [Server]
    private void ApplyAttackHit()
    {
        if (!isExecuting ||
            activeContext == null ||
            hasAppliedHit)
        {
            return;
        }

        /*
         * 애니메이션 이벤트가 중복 호출되더라도
         * 한 번의 일반 공격에서는 한 번만 판정합니다.
         */
        hasAppliedHit = true;

        ApplyAreaDamage(
            activeContext,
            baseDamage,
            hitCount,
            maxTargets,
            attackSize,
            attackOffset
        );
    }

    private void EndAttack()
    {
        if (!isExecuting ||
            activeContext == null)
        {
            return;
        }

        UnsubscribeAnimationEvents();

        if (movementLocked)
        {
            activeContext.Move
                .SetMovementEnabled(true);

            movementLocked = false;
        }

        activeContext.SkillController
            .EndAttack();

        isExecuting = false;
        hasAppliedHit = false;
        activeContext = null;
    }

    private void UnsubscribeAnimationEvents()
    {
        if (activeContext == null)
            return;

        activeContext.Anim.AttackHitFrame -=
            ApplyAttackHit;

        activeContext.Anim.AttackAnimationEnded -=
            EndAttack;
    }

    private void OnDisable()
    {
        if (activeContext != null)
        {
            UnsubscribeAnimationEvents();

            if (movementLocked)
            {
                activeContext.Move
                    .SetMovementEnabled(true);
            }

            activeContext.SkillController
                .EndAttack();
        }

        movementLocked = false;
        isExecuting = false;
        hasAppliedHit = false;
        activeContext = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawAttackRangeGizmo(
            attackSize,
            attackOffset
        );
    }
#endif
}