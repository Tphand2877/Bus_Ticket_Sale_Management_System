using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.Data;

public static class DatabaseMigrator
{
    private const string HistoryTable = "__DatabaseMigrations";
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
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            return false;
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
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Migration script not found: {path}");
        var script = NormalizeScript(await File.ReadAllTextAsync(path, cancellationToken));
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            foreach (var batch in SplitBatches(script))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                await using var command = new SqlCommand(batch, connection, transaction);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var record = new SqlCommand("INSERT INTO dbo.__DatabaseMigrations (ScriptName) VALUES (@ScriptName);", connection, transaction);
            record.Parameters.Add("@ScriptName", SqlDbType.NVarChar, 255).Value = name;
            await record.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<bool> IsAppliedAsync(SqlConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT COUNT(1) FROM dbo.__DatabaseMigrations WHERE ScriptName=@ScriptName;", connection);
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
        await using var command = new SqlCommand("INSERT INTO dbo.__DatabaseMigrations (ScriptName) VALUES (@ScriptName);", connection);
        command.Parameters.Add("@ScriptName", SqlDbType.NVarChar, 255).Value = name;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeScript(string script)
    {
        script = Regex.Replace(script, @"^\s*USE\s+[^;\r\n]+;\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return Regex.Replace(script, @"^\s*IF\s+DB_ID\([^)]+\)\s+IS\s+NULL\s+CREATE\s+DATABASE\s+[^;]+;\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    private static IEnumerable<string> SplitBatches(string script) =>
        Regex.Split(script, @"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
}
