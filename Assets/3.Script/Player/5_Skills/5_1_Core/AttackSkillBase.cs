using System.Collections.Generic;
using Mirror;
using UnityEngine;

public abstract class AttackSkillBase :
    PlayerSkillBase
{
    private const int MaxHitBufferSize = 16;

    private readonly Collider2D[] hitBuffer =
        new Collider2D[MaxHitBufferSize];

    private readonly HashSet<MonsterHealth>
        processedMonsters = new();

    private ContactFilter2D monsterFilter;

    protected virtual void Awake()
    {
        monsterFilter =
            new ContactFilter2D
            {
                useLayerMask = true,
                layerMask =
                    LayerMask.GetMask("Monster"),
                useTriggers = true
            };
    }

    /// <summary>
    /// 전달받은 공격 설정을 기준으로 범위 안의 몬스터에게
    /// 최대 대상 수만큼 피해를 적용합니다.
    /// </summary>
    [Server]
    protected int ApplyAreaDamage(
        SkillContext context,
        int baseDamage,
        int hitCount,
        int maxTargets,
        Vector2 attackSize,
        Vector2 attackOffset)
    {
        if (context == null)
            return 0;

        Vector2 attackCenter =
            CalculateAttackCenter(
                context,
                attackOffset
            );

        PhysicsScene2D physicsScene =
            context.User.scene
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

            DamageHitResult[] hitResults =
                DamageCalculator.CalculateHits(
                    baseDamage,
                    hitCount,
                    out int totalDamage
                );

            monsterHealth.TakeDamage(
                context.User,
                totalDamage,
                hitResults
            );

            damagedTargetCount++;

            if (damagedTargetCount >= maxTargets)
                break;
        }

        return damagedTargetCount;
    }

    private Vector2 CalculateAttackCenter(
        SkillContext context,
        Vector2 attackOffset)
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

    protected void DrawAttackRangeGizmo(
        Vector2 attackSize,
        Vector2 attackOffset)
    {
#if UNITY_EDITOR
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

        Gizmos.color =
            Color.red;

        Gizmos.DrawWireCube(
            center,
            attackSize
        );
#endif
    }
}