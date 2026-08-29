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

    private float direction;

    private bool isExecuting;
    private bool movementLocked;
    private bool hasAppliedHit;
    private bool hasPlayedEffect;

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
            .IsActionExecuting)
        {
            return false;
        }

        return true;
    }

    public override void Execute(
        SkillContext context)
    {
        if (!context.SkillController
            .TryBeginAction())
        {
            return;
        }

        activeContext = context;

        direction =
            context.FacingDirection.x;

        isExecuting = true;
        hasAppliedHit = false;
        hasPlayedEffect = false;

        bool isGrounded =
            context.Move.IsGrounded;

        context.Move.SetMovementEnabled(
            enabled: false,
            stopHorizontalMovement: isGrounded
        );

        movementLocked = true;

        context.Anim.ActionEffectFrame +=
            PlayEffect;

        context.Anim.ActionExecuteFrame +=
            ApplyAttackHit;

        context.Anim.ActionEnded +=
            EndAction;

        context.Anim.PlayAttackSkill1();
    }

    [Server]
    private void PlayEffect()
    {
        if (!isExecuting ||
            activeContext == null ||
            hasPlayedEffect)
        {
            return;
        }

        hasPlayedEffect = true;

        activeContext.Effect
            .PlayAttackSkill1Effect(
                direction
            );
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

    private void EndAction()
    {
        if (!isExecuting)
            return;

        FinishAction();
    }

    private void FinishAction()
    {
        if (activeContext == null)
            return;

        UnsubscribeAnimationEvents();

        if (movementLocked)
        {
            activeContext.Move
                .SetMovementEnabled(true);
        }

        activeContext.SkillController
            .EndAction();

        movementLocked = false;
        isExecuting = false;
        hasAppliedHit = false;
        hasPlayedEffect = false;
        activeContext = null;
    }

    private void UnsubscribeAnimationEvents()
    {
        if (activeContext == null)
            return;

        activeContext.Anim.ActionEffectFrame -=
            PlayEffect;

        activeContext.Anim.ActionExecuteFrame -=
            ApplyAttackHit;

        activeContext.Anim.ActionEnded -=
            EndAction;
    }

    private void OnDisable()
    {
        FinishAction();
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