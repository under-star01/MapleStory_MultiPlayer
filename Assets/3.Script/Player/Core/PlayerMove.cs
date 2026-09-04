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
    [SerializeField, Min(0f)]
    private float moveSpeed = 5f;

    [SerializeField, Min(0f)]
    private float jumpForce = 8f;

    [SerializeField, Min(0f)]
    private float maxFallSpeed = 20f;

    [Header("Air Jump")]
    [SerializeField, Min(0f)]
    private float doubleJumpForceX = 8f;

    [SerializeField, Min(0f)]
    private float doubleJumpForceY = 2f;

    [SerializeField, Min(0f)]
    private float upJumpForce = 8f;

    [Header("Knockback")]
    [SerializeField, Min(0f)]
    private float knockbackForceX = 4f;

    [SerializeField, Min(0f)]
    private float knockbackForceY = 4f;

    [SerializeField, Min(0f)]
    private float knockbackRecoverySpeed = 12f;

    [Header("Dash")]
    [SerializeField, Min(0f)]
    private float dashDistance = 2.5f;

    [SerializeField, Min(0f)]
    private float dashStopOffset = 0.05f;

    [SerializeField]
    private LayerMask dashBlockLayer;

    private readonly RaycastHit2D[] dashHits =
    new RaycastHit2D[8];

    [Header("Ground Check")]
    [SerializeField]
    private Transform groundCheck;

    [SerializeField, Min(0f)]
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

    private Vector2 airJumpForce;

    private bool jumpRequested;
    private bool airJumpRequested;
    private bool hasUsedAirJump;
    private bool keepAirJumpMomentum;
    private bool movementEnabled = true;
    private bool keepHorizontal;

    public bool IsGrounded { get; private set; }

    public bool IsMoving =>
        movementEnabled &&
        Mathf.Abs(moveInput) > 0.01f;

    public bool CanJump =>
        movementEnabled &&
        IsGrounded;

    public bool CanAirJump =>
        movementEnabled &&
        !IsGrounded &&
        !hasUsedAirJump &&
        !airJumpRequested;

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

    public bool CanDash =>
    movementEnabled;

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
         * 클라이언트는 물리를 계산하지 않고
         * 서버의 물리 결과만 표시
         */
        if (!isServer)
        {
            rb.simulated = false;
        }
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
        ApplyJump();
        ApplyAirJump();
        RecoverKnockback();
        ClampFallSpeed();
    }

    // 서버에서 플레이어의 좌우 이동 입력을 설정
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

    // 점프를 요청
    [Server]
    public bool RequestJump()
    {
        if (!CanJump)
            return false;

        jumpRequested = true;

        return true;
    }

    // 입력한 좌우 방향으로 추가 점프 요청
    [Server]
    public bool RequestDoubleJump(
        float direction)
    {
        if (!CanAirJump ||
            Mathf.Approximately(direction, 0f))
        {
            return false;
        }

        direction =
            Mathf.Sign(direction);

        keepHorizontal = false;

        RequestAirJump(
            new Vector2(
                direction * doubleJumpForceX,
                doubleJumpForceY
            )
        );

        return true;
    }

    // 위쪽으로 추가 점프 요청
    [Server]
    public bool RequestUpJump()
    {
        if (!CanAirJump)
            return false;

        keepHorizontal = true;

        RequestAirJump(
            Vector2.up * upJumpForce
        );

        return true;
    }

    private void RequestAirJump(
        Vector2 force)
    {
        airJumpForce = force;
        airJumpRequested = true;
        hasUsedAirJump = true;
    }

    // 하단 점프 요청
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

    // 이동 가능 여부 설정
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

        ClearJumpRequests();

        if (stopHorizontalMovement)
        {
            StopHorizontalMovement();
        }
    }

    // 넉백 반응 적용
    [Server]
    public void ApplyKnockback(
        Vector2 attackerPosition)
    {
        float direction =
            transform.position.x >=
            attackerPosition.x
                ? 1f
                : -1f;

        ClearJumpRequests();

        keepAirJumpMomentum = false;
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
        if (!movementEnabled ||
            keepAirJumpMomentum)
        {
            return;
        }

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

    private void ApplyAirJump()
    {
        if (!airJumpRequested)
            return;

        float horizontalVelocity =
            keepHorizontal
                ? rb.linearVelocity.x
                : 0f;

        // 더블 점프: 기존 속도 모두 제거
        // 윗점프: 기존 수평 속도만 유지
        rb.linearVelocity =
            new Vector2(
                horizontalVelocity,
                0f
            );

        knockbackVelocityX = 0f;

        rb.AddForce(
            airJumpForce,
            ForceMode2D.Impulse
        );

        airJumpRequested = false;
        keepAirJumpMomentum = true;
        keepHorizontal = false;

        IsGrounded = false;
    }

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

        if (IsGrounded)
        {
            ResetAirJump();
        }
    }

    private void ResetAirJump()
    {
        airJumpForce = Vector2.zero;
        airJumpRequested = false;
        hasUsedAirJump = false;
        keepAirJumpMomentum = false;
        keepHorizontal = false;
    }

    private void ClearJumpRequests()
    {
        jumpRequested = false;
        airJumpRequested = false;
        airJumpForce = Vector2.zero;
        keepHorizontal = false;
    }

    private IEnumerator DropThroughPlatform(
        Collider2D platform)
    {
        ignoredPlatform = platform;

        IsGrounded = false;
        ClearJumpRequests();

        // 하단 점프 후 공중 추가 점프 초기화
        hasUsedAirJump = false;
        keepAirJumpMomentum = false;

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

    // 서버에서 플레이어를 지정한 위치로 이동 및 물리상태 초기화
    [Server]
    public void MovePosition(
        Vector2 position)
    {
        RestoreIgnoredPlatformCollision();

        moveInput = 0f;
        knockbackVelocityX = 0f;

        ClearJumpRequests();
        ResetAirJump();

        IsGrounded = false;

        rb.linearVelocity =
            Vector2.zero;

        rb.position =
            position;
    }


    [Server]
    public bool RequestDash(
    float direction)
    {
        if (Mathf.Approximately(
                direction,
                0f))
        {
            return false;
        }

        direction =
            Mathf.Sign(direction);

        Vector2 moveDirection =
            Vector2.right * direction;

        float moveDistance =
            FindDashDistance(
                moveDirection
            );

        rb.linearVelocity =
            Vector2.zero;

        knockbackVelocityX = 0f;

        rb.position +=
            moveDirection * moveDistance;

        return true;
    }

    private float FindDashDistance(
    Vector2 direction)
    {
        ContactFilter2D filter =
            new ContactFilter2D();

        filter.SetLayerMask(
            dashBlockLayer
        );

        filter.useTriggers = false;

        int hitCount =
            playerCollider.Cast(
                direction,
                filter,
                dashHits,
                dashDistance
            );

        float nearestDistance =
            dashDistance;

        for (int i = 0;
             i < hitCount;
             i++)
        {
            RaycastHit2D hit =
                dashHits[i];

            if (hit.collider == null)
                continue;

            // 밟고 있는 바닥처럼 이동 방향을 막지 않는 접촉은 제외
            if (hit.distance <= 0.001f &&
                Vector2.Dot(
                    hit.normal,
                    -direction
                ) < 0.5f)
            {
                continue;
            }

            nearestDistance =
                Mathf.Min(
                    nearestDistance,
                    hit.distance
                );
        }

        return Mathf.Max(
            0f,
            nearestDistance -
            dashStopOffset
        );
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.DrawLine(
                groundCheck.position,
                groundCheck.position +
                Vector3.down *
                groundCheckDistance
            );
        }

        Collider2D collider =
            GetComponent<Collider2D>();

        SpriteRenderer renderer =
            GetComponent<SpriteRenderer>();

        if (collider == null ||
            renderer == null)
        {
            return;
        }

        float direction =
            renderer.flipX
                ? 1f
                : -1f;

        Bounds bounds =
            collider.bounds;

        Vector3 start =
            bounds.center;

        Vector3 end =
            start +
            Vector3.right *
            direction *
            dashDistance;

        Gizmos.DrawWireCube(
            start,
            bounds.size
        );

        Gizmos.DrawWireCube(
            end,
            bounds.size
        );

        Gizmos.DrawLine(
            start,
            end
        );
    }
#endif
}