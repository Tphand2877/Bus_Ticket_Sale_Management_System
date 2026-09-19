using System.Data;
using BusTicketSaleSystem.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Proves the anti-double-sell invariant against a real SQL Server database with
/// separate connections per caller. Deliberately not InMemory/SQLite: the invariant
/// depends on server-side locking and constraints.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConcurrencyBookingTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task BookTicketAsync_AllowsOnlyOneConcurrentReservation()
    {
        _fixture.SkipIfUnavailable();

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var seatId = seatIds[0];

        var results = await Task.WhenAll(_fixture.AccountIds.Select(accountId =>
            DatabaseHelper.BookTicketAsync(accountId, tripId, seatId, $"Concurrency Test {accountId}", null)));

        Assert.Equal(1, results.Count(result => result.Success));
        Assert.Equal(_fixture.AccountIds.Count - 1, results.Count(result => !result.Success));
        Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatId));

        // Every loser must receive the controlled message, never a raw failure.
        foreach (var failure in results.Where(result => !result.Success))
            Assert.False(string.IsNullOrWhiteSpace(failure.Error));
    }

    [SkippableFact]
    public async Task BookTicketsAsync_MultiSeat_AllowsOnlyOneConcurrentReservationPerSeat()
    {
        _fixture.SkipIfUnavailable();

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        // Two overlapping three-seat requests sharing exactly one seat.
        var first = new[] { seatIds[0], seatIds[1], seatIds[2] };
        var second = new[] { seatIds[2], seatIds[3], seatIds[4] };

        var results = await Task.WhenAll(
            DatabaseHelper.BookTicketsAsync(_fixture.AccountIds[0], tripId, first, "Multi A", null),
            DatabaseHelper.BookTicketsAsync(_fixture.AccountIds[1], tripId, second, "Multi B", null));

        Assert.Equal(1, results.Count(result => result.Success));
        Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[2]));

        // The losing request must not leave a partial reservation behind.
        var strandedSeats = await Task.WhenAll(
            _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[0]),
            _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[4]));
        Assert.Equal(1, strandedSeats.Sum());
    }

    /// <summary>
    /// AUD-W1-04: when the seat is taken between the guard and the insert, the caller
    /// must receive the controlled message. Previously the trigger's
    /// ROLLBACK TRANSACTION made the ambient rollback throw InvalidOperationException.
    /// </summary>
    [SkippableFact]
    public async Task BookTicketsAsync_SeatTakenByAnotherWriter_ReturnsControlledFailure()
    {
        _fixture.SkipIfUnavailable();

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var seatId = seatIds[0];

        var first = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[0], tripId, seatId, "Holder", null);
        Assert.True(first.Success, first.Error);

        var second = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[1], tripId, seatId, "Latecomer", null);

        Assert.False(second.Success);
        Assert.Null(second.Ticket);
        Assert.Contains("has just been taken", second.Error);
        Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatId));
    }

    /// <summary>
    /// The invariant must survive the UI, the application layer and the isolation level
    /// being bypassed: two raw SQL sessions inserting the same seat must not both commit.
    /// Before AUD-W1-03 this failed under READ_COMMITTED_SNAPSHOT, which is a routine
    /// database setting.
    /// </summary>
    [SkippableFact]
    public async Task RawSqlInsert_CannotSellTheSameSeatTwice_UnderRowVersioning()
    {
        _fixture.SkipIfUnavailable();

        await _fixture.ExecuteAsync($"ALTER DATABASE [{SqlServerFixture.DatabaseName}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;");
        try
        {
            // The test is only meaningful if row versioning really is active.
            Assert.Equal(1, await _fixture.ScalarAsync<int>(
                $"SELECT CAST(is_read_committed_snapshot_on AS int) FROM sys.databases WHERE name = N'{SqlServerFixture.DatabaseName}';"));

            var (tripId, seatIds) = await _fixture.CreateTripAsync();
            var seatId = seatIds[0];

            // Both sessions resume at the same server clock instant, so each one's
            // guard/trigger evaluates before the other commits. Without a declarative
            // constraint this is exactly the window in which both inserts succeed.
            var syncPoint = await _fixture.ScalarAsync<string>(
                "SELECT CONVERT(varchar(8), DATEADD(SECOND, 10, GETDATE()), 108);");

            var outcomes = await Task.WhenAll(
                InsertSeatDirectlyAsync(tripId, seatId, _fixture.AccountIds[0], "RAW-A", syncPoint!),
                InsertSeatDirectlyAsync(tripId, seatId, _fixture.AccountIds[1], "RAW-B", syncPoint!));

            var rejections = outcomes.Where(outcome => outcome is not null).ToArray();
            Assert.Equal(1, outcomes.Count(outcome => outcome is null));
            Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatId));

            // The rejection must be an engine-level refusal, not an incidental failure:
            // 2601/2627 = the uniqueness object rejected it, 1205 = the engine chose this
            // session as the deadlock victim while resolving the same contention,
            // 51002 = the belt-and-braces trigger got there first.
            Assert.All(rejections, rejection =>
                Assert.True(rejection is 2601 or 2627 or 1205 or 51002,
                    $"Unexpected rejection reason: SQL error {rejection}."));
        }
        finally
        {
            await _fixture.ExecuteAsync($"ALTER DATABASE [{SqlServerFixture.DatabaseName}] SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;");
        }
    }

    /// <summary>
    /// Reproduces <see cref="DatabaseHelper.BookTicketsAsync"/>'s statements without its
    /// Serializable transaction, so only server-side protection can prevent the duplicate.
    /// </summary>
    private async Task<int?> InsertSeatDirectlyAsync(int tripId, int seatId, int accountId, string code, string syncPoint)
    {
        var sql = $@"
SET NOCOUNT ON;
BEGIN TRANSACTION;
INSERT INTO dbo.Tickets (TicketCode, TripId, AccountId, PassengerName, Price, SeatCount, Status, PaymentStatus, PaymentExpiresAt)
SELECT @Code, tr.TripId, @AccountId, N'Raw', tr.Price, 1, N'Đã đặt', N'Chưa thanh toán', DATEADD(MINUTE, 15, SYSUTCDATETIME())
FROM dbo.Trips tr WHERE tr.TripId = @TripId;
DECLARE @TicketId INT = SCOPE_IDENTITY();
WAITFOR TIME '{syncPoint}';
INSERT INTO dbo.TicketSeats (TicketId, SeatId) VALUES (@TicketId, @SeatId);
COMMIT;";

        await using var connection = _fixture.OpenTestConnection();
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Code", SqlDbType.NVarChar, 30).Value = $"{code}-{Guid.NewGuid():N}"[..30];
        command.Parameters.Add("@TripId", SqlDbType.Int).Value = tripId;
        command.Parameters.Add("@SeatId", SqlDbType.Int).Value = seatId;
        command.Parameters.Add("@AccountId", SqlDbType.Int).Value = accountId;

        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqlException ex)
        {
            // A constraint or trigger refused the duplicate: the controlled outcome.
            return ex.Number;
        }
    }

    /// <summary>
    /// Guards the constraint itself: a future migration must not silently drop the
    /// database-level seat uniqueness the way Migration_006 dropped its predecessor.
    /// </summary>
    [SkippableFact]
    public async Task Database_EnforcesSeatUniquenessDeclaratively()
    {
        _fixture.SkipIfUnavailable();

        var uniqueObjects = await _fixture.ScalarAsync<int>(@"
SELECT COUNT(*) FROM sys.indexes
WHERE is_unique = 1
  AND object_id = OBJECT_ID(N'dbo.UX_ActiveTicketSeats')
  AND name = N'UX_ActiveTicketSeats_Trip_Seat';");

        Assert.True(uniqueObjects > 0,
            "The (TripId, SeatId) uniqueness object is missing: the anti-double-sell invariant is no longer enforced by the database.");
    }
}
