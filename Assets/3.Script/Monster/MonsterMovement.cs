using System.Collections;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(MonsterHealth))]
public class MonsterMovement : NetworkBehaviour
{
    private static readonly int IsMovingHash =
        Animator.StringToHash("isMoving");

    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 1.2f;
    public float MoveSpeed =>
    moveSpeed;

    [SerializeField]
    private float minMoveTime = 0.7f;

    [SerializeField]
    private float maxMoveTime = 2.5f;

    [Header("Waiting")]
    [SerializeField]
    private float minWaitTime = 0.4f;

    [SerializeField]
    private float maxWaitTime = 1.8f;

    [Header("Ground Check")]
    [SerializeField]
    private Transform frontGroundCheck;

    [SerializeField]
    private float groundCheckDistance = 0.2f;

    [SerializeField]
    private LayerMask groundLayer;

    [Header("Sprite")]
    [Tooltip("원본 스프라이트가 오른쪽을 바라보면 체크합니다.")]
    [SerializeField]
    private bool spriteFacesRight = false;

    [SyncVar(hook = nameof(OnMoveDirectionChanged))]
    private int moveDirection = 1;

    [SyncVar(hook = nameof(OnMovingChanged))]
    private bool isMoving;

    private Rigidbody2D rigidBody;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private MonsterHealth monsterHealth;

    private PhysicsScene2D physicsScene;

    private Transform attackTarget;

    private bool isAttackMode;
    private bool isHit;
    private int forcedNextDirection;

    private Coroutine patrolCoroutine;

    private Vector3 frontCheckBaseLocalPosition;

    private void Awake()
    {
        rigidBody =
            GetComponent<Rigidbody2D>();

        spriteRenderer =
            GetComponent<SpriteRenderer>();

        animator =
            GetComponent<Animator>();

        monsterHealth =
            GetComponent<MonsterHealth>();

        if (frontGroundCheck != null)
        {
            frontCheckBaseLocalPosition =
                frontGroundCheck.localPosition;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (patrolCoroutine != null)
        {
            StopCoroutine(
                patrolCoroutine
            );

            patrolCoroutine = null;
        }

        physicsScene =
            gameObject.scene.GetPhysicsScene2D();

        rigidBody.bodyType =
            RigidbodyType2D.Dynamic;

        rigidBody.simulated = true;

        rigidBody.linearVelocity =
            Vector2.zero;

        rigidBody.angularVelocity =
            0f;

        attackTarget = null;
        isAttackMode = false;
        isHit = false;
        forcedNextDirection = 0;

        SetMoving(false);

        SetMoveDirection(
            Random.value < 0.5f
                ? -1
                : 1
        );

        patrolCoroutine =
            StartCoroutine(Patrol());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        /*
         * 원격 클라이언트는 물리를 직접 계산하지 않고
         * NetworkTransform의 결과만 표시합니다.
         */
        if (!isServer)
        {
            rigidBody.simulated = false;
        }

        ApplyDirection(moveDirection);
        ApplyMoving(isMoving);
    }

    [Server]
    private IEnumerator Patrol()
    {
        while (!monsterHealth.IsDead)
        {
            StopMoving();

            yield return new WaitForSeconds(
                GetRandomWaitTime()
            );

            if (monsterHealth.IsDead)
                break;

            ChooseMoveDirection();

            /*
             * 선택한 방향에 바닥이 없다면
             * 반대 방향으로 바꿉니다.
             */
            if (!HasGroundAhead())
            {
                SetMoveDirection(
                    -moveDirection
                );

                /*
                 * 반대 방향에도 바닥이 없다면
                 * 이번 이동을 취소합니다.
                 */
                if (!HasGroundAhead())
                    continue;
            }

            float moveTime =
                GetRandomMoveTime();

            float elapsedTime = 0f;

            SetMoving(true);

            while (elapsedTime < moveTime &&
                   !monsterHealth.IsDead)
            {
                if (!HasGroundAhead())
                {
                    forcedNextDirection =
                        -moveDirection;

                    break;
                }

                rigidBody.linearVelocity =
                    new Vector2(
                        moveDirection * moveSpeed,
                        rigidBody.linearVelocity.y
                    );

                elapsedTime +=
                    Time.fixedDeltaTime;

                yield return new WaitForFixedUpdate();
            }
        }

        StopMoving();
        patrolCoroutine = null;
    }

    [Server]
    private void ChooseMoveDirection()
    {
        /*
         * 플랫폼 끝에서 멈춘 경우에는
         * 안전한 반대 방향을 우선합니다.
         */
        if (forcedNextDirection != 0)
        {
            SetMoveDirection(
                forcedNextDirection
            );

            forcedNextDirection = 0;
            return;
        }

        /*
         * 피격 후에는 마지막 공격자 방향을
         * 다음 이동 방향으로 선택합니다.
         */
        if (isAttackMode &&
            attackTarget != null)
        {
            SetMoveDirection(
                attackTarget.position.x >=
                transform.position.x
                    ? 1
                    : -1
            );

            return;
        }

        SetMoveDirection(
            Random.value < 0.5f
                ? -1
                : 1
        );
    }

    [Server]
    public void EnterAttackMode(
        Transform attacker)
    {
        if (attacker == null)
            return;

        attackTarget = attacker;
        isAttackMode = true;
    }

    /// <summary>
    /// 피격 애니메이션이 재생되는 동안
    /// 현재 이동과 순찰을 중단합니다.
    /// </summary>
    [Server]
    public void OnHit()
    {
        if (monsterHealth.IsDead)
            return;

        isHit = true;

        if (patrolCoroutine != null)
        {
            StopCoroutine(
                patrolCoroutine
            );

            patrolCoroutine = null;
        }

        StopMoving();
    }

    /// <summary>
    /// 피격 애니메이션 종료 후
    /// 몬스터의 순찰을 다시 시작합니다.
    /// </summary>
    [Server]
    public void OnHitEnded()
    {
        if (monsterHealth.IsDead ||
            !isHit)
        {
            return;
        }

        isHit = false;

        if (patrolCoroutine == null)
        {
            patrolCoroutine =
                StartCoroutine(Patrol());
        }
    }

    /// <summary>
    /// MonsterHealth가 사망을 결정한 순간 호출합니다.
    /// 현재 위치에서 모든 물리 이동을 멈춥니다.
    /// </summary>

    [Server]
    public void OnDeath()
    {
        isHit = false;

        if (patrolCoroutine != null)
        {
            StopCoroutine(
                patrolCoroutine
            );

            patrolCoroutine = null;
        }

        attackTarget = null;
        isAttackMode = false;

        SetMoving(false);

        rigidBody.linearVelocity =
            Vector2.zero;

        rigidBody.angularVelocity =
            0f;

        rigidBody.bodyType =
            RigidbodyType2D.Kinematic;
    }

    [Server]
    private bool HasGroundAhead()
    {
        if (frontGroundCheck == null)
            return false;

        UpdateFrontGroundCheckPosition();

        RaycastHit2D hit =
            physicsScene.Raycast(
                frontGroundCheck.position,
                Vector2.down,
                groundCheckDistance,
                groundLayer
            );

        return hit.collider != null;
    }

    [Server]
    private void SetMoveDirection(
        int direction)
    {
        int newDirection =
            direction >= 0 ? 1 : -1;

        if (moveDirection == newDirection)
        {
            ApplyDirection(newDirection);
            UpdateFrontGroundCheckPosition();
            return;
        }

        moveDirection = newDirection;

        /*
         * 호스트 화면은 즉시 적용하고,
         * 원격 클라이언트는 SyncVar Hook으로 적용합니다.
         */
        ApplyDirection(moveDirection);
        UpdateFrontGroundCheckPosition();
    }

    private void UpdateFrontGroundCheckPosition()
    {
        if (frontGroundCheck == null)
            return;

        Vector3 position =
            frontCheckBaseLocalPosition;

        position.x =
            Mathf.Abs(
                frontCheckBaseLocalPosition.x
            ) * moveDirection;

        frontGroundCheck.localPosition =
            position;
    }

    private void OnMoveDirectionChanged(
        int previousDirection,
        int newDirection)
    {
        ApplyDirection(newDirection);
        UpdateFrontGroundCheckPosition();
    }

    private void ApplyDirection(
        int direction)
    {
        bool faceLeft =
            direction < 0;

        spriteRenderer.flipX =
            spriteFacesRight
                ? faceLeft
                : !faceLeft;
    }

    private void OnMovingChanged(
    bool previousValue,
    bool newValue)
    {
        ApplyMoving(newValue);
    }

    private void ApplyMoving(
        bool value)
    {
        animator.SetBool(
            IsMovingHash,
            value
        );
    }

    [Server]
    private void StopMoving()
    {
        rigidBody.linearVelocity =
            new Vector2(
                0f,
                rigidBody.linearVelocity.y
            );

        SetMoving(false);
    }

    [Server]
    private void SetMoving(
        bool value)
    {
        if (isMoving == value)
        {
            /*
             * 호스트 화면에서 현재 값이 이미 같더라도
             * Animator 상태는 확실히 맞춥니다.
             */
            if (isClient)
            {
                ApplyMoving(value);
            }

            return;
        }

        isMoving = value;

        /*
         * 호스트는 서버와 클라이언트가 같은 오브젝트이므로
         * 즉시 화면에 적용합니다.
         *
         * 원격 클라이언트는 SyncVar Hook에서 적용됩니다.
         */
        if (isClient)
        {
            ApplyMoving(value);
        }
    }

    private float GetRandomMoveTime()
    {
        return Random.Range(
            Mathf.Min(
                minMoveTime,
                maxMoveTime
            ),
            Mathf.Max(
                minMoveTime,
                maxMoveTime
            )
        );
    }

    private float GetRandomWaitTime()
    {
        return Random.Range(
            Mathf.Min(
                minWaitTime,
                maxWaitTime
            ),
            Mathf.Max(
                minWaitTime,
                maxWaitTime
            )
        );
    }

    private void OnDisable()
    {
        if (patrolCoroutine == null)
            return;

        StopCoroutine(
            patrolCoroutine
        );

        patrolCoroutine = null;
    }

    public void ApplyMoveSpeed(
    float value)
    {
        if (value < 0f)
            return;

        moveSpeed = value;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (frontGroundCheck == null)
            return;

        Gizmos.color = Color.red;

        Gizmos.DrawLine(
            frontGroundCheck.position,
            frontGroundCheck.position +
            Vector3.down *
            groundCheckDistance
        );
    }
#endif
}