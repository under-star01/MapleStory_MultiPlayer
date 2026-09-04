using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class MonsterHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField]
    [SyncVar(hook = nameof(OnMaxHpChanged))]
    private int maxHp = 300;

    [SerializeField]
    [SyncVar(hook = nameof(OnCurrentHpChanged))]
    private int currentHp;

    private int monsterId;

    [Header("Death")]
    [SerializeField]
    [Min(0f)]
    private float deathAnimationDuration = 1f;

    [Header("Drop")]
    [SerializeField]
    private WorldDropItem worldDropItemPrefab;

    [SerializeField]
    private Vector2 dropSpawnOffset =
        new Vector2(0f, 0.3f);

    [SyncVar]
    private bool isDead;

    private Collider2D monsterCollider;
    private Animator animator;
    private MonsterMovement monsterMovement;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public bool IsDead => isDead;

    public event Action<int, int> HealthChanged;
    public event Action<DamageHitResult[]> DamageReceived;
    public event Action<MonsterHealth> DeathCompleted;

    private static readonly int HitHash =
        Animator.StringToHash("Hit");

    private static readonly int DieHash =
        Animator.StringToHash("Die");

    private void Awake()
    {
        monsterCollider =
            GetComponent<Collider2D>();

        animator =
            GetComponent<Animator>();

        monsterMovement =
            GetComponent<MonsterMovement>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        currentHp = maxHp;
        isDead = false;

        monsterCollider.enabled = true;

        animator.ResetTrigger(
            HitHash
        );

        animator.ResetTrigger(
            DieHash
        );

        animator.Rebind();
        animator.Update(0f);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        HealthChanged?.Invoke(
            currentHp,
            maxHp
        );
    }

    public void ApplyMonsterId(
        int value)
    {
        if (value <= 0)
            return;

        monsterId = value;
    }

    // 타격 데미지를 합산해 HP 감소 및 피격 처리
    [Server]
    public void TakeDamage(
        GameObject attacker,
        int totalDamage,
        DamageHitResult[] hitResults)
    {
        if (isDead)
            return;

        if (totalDamage <= 0)
            return;

        if (hitResults == null ||
            hitResults.Length == 0)
        {
            return;
        }

        if (attacker != null)
        {
            monsterMovement?.EnterAttackMode(
                attacker.transform
            );
        }

        currentHp = Mathf.Max(
            currentHp - totalDamage,
            0
        );

        RpcNotifyDamage(
            hitResults
        );

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        monsterMovement?.OnHit();

        PlayHitAnimation();
        RpcPlayHitAnimation();
    }

    [Server]
    public void TakeDamage(
        int damage)
    {
        DamageHitResult[] hitResults =
        {
            new DamageHitResult(
                damage,
                false
            )
        };

        TakeDamage(
            null,
            damage,
            hitResults
        );
    }

    // 몬스터 사망 및 드롭 처리
    [Server]
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        monsterMovement?.OnDeath();

        monsterCollider.enabled =
            false;

        SpawnDrops();

        RpcPlayDieAnimation();

        StartCoroutine(
            CompleteDeathAfterDelay()
        );
    }

    // 등록된 드롭 데이터를 기준으로 확률 판정 후 아이템 생성
    [Server]
    private void SpawnDrops()
    {
        if (worldDropItemPrefab == null)
            return;

        DatabaseManager databaseManager =
            DatabaseManager.Instance;

        if (databaseManager == null ||
            !databaseManager.IsInitialized)
        {
            return;
        }

        if (!databaseManager.StaticData
                .TryGetMonsterDrops(
                    monsterId,
                    out IReadOnlyList
                        <MonsterDropRecord> drops))
        {
            return;
        }

        foreach (MonsterDropRecord dropRecord
                 in drops)
        {
            if (UnityEngine.Random.value >
                dropRecord.DropRate)
            {
                continue;
            }

            ConsumableId consumableId =
                (ConsumableId)dropRecord.ItemId;

            if (consumableId ==
                ConsumableId.None)
            {
                continue;
            }

            Vector3 spawnPosition =
                transform.position +
                (Vector3)dropSpawnOffset;

            WorldDropItem drop =
                Instantiate(
                    worldDropItemPrefab,
                    spawnPosition,
                    Quaternion.identity
                );

            SceneManager.MoveGameObjectToScene(
                drop.gameObject,
                gameObject.scene
            );

            drop.Initialize(
                consumableId
            );

            NetworkServer.Spawn(
                drop.gameObject
            );
        }
    }

    [ClientRpc]
    private void RpcNotifyDamage(
        DamageHitResult[] hitResults)
    {
        DamageReceived?.Invoke(
            hitResults
        );
    }

    private void PlayHitAnimation()
    {
        if (isDead)
            return;

        animator.SetTrigger(
            HitHash
        );
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
    {
        if (!isServer)
        {
            PlayHitAnimation();
        }

        AudioManager.Instance?.PlayMonster(
            MonsterSoundId.Hit
        );
    }

    // 피격 애니메이션 종료 후 이동 상태 복구
    public void OnHitAnimationEnded()
    {
        if (!isServer ||
            isDead)
        {
            return;
        }

        monsterMovement?.OnHitEnded();
    }

    [ClientRpc]
    private void RpcPlayDieAnimation()
    {
        animator.ResetTrigger(
            HitHash
        );

        animator.SetTrigger(
            DieHash
        );

        AudioManager.Instance?.PlayMonster(
            MonsterSoundId.Die
        );
    }

    [Server]
    private IEnumerator CompleteDeathAfterDelay()
    {
        yield return new WaitForSeconds(
            deathAnimationDuration
        );

        DeathCompleted?.Invoke(
            this
        );
    }

    private void OnMaxHpChanged(
        int previousMaxHp,
        int newMaxHp)
    {
        HealthChanged?.Invoke(
            currentHp,
            newMaxHp
        );
    }

    private void OnCurrentHpChanged(
        int previousHp,
        int newHp)
    {
        HealthChanged?.Invoke(
            newHp,
            maxHp
        );
    }

    public void ApplyMaxHp(
        int value)
    {
        if (value <= 0)
            return;

        maxHp = value;
    }
}