using System.Data;
using BusTicketSaleSystem.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Provisions the dedicated integration-test database declared in AGENTS.md
/// (<c>BusTicketSaleSystem_Tests</c>) and points the application configuration at it
/// for the lifetime of the test run.
/// </summary>
/// <remarks>
/// <para>
/// Only the declared test database is created, migrated, reset or dropped. The
/// development database is never opened by these tests: the two connection-string
/// environment variables are set before any <see cref="DatabaseHelper"/> call and
/// <see cref="AppliesToTestDatabaseOnly"/> asserts the target catalog on every use.
/// </para>
/// <para>
/// When SQL Server is unreachable the fixture records the reason instead of throwing,
/// and the tests that need it skip with that reason.
/// </para>
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string DatabaseName = "BusTicketSaleSystem_Tests";

    /// <summary>
    /// Server hosting the test database. Defaults to the developer's local instance;
    /// CI overrides it with <c>(localdb)\MSSQLLocalDB</c> via BUS_TICKET_TEST_SERVER.
    /// </summary>
    private static string Server =>
        Environment.GetEnvironmentVariable("BUS_TICKET_TEST_SERVER") is { Length: > 0 } server
            ? server
            : "localhost";

    private static string ServerConnectionString =>
        $"Server={Server};Database=master;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=15;";

    private static string TestConnectionString =>
        $"Server={Server};Database={DatabaseName};Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=15;";

    /// <summary>Null when the fixture is usable; otherwise the reason the tests must skip.</summary>
    public string? SkipReason { get; private set; }

    /// <summary>Trip seeded on a bus whose seats are all free.</summary>
    public int TripId { get; private set; }

    /// <summary>Seats belonging to <see cref="TripId"/>'s bus, ordered by seat id.</summary>
    public IReadOnlyList<int> SeatIds { get; private set; } = [];

    /// <summary>Customer accounts available to concurrency tests.</summary>
    public IReadOnlyList<int> AccountIds { get; private set; } = [];

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("BUS_TICKET_CONNECTION_STRING", TestConnectionString);
        Environment.SetEnvironmentVariable("BUS_TICKET_MASTER_CONNECTION_STRING", ServerConnectionString);

        AppliesToTestDatabaseOnly();

        try
        {
            await ResetDatabaseAsync();
        }
        catch (SqlException ex)
        {
            SkipReason = $"SQL Server on localhost is not reachable: {ex.Message}";
            return;
        }

        if (!await DatabaseMigrator.MigrateAsync())
        {
            SkipReason = $"The test database could not be migrated: {DatabaseMigrator.LastError}";
            return;
        }

        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Skips the calling test when the fixture could not be provisioned.</summary>
    public void SkipIfUnavailable() => Skip.If(SkipReason is not null, SkipReason ?? string.Empty);

    /// <summary>
    /// Creates an additional trip on a bus with no other tickets, so tests never
    /// contend for the same seats. Returns the trip and its seats.
    /// </summary>
    public async Task<(int TripId, IReadOnlyList<int> SeatIds)> CreateTripAsync(int daysAhead = 3)
    {
        const string sql = @"
SET NOCOUNT ON;
DECLARE @RouteId INT = (SELECT TOP (1) RouteId FROM dbo.Routes ORDER BY RouteId);
DECLARE @BusId   INT = (SELECT TOP (1) BusId FROM dbo.Buses ORDER BY BusId);
INSERT INTO dbo.Trips (RouteId, BusId, DepartureTime, ArrivalTime, Price)
VALUES (@RouteId, @BusId,
        DATEADD(DAY, @DaysAhead, SYSUTCDATETIME()),
        DATEADD(HOUR, 4, DATEADD(DAY, @DaysAhead, SYSUTCDATETIME())),
        250000);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        await using var connection = OpenTestConnection();
        await connection.OpenAsync();

        int tripId;
        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.Add("@DaysAhead", SqlDbType.Int).Value = daysAhead;
            tripId = (int)(await command.ExecuteScalarAsync())!;
        }

        const string seatSql = @"
SELECT s.SeatId FROM dbo.Seats s
JOIN dbo.Trips tr ON tr.BusId = s.BusId
WHERE tr.TripId = @TripId ORDER BY s.SeatId;";

        var seats = new List<int>();
        await using (var seatCommand = new SqlCommand(seatSql, connection))
        {
            seatCommand.Parameters.Add("@TripId", SqlDbType.Int).Value = tripId;
            await using var reader = await seatCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync()) seats.Add(reader.GetInt32(0));
        }

        return (tripId, seats);
    }

    /// <summary>Counts non-cancelled tickets holding <paramref name="seatId"/> on <paramref name="tripId"/>.</summary>
    public async Task<int> CountActiveSeatHoldersAsync(int tripId, int seatId)
    {
        const string sql = @"
SELECT COUNT(*) FROM dbo.TicketSeats ts
JOIN dbo.Tickets t ON t.TicketId = ts.TicketId
WHERE t.TripId = @TripId AND ts.SeatId = @SeatId
  AND t.Status IN (N'Đã đặt', N'Đã thanh toán', N'Đã sử dụng');";

        await using var connection = OpenTestConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@TripId", SqlDbType.Int).Value = tripId;
        command.Parameters.Add("@SeatId", SqlDbType.Int).Value = seatId;
        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Runs a statement against the test database. Test-database only.</summary>
    public async Task ExecuteAsync(string sql)
    {
        AppliesToTestDatabaseOnly();
        await using var connection = OpenTestConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Reads a single scalar from the test database.</summary>
    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = OpenTestConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, typeof(T));
    }

    public SqlConnection OpenTestConnection() => new(TestConnectionString);

    /// <summary>
    /// Hard guard against ever touching the development database: every destructive
    /// helper calls this first.
    /// </summary>
    private static void AppliesToTestDatabaseOnly()
    {
        var configured = new SqlConnectionStringBuilder(DatabaseOptions.ConnectionString).InitialCatalog;
        if (!string.Equals(configured, DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration tests may only run against '{DatabaseName}', but the configured catalog is '{configured}'.");
        }
    }

    private static async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(ServerConnectionString);
        await connection.OpenAsync();

        const string sql = $@"
IF DB_ID(N'{DatabaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{DatabaseName}];
END;
CREATE DATABASE [{DatabaseName}];";

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        // The pool still holds connections to the dropped database.
        SqlConnection.ClearAllPools();
    }

    private async Task SeedAsync()
    {
        var accounts = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            var (created, error) = await DatabaseHelper.CreateAccountAsync(
                $"conc_user_{i}", "IntegrationPass123", $"Concurrency User {i}", null, $"conc{i}@test.local");
            Assert.True(created, error);
        }

        const string accountSql = "SELECT AccountId FROM dbo.Accounts WHERE Username LIKE N'conc\\_user\\_%' ESCAPE N'\\' ORDER BY AccountId;";
        await using (var connection = OpenTestConnection())
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(accountSql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) accounts.Add(reader.GetInt32(0));
        }

        AccountIds = accounts;

        var (tripId, seatIds) = await CreateTripAsync();
        TripId = tripId;
        SeatIds = seatIds;
    }
}

/// <summary>Shares one provisioned test database across the integration test classes.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
