using System.Collections.Generic;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(MonsterHealth))]
public class MonsterCombat : NetworkBehaviour
{
    [Header("Contact Damage")]
    [SerializeField]
    [Min(1)]
    private int contactDamage = 10;

    [SerializeField]
    [Min(0.1f)]
    private float damageInterval = 1f;

    private MonsterHealth monsterHealth;

    /*
     * 플레이어별 다음 피해 가능 시간을 저장합니다.
     * 여러 플레이어가 같은 몬스터에 닿아도
     * 각 플레이어의 피해 간격을 따로 관리합니다.
     */
    private readonly Dictionary<PlayerHealth, float>
        nextDamageTimes = new();

    private void Awake()
    {
        monsterHealth =
            GetComponent<MonsterHealth>();
    }

    /// <summary>
    /// 몬스터와 계속 접촉 중인 플레이어에게
    /// 서버에서 일정 간격으로 피해를 적용합니다.
    /// </summary>
    [ServerCallback]
    private void OnTriggerStay2D(
    Collider2D other)
    {
        if (monsterHealth.IsDead)
            return;

        PlayerHealth playerHealth =
            other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null)
            return;

        if (playerHealth.IsDead)
            return;

        if (!CanDamage(playerHealth))
            return;

        playerHealth.TakeDamage(
            contactDamage,
            transform.position
        );

        nextDamageTimes[playerHealth] =
            Time.time + damageInterval;
    }

    [Server]
    private bool CanDamage(
        PlayerHealth playerHealth)
    {
        if (!nextDamageTimes.TryGetValue(
                playerHealth,
                out float nextDamageTime))
        {
            return true;
        }

        return Time.time >= nextDamageTime;
    }

    private void OnDisable()
    {
        nextDamageTimes.Clear();
    }
}