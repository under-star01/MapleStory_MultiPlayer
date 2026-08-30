using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

public class ItemRepository
{
    private readonly string connectionString;

    public ItemRepository(
        string connectionString)
    {
        this.connectionString =
            connectionString;
    }

    public async Task<List<ItemRecord>>
        LoadAllAsync()
    {
        List<ItemRecord> items =
            new();

        await using MySqlConnection connection =
            new MySqlConnection(
                connectionString
            );

        await connection.OpenAsync();

        const string query = @"
            SELECT
                item_id,
                item_name,
                heal_amount,
                max_stack
            FROM items
            ORDER BY item_id;
            ";

        await using MySqlCommand command =
            new MySqlCommand(
                query,
                connection
            );

        await using MySqlDataReader reader =
            await command.ExecuteReaderAsync();

        int itemIdIndex =
            reader.GetOrdinal("item_id");

        int itemNameIndex =
            reader.GetOrdinal("item_name");

        int healAmountIndex =
            reader.GetOrdinal("heal_amount");

        int maxStackIndex =
            reader.GetOrdinal("max_stack");

        while (await reader.ReadAsync())
        {
            ItemRecord record =
                new ItemRecord(
                    reader.GetInt32(
                        itemIdIndex
                    ),
                    reader.GetString(
                        itemNameIndex
                    ),
                    reader.GetInt32(
                        healAmountIndex
                    ),
                    reader.GetInt32(
                        maxStackIndex
                    )
                );

            items.Add(record);
        }

        return items;
    }
}