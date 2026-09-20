using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Data-integrity behaviour of the payment and admin write paths under concurrency
/// and partial failure.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AdminDataIntegrityTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    /// <summary>
    /// AUD-C-002: the "a payment already exists" guard ran in autocommit, so its
    /// UPDLOCK was released before the insert and two concurrent calls could both
    /// create a Pending payment for one ticket.
    /// </summary>
    [SkippableFact]
    public async Task CreatePaymentAsync_ConcurrentCallsCreateExactlyOnePayment()
    {
        _fixture.SkipIfUnavailable();

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var booking = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[0], tripId, seatIds[0], "Duplicate Payment", null);
        Assert.True(booking.Success, booking.Error);
        var ticket = booking.Ticket!;

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ =>
                PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"))));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.All(results.Where(result => !result.IsSuccess),
            failure => Assert.False(string.IsNullOrWhiteSpace(failure.ErrorMessage)));

        var liveRows = await _fixture.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM dbo.Payments WHERE TicketId = {ticket.TicketId} AND Status IN (N'Pending', N'Success');");
        Assert.Equal(1, liveRows);
    }

    /// <summary>
    /// AUD-B-004: the delete batch ran without SET XACT_ABORT ON, so a failure on the
    /// second DELETE committed the first and still reported success — a bus surviving
    /// with its seat map destroyed.
    /// </summary>
    [SkippableFact]
    public async Task DeleteBusAsync_WhenTheBusDeleteFails_LeavesTheSeatMapIntact()
    {
        _fixture.SkipIfUnavailable();
        CurrentUser.Account = new UserSession(_fixture.AccountIds[0], "integration_admin", "Integration Admin", null, null, "Admin");
        try
        {
            await _fixture.ExecuteAsync(@"
INSERT INTO dbo.Buses (LicensePlate, SeatCount, BusType) VALUES (N'99Z-000.11', 8, N'Integration');
DECLARE @BusId INT = SCOPE_IDENTITY();
INSERT INTO dbo.Seats (BusId, SeatNumber)
SELECT @BusId, CONVERT(NVARCHAR(10), n.Number)
FROM (SELECT TOP (8) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Number FROM sys.objects) n;");

            var busId = await _fixture.ScalarAsync<int>("SELECT BusId FROM dbo.Buses WHERE LicensePlate = N'99Z-000.11';");
            var seatsBefore = await _fixture.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.Seats WHERE BusId = {busId};");
            Assert.Equal(8, seatsBefore);

            // A second table referencing the bus makes the Buses delete fail while the
            // Seats delete has already run — the exact shape of the original defect.
            await _fixture.ExecuteAsync($@"
CREATE TABLE dbo.IntegrationBusRef (BusId INT NOT NULL CONSTRAINT FK_IntegrationBusRef REFERENCES dbo.Buses(BusId));
INSERT INTO dbo.IntegrationBusRef (BusId) VALUES ({busId});");

            try
            {
                var (success, error) = await AdminDatabaseService.DeleteBusAsync(busId);

                Assert.False(success);
                Assert.False(string.IsNullOrWhiteSpace(error));
                Assert.Equal(8, await _fixture.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.Seats WHERE BusId = {busId};"));
                Assert.Equal(1, await _fixture.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.Buses WHERE BusId = {busId};"));
            }
            finally
            {
                await _fixture.ExecuteAsync("DROP TABLE dbo.IntegrationBusRef;");
            }
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }

    /// <summary>
    /// AUD-B-005: the overlap rule lived in an unlocked NOT EXISTS, so two admins
    /// could both schedule one bus onto the same slot.
    /// </summary>
    [SkippableFact]
    public async Task AddTripAsync_ConcurrentIdenticalSlotsInsertExactlyOneTrip()
    {
        _fixture.SkipIfUnavailable();
        CurrentUser.Account = new UserSession(_fixture.AccountIds[0], "integration_admin", "Integration Admin", null, null, "Admin");
        var previousStation = LocationService.Instance.MachineLocation;
        try
        {
            var routeId = await _fixture.ScalarAsync<int>("SELECT TOP (1) RouteId FROM dbo.Routes ORDER BY RouteId;");
            var origin = await _fixture.ScalarAsync<string>($"SELECT Origin FROM dbo.Routes WHERE RouteId = {routeId};");
            // The data layer now refuses routes outside the workstation's station, so
            // the fixture's station is pinned to the route under test.
            LocationService.Instance.SetFixedMachineLocation(origin!);

            var busId = await _fixture.ScalarAsync<int>("SELECT TOP (1) BusId FROM dbo.Buses ORDER BY BusId;");
            var departure = DateTime.UtcNow.AddDays(30).Date.AddHours(6);
            var arrival = departure.AddHours(4);

            var results = await Task.WhenAll(
                Enumerable.Range(0, 5).Select(_ =>
                    AdminDatabaseService.AddTripAsync(routeId, busId, departure, arrival, 199000m)));

            Assert.Equal(1, results.Count(result => result.Success));

            var inserted = await _fixture.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.Trips WHERE BusId = {busId} AND DepartureTime = '{departure:yyyy-MM-ddTHH:mm:ss.fff}';");
            Assert.Equal(1, inserted);
        }
        finally
        {
            LocationService.Instance.SetFixedMachineLocation(previousStation);
            CurrentUser.Account = null;
        }
    }

    /// <summary>
    /// AUD-D-005: the per-station rule lived only in two ViewModels, so any other
    /// caller — or a direct data-layer call — could administer a foreign station.
    /// </summary>
    [SkippableFact]
    public async Task AddTripAsync_RefusesARouteOutsideTheStationScope()
    {
        _fixture.SkipIfUnavailable();
        CurrentUser.Account = new UserSession(_fixture.AccountIds[0], "integration_admin", "Integration Admin", null, null, "Admin");
        var previousStation = LocationService.Instance.MachineLocation;
        try
        {
            var routeId = await _fixture.ScalarAsync<int>("SELECT TOP (1) RouteId FROM dbo.Routes ORDER BY RouteId;");
            var busId = await _fixture.ScalarAsync<int>("SELECT TOP (1) BusId FROM dbo.Buses ORDER BY BusId;");
            LocationService.Instance.SetFixedMachineLocation("Cà Mau");

            var departure = DateTime.UtcNow.AddDays(45).Date.AddHours(9);
            var (success, error) = await AdminDatabaseService.AddTripAsync(routeId, busId, departure, departure.AddHours(3), 150000m);

            Assert.False(success);
            Assert.Contains("Cà Mau", error);
            Assert.Equal(0, await _fixture.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.Trips WHERE BusId = {busId} AND DepartureTime = '{departure:yyyy-MM-ddTHH:mm:ss.fff}';"));
        }
        finally
        {
            LocationService.Instance.SetFixedMachineLocation(previousStation);
            CurrentUser.Account = null;
        }
    }

    /// <summary>Route creation must obey the same station rule as trip scheduling.</summary>
    [SkippableFact]
    public async Task AddRouteAsync_RefusesARouteOutsideTheStationScope()
    {
        _fixture.SkipIfUnavailable();
        CurrentUser.Account = new UserSession(_fixture.AccountIds[0], "integration_admin", "Integration Admin", null, null, "Admin");
        var previousStation = LocationService.Instance.MachineLocation;
        try
        {
            LocationService.Instance.SetFixedMachineLocation("Cà Mau");

            var (success, error) = await AdminDatabaseService.AddRouteAsync("Hà Nội", "Hải Phòng", 120m);

            Assert.False(success);
            Assert.Contains("Cà Mau", error);
        }
        finally
        {
            LocationService.Instance.SetFixedMachineLocation(previousStation);
            CurrentUser.Account = null;
        }
    }
}
