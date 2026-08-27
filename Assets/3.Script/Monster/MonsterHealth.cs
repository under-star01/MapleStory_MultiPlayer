using System;
using System.Collections;
using Mirror;
using UnityEngine;

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

        /*
         * 패트롤을 중단하고 Rigidbody2D를
         * Kinematic으로 바꿔 현재 위치에 고정합니다.
         */
        monsterMovement?.OnDeath();

        monsterCollider.enabled = false;

        RpcPlayDieAnimation();

        StartCoroutine(
            DestroyAfterDelay()
        );
    }

    [ClientRpc]
    private void RpcNotifyDamage(
        DamageHitResult[] hitResults)
    {
        DamageReceived?.Invoke(
            hitResults
        );
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
    {
        if (isDead)
            return;

        animator.SetTrigger(HitHash);
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