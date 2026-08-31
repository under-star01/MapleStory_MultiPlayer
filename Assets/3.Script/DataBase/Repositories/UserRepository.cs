using System;
using System.Threading.Tasks;
using MySqlConnector;

public class UserRepository
{
    private readonly string connectionString;

    public UserRepository(
        string connectionString)
    {
        this.connectionString =
            connectionString;
    }

    /// <summary>
    /// 로그인 아이디로 유저 한 명을 조회합니다.
    /// 존재하지 않으면 null을 반환합니다.
    /// </summary>
    public async Task<UserRecord>
        FindByLoginIdAsync(
            string loginId)
    {
        const string query = @"
            SELECT
                user_id,
                login_id,
                password_hash,
                nickname,
                last_map_id,
                last_spawn_id,
                created_at,
                updated_at
            FROM users
            WHERE login_id = @loginId
            LIMIT 1;
            ";

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
            "@loginId",
            loginId
        );

        await using MySqlDataReader reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return CreateUserRecord(
            reader
        );
    }

    /// <summary>
    /// 동일한 로그인 아이디가 존재하는지 확인합니다.
    /// </summary>
    public async Task<bool> IsLoginIdTakenAsync(
        string loginId)
    {
        const string query = @"
            SELECT EXISTS
            (
                SELECT 1
                FROM users
                WHERE login_id = @loginId
            );
            ";

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
            "@loginId",
            loginId
        );

        object result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    /// <summary>
    /// 동일한 닉네임이 존재하는지 확인합니다.
    /// </summary>
    public async Task<bool> IsNicknameTakenAsync(
        string nickname)
    {
        const string query = @"
            SELECT EXISTS
            (
                SELECT 1
                FROM users
                WHERE nickname = @nickname
            );
            ";

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
            "@nickname",
            nickname
        );

        object result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) == 1;
    }

    /// <summary>
    /// 신규 유저를 생성하고 발급된 user_id를 반환합니다.
    /// passwordHash에는 이미 해시된 값이 전달되어야 합니다.
    /// </summary>
    public async Task<int> CreateUserAsync(
        string loginId,
        string passwordHash,
        string nickname)
    {
        const string query = @"
            INSERT INTO users
            (
                login_id,
                password_hash,
                nickname
            )
            VALUES
            (
                @loginId,
                @passwordHash,
                @nickname
            );

            SELECT LAST_INSERT_ID();
            ";

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
            "@loginId",
            loginId
        );

        command.Parameters.AddWithValue(
            "@passwordHash",
            passwordHash
        );

        command.Parameters.AddWithValue(
            "@nickname",
            nickname
        );

        object result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    /// <summary>
    /// 유저가 마지막으로 도착한 맵과
    /// 스폰 지점을 저장합니다.
    /// </summary>
    public async Task UpdateLastLocationAsync(
        int userId,
        string lastMapId,
        string lastSpawnId)
    {
        const string query = @"
            UPDATE users
            SET
                last_map_id = @lastMapId,
                last_spawn_id = @lastSpawnId
            WHERE user_id = @userId;
            ";

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

        command.Parameters.AddWithValue(
            "@lastMapId",
            lastMapId
        );

        command.Parameters.AddWithValue(
            "@lastSpawnId",
            lastSpawnId
        );

        int affectedRows =
            await command.ExecuteNonQueryAsync();

        if (affectedRows == 0)
        {
            throw new InvalidOperationException(
                $"마지막 위치를 저장할 유저를 " +
                $"찾지 못했습니다: {userId}"
            );
        }
    }

    private static UserRecord CreateUserRecord(
        MySqlDataReader reader)
    {
        return new UserRecord(
            reader.GetInt32("user_id"),
            reader.GetString("login_id"),
            reader.GetString("password_hash"),
            reader.GetString("nickname"),
            reader.GetString("last_map_id"),
            reader.GetString("last_spawn_id"),
            reader.GetDateTime("created_at"),
            reader.GetDateTime("updated_at")
        );
    }
}