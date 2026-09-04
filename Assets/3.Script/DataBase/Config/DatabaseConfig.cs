using System;
using MySqlConnector;

public static class DatabaseConfig
{
    private const string HostKey =
        "MAPLE_DB_HOST";

    private const string PortKey =
        "MAPLE_DB_PORT";

    private const string DatabaseKey =
        "MAPLE_DB_NAME";

    private const string UserKey =
        "MAPLE_DB_USER";

    private const string PasswordKey =
        "MAPLE_DB_PASSWORD";

    public static string CreateConnectionString()
    {
        string host =
            GetValueOrDefault(
                HostKey,
                "127.0.0.1"
            );

        uint port =
            GetPortOrDefault(
                PortKey,
                3306
            );

        string database =
            GetRequiredValue(
                DatabaseKey
            );

        string user =
            GetRequiredValue(
                UserKey
            );

        string password =
            GetRequiredValue(
                PasswordKey
            );

        MySqlConnectionStringBuilder builder =
            new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = port,
                Database = database,
                UserID = user,
                Password = password,

                SslMode = MySqlSslMode.None,

                ConnectionTimeout = 5
            };

        return builder.ConnectionString;
    }

    private static string GetRequiredValue(
        string key)
    {
        string value =
            Environment.GetEnvironmentVariable(
                key
            );

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"필수 환경 변수가 없습니다: {key}"
            );
        }

        return value;
    }

    private static string GetValueOrDefault(
        string key,
        string defaultValue)
    {
        string value =
            Environment.GetEnvironmentVariable(
                key
            );

        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value;
    }

    private static uint GetPortOrDefault(
        string key,
        uint defaultValue)
    {
        string value =
            Environment.GetEnvironmentVariable(
                key
            );

        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (uint.TryParse(
                value,
                out uint port))
        {
            return port;
        }

        throw new InvalidOperationException(
            $"DB 포트 환경 변수의 값이 올바르지 않습니다: " +
            $"{key}={value}"
        );
    }
}