using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class BasicAttackSkill : PlayerSkillBase
{
    [Header("Attack")]
    [SerializeField]
    private int baseDamage = 50;

    [SerializeField]
    private int hitCount = 5;

    [SerializeField]
    private int maxTargets = 1;

    [SerializeField]
    private Vector2 attackSize =
        new Vector2(0.8f, 0.6f);

    [SerializeField]
    private Vector2 attackOffset =
        new Vector2(0.5f, 0.05f);

    private const int MaxHitBufferSize = 16;

    private readonly Collider2D[] hitBuffer =
        new Collider2D[MaxHitBufferSize];

    private readonly HashSet<MonsterHealth>
        processedMonsters = new();

    private SkillContext activeContext;

    private ContactFilter2D monsterFilter;

    private bool isExecuting;
    private bool movementLocked;
    private bool hasAppliedHit;

    private void Awake()
    {
        monsterFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Monster"),
            useTriggers = true
        };
    }

    public override bool CanExecute(
        SkillContext context)
    {
        if (context == null)
            return false;

        if (!NetworkServer.active)
            return false;

        if (isExecuting)
            return false;

        return true;
    }

    public override void Execute(
        SkillContext context)
    {
        activeContext = context;
        isExecuting = true;
        hasAppliedHit = false;

        bool isGrounded =
            context.Move.IsGrounded;

        context.Move.SetMovementEnabled(
            enabled: false,
            stopHorizontalMovement: isGrounded
        );

        movementLocked = true;

        context.Anim.AttackHitFrame +=
            ApplyAttackHit;

        context.Anim.AttackAnimationEnded +=
            EndAttack;

        context.Anim.PlayAttack();
    }

    [Server]
    private void ApplyAttackHit()
    {
        if (!isExecuting ||
            activeContext == null ||
            hasAppliedHit)
        {
            return;
        }

        hasAppliedHit = true;

        Vector2 attackCenter =
            CalculateAttackCenter(
                activeContext
            );

        PhysicsScene2D physicsScene =
            activeContext.User.scene
                .GetPhysicsScene2D();

        int colliderCount =
            physicsScene.OverlapBox(
                attackCenter,
                attackSize,
                0f,
                monsterFilter,
                hitBuffer
            );

        processedMonsters.Clear();

        int damagedTargetCount = 0;

        for (int i = 0; i < colliderCount; i++)
        {
            Collider2D hitCollider =
                hitBuffer[i];

            if (hitCollider == null)
                continue;

            MonsterHealth monsterHealth =
                hitCollider.GetComponentInParent
                    <MonsterHealth>();

            if (monsterHealth == null ||
                monsterHealth.IsDead)
            {
                continue;
            }

            if (!processedMonsters.Add(
                    monsterHealth))
            {
                continue;
            }

            /*
             * 해당 몬스터가 받을 각 타격의
             * 데미지와 크리티컬 여부를 서버에서 계산합니다.
             */
            DamageHitResult[] hitResults =
                DamageCalculator.CalculateHits(
                    baseDamage,
                    hitCount,
                    out int totalDamage
                );

            /*
             * HP는 타격별로 나누지 않고
             * 합산값을 한 번만 전달합니다.
             */
            monsterHealth.TakeDamage(
                totalDamage,
                hitResults
            );

            damagedTargetCount++;

            if (damagedTargetCount >= maxTargets)
                break;
        }
    }

    private Vector2 CalculateAttackCenter(
        SkillContext context)
    {
        float facingX =
            context.FacingDirection.x >= 0f
                ? 1f
                : -1f;

        return (Vector2)context.Position +
            new Vector2(
                attackOffset.x * facingX,
                attackOffset.y
            );
    }

    private void EndAttack()
    {
        if (!isExecuting ||
            activeContext == null)
        {
            return;
        }

        UnsubscribeAnimationEvents();

        if (movementLocked)
        {
            activeContext.Move
                .SetMovementEnabled(true);

            movementLocked = false;
        }

        isExecuting = false;
        hasAppliedHit = false;
        activeContext = null;
    }

    private void UnsubscribeAnimationEvents()
    {
        if (activeContext == null)
            return;

        activeContext.Anim.AttackHitFrame -=
            ApplyAttackHit;

        activeContext.Anim.AttackAnimationEnded -=
            EndAttack;
    }

    private void OnDisable()
    {
        if (activeContext != null)
        {
            UnsubscribeAnimationEvents();

            if (movementLocked)
            {
                activeContext.Move
                    .SetMovementEnabled(true);
            }
        }

        movementLocked = false;
        isExecuting = false;
        hasAppliedHit = false;
        activeContext = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        PlayerMove move =
            GetComponent<PlayerMove>();

        float facingX =
            move == null ||
            move.FacingDirection.x >= 0f
                ? 1f
                : -1f;

        Vector2 center =
            (Vector2)transform.position +
            new Vector2(
                attackOffset.x * facingX,
                attackOffset.y
            );

        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(
            center,
            attackSize
        );
    }
#endif
}