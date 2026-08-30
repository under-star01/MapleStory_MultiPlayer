public sealed class MonsterRecord
{
    public int MonsterId { get; }
    public string MonsterName { get; }
    public int MaxHp { get; }
    public int AttackPower { get; }
    public float MoveSpeed { get; }

    public MonsterRecord(
        int monsterId,
        string monsterName,
        int maxHp,
        int attackPower,
        float moveSpeed)
    {
        MonsterId = monsterId;
        MonsterName = monsterName;
        MaxHp = maxHp;
        AttackPower = attackPower;
        MoveSpeed = moveSpeed;
    }
}