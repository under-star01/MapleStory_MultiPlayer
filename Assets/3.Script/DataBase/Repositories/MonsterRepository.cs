using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

public sealed class MonsterRepository
{
    private const string SelectAllSql = @"
        SELECT
            monster_id,
            monster_name,
            max_hp,
            attack_power,
            move_speed
        FROM monsters
        ORDER BY monster_id;
    ";

    private readonly string connectionString;

    public MonsterRepository(
        string connectionString)
    {
        this.connectionString =
            connectionString;
    }

    public async Task<List<MonsterRecord>>
        LoadAllAsync()
    {
        List<MonsterRecord> records =
            new();

        using MySqlConnection connection =
            new MySqlConnection(
                connectionString
            );

        await connection.OpenAsync();

        using MySqlCommand command =
            new MySqlCommand(
                SelectAllSql,
                connection
            );

        using MySqlDataReader reader =
            await command.ExecuteReaderAsync();

        int monsterIdOrdinal =
            reader.GetOrdinal("monster_id");

        int monsterNameOrdinal =
            reader.GetOrdinal("monster_name");

        int maxHpOrdinal =
            reader.GetOrdinal("max_hp");

        int attackPowerOrdinal =
            reader.GetOrdinal("attack_power");

        int moveSpeedOrdinal =
            reader.GetOrdinal("move_speed");

        while (await reader.ReadAsync())
        {
            MonsterRecord record =
                new MonsterRecord(
                    reader.GetInt32(
                        monsterIdOrdinal
                    ),
                    reader.GetString(
                        monsterNameOrdinal
                    ),
                    reader.GetInt32(
                        maxHpOrdinal
                    ),
                    reader.GetInt32(
                        attackPowerOrdinal
                    ),
                    reader.GetFloat(
                        moveSpeedOrdinal
                    )
                );

            records.Add(record);
        }

        return records;
    }
}