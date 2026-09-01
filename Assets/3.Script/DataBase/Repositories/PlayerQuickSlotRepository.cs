using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;

public class PlayerQuickSlotRepository
{
    private readonly string connectionString;

    public PlayerQuickSlotRepository(
        string connectionString)
    {
        this.connectionString =
            connectionString ??
            throw new ArgumentNullException(
                nameof(connectionString)
            );
    }

    public async Task<List<PlayerQuickSlotRecord>>
        LoadByUserIdAsync(
            int userId)
    {
        const string query = @"
            SELECT
                quick_key,
                binding_type,
                binding_source,
                target_id
            FROM player_quickslots
            WHERE user_id = @userId
            ORDER BY quick_key;
            ";

        List<PlayerQuickSlotRecord> records =
            new();

        await using MySqlConnection connection =
            new(connectionString);

        await connection.OpenAsync();

        await using MySqlCommand command =
            new(query, connection);

        command.Parameters.AddWithValue(
            "@userId",
            userId
        );

        await using MySqlDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(
                new PlayerQuickSlotRecord(
                    reader.GetInt32("quick_key"),
                    reader.GetInt32("binding_type"),
                    reader.GetInt32("binding_source"),
                    reader.GetInt32("target_id")
                )
            );
        }

        return records;
    }

    public async Task SaveAllAsync(
        int userId,
        IReadOnlyCollection<PlayerQuickSlotRecord>
            records)
    {
        if (records == null)
        {
            throw new ArgumentNullException(
                nameof(records)
            );
        }

        await using MySqlConnection connection =
            new(connectionString);

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
            DELETE FROM player_quickslots
            WHERE user_id = @userId;
            ";

        await using MySqlCommand command =
            new(
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
        IReadOnlyCollection<PlayerQuickSlotRecord>
            records)
    {
        const string query = @"
            INSERT INTO player_quickslots
            (
                user_id,
                quick_key,
                binding_type,
                binding_source,
                target_id
            )
            VALUES
            (
                @userId,
                @quickKey,
                @bindingType,
                @bindingSource,
                @targetId
            );
            ";

        foreach (PlayerQuickSlotRecord record
                 in records)
        {
            if (record == null)
            {
                throw new InvalidOperationException(
                    "저장할 퀵슬롯 데이터에 " +
                    "null 항목이 포함되어 있습니다."
                );
            }

            await using MySqlCommand command =
                new(
                    query,
                    connection,
                    transaction
                );

            command.Parameters.AddWithValue(
                "@userId",
                userId
            );

            command.Parameters.AddWithValue(
                "@quickKey",
                record.QuickKey
            );

            command.Parameters.AddWithValue(
                "@bindingType",
                record.BindingType
            );

            command.Parameters.AddWithValue(
                "@bindingSource",
                record.BindingSource
            );

            command.Parameters.AddWithValue(
                "@targetId",
                record.TargetId
            );

            await command.ExecuteNonQueryAsync();
        }
    }
}