using Npgsql;

namespace SchoolAdmission.Api.Tests.Infrastructure;

internal sealed class PostgresFixture : IAsyncDisposable
{
    private readonly string _adminConnectionString;

    private PostgresFixture(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public static async Task<PostgresFixture> CreateAsync()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("TEST_DATABASE_CONNECTION must point to the disposable PostgreSQL test server.");

        var configuredBuilder = new NpgsqlConnectionStringBuilder(configured);
        var adminBuilder = new NpgsqlConnectionStringBuilder(configured)
        {
            Database = "postgres",
            Pooling = false
        };
        var databaseName = $"admission_test_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        configuredBuilder.Database = databaseName;
        return new PostgresFixture(adminBuilder.ConnectionString, databaseName, configuredBuilder.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (!DatabaseName.StartsWith("admission_test_", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop a database outside the admission_test_ prefix.");

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid()",
            admin);
        terminate.Parameters.AddWithValue("database", DatabaseName);
        await terminate.ExecuteNonQueryAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{DatabaseName}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }
}
