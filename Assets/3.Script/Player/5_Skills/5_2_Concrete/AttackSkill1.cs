using Mirror;
using UnityEngine;

public class AttackSkill1 : AttackSkillBase
{
    [Header("Attack")]
    [SerializeField]
    private int baseDamage = 20;

    [SerializeField]
    private int hitCount = 4;

    [SerializeField]
    private int maxTargets = 5;

    [SerializeField]
    private Vector2 attackSize =
        new Vector2(2f, 1f);

    [SerializeField]
    private Vector2 attackOffset =
        new Vector2(0.8f, 0.1f);

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

        context.Move.SetMovementEnabled(
            enabled: false,
            stopHorizontalMovement: isGrounded
        );

        movementLocked = true;

        context.Anim.AttackHitFrame +=
            ApplyAttackHit;

        context.Anim.AttackAnimationEnded +=
            EndAttack;

        context.Anim.PlayAttackSkill1();
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

        activeContext.SkillController.EndAttack();

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

            activeContext.SkillController.
                EndAttack();
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

    private void OnValidate()
    {
        baseDamage =
            Mathf.Max(0, baseDamage);

        hitCount =
            Mathf.Max(1, hitCount);

        maxTargets =
            Mathf.Clamp(
                maxTargets,
                1,
                16
            );

        attackSize.x =
            Mathf.Max(0f, attackSize.x);

        attackSize.y =
            Mathf.Max(0f, attackSize.y);
    }
#endif
}