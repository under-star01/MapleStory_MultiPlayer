using System;

[Serializable]
public struct DamageHitResult
{
    public int damage;
    public bool isCritical;

    public DamageHitResult(
        int damage,
        bool isCritical)
    {
        this.damage = damage;
        this.isCritical = isCritical;
    }
}