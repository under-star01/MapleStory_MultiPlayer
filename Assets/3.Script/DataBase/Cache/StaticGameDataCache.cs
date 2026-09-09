using System.Collections.Generic;

public sealed class StaticGameDataCache
{
    private readonly Dictionary<int, MonsterRecord>
        monsters = new();

    public int MonsterCount =>
        monsters.Count;

    private readonly Dictionary<int, ItemRecord>
    items = new();

    public int ItemCount =>
        items.Count;

    private readonly Dictionary<
    int,
    List<MonsterDropRecord>>
    monsterDrops = new();

    public int MonsterDropCount
    {
        get;
        private set;
    }

    /// DB에서 읽은 몬스터 데이터를
    /// 서버 메모리 캐시에 저장
    public void SetMonsters(
        IEnumerable<MonsterRecord> records)
    {
        monsters.Clear();

        foreach (MonsterRecord record in records)
        {
            if (record == null)
                continue;

            if (!monsters.TryAdd(
                    record.MonsterId,
                    record))
            {
                throw new System.InvalidOperationException(
                    $"중복된 MonsterId입니다: " +
                    $"{record.MonsterId}"
                );
            }
        }
    }

    public bool TryGetMonster(
        int monsterId,
        out MonsterRecord record)
    {
        return monsters.TryGetValue(
            monsterId,
            out record
        );
    }

    public void SetItems(
    IEnumerable<ItemRecord> records)
    {
        items.Clear();

        foreach (ItemRecord record in records)
        {
            if (!items.TryAdd(
                    record.ItemId,
                    record))
            {
                throw new System.InvalidOperationException(
                    $"중복된 아이템 ID입니다: " +
                    $"{record.ItemId}"
                );
            }
        }
    }

    public bool TryGetItem(
    int itemId,
    out ItemRecord record)
    {
        return items.TryGetValue(
            itemId,
            out record
        );
    }

    public void SetMonsterDrops(
    IEnumerable<MonsterDropRecord> records)
    {
        monsterDrops.Clear();
        MonsterDropCount = 0;

        foreach (MonsterDropRecord record
                 in records)
        {
            if (!monsterDrops.TryGetValue(
                    record.MonsterId,
                    out List<MonsterDropRecord> drops))
            {
                drops =
                    new List<MonsterDropRecord>();

                monsterDrops.Add(
                    record.MonsterId,
                    drops
                );
            }

            drops.Add(record);
            MonsterDropCount++;
        }
    }

    public bool TryGetMonsterDrops(
    int monsterId,
    out IReadOnlyList<MonsterDropRecord> drops)
    {
        if (monsterDrops.TryGetValue(
                monsterId,
                out List<MonsterDropRecord> dropList))
        {
            drops = dropList;
            return true;
        }

        drops = null;
        return false;
    }
}