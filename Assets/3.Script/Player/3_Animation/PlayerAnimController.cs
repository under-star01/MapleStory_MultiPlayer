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

    public event Action AttackHitFrame;
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

    /// <summary>
    /// 공격 애니메이션의 실제 타격 프레임에서
    /// Animation Event가 호출합니다.
    /// </summary>
    public void OnAttackHitFrame()
    {
        /*
         * 클라이언트에서도 애니메이션 이벤트가
         * 호출될 수 있으므로 서버에서만 전달합니다.
         */
        if (!isServer)
            return;

        AttackHitFrame?.Invoke();
    }

    public void OnAttackAnimationEnded()
    {
        AttackAnimationEnded?.Invoke();
    }
}