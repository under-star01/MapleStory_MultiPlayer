using UnityEngine;

public static class DamageCalculator
{
    private const float CriticalChance = 0.5f;
    private const float CriticalMultiplier = 1.5f;

    public static DamageHitResult[] CalculateHits(
        int baseDamage,
        int hitCount,
        out int totalDamage)
    {
        baseDamage = Mathf.Max(baseDamage, 0);
        hitCount = Mathf.Max(hitCount, 0);

        DamageHitResult[] results =
            new DamageHitResult[hitCount];

        totalDamage = 0;

        for (int i = 0; i < hitCount; i++)
        {
            bool isCritical =
                Random.value < CriticalChance;

            int damage = isCritical
                ? Mathf.RoundToInt(
                    baseDamage * CriticalMultiplier
                )
                : baseDamage;

            damage = Mathf.Max(damage, 0);

            results[i] =
                new DamageHitResult(
                    damage,
                    isCritical
                );

            totalDamage += damage;
        }

        return results;
    }
}