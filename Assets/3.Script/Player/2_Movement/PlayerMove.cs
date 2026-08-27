using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMove : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnFacingRightChanged))]
    private bool facingRight;

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 5f;

    [SerializeField]
    private float jumpForce = 8f;

    [SerializeField]
    private float maxFallSpeed = 20f;

    [Header("Knockback")]
    [SerializeField]
    private float knockbackForceX = 4f;

    [SerializeField]
    private float knockbackForceY = 4f;

    [SerializeField]
    private float knockbackRecoverySpeed = 12f;

    [Header("Ground Check")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField]
    private float groundCheckDistance = 0.1f;

    [SerializeField]
    private LayerMask groundLayer;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D playerCollider;

    private RaycastHit2D groundHit;
    private Collider2D ignoredPlatform;

    private float moveInput;
    private float knockbackVelocityX;

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
            if (!movementEnabled ||
                ignoredPlatform != null ||
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
        facingRight
            ? Vector2.right
            : Vector2.left;

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody2D>();

        spriteRenderer =
            GetComponent<SpriteRenderer>();

        playerCollider =
            GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        RestoreIgnoredPlatformCollision();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        rb.simulated = true;

        facingRight =
            spriteRenderer.flipX;

        ApplyFacingDirection(
            facingRight
        );
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplyFacingDirection(
            facingRight
        );

        /*
         * 순수 클라이언트는 물리를 계산하지 않고
         * 서버의 물리 결과만 표시합니다.
         */
        if (!isServer)
        {
            rb.simulated = false;
        }
    }

    private void FixedUpdate()
    {
        if (!isServer)
            return;

        CheckGround();
        ApplyMovement();
        ApplyJump();
        RecoverKnockback();
        ClampFallSpeed();
    }

    /// <summary>
    /// 서버에서 플레이어의 좌우 이동 입력을 설정합니다.
    /// </summary>
    [Server]
    public void SetMoveInput(
        float input)
    {
        moveInput =
            Mathf.Clamp(
                input,
                -1f,
                1f
            );

        if (!movementEnabled)
            return;

        UpdateDirection();
    }

    /// <summary>
    /// 점프 가능한 상태라면 점프를 예약합니다.
    /// 실제 물리 처리는 FixedUpdate에서 수행합니다.
    /// </summary>
    [Server]
    public bool RequestJump()
    {
        if (!CanJump)
            return false;

        jumpRequested = true;

        return true;
    }

    /// <summary>
    /// 현재 밟고 있는 단방향 발판을
    /// 아래로 통과합니다.
    /// </summary>
    [Server]
    public bool RequestDropDown()
    {
        if (!CanDropDown)
            return false;

        StartCoroutine(
            DropThroughPlatform(
                groundHit.collider
            )
        );

        return true;
    }

    /// <summary>
    /// 외부 기능에서 플레이어의
    /// 이동 가능 여부를 설정합니다.
    /// </summary>
    [Server]
    public void SetMovementEnabled(
        bool enabled,
        bool stopHorizontalMovement = false)
    {
        movementEnabled = enabled;

        if (enabled)
        {
            UpdateDirection();
            return;
        }

        jumpRequested = false;

        if (stopHorizontalMovement)
        {
            StopHorizontalMovement();
        }
    }

    /// <summary>
    /// 공격자의 반대 방향과 위쪽으로
    /// 피격 넉백을 적용합니다.
    /// </summary>
    [Server]
    public void ApplyKnockback(
        Vector2 attackerPosition)
    {
        float direction =
            transform.position.x >=
            attackerPosition.x
                ? 1f
                : -1f;

        jumpRequested = false;
        IsGrounded = false;

        knockbackVelocityX =
            direction * knockbackForceX;

        rb.linearVelocity =
            new Vector2(
                moveInput * moveSpeed +
                knockbackVelocityX,
                knockbackForceY
            );
    }

    private void ApplyMovement()
    {
        if (!movementEnabled)
            return;

        /*
         * 기존 이동 속도에 남아 있는
         * 넉백 속도를 더합니다.
         */
        rb.linearVelocity =
            new Vector2(
                moveInput * moveSpeed +
                knockbackVelocityX,
                rb.linearVelocity.y
            );
    }

    private void ApplyJump()
    {
        if (!jumpRequested)
            return;

        rb.linearVelocity =
            new Vector2(
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

    /// <summary>
    /// 수평 넉백 속도를 서서히 0으로 복구합니다.
    /// </summary>
    private void RecoverKnockback()
    {
        knockbackVelocityX =
            Mathf.MoveTowards(
                knockbackVelocityX,
                0f,
                knockbackRecoverySpeed *
                Time.fixedDeltaTime
            );
    }

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y >=
            -maxFallSpeed)
        {
            return;
        }

        rb.linearVelocity =
            new Vector2(
                rb.linearVelocity.x,
                -maxFallSpeed
            );
    }

    private void StopHorizontalMovement()
    {
        knockbackVelocityX = 0f;

        rb.linearVelocity =
            new Vector2(
                0f,
                rb.linearVelocity.y
            );
    }

    private void UpdateDirection()
    {
        if (Mathf.Approximately(
                moveInput,
                0f))
        {
            return;
        }

        bool newFacingRight =
            moveInput > 0f;

        if (facingRight ==
            newFacingRight)
        {
            return;
        }

        facingRight =
            newFacingRight;

        ApplyFacingDirection(
            facingRight
        );
    }

    private void ApplyFacingDirection(
        bool isFacingRight)
    {
        spriteRenderer.flipX =
            isFacingRight;
    }

    private void OnFacingRightChanged(
        bool oldValue,
        bool newValue)
    {
        ApplyFacingDirection(
            newValue
        );
    }

    private void CheckGround()
    {
        PhysicsScene2D physicsScene =
            gameObject.scene
                .GetPhysicsScene2D();

        groundHit =
            physicsScene.Raycast(
                groundCheck.position,
                Vector2.down,
                groundCheckDistance,
                groundLayer
            );

        IsGrounded =
            ignoredPlatform == null &&
            groundHit.collider != null;
    }

    private IEnumerator DropThroughPlatform(
        Collider2D platform)
    {
        ignoredPlatform = platform;
        IsGrounded = false;
        jumpRequested = false;

        Physics2D.IgnoreCollision(
            playerCollider,
            platform,
            true
        );

        if (rb.linearVelocity.y > -1f)
        {
            rb.linearVelocity =
                new Vector2(
                    rb.linearVelocity.x,
                    -1f
                );
        }

        while (platform != null &&
               playerCollider.bounds.max.y >=
               platform.bounds.min.y)
        {
            yield return
                new WaitForFixedUpdate();
        }

        RestoreIgnoredPlatformCollision();
    }

    private void RestoreIgnoredPlatformCollision()
    {
        if (ignoredPlatform == null ||
            playerCollider == null)
        {
            return;
        }

        Physics2D.IgnoreCollision(
            playerCollider,
            ignoredPlatform,
            false
        );

        ignoredPlatform = null;
    }

    /// <summary>
    /// 서버에서 플레이어를 지정한 위치로 이동시키고
    /// 기존 물리 상태를 초기화합니다.
    /// </summary>
    [Server]
    public void Teleport(
        Vector2 position)
    {
        RestoreIgnoredPlatformCollision();

        moveInput = 0f;
        knockbackVelocityX = 0f;

        jumpRequested = false;
        IsGrounded = false;

        rb.linearVelocity =
            Vector2.zero;

        rb.position =
            position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawLine(
            groundCheck.position,
            groundCheck.position +
            Vector3.down *
            groundCheckDistance
        );
    }
#endif
}