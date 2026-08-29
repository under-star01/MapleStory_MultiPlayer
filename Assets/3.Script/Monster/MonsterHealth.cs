using System;
using System.Collections;
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
    private int maxHp = 5;

    [Header("Death")]
    [SerializeField]
    private float destroyDelay = 1f;

    [Header("Drop")]
    [SerializeField]
    private MonsterData monsterData;

    [SerializeField]
    private WorldDropItem worldDropItemPrefab;

    [SerializeField]
    private Vector2 dropSpawnOffset = new Vector2(0f, 0.3f);

    [SerializeField]
    [SyncVar(hook = nameof(OnCurrentHpChanged))]
    private int currentHp;

    [SyncVar]
    private bool isDead;

    private Collider2D monsterCollider;
    private Animator animator;
    private MonsterMovement monsterMovement;

    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public bool IsDead => isDead;

    public event Action<int, int> HealthChanged;

    /*
     * 이후 DamageNumberController가 구독할 이벤트입니다.
     * 배열 하나가 스킬 한 번의 데미지 묶음을 의미합니다.
     */
    public event Action<DamageHitResult[]>
        DamageReceived;

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
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        HealthChanged?.Invoke(
            currentHp,
            maxHp
        );
    }

    /// <summary>
    /// 계산된 모든 타격 데미지를 합산해
    /// 몬스터 HP를 한 번만 감소시킵니다.
    /// </summary>
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

        /*
         * 자신을 공격한 플레이어를 기억하고
         * 공격 모드로 전환합니다.
         */
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

        RpcNotifyDamage(hitResults);

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        monsterMovement?.OnHit();

        PlayHitAnimation();
        RpcPlayHitAnimation();
    }

    /*
     * 기존 단일 데미지 호출과의 호환용입니다.
     * 다른 코드에서 TakeDamage(int)를 사용해도
     * 당장 오류가 발생하지 않게 유지합니다.
     */
    [Server]
    public void TakeDamage(int damage)
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

    [Server]
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        monsterMovement?.OnDeath();

        monsterCollider.enabled = false;

        SpawnDrops();

        RpcPlayDieAnimation();

        StartCoroutine(
            DestroyAfterDelay()
        );
    }

    /// <summary>
    /// 몬스터 데이터에 등록된 드롭 목록을 확인하고
    /// 확률 판정에 성공한 소비 아이템을 생성합니다.
    /// </summary>
    [Server]
    private void SpawnDrops()
    {
        if (monsterData == null ||
            worldDropItemPrefab == null)
        {
            return;
        }

        foreach (MonsterData.DropEntry entry
                 in monsterData.Drops)
        {
            if (entry == null ||
                entry.consumableId ==
                    ConsumableId.None ||
                entry.dropChance <= 0f)
            {
                continue;
            }

            if (UnityEngine.Random.value >
                entry.dropChance)
            {
                continue;
            }

            Vector3 spawnPosition = transform.position + (Vector3)dropSpawnOffset;

            WorldDropItem drop =
                Instantiate(
                    worldDropItemPrefab,
                    spawnPosition,
                    Quaternion.identity
                );

            /*
             * 서버가 여러 맵 씬을 동시에 로드하므로,
             * 드롭 아이템을 몬스터와 같은 맵 씬에 둡니다.
             */
            SceneManager.MoveGameObjectToScene(
                drop.gameObject,
                gameObject.scene
            );

            drop.Initialize(
                entry.consumableId
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

        animator.SetTrigger(HitHash);
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
    {
        /*
         * 호스트에서는 서버에서 이미 실행했으므로
         * 같은 Trigger를 중복 실행하지 않습니다.
         */
        if (isServer)
            return;

        PlayHitAnimation();
    }

    /// <summary>
    /// 서버 Animator의 Hit 상태가 종료되었을 때 호출됩니다.
    /// </summary>
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
        animator.ResetTrigger(HitHash);
        animator.SetTrigger(DieHash);
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(
            destroyDelay
        );

        NetworkServer.Destroy(gameObject);
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
}