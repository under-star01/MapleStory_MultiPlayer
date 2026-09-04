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

    public int ContactDamage =>
        contactDamage;

    [SerializeField]
    [Min(0.1f)]
    private float damageInterval = 1f;

    private MonsterHealth monsterHealth;

    private readonly Dictionary<PlayerHealth, float>
        nextDamageTimes = new();

    private void Awake()
    {
        monsterHealth =
            GetComponent<MonsterHealth>();
    }

    // 접촉 중인 플레이어에게 일정 간격으로 피해 적용
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

    public void ApplyContactDamage(
        int value)
    {
        if (value <= 0)
            return;

        contactDamage = value;
    }
}