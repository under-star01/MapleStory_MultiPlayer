using System;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMove))]
public class PlayerAnimController : MonoBehaviour
{
    private static readonly int IsMovingHash =
        Animator.StringToHash("isMoving");

    private static readonly int IsGroundHash =
        Animator.StringToHash("isGround");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private Animator animator;
    private PlayerMove playerMove;

    public event Action AttackAnimationEnded;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerMove = GetComponent<PlayerMove>();
    }

    private void Update()
    {
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
        animator.SetTrigger(AttackHash);
    }

    /// <summary>
    /// 공격 애니메이션의 마지막 프레임에서
    /// Animation Event로 호출합니다.
    /// </summary>
    public void OnAttackAnimationEnded()
    {
        AttackAnimationEnded?.Invoke();
    }
}