using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
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

    public override void OnStartServer()
    {
        base.OnStartServer();

        rb.simulated = true;

        // Prefab에 설정된 초기 방향을 서버 상태로 사용합니다.
        facingRight = spriteRenderer.flipX;
        ApplyFacingDirection(facingRight);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplyFacingDirection(facingRight);

        // 순수 클라이언트는 서버의 물리 결과만 전달받습니다.
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
        ClampFallSpeed();
    }

    /// <summary>
    /// 서버에서 플레이어의 좌우 이동 입력을 설정합니다.
    /// </summary>
    [Server]
    public void SetMoveInput(float input)
    {
        moveInput =
            Mathf.Clamp(input, -1f, 1f);

        // 이동이 잠긴 동안에는 방향을 바꾸지 않습니다.
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
    /// 현재 밟고 있는 단방향 발판을 아래로 통과합니다.
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
    /// 외부 기능에서 플레이어의 이동 가능 여부를 설정합니다.
    /// </summary>
    [Server]
    public void SetMovementEnabled(
        bool enabled,
        bool stopHorizontalMovement = false)
    {
        movementEnabled = enabled;

        if (enabled)
        {
            // 공격이 끝난 순간, 공격 중 마지막으로 입력한 방향을 적용합니다.
            UpdateDirection();
            return;
        }

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
        if (Mathf.Approximately(moveInput, 0f))
            return;

        bool newFacingRight =
            moveInput > 0f;

        if (facingRight == newFacingRight)
            return;

        facingRight = newFacingRight;

        // Dedicated Server에서는 화면이 없지만,
        // Host 화면에는 즉시 반영되도록 직접 적용합니다.
        ApplyFacingDirection(facingRight);
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
        ApplyFacingDirection(newValue);
    }

    private void CheckGround()
    {
        PhysicsScene2D physicsScene =
            gameObject.scene.GetPhysicsScene2D();

        groundHit = physicsScene.Raycast(
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
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                -1f
            );
        }

        while (platform != null &&
               playerCollider.bounds.max.y >=
               platform.bounds.min.y)
        {
            yield return new WaitForFixedUpdate();
        }

        RestoreIgnoredPlatformCollision();
    }

    /// <summary>
    /// 서버에서 플레이어를 지정한 위치로 이동시키고
    /// 기존 물리 상태를 초기화합니다.
    /// </summary>
    [Server]
    public void Teleport(Vector2 position)
    {
        RestoreIgnoredPlatformCollision();

        moveInput = 0f;
        jumpRequested = false;
        IsGrounded = false;

        rb.linearVelocity = Vector2.zero;
        rb.position = position;
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