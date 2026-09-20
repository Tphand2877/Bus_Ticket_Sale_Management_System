using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Privileged operations must leave a durable, attributable record, and the
/// settlement state machine must refuse a refund for a ticket already travelled.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AuditTrailTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task UpdateAccountRoleAsync_WritesAnAttributableAuditRow()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        var targetId = _fixture.AccountIds[1];
        CurrentUser.Account = new UserSession(adminId, "integration_admin", "Integration Admin", null, null, "Admin");
        try
        {
            var before = await CountAsync("AccountRoleChanged", targetId);

            var (success, error) = await AdminDatabaseService.UpdateAccountRoleAsync(targetId, "Admin");
            Assert.True(success, error);

            Assert.Equal(before + 1, await CountAsync("AccountRoleChanged", targetId));
            Assert.Equal(adminId, await _fixture.ScalarAsync<int>(
                $"SELECT TOP (1) ActorAccountId FROM dbo.AuditLog WHERE Action = N'AccountRoleChanged' AND TargetId = N'{targetId}' ORDER BY AuditId DESC;"));
        }
        finally
        {
            await _fixture.ExecuteAsync($"UPDATE dbo.Accounts SET Role = N'Customer' WHERE AccountId = {targetId};");
            CurrentUser.Account = null;
        }
    }

    /// <summary>
    /// AUD-C-006: ApproveRefundAsync locked and read the ticket state but never used
    /// it, so a travelled ticket could be refunded.
    /// </summary>
    [SkippableFact]
    public async Task ApproveRefundAsync_RefusesATicketAlreadyUsedForTravel()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        CurrentUser.Account = new UserSession(adminId, "integration_admin", "Integration Admin", null, null, "Admin");
        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync();
            var booking = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[2], tripId, seatIds[0], "Refund Guard", null);
            Assert.True(booking.Success, booking.Error);
            var ticket = booking.Ticket!;

            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"));
            Assert.True(payment.IsSuccess, payment.ErrorMessage);

            var paymentId = await _fixture.ScalarAsync<int>(
                $"SELECT PaymentId FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';");
            var confirm = await PaymentService.ConfirmManualPaymentAsync(paymentId, adminId, "REF-1", null);
            Assert.True(confirm.Success, confirm.Error);

            // The passenger travels, then a refund is requested against the same payment.
            await _fixture.ExecuteAsync($"UPDATE dbo.Tickets SET Status = N'Đã sử dụng', UsedAt = SYSUTCDATETIME() WHERE TicketId = {ticket.TicketId};");
            await _fixture.ExecuteAsync($"UPDATE dbo.Payments SET RefundRequestedAt = SYSUTCDATETIME() WHERE PaymentId = {paymentId};");

            var (success, error) = await PaymentService.ApproveRefundAsync(paymentId, adminId, null);

            Assert.False(success);
            Assert.Contains("already been used", error);
            Assert.Equal("Success", await _fixture.ScalarAsync<string>(
                $"SELECT Status FROM dbo.Payments WHERE PaymentId = {paymentId};"));
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }

    [SkippableFact]
    public async Task ConfirmManualPaymentAsync_WritesAnAuditRowThatCommitsWithTheSettlement()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        CurrentUser.Account = new UserSession(adminId, "integration_admin", "Integration Admin", null, null, "Admin");
        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync();
            var booking = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[3], tripId, seatIds[1], "Audit Settlement", null);
            Assert.True(booking.Success, booking.Error);
            var ticket = booking.Ticket!;

            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "Cash"));
            Assert.True(payment.IsSuccess, payment.ErrorMessage);
            var paymentId = await _fixture.ScalarAsync<int>(
                $"SELECT PaymentId FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';");

            var confirm = await PaymentService.ConfirmManualPaymentAsync(paymentId, adminId, "CASH-1", null);
            Assert.True(confirm.Success, confirm.Error);

            Assert.Equal(1, await _fixture.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = N'PaymentConfirmed' AND TargetId = N'{paymentId}';"));

            // A refused settlement must leave no audit row behind.
            var repeat = await PaymentService.ConfirmManualPaymentAsync(paymentId, adminId, "CASH-2", null);
            Assert.False(repeat.Success);
            Assert.Equal(1, await _fixture.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = N'PaymentConfirmed' AND TargetId = N'{paymentId}';"));
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }

    private Task<int> CountAsync(string action, int targetId) =>
        _fixture.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = N'{action}' AND TargetId = N'{targetId}';");
}
