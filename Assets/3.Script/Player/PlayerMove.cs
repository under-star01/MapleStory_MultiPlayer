using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]

public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D playerCollider;

    private RaycastHit2D groundHit;
    private Collider2D ignoredPlatform;

    private float moveInput;
    private bool jumpInput;
    private bool isGrounded;
    private bool isAttacking;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        // 바닥 확인
        CheckGround();

        // 입력 적용
        float rawMoveInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        if (!isAttacking)
        {
            moveInput = rawMoveInput;
            UpdateDirection();

            // 공격 입력을 우선 처리
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                StartAttack();
            }
            // 공격 입력이 없는 프레임에만 점프 입력 처리
            else if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                if (verticalInput < -0.5f)
                    TryDropDown();
                else
                    jumpInput = true;
            }
        }
        else
        {
            moveInput = 0f;
        }

        // 애니메이션 적용
        animator.SetBool("isMoving", Mathf.Abs(rawMoveInput) > 0.01f);
        animator.SetBool("isGround", isGrounded);
    }

    private void FixedUpdate()
    {
        // 기본 이동 상태
        if (!isAttacking)
        {
            rb.linearVelocity = new Vector2(
                moveInput * moveSpeed,
                rb.linearVelocity.y
            );
        }
        // 지상 공격 : 수평 이동 제한
        else if (isGrounded)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }

        // 점프 상태 : 관성 유지
        if (jumpInput)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                0f
            );

            rb.AddForce(
                Vector2.up * jumpForce,
                ForceMode2D.Impulse
            );

            jumpInput = false;
            isGrounded = false;
        }
        // 최대 낙하 속도 제한
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                -maxFallSpeed
            );
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        jumpInput = false;

        // 지상 공격시 이동 제한
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }

        animator.SetTrigger("Attack");
    }

    // Attack 애니메이션 마지막 프레임 호출 이벤트.
    public void EndAttack()
    {
        isAttacking = false;
    }

    private void UpdateDirection()
    {
        if (moveInput < 0f)
            spriteRenderer.flipX = false;
        else if (moveInput > 0f)
            spriteRenderer.flipX = true;
    }

    private void CheckGround()
    {
        groundHit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        isGrounded =
            ignoredPlatform == null &&
            groundHit.collider != null;
    }

    private void TryDropDown()
    {
        if (ignoredPlatform != null || groundHit.collider == null)
            return;

        Collider2D platform = groundHit.collider;

        PlatformEffector2D effector =
            platform.GetComponentInParent<PlatformEffector2D>();

        // Effector가 없는 일반 바닥에서는 하단 점프 불가
        if (effector == null)
            return;

        StartCoroutine(DropThroughPlatform(platform));
    }

    private IEnumerator DropThroughPlatform(Collider2D platform)
    {
        ignoredPlatform = platform;
        isGrounded = false;
        jumpInput = false;

        // 플레이어와 현재 발판 사이의 충돌만 무시
        Physics2D.IgnoreCollision(
            playerCollider,
            platform,
            true
        );

        // 중력으로 떨어지되, 바로 내려가기 시작하도록 작은 하강 속도 부여
        if (rb.linearVelocity.y > -1f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                -1f
            );
        }

        // 플레이어 전체가 발판 아래로 내려갈 때까지 대기
        while (platform != null &&
               playerCollider.bounds.max.y >= platform.bounds.min.y)
        {
            yield return new WaitForFixedUpdate();
        }

        // 해당 발판과의 충돌 복구
        if (platform != null)
        {
            Physics2D.IgnoreCollision(
                playerCollider,
                platform,
                false
            );
        }
        ignoredPlatform = null;
    }
}