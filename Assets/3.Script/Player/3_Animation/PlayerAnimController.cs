using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NetworkAnimator))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerAnimController : NetworkBehaviour
{
    private enum ActionAnimationType
    {
        BasicAttack,
        AttackSkill1,
        TeleportSkill
    }

    private static readonly int IsMovingHash =
        Animator.StringToHash("isMoving");

    private static readonly int IsGroundHash =
        Animator.StringToHash("isGround");

    private static readonly int ActionHash =
        Animator.StringToHash("Action");

    private static readonly int DeadHash =
        Animator.StringToHash("Dead");

    [Header("Action Animation")]
    [SerializeField]
    private AnimationClip actionPlaceholderClip;

    [SerializeField]
    private AnimationClip basicAttackClip;

    [SerializeField]
    private AnimationClip attackSkill1Clip;

    [SerializeField]
    private AnimationClip teleportSkillClip;

    private Animator animator;
    private NetworkAnimator networkAnimator;

    private PlayerMove playerMove;
    private PlayerHealth playerHealth;

    private AnimatorOverrideController overrideController;

    /*
     * 공격 판정, 실제 이동 등
     * 스킬의 핵심 기능을 실행할 타이밍입니다.
     */
    public event Action ActionExecuteFrame;

    /*
     * 스킬 이펙트를 실행할 타이밍입니다.
     */
    public event Action ActionEffectFrame;

    /*
     * Animator가 Action State를
     * 완전히 빠져나간 시점입니다.
     */
    public event Action ActionEnded;

    private void Awake()
    {
        animator =
            GetComponent<Animator>();

        networkAnimator =
            GetComponent<NetworkAnimator>();

        playerMove =
            GetComponent<PlayerMove>();

        playerHealth =
            GetComponent<PlayerHealth>();

        overrideController =
            new AnimatorOverrideController(
                animator.runtimeAnimatorController
            );

        animator.runtimeAnimatorController =
            overrideController;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        playerHealth.Died +=
            PlayDead;

        playerHealth.Revived +=
            PlayRevive;
    }

    public override void OnStopServer()
    {
        playerHealth.Died -=
            PlayDead;

        playerHealth.Revived -=
            PlayRevive;

        base.OnStopServer();
    }

    [ServerCallback]
    private void Update()
    {
        animator.SetBool(
            IsMovingHash,
            playerMove.IsMoving
        );

        animator.SetBool(
            IsGroundHash,
            playerMove.IsGrounded
        );
    }

    [Server]
    public void PlayAttack()
    {
        PlayAction(
            ActionAnimationType.BasicAttack
        );
    }

    [Server]
    public void PlayAttackSkill1()
    {
        PlayAction(
            ActionAnimationType.AttackSkill1
        );
    }

    [Server]
    public void PlayTeleportSkill()
    {
        PlayAction(
            ActionAnimationType.TeleportSkill
        );
    }

    [Server]
    private void PlayAction(
        ActionAnimationType animationType)
    {
        if (playerHealth.IsDead)
            return;

        SetActionClip(
            animationType
        );

        RpcSetActionClip(
            animationType
        );

        networkAnimator.SetTrigger(
            ActionHash
        );
    }

    [ClientRpc]
    private void RpcSetActionClip(
        ActionAnimationType animationType)
    {
        /*
         * 호스트는 서버에서 이미
         * 클립을 교체했습니다.
         */
        if (!isServer)
        {
            SetActionClip(
                animationType
            );
        }

        PlayActionSound(
            animationType
        );
    }

    private void PlayActionSound(
        ActionAnimationType animationType)
    {
        SkillSoundId soundId =
            SkillSoundId.None;

        switch (animationType)
        {
            case ActionAnimationType.BasicAttack:
                soundId =
                    SkillSoundId.BasicAttack;
                break;

            case ActionAnimationType.AttackSkill1:
                soundId =
                    SkillSoundId.SkillAttack1;
                break;

            case ActionAnimationType.TeleportSkill:
                soundId =
                    SkillSoundId.DashSkill;
                break;
        }

        if (soundId == SkillSoundId.None)
            return;

        AudioManager.Instance?.PlaySkill(
            soundId,
            isOwned
        );
    }

    private void SetActionClip(
        ActionAnimationType animationType)
    {
        AnimationClip actionClip =
            animationType switch
            {
                ActionAnimationType.BasicAttack
                    => basicAttackClip,

                ActionAnimationType.AttackSkill1
                    => attackSkill1Clip,

                ActionAnimationType.TeleportSkill
                    => teleportSkillClip,

                _ => null
            };

        if (actionPlaceholderClip == null ||
            actionClip == null)
        {
            return;
        }

        overrideController[
            actionPlaceholderClip
        ] = actionClip;
    }

    /*
     * Animation Event에서 호출됩니다.
     *
     * 공격 스킬에서는 공격 판정,
     * 텔레포트에서는 실제 위치 이동을
     * 담당합니다.
     */
    public void OnActionExecuteFrame()
    {
        if (!isServer)
            return;

        ActionExecuteFrame?.Invoke();
    }

    /*
     * Animation Event에서 호출됩니다.
     *
     * AttackSkill1, TeleportSkill 등의
     * 이펙트 타이밍을 담당합니다.
     */
    public void OnActionEffectFrame()
    {
        if (!isServer)
            return;

        ActionEffectFrame?.Invoke();
    }

    /*
     * ActionStateBehaviour의
     * OnStateExit에서 호출됩니다.
     */
    public void OnActionStateExited()
    {
        if (!isServer)
            return;

        ActionEnded?.Invoke();
    }

    [Server]
    private void PlayDead()
    {
        networkAnimator.ResetTrigger(
            ActionHash
        );

        networkAnimator.SetTrigger(
            DeadHash
        );
    }

    [Server]
    private void PlayRevive()
    {
        animator.ResetTrigger(
            DeadHash
        );

        animator.Play(
            "Idle",
            0,
            0f
        );
    }

    [Server]
    public void PlaySkillSound(
    SkillSoundId soundId)
    {
        RpcPlaySkillSound(
            soundId
        );
    }

    [ClientRpc]
    private void RpcPlaySkillSound(
        SkillSoundId soundId)
    {
        AudioManager.Instance?.PlaySkill(
            soundId,
            isOwned
        );
    }
}