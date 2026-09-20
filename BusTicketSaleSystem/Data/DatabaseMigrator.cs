using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.Data;

public static class DatabaseMigrator
{
    private const string HistoryTable = "__DatabaseMigrations";

    /// <summary>Below this many days of future departures the schedule is topped up.</summary>
    private const int MinimumScheduleDaysAhead = 2;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string? LastError { get; private set; }

    public static async Task<bool> MigrateAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            LastError = null;
            await EnsureDatabaseCreatedAsync(cancellationToken);
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);
            await EnsureHistoryTableAsync(connection, cancellationToken);

            await ApplyMigrationAsync(connection, "001_InitialSchema", "Schema1.sql", skipWhenAccountsExist: true, cancellationToken);
            await ApplyMigrationAsync(connection, "002_TicketFields", "Migration_001.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "003_AdminRole", "Migration_002_Admin.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "004_Payments", "Schema_Payments.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "005_PerformanceIndexes", "Migration_003_PerformanceIndexes.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "006_PaymentExpiry", "Migration_004_PaymentExpiry.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "007_DevelopmentSeedData", "SeedData.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "008_SettlementHardening", "Migration_005_Settlement.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "009_MultiSeatTicket", "Migration_006_MultiSeatTicket.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "010_BusCompanyAndRegistration", "Migration_007_BusCompanyAndRegistration.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "011_SeatUniqueness", "Migration_008_SeatUniqueness.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "012_TriggerRollbackFix", "Migration_009_TriggerRollbackFix.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "013_LoginThrottling", "Migration_010_LoginThrottling.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "014_PaymentIntegrity", "Migration_011_PaymentIntegrity.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "015_AuditLog", "Migration_012_AuditLog.sql", skipWhenAccountsExist: false, cancellationToken);
            await ApplyMigrationAsync(connection, "016_SeatAvailabilityIndexes", "Migration_013_SeatAvailabilityIndexes.sql", skipWhenAccountsExist: false, cancellationToken);
            await EnsureStagePriceColumnsAsync(connection, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            LoggerService.LogError("Database migration failed.", ex);
            return false;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Keeps the departure schedule populated. Runs <c>Data/DevSeedTrips.sql</c>
    /// when the database has fewer than <see cref="MinimumScheduleDaysAhead"/>
    /// days of future departures left, which tops the schedule up to 14 days
    /// ahead. Safe to call on every startup and never throws.
    /// </summary>
    /// <returns>The number of trips added; zero when nothing was needed.</returns>
    public static async Task<int> EnsureUpcomingTripsAsync(CancellationToken cancellationToken = default)
    {
        const string horizonSql =
            "SELECT COALESCE(DATEDIFF(DAY, SYSUTCDATETIME(), MAX(DepartureTime)), -1) FROM dbo.Trips WHERE DepartureTime >= SYSUTCDATETIME();";
        const string countSql = "SELECT COUNT(*) FROM dbo.Trips WHERE DepartureTime >= SYSUTCDATETIME();";

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = DatabaseHelper.GetConnection();
            await connection.OpenAsync(cancellationToken);

            await DatabaseHelper.EnsureBusCompaniesSyncedAsync();

            var daysAhead = await ExecuteIntAsync(connection, horizonSql, cancellationToken);
            if (daysAhead >= MinimumScheduleDaysAhead)
                return 0;

            var before = await ExecuteIntAsync(connection, countSql, cancellationToken);
            var script = await LoadScriptAsync("DevSeedTrips.sql", cancellationToken);
            await ExecuteBatchesAsync(connection, transaction: null, script, "DevSeedTrips.sql", cancellationToken);
            var added = await ExecuteIntAsync(connection, countSql, cancellationToken) - before;

            LoggerService.LogInfo(
                $"Schedule top-up: only {daysAhead} day(s) of departures remained, added {added} trip(s) for the next two weeks.");
            return added;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggerService.LogError("Could not top up the upcoming trip schedule.", ex);
            return 0;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken)
    {
        var appBuilder = new SqlConnectionStringBuilder(DatabaseOptions.ConnectionString);
        if (string.IsNullOrWhiteSpace(appBuilder.InitialCatalog))
            throw new InvalidOperationException("Connection string must specify a database name.");

        var databaseName = appBuilder.InitialCatalog;
        await using var connection = new SqlConnection(DatabaseOptions.MasterConnectionString);
        await connection.OpenAsync(cancellationToken);
        var escapedName = databaseName.Replace("]", "]]", StringComparison.Ordinal);
        var sql = $"IF DB_ID(N'{databaseName.Replace("'", "''", StringComparison.Ordinal)}') IS NULL CREATE DATABASE [{escapedName}];";
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureHistoryTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
IF OBJECT_ID(N'dbo.__DatabaseMigrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__DatabaseMigrations
    (
        MigrationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DatabaseMigrations PRIMARY KEY,
        ScriptName NVARCHAR(255) NOT NULL CONSTRAINT UQ_DatabaseMigrations_ScriptName UNIQUE,
        AppliedAt DATETIME2 NOT NULL CONSTRAINT DF_DatabaseMigrations_AppliedAt DEFAULT SYSUTCDATETIME()
    );
END;";
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyMigrationAsync(SqlConnection connection, string name, string fileName, bool skipWhenAccountsExist, CancellationToken cancellationToken)
    {
        if (await IsAppliedAsync(connection, name, cancellationToken)) return;

        if (skipWhenAccountsExist && await TableExistsAsync(connection, "Accounts", cancellationToken))
        {
            await RecordMigrationAsync(connection, name, cancellationToken);
            LoggerService.LogWarning(
                $"Migration '{name}' ({fileName}) was recorded as applied WITHOUT executing because table dbo.Accounts already exists. " +
                $"The objects it would have created are therefore absent from this database: the CK_Tickets_Status / CK_Tickets_PaymentStatus " +
                $"check constraints, the UX_Tickets_Active_Trip_Seat anti-double-booking unique index and the TR_Tickets_ValidateSeatBus " +
                $"seat/bus trigger. Migration '008_SettlementHardening' ({nameof(DatabaseMigrator)} step 8) creates all of them idempotently.");
            return;
        }

        var script = await LoadScriptAsync(fileName, cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // The script and its history row commit together: a crash can never
            // record a migration that did not run, nor run one it forgot.
            await ExecuteBatchesAsync(connection, transaction, script, fileName, cancellationToken);

            await using var record = new SqlCommand($"INSERT INTO dbo.{HistoryTable} (ScriptName) VALUES (@ScriptName);", connection, transaction);
            record.Parameters.Add("@ScriptName", SqlDbType.NVarChar, 255).Value = name;
            await record.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LoggerService.LogInfo($"Applied migration '{name}' ({fileName}).");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<string> LoadScriptAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Migration script not found: {path}");
        return NormalizeScript(await File.ReadAllTextAsync(path, cancellationToken));
    }

    private static async Task ExecuteBatchesAsync(SqlConnection connection, SqlTransaction? transaction, string script, string fileName, CancellationToken cancellationToken)
    {
        var batchIndex = 0;
        foreach (var batch in SplitBatches(script))
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            batchIndex++;
            try
            {
                await using var command = transaction is null
                    ? new SqlCommand(batch, connection)
                    : new SqlCommand(batch, connection, transaction);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LoggerService.LogError($"Script '{fileName}' failed in batch {batchIndex}.", ex);
                throw;
            }
        }
    }

    private static async Task<int> ExecuteIntAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> IsAppliedAsync(SqlConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"SELECT COUNT(1) FROM dbo.{HistoryTable} WHERE ScriptName=@ScriptName;", connection);
        command.Parameters.Add("@ScriptName", SqlDbType.NVarChar, 255).Value = name;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@TableName, N'U') IS NULL THEN 0 ELSE 1 END;", connection);
        command.Parameters.Add("@TableName", SqlDbType.NVarChar, 256).Value = $"dbo.{tableName}";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task RecordMigrationAsync(SqlConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"INSERT INTO dbo.{HistoryTable} (ScriptName) VALUES (@ScriptName);", connection);
        command.Parameters.Add("@ScriptName", SqlDbType.NVarChar, 255).Value = name;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeScript(string script)
    {
        script = Regex.Replace(script, @"^\s*USE\s+[^;\r\n]+;\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return Regex.Replace(script, @"^\s*IF\s+DB_ID\([^)]+\)\s+IS\s+NULL\s+CREATE\s+DATABASE\s+[^;]+;\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }
    private static async Task EnsureStagePriceColumnsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
IF COL_LENGTH(N'dbo.Buses', N'StagePrice') IS NULL
    ALTER TABLE dbo.Buses ADD StagePrice DECIMAL(10,0) NULL;

IF COL_LENGTH(N'dbo.Trips', N'StagePrice') IS NULL
    ALTER TABLE dbo.Trips ADD StagePrice DECIMAL(10,0) NULL;
";
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<string> SplitBatches(string script) =>
        Regex.Split(script, @"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
}
