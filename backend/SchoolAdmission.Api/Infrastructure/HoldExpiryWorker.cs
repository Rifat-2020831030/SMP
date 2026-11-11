using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SchoolAdmission.Api.Infrastructure;

public sealed class HoldExpiryWorker(IConfiguration configuration, ILogger<HoldExpiryWorker> logger) : BackgroundService
{
    private const long AdvisoryLockId = 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredHoldsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error sweeping expired holds");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepExpiredHoldsAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var lockCmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@lockId)", connection))
        {
            lockCmd.Parameters.AddWithValue("lockId", AdvisoryLockId);
            var acquired = (bool?)await lockCmd.ExecuteScalarAsync(cancellationToken);
            if (acquired != true)
            {
                return;
            }
        }

        try
        {
            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            const string updateClassesSql = """
                UPDATE classes SET held_seats = held_seats - expired_count FROM (
                  SELECT class_id, COUNT(*) as expired_count FROM holds
                  WHERE status = 'HELD' AND expires_at < NOW()
                  GROUP BY class_id
                ) expired WHERE classes.id = expired.class_id
                """;

            const string updateHoldsSql = """
                UPDATE holds SET status = 'EXPIRED' WHERE status = 'HELD' AND expires_at < NOW()
                """;

            await using (var updateClassesCmd = new NpgsqlCommand(updateClassesSql, connection, tx))
            {
                await updateClassesCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            int holdsUpdated;
            await using (var updateHoldsCmd = new NpgsqlCommand(updateHoldsSql, connection, tx))
            {
                holdsUpdated = await updateHoldsCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (holdsUpdated > 0)
            {
                await using var notifyCmd = new NpgsqlCommand("NOTIFY admission_classes", connection, tx);
                await notifyCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            try
            {
                await using var unlockCmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@lockId)", connection);
                unlockCmd.Parameters.AddWithValue("lockId", AdvisoryLockId);
                await unlockCmd.ExecuteScalarAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to release advisory lock {LockId}", AdvisoryLockId);
            }
        }
    }
}
