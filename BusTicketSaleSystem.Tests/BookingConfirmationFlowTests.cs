using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Drives the booking→payment path with no WPF dialog. Before AUD-W3-01 this was
/// impossible: the ViewModel constructed <c>PaymentWindow</c> and called
/// <c>ShowDialog()</c>, so the revenue path needed a live STA Application.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class BookingConfirmationFlowTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [SkippableFact]
    public async Task CompleteBooking_BankTransfer_BooksTheSeatAndAsksTheDialogForTheRightAmount()
    {
        _fixture.SkipIfUnavailable();
        var accountId = _fixture.AccountIds[9];
        CurrentUser.Account = new UserSession(accountId, "flow_user", "Flow User", null, null, "Customer");
        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync();
            var trip = await TripAsync(tripId);
            var seats = (await DatabaseHelper.GetSeatsAsync(tripId)).Where(s => s.SeatId == seatIds[0]).ToArray();

            var navigation = new NavigationService();
            navigation.SetFactory(_ => new MyTicketsViewModel(navigation));
            var dialog = new FakePaymentDialogService(PaymentDialogOutcome.TransferSubmitted);

            var confirmation = new BookingConfirmationViewModel(
                navigation, dialog, trip, seats, "Flow Passenger", "0900000000");

            await ExecuteAsync(confirmation);

            var request = Assert.Single(dialog.Requests);
            Assert.True(request.IsBankTransfer);
            Assert.Equal(trip.Price * seats.Length, request.Amount);
            Assert.False(string.IsNullOrWhiteSpace(request.TicketCode));

            // The seat really is held, and the customer landed on their tickets.
            Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[0]));
            Assert.IsType<MyTicketsViewModel>(navigation.CurrentViewModel);
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }

    [SkippableFact]
    public async Task CompleteBooking_Cash_DoesNotOpenAPaymentDialog()
    {
        _fixture.SkipIfUnavailable();
        var accountId = _fixture.AccountIds[10];
        CurrentUser.Account = new UserSession(accountId, "cash_user", "Cash User", null, null, "Customer");
        try
        {
            var (tripId, seatIds) = await _fixture.CreateTripAsync();
            var trip = await TripAsync(tripId);
            var seats = (await DatabaseHelper.GetSeatsAsync(tripId)).Where(s => s.SeatId == seatIds[1]).ToArray();

            var navigation = new NavigationService();
            navigation.SetFactory(_ => new MyTicketsViewModel(navigation));
            var dialog = new FakePaymentDialogService();

            var confirmation = new BookingConfirmationViewModel(
                navigation, dialog, trip, seats, "Cash Passenger", "0900000001");
            confirmation.SelectedPaymentMethod = confirmation.PaymentMethods.Single(m => m.Code == "Cash");

            await ExecuteAsync(confirmation);

            Assert.Empty(dialog.Requests);
            Assert.Equal(1, await _fixture.CountActiveSeatHoldersAsync(tripId, seatIds[1]));
        }
        finally
        {
            CurrentUser.Account = null;
        }
    }

    /// <summary>Runs the command and waits for the asynchronous handler to finish.</summary>
    private static async Task ExecuteAsync(BookingConfirmationViewModel confirmation)
    {
        confirmation.CompleteBookingCommand.Execute(null);
        for (var i = 0; i < 100 && confirmation.IsBusy; i++) await Task.Delay(50);
        Assert.False(confirmation.IsBusy, "The booking command did not complete in time.");
    }

    private async Task<TripSearchResult> TripAsync(int tripId)
    {
        var departure = await _fixture.ScalarAsync<DateTime>($"SELECT DepartureTime FROM dbo.Trips WHERE TripId = {tripId};");
        var arrival = await _fixture.ScalarAsync<DateTime>($"SELECT ArrivalTime FROM dbo.Trips WHERE TripId = {tripId};");
        var price = await _fixture.ScalarAsync<decimal>($"SELECT Price FROM dbo.Trips WHERE TripId = {tripId};");
        return new TripSearchResult(tripId, "Hà Nội", "Hải Phòng", "Ghế ngồi", departure, arrival, price, 40);
    }
}
