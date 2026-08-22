using System;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerAnimController : NetworkBehaviour
{
    private static readonly int IsMovingHash =
        Animator.StringToHash("isMoving");

    private static readonly int IsGroundHash =
        Animator.StringToHash("isGround");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private Animator animator;
    private NetworkAnimator networkAnimator;
    private PlayerMove playerMove;

    public event Action AttackAnimationEnded;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        playerMove = GetComponent<PlayerMove>();
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

        networkAnimator.SetTrigger(
            AttackHash
        );
    }

    public void OnAttackAnimationEnded()
    {
        AttackAnimationEnded?.Invoke();
    }
}