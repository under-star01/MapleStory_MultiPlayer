using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerEffectController))]
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

    [Header("Air Jump")]
    [SerializeField]
    private float doubleJumpForceX = 8f;

    [SerializeField]
    private float doubleJumpForceY = 2f;

    [SerializeField]
    private float upJumpForce = 8f;

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
    private PlayerEffectController playerEffect;

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

    private void Awake()
    {
        rb =
            GetComponent<Rigidbody2D>();

        spriteRenderer =
            GetComponent<SpriteRenderer>();

        playerCollider =
            GetComponent<Collider2D>();

        playerEffect =
            GetComponent<PlayerEffectController>();
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
        ApplyAirJump();
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
    /// 지상 점프를 예약합니다.
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
    /// 입력한 좌우 방향으로 추가 점프를 예약합니다.
    /// </summary>
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

        return RequestAirJump(
            new Vector2(
                direction * doubleJumpForceX,
                doubleJumpForceY
            ),
            EffectId.DoubleJump,
            direction
        );
    }

    /// <summary>
    /// 위쪽으로 추가 점프를 예약합니다.
    /// </summary>
    [Server]
    public bool RequestUpJump()
    {
        if (!CanAirJump)
            return false;

        keepHorizontal = true;

        return RequestAirJump(
            Vector2.up * upJumpForce,
            EffectId.UpJump
        );
    }

    [Server]
    private bool RequestAirJump(
        Vector2 force,
        EffectId effectId,
        float direction = 0f)
    {
        airJumpForce = force;
        airJumpRequested = true;
        hasUsedAirJump = true;

        if (effectId == EffectId.DoubleJump)
        {
            playerEffect.PlayDoubleJumpEffect(
                direction
            );
        }
        else
        {
            playerEffect.PlayUpJumpEffect();
        }

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

        ClearJumpRequests();

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

        /*
         * 더블 점프는 기존 속도를 모두 제거하고,
         * 윗점프는 기존 수평 속도만 유지합니다.
         */
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

        /*
         * 하단 점프 후에는 공중 추가 점프를
         * 한 번 사용할 수 있도록 초기화합니다.
         */
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

        ClearJumpRequests();
        ResetAirJump();

        IsGrounded = false;

        rb.linearVelocity =
            Vector2.zero;

        rb.position =
            position;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        moveSpeed =
            Mathf.Max(0f, moveSpeed);

        jumpForce =
            Mathf.Max(0f, jumpForce);

        doubleJumpForceX =
            Mathf.Max(0f, doubleJumpForceX);

        doubleJumpForceY =
            Mathf.Max(0f, doubleJumpForceY);

        upJumpForce =
            Mathf.Max(0f, upJumpForce);

        maxFallSpeed =
            Mathf.Max(0f, maxFallSpeed);
    }

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