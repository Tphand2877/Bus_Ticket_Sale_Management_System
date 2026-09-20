using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Services;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// The expiry sweep releases lapsed seat holds. Its two updates must commit together
/// and its return value must mean something (AUD-C-010).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ExpirySweepTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task ExpirePendingPaymentsAsync_ReleasesTheHoldAndFailsItsPaymentTogether()
    {
        _fixture.SkipIfUnavailable();

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var booking = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[4], tripId, seatIds[0], "Expiry Sweep", null);
        Assert.True(booking.Success, booking.Error);
        var ticket = booking.Ticket!;

        var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"));
        Assert.True(payment.IsSuccess, payment.ErrorMessage);

        await _fixture.ExecuteAsync(
            $"UPDATE dbo.Tickets SET PaymentExpiresAt = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE TicketId = {ticket.TicketId};");

        var released = await PaymentService.ExpirePendingPaymentsAsync();

        Assert.True(released >= 1, "The sweep must report the number of seat holds it released.");
        Assert.Equal("Đã hủy", await _fixture.ScalarAsync<string>(
            $"SELECT Status FROM dbo.Tickets WHERE TicketId = {ticket.TicketId};"));
        Assert.Equal("Failed", await _fixture.ScalarAsync<string>(
            $"SELECT Status FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';"));

        // The seat is sellable again, which is the point of the sweep.
        Assert.Equal(0, await _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[0]));
        var rebook = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[5], tripId, seatIds[0], "Rebooked", null);
        Assert.True(rebook.Success, rebook.Error);
    }

    [SkippableFact]
    public async Task ExpirePendingPaymentsAsync_LeavesLiveHoldsAlone()
    {
        _fixture.SkipIfUnavailable();

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var booking = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[6], tripId, seatIds[1], "Live Hold", null);
        Assert.True(booking.Success, booking.Error);

        await PaymentService.ExpirePendingPaymentsAsync();

        Assert.Equal("Đã đặt", await _fixture.ScalarAsync<string>(
            $"SELECT Status FROM dbo.Tickets WHERE TicketId = {booking.Ticket!.TicketId};"));
        Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[1]));
    }
}
