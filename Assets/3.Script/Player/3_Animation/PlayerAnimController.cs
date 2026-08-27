using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerAnimController : NetworkBehaviour
{
    private static readonly int IsMovingHash =
        Animator.StringToHash("isMoving");

    private static readonly int IsGroundHash =
        Animator.StringToHash("isGround");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int DeadHash =
        Animator.StringToHash("Dead");

    private Animator animator;
    private NetworkAnimator networkAnimator;
    private PlayerMove playerMove;
    private PlayerHealth playerHealth;

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

        UpdateMovementAnimation();
    }

    private void UpdateMovementAnimation()
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

    public void PlayAttack()
    {
        if (!isServer)
            return;

        if (playerHealth.IsDead)
            return;

        networkAnimator.SetTrigger(
            AttackHash
        );
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

    public void OnAttackHitFrame()
    {
        if (!isServer)
            return;

        AttackHitFrame?.Invoke();
    }

    public void OnAttackAnimationEnded()
    {
        AttackAnimationEnded?.Invoke();
    }
}