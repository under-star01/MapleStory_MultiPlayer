using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
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
    private SpriteRenderer spriteRenderer;
    private Collider2D playerCollider;

    private RaycastHit2D groundHit;
    private Collider2D ignoredPlatform;

    private float moveInput;
    private bool jumpRequested;
    private bool movementEnabled = true;

    public bool IsGrounded { get; private set; }

    public bool IsMoving =>
        movementEnabled &&
        Mathf.Abs(moveInput) > 0.01f;

    public bool CanJump =>
        movementEnabled &&
        IsGrounded;

    public bool CanDropDown
    {
        get
        {
            if (!movementEnabled)
                return false;

            if (ignoredPlatform != null ||
                groundHit.collider == null)
            {
                return false;
            }

            PlatformEffector2D effector =
                groundHit.collider
                    .GetComponentInParent<PlatformEffector2D>();

            return effector != null;
        }
    }

    public Vector2 FacingDirection =>
        spriteRenderer.flipX
            ? Vector2.right
            : Vector2.left;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        CheckGround();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyJump();
        ClampFallSpeed();
    }

    /// <summary>
    /// 외부 입력 시스템에서 좌우 입력값을 전달합니다.
    /// </summary>
    public void SetMoveInput(float input)
    {
        if (!movementEnabled)
        {
            moveInput = 0f;
            return;
        }

        moveInput = Mathf.Clamp(input, -1f, 1f);

        UpdateDirection();
    }

    /// <summary>
    /// 점프 가능한 상태라면 점프를 예약합니다.
    /// 실제 물리 처리는 FixedUpdate에서 수행합니다.
    /// </summary>
    public bool RequestJump()
    {
        if (!movementEnabled || !IsGrounded)
            return false;

        jumpRequested = true;
        return true;
    }

    /// <summary>
    /// 현재 밟고 있는 발판이 단방향 발판이면 아래로 통과합니다.
    /// </summary>
    public bool RequestDropDown()
    {
        if (!CanDropDown)
            return false;

        Collider2D platform = groundHit.collider;

        StartCoroutine(DropThroughPlatform(platform));
        return true;
    }

    /// <summary>
    /// 공격, 피격, 대화 등의 외부 기능이 이동 가능 여부를 설정합니다.
    /// PlayerMove는 이동이 제한된 이유를 알 필요가 없습니다.
    /// </summary>
    public void SetMovementEnabled(
        bool enabled,
        bool stopHorizontalMovement = false)
    {
        movementEnabled = enabled;

        if (enabled)
            return;

        moveInput = 0f;
        jumpRequested = false;

        if (stopHorizontalMovement)
        {
            StopHorizontalMovement();
        }
    }

    private void ApplyMovement()
    {
        if (!movementEnabled)
            return;

        rb.linearVelocity = new Vector2(
            moveInput * moveSpeed,
            rb.linearVelocity.y
        );
    }

    private void ApplyJump()
    {
        if (!jumpRequested)
            return;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            0f
        );

        rb.AddForce(
            Vector2.up * jumpForce,
            ForceMode2D.Impulse
        );

        jumpRequested = false;
        IsGrounded = false;
    }

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y >= -maxFallSpeed)
            return;

        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            -maxFallSpeed
        );
    }

    private void StopHorizontalMovement()
    {
        rb.linearVelocity = new Vector2(
            0f,
            rb.linearVelocity.y
        );
    }

    private void UpdateDirection()
    {
        if (moveInput < 0f)
        {
            spriteRenderer.flipX = false;
        }
        else if (moveInput > 0f)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void CheckGround()
    {
        groundHit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        IsGrounded =
            ignoredPlatform == null &&
            groundHit.collider != null;
    }

    private IEnumerator DropThroughPlatform(Collider2D platform)
    {
        ignoredPlatform = platform;
        IsGrounded = false;
        jumpRequested = false;

        Physics2D.IgnoreCollision(
            playerCollider,
            platform,
            true
        );

        // 곧바로 아래로 내려가기 시작하도록 작은 하강 속도를 적용합니다.
        if (rb.linearVelocity.y > -1f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                -1f
            );
        }

        // 플레이어 전체가 발판 아래로 내려갈 때까지 기다립니다.
        while (platform != null &&
               playerCollider.bounds.max.y >= platform.bounds.min.y)
        {
            yield return new WaitForFixedUpdate();
        }

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawLine(
            groundCheck.position,
            groundCheck.position +
            Vector3.down * groundCheckDistance
        );
    }
#endif
}