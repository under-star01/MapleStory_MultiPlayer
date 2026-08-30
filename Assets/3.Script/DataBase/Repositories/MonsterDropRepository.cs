using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

public class MonsterDropRepository
{
    private readonly string connectionString;

    public MonsterDropRepository(
        string connectionString)
    {
        this.connectionString =
            connectionString;
    }

    public async Task<List<MonsterDropRecord>>
        LoadAllAsync()
    {
        List<MonsterDropRecord> drops =
            new();

        await using MySqlConnection connection =
            new MySqlConnection(
                connectionString
            );

        await connection.OpenAsync();

        const string query = @"
            SELECT
                monster_id,
                item_id,
                drop_rate
            FROM monster_drops
            ORDER BY monster_id, item_id;
            ";

        await using MySqlCommand command =
            new MySqlCommand(
                query,
                connection
            );

        await using MySqlDataReader reader =
            await command.ExecuteReaderAsync();

        int monsterIdIndex =
            reader.GetOrdinal("monster_id");

        int itemIdIndex =
            reader.GetOrdinal("item_id");

        int dropRateIndex =
            reader.GetOrdinal("drop_rate");

        while (await reader.ReadAsync())
        {
            MonsterDropRecord record =
                new MonsterDropRecord(
                    reader.GetInt32(
                        monsterIdIndex
                    ),
                    reader.GetInt32(
                        itemIdIndex
                    ),
                    reader.GetFloat(
                        dropRateIndex
                    )
                );

            drops.Add(record);
        }

        return drops;
    }
}