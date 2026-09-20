using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Verifies the barcode/QR ticket check-in and boarding workflow (Task 3.2).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CheckInTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task CheckIn_PaidTicket_SuccessfullyMovesToUsed()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        var customerId = _fixture.AccountIds[2];
        CurrentUser.Account = new UserSession(adminId, "admin_user", "Admin User", null, null, "Admin");

        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync(daysAhead: 2);
            var booking = await DatabaseHelper.BookTicketAsync(customerId, tripId, seatIds[0], "CheckIn Passenger", null);
            Assert.True(booking.Success, booking.Error);
            var ticket = booking.Ticket!;

            // Confirm payment so ticket is Paid (Đã thanh toán)
            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "BankTransfer"));
            Assert.True(payment.IsSuccess, payment.ErrorMessage);
            var paymentId = await _fixture.ScalarAsync<int>(
                $"SELECT PaymentId FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';");
            Assert.True((await PaymentService.ConfirmManualPaymentAsync(paymentId, adminId, "BANK-CHK", null)).Success);

            // Test look-up by code
            var lookedUp = await DatabaseHelper.GetTicketByCodeAsync(ticket.TicketCode);
            Assert.NotNull(lookedUp);
            Assert.Equal(ticket.TicketCode, lookedUp.TicketCode);
            Assert.Equal(TicketStatuses.Paid, lookedUp.Status);

            // Test AdminCheckInViewModel
            var vm = new AdminCheckInViewModel
            {
                TicketCodeInput = ticket.TicketCode
            };

            vm.SearchCommand.Execute(null);
            // Wait for search task
            for (var i = 0; i < 50 && vm.IsSearching; i++) await Task.Delay(50);

            Assert.True(vm.HasTicket);
            Assert.True(vm.CanCheckIn);
            Assert.Equal("HỢP LỆ — CHỜ SOÁT VÉ", vm.TicketStatusBadge);

            // Execute Check-in
            vm.CheckInCommand.Execute(null);
            for (var i = 0; i < 50 && vm.IsCheckingIn; i++) await Task.Delay(50);

            // Verify recent check-ins list contains this ticket
            Assert.NotEmpty(vm.RecentCheckIns);
            Assert.Equal(ticket.TicketCode, vm.RecentCheckIns[0].TicketCode);

            // Verify database state is 'Đã sử dụng'
            var statusInDb = await _fixture.ScalarAsync<string>(
                $"SELECT Status FROM dbo.Tickets WHERE TicketId = {ticket.TicketId};");
            Assert.Equal("Đã sử dụng", statusInDb);

            // Second check-in attempt must fail
            var (secondSuccess, secondError) = await TicketLifecycleService.CheckInTicketAsync(ticket.TicketId, adminId);
            Assert.False(secondSuccess);
            Assert.Contains("already been checked in", secondError);
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }

    [SkippableFact]
    public async Task CheckIn_UnpaidTicket_IsRefused()
    {
        _fixture.SkipIfUnavailable();
        var adminId = _fixture.AccountIds[0];
        var customerId = _fixture.AccountIds[3];
        CurrentUser.Account = new UserSession(adminId, "admin_user", "Admin User", null, null, "Admin");

        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync(daysAhead: 3);
            var booking = await DatabaseHelper.BookTicketAsync(customerId, tripId, seatIds[0], "Unpaid Passenger", null);
            Assert.True(booking.Success, booking.Error);
            var ticket = booking.Ticket!;

            var vm = new AdminCheckInViewModel
            {
                TicketCodeInput = ticket.TicketCode
            };

            vm.SearchCommand.Execute(null);
            for (var i = 0; i < 50 && vm.IsSearching; i++) await Task.Delay(50);

            Assert.True(vm.HasTicket);
            Assert.False(vm.CanCheckIn);
            Assert.Equal("CHƯA THANH TOÁN", vm.TicketStatusBadge);
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }
}
