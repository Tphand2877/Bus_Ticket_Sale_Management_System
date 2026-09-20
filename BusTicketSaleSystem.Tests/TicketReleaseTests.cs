using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Releasing a ticket: an unpaid hold is cancelled outright, a settled ticket goes
/// through the refund request. Both used to be implemented twice with different
/// preconditions (AUD-C-015).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class TicketReleaseTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task CancelTicketAsync_UnpaidHold_ReleasesTheSeatAndFailsItsPendingPayment()
    {
        _fixture.SkipIfUnavailable();
        var accountId = _fixture.AccountIds[7];

        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var booking = await DatabaseHelper.BookTicketAsync(accountId, tripId, seatIds[0], "Cancel Hold", null);
        Assert.True(booking.Success, booking.Error);
        var ticket = booking.Ticket!;

        var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"));
        Assert.True(payment.IsSuccess, payment.ErrorMessage);

        var (success, error) = await DatabaseHelper.CancelTicketAsync(ticket.TicketId, accountId);

        Assert.True(success, error);
        Assert.Equal("Đã hủy", await _fixture.ScalarAsync<string>($"SELECT Status FROM dbo.Tickets WHERE TicketId = {ticket.TicketId};"));
        Assert.Equal("Failed", await _fixture.ScalarAsync<string>($"SELECT Status FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';"));
        Assert.Equal(0, await _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[0]));
    }

    [SkippableFact]
    public async Task CancelTicketAsync_SettledTicket_RaisesARefundRequestInsteadOfCancellingSilently()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        var customerId = _fixture.AccountIds[8];
        CurrentUser.Account = new UserSession(adminId, "integration_admin", "Integration Admin", null, null, "Admin");
        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync();
            var booking = await DatabaseHelper.BookTicketAsync(customerId, tripId, seatIds[0], "Paid Release", null);
            Assert.True(booking.Success, booking.Error);
            var ticket = booking.Ticket!;

            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"));
            Assert.True(payment.IsSuccess, payment.ErrorMessage);
            var paymentId = await _fixture.ScalarAsync<int>($"SELECT PaymentId FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';");
            Assert.True((await PaymentService.ConfirmManualPaymentAsync(paymentId, adminId, "REF", null)).Success);

            var (success, error) = await DatabaseHelper.CancelTicketAsync(ticket.TicketId, customerId);

            Assert.True(success, error);
            // The refund path, not the cancel path: the payment stays settled and is
            // queued for an operator decision rather than being written off.
            Assert.Equal("Success", await _fixture.ScalarAsync<string>($"SELECT Status FROM dbo.Payments WHERE PaymentId = {paymentId};"));
            Assert.NotNull(await _fixture.ScalarAsync<string>(
                $"SELECT CONVERT(varchar(30), RefundRequestedAt, 126) FROM dbo.Payments WHERE PaymentId = {paymentId};"));
            Assert.Equal("Đã hủy", await _fixture.ScalarAsync<string>($"SELECT Status FROM dbo.Tickets WHERE TicketId = {ticket.TicketId};"));
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }
}
