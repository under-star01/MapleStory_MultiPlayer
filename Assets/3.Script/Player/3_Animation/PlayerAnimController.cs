using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerEffectController))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerAnimController : NetworkBehaviour
{
    private enum AttackAnimationType
    {
        BasicAttack,
        AttackSkill1
    }

    private static readonly int IsMovingHash =
        Animator.StringToHash("isMoving");

    private static readonly int IsGroundHash =
        Animator.StringToHash("isGround");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int DeadHash =
        Animator.StringToHash("Dead");

    [Header("Attack Animation")]
    [SerializeField]
    private AnimationClip attackPlaceholderClip;

    [SerializeField]
    private AnimationClip basicAttackClip;

    [SerializeField]
    private AnimationClip attackSkill1Clip;

    private Animator animator;
    private NetworkAnimator networkAnimator;

    private PlayerMove playerMove;
    private PlayerHealth playerHealth;
    private PlayerEffectController playerEffect;

    private AnimatorOverrideController overrideController;

    public event Action AttackHitFrame;
    public event Action AttackAnimationEnded;

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

        playerEffect =
            GetComponent<PlayerEffectController>();

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

    private void Update()
    {
        if (!isServer)
            return;

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
        PlayAttackAnimation(
            AttackAnimationType.BasicAttack
        );
    }

    [Server]
    public void PlayAttackSkill1()
    {
        PlayAttackAnimation(
            AttackAnimationType.AttackSkill1
        );
    }

    [Server]
    private void PlayAttackAnimation(
        AttackAnimationType animationType)
    {
        if (playerHealth.IsDead)
            return;

        /*
         * 공격 시작 순간의 방향을
         * 이펙트 컨트롤러에 저장합니다.
         */
        playerEffect
            .PrepareAttackEffectDirection();

        SetAttackClip(
            animationType
        );

        RpcSetAttackClip(
            animationType
        );

        networkAnimator.SetTrigger(
            AttackHash
        );
    }

    [ClientRpc]
    private void RpcSetAttackClip(
        AttackAnimationType animationType)
    {
        /*
         * 호스트는 서버에서 이미
         * 클립을 변경했습니다.
         */
        if (isServer)
            return;

        SetAttackClip(
            animationType
        );
    }

    private void SetAttackClip(
        AttackAnimationType animationType)
    {
        AnimationClip animationClip =
            animationType ==
            AttackAnimationType.BasicAttack
                ? basicAttackClip
                : attackSkill1Clip;

        if (animationClip == null ||
            attackPlaceholderClip == null)
        {
            return;
        }

        overrideController[
            attackPlaceholderClip
        ] = animationClip;
    }

    [Server]
    private void PlayDead()
    {
        networkAnimator.ResetTrigger(
            AttackHash
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

    /*
     * 일반 공격과 AttackSkill1이
     * 공통으로 사용하는 타격 이벤트입니다.
     */
    public void OnAttackHitFrame()
    {
        if (!isServer)
            return;

        AttackHitFrame?.Invoke();
    }

    /*
     * 일반 공격과 AttackSkill1이
     * 공통으로 사용하는 종료 이벤트입니다.
     */
    public void OnAttackAnimationEnded()
    {
        if (!isServer)
            return;

        AttackAnimationEnded?.Invoke();
    }
}