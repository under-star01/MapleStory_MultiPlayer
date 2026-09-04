using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

public class PlayerInventoryRepository
{
    private readonly string connectionString;

    public PlayerInventoryRepository(
        string connectionString)
    {
        this.connectionString =
            connectionString ??
            throw new ArgumentNullException(
                nameof(connectionString)
            );
    }

    // 지정한 유저의 전체 인벤토리 조회
    public async Task<List<PlayerInventoryRecord>>
        LoadByUserIdAsync(
            int userId)
    {
        const string query = @"
            SELECT
                item_id,
                quantity
            FROM player_inventory
            WHERE user_id = @userId
            ORDER BY item_id;
            ";

        List<PlayerInventoryRecord> records =
            new List<PlayerInventoryRecord>();

        await using MySqlConnection connection =
            new MySqlConnection(
                connectionString
            );

        await connection.OpenAsync();

        await using MySqlCommand command =
            new MySqlCommand(
                query,
                connection
            );

        command.Parameters.AddWithValue(
            "@userId",
            userId
        );

        await using MySqlDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(
                new PlayerInventoryRecord(
                    reader.GetInt32("item_id"),
                    reader.GetInt32("quantity")
                )
            );
        }

        return records;
    }

    // 현재 인벤토리 스냅샷을 트랜잭션으로 전체 저장
    public async Task SaveAllAsync(
        int userId,
        IReadOnlyCollection<PlayerInventoryRecord>
            records)
    {
        if (records == null)
        {
            throw new ArgumentNullException(
                nameof(records)
            );
        }

        await using MySqlConnection connection =
            new MySqlConnection(
                connectionString
            );

        await connection.OpenAsync();

        await using MySqlTransaction transaction =
            await connection.BeginTransactionAsync();

        try
        {
            await DeleteAllAsync(
                connection,
                transaction,
                userId
            );

            await InsertAllAsync(
                connection,
                transaction,
                userId,
                records
            );

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task DeleteAllAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int userId)
    {
        const string query = @"
            DELETE FROM player_inventory
            WHERE user_id = @userId;
            ";

        await using MySqlCommand command =
            new MySqlCommand(
                query,
                connection,
                transaction
            );

        command.Parameters.AddWithValue(
            "@userId",
            userId
        );

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAllAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int userId,
        IReadOnlyCollection<PlayerInventoryRecord>
            records)
    {
        const string query = @"
            INSERT INTO player_inventory
            (
                user_id,
                item_id,
                quantity
            )
            VALUES
            (
                @userId,
                @itemId,
                @quantity
            );
            ";

        foreach (PlayerInventoryRecord record
                 in records)
        {
            if (record == null)
            {
                throw new InvalidOperationException(
                    "저장할 인벤토리 데이터에 " +
                    "null 항목이 포함되어 있습니다."
                );
            }

            if (record.ItemId <= 0 ||
                record.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    $"유효하지 않은 인벤토리 데이터입니다: " +
                    $"ItemId={record.ItemId}, " +
                    $"Quantity={record.Quantity}"
                );
            }

            await using MySqlCommand command =
                new MySqlCommand(
                    query,
                    connection,
                    transaction
                );

            command.Parameters.AddWithValue(
                "@userId",
                userId
            );

            command.Parameters.AddWithValue(
                "@itemId",
                record.ItemId
            );

            command.Parameters.AddWithValue(
                "@quantity",
                record.Quantity
            );

            await command.ExecuteNonQueryAsync();
        }
    }
}