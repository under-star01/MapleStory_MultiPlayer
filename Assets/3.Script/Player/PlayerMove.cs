using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]

public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 8f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float moveInput;
    private bool jumpInput;
    private bool isGrounded;
    private bool isAttacking;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        // 바닥 확인
        CheckGround();

        // 입력 적용
        float rawMoveInput = Input.GetAxisRaw("Horizontal");

        if (!isAttacking)
        {
            moveInput = rawMoveInput;

            UpdateDirection();

            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
                jumpInput = true;

            if (Input.GetKeyDown(KeyCode.LeftControl))
                StartAttack();
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
        // 기본 이동 상태 (공격x)
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
        if (!jumpInput) return;

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

    private void CheckGround()
    {
        isGrounded = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );
    }

    private void UpdateDirection()
    {
        if (moveInput < 0f)
            spriteRenderer.flipX = false;
        else if (moveInput > 0f)
            spriteRenderer.flipX = true;
    }
}