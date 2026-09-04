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

    public event Action ActionExecuteFrame;
    public event Action ActionEffectFrame;
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

    // 액션 종류에 맞는 애니메이션 실행
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

    // 클라이언트의 액션 클립 및 효과음 동기화
    [ClientRpc]
    private void RpcSetActionClip(
        ActionAnimationType animationType)
    {
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

    // Animation Event에서 액션 기능 실행
    public void OnActionExecuteFrame()
    {
        if (!isServer)
            return;

        ActionExecuteFrame?.Invoke();
    }

    // Animation Event에서 액션 이펙트 실행
    public void OnActionEffectFrame()
    {
        if (!isServer)
            return;

        ActionEffectFrame?.Invoke();
    }

    // Action State 종료 처리
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