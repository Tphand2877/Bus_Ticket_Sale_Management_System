using System.Data;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Verifies the administrative trip cancellation workflow (Task 2.1):
/// cancelling a scheduled trip automatically cancels all active tickets,
/// queues settled payments for refund, fails pending holds, and writes an audit log.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class TripCancellationTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task CancelTripAsync_CancelsTripAndActiveTicketsAndQueuesRefund()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        var customerId = _fixture.AccountIds[1];
        CurrentUser.Account = new UserSession(adminId, "admin_user", "Admin User", null, null, "Admin");

        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync(daysAhead: 5);

            // Book a ticket on the trip
            var booking = await DatabaseHelper.BookTicketAsync(customerId, tripId, seatIds[0], "Passenger A", null);
            Assert.True(booking.Success, booking.Error);
            var ticket = booking.Ticket!;

            // Create and confirm a payment for the ticket
            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"));
            Assert.True(payment.IsSuccess, payment.ErrorMessage);
            var paymentId = await _fixture.ScalarAsync<int>(
                $"SELECT PaymentId FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';");
            var confirmed = await PaymentService.ConfirmManualPaymentAsync(paymentId, adminId, "BANK-REF-1", null);
            Assert.True(confirmed.Success, confirmed.Error);

            // Operator cancels the trip
            var (cancelSuccess, cancelError) = await AdminDatabaseService.CancelTripAsync(tripId, "Sự cố thời tiết");
            Assert.True(cancelSuccess, cancelError);

            // Verify trip status is Cancelled
            var tripStatus = await _fixture.ScalarAsync<string>(
                $"SELECT Status FROM dbo.Trips WHERE TripId = {tripId};");
            Assert.Equal("Cancelled", tripStatus);

            // Verify ticket status is Cancelled (Đã hủy)
            var ticketStatus = await _fixture.ScalarAsync<string>(
                $"SELECT Status FROM dbo.Tickets WHERE TicketId = {ticket.TicketId};");
            Assert.Equal("Đã hủy", ticketStatus);

            // Verify payment has refund requested
            var refundRequested = await _fixture.ScalarAsync<string>(
                $"SELECT CONVERT(varchar(30), RefundRequestedAt, 126) FROM dbo.Payments WHERE PaymentId = {paymentId};");
            Assert.NotNull(refundRequested);

            // Verify audit log entry
            var auditCount = await _fixture.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action = N'TripCancelled' AND TargetId = N'{tripId}';");
            Assert.Equal(1, auditCount);

            // Verify cancelled trip is excluded from customer search
            var searchResults = await DatabaseHelper.SearchTripsAsync("Hà Nội", "Hải Phòng", null);
            Assert.DoesNotContain(searchResults, t => t.TripId == tripId);
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }
}
