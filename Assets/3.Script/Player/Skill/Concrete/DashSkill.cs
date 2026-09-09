using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerEffectController))]
public class DashSkill : PlayerSkillBase
{
    private PlayerEffectController playerEffect;
    private SkillContext activeContext;

    private float direction;

    private bool isExecuting;
    private bool movementLocked;
    private bool hasDashed;
    private bool hasPlayedEffect;

    private void Awake()
    {
        playerEffect =
            GetComponent<PlayerEffectController>();
    }

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

        return context.Move.CanDash;
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
        hasDashed = false;
        hasPlayedEffect = false;

        context.Move.SetMovementEnabled(
            enabled: false,
            stopHorizontalMovement: true
        );

        movementLocked = true;

        context.Anim.ActionEffectFrame +=
            PlayEffect;

        context.Anim.ActionExecuteFrame +=
            Dash;

        context.Anim.ActionEnded +=
            EndAction;

        context.Anim.PlayTeleportSkill();
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

        playerEffect.PlayTeleportEffect(
            direction
        );
    }

    [Server]
    private void Dash()
    {
        if (!isExecuting ||
            activeContext == null ||
            hasDashed)
        {
            return;
        }

        hasDashed = true;

        activeContext.Move.RequestDash(
            direction
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
        hasDashed = false;
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
            Dash;

        activeContext.Anim.ActionEnded -=
            EndAction;
    }

    private void OnDisable()
    {
        FinishAction();
    }
}