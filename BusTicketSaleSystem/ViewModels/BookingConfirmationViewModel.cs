using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.Views;
using System.Windows;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

/// <summary>A payment method offered on the confirmation screen; disabled options explain themselves.</summary>
public sealed record PaymentOption(string Code, string Display, bool IsEnabled, string? DisabledReason);

public sealed class BookingConfirmationViewModel : ViewModelBase
{
    private static readonly TimeSpan BankTransferHold = TimeSpan.FromMinutes(15);

    private readonly INavigationService _navigationService;

    public TripSearchResult Trip { get; }
    public IReadOnlyList<SeatInfo> Seats { get; }
    public SeatInfo Seat => Seats[0];
    public string PassengerName { get; }
    public string PassengerPhone { get; }
    public IReadOnlyList<PaymentOption> PaymentMethods { get; }
    private PaymentOption _selectedPaymentMethod;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public PaymentOption SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (!SetProperty(ref _selectedPaymentMethod, value)) return;
            OnPropertyChanged(nameof(SelectedMethodWarning));
        }
    }

    /// <summary>
    /// Explains why the selected method cannot be used, or — when everything selectable is fine —
    /// why one of the greyed-out methods is unavailable. Empty when every method works.
    /// </summary>
    public string SelectedMethodWarning
    {
        get
        {
            if (_selectedPaymentMethod is { IsEnabled: false } selected)
                return selected.DisabledReason ?? "This payment method is unavailable.";
            return PaymentMethods.FirstOrDefault(option => !option.IsEnabled)?.DisabledReason ?? string.Empty;
        }
    }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public int SeatCount => Seats.Count;
    public string SelectedSeatNumbers => string.Join(", ", Seats.Select(seat => seat.SeatNumber));
    public string PriceDisplay => $"{Trip.Price * SeatCount:N0} VND";
    public ICommand CompleteBookingCommand { get; }
    public ICommand BackToSeatsCommand { get; }

    public BookingConfirmationViewModel(INavigationService navigationService, TripSearchResult trip, SeatInfo seat, string passengerName, string passengerPhone)
        : this(navigationService, trip, new[] { seat }, passengerName, passengerPhone)
    {
    }

    public BookingConfirmationViewModel(INavigationService navigationService, TripSearchResult trip, IReadOnlyList<SeatInfo> seats, string passengerName, string passengerPhone)
    {
        if (seats.Count == 0) throw new ArgumentException("At least one seat is required.", nameof(seats));

        _navigationService = navigationService;
        Trip = trip;
        Seats = seats;
        PassengerName = passengerName;
        PassengerPhone = passengerPhone;

        var bankTransfer = new PaymentOption("BankTransfer", "Bank transfer (VietQR)", true, null);
        PaymentMethods =
        [
            // VNPay disabled — requires a paid subscription for API credentials.
            // Re-add once VNPay sandbox/production keys are configured in appsettings.json.
            new PaymentOption("Cash", "Cash on board", true, null),
            bankTransfer
        ];
        _selectedPaymentMethod = bankTransfer;

        // AsyncRelayCommand: a RelayCommand with an async lambda is async void, so it returns at
        // the first await and a second click books a second ticket. This awaits and blocks re-entry.
        CompleteBookingCommand = new AsyncRelayCommand(
            CompleteBookingAsync,
            onError: _ => StatusMessage = LocalizationService.Instance.GetString("ToastBookingFailed"));
        BackToSeatsCommand = new RelayCommand(
            () => _navigationService.NavigateTo(new SeatSelectionViewModel(_navigationService, Trip)),
            () => !IsBusy);
    }

    private async Task CompleteBookingAsync()
    {
        if (CurrentUser.Account is null) { StatusMessage = "Your session has expired. Please sign in again."; return; }

        var method = SelectedPaymentMethod;
        if (!method.IsEnabled)
        {
            StatusMessage = method.DisabledReason ?? "This payment method is unavailable.";
            return;
        }

        IsBusy = true;
        try
        {
            // One ticket for all selected seats.
            var booking = await DatabaseHelper.BookTicketsAsync(
                CurrentUser.Account.AccountId,
                Trip.TripId,
                Seats.Select(seat => seat.SeatId).ToArray(),
                PassengerName,
                PassengerPhone);
            if (!booking.Success || booking.Ticket is null)
            {
                StatusMessage = booking.Error ?? "The selected seats could not be booked.";
                return;
            }

            var ticket = booking.Ticket;

            // One payment for the whole booking.
            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, method.Code));
            if (!payment.IsSuccess)
            {
                // The hold and the payment are separate transactions joined by this
                // compensation. If it fails the seat stays blocked with no payment
                // against it, so the ticket code must reach the log (AUD-C-007).
                var released = await DatabaseHelper.CancelTicketAsync(ticket.TicketId, CurrentUser.Account.AccountId);
                if (!released.Success)
                {
                    LoggerService.LogError(
                        $"Payment creation failed for ticket {ticket.TicketCode} and releasing the seat hold also failed: {released.Error}. " +
                        "The hold will stay until the expiry sweep runs.");
                }
                StatusMessage = payment.ErrorMessage ?? "The payment could not be started, so the selected seats were released.";
                return;
            }

            var holdExpiresAtUtc = ticket.BookingTime.Add(BankTransferHold);

            switch (method.Code)
            {
                case "Cash":
                    // Navigating away destroys this ViewModel, so an inline StatusMessage would
                    // never render. Toasts are owned by the shell and outlive the navigation.
                    NotificationService.Instance.ShowSuccess(
                        LocalizationService.Instance.Format("ToastCashReserved", SelectedSeatNumbers));
                    _navigationService.NavigateTo<MyTicketsViewModel>();
                    return;

                case "VNPay":
                    ShowPaymentDialog(ticket, payment, isBankTransfer: false, holdExpiresAtUtc);
                    _navigationService.NavigateTo<MyTicketsViewModel>();
                    return;

                default:
                    ShowPaymentDialog(ticket, payment, isBankTransfer: true, holdExpiresAtUtc);
                    _navigationService.NavigateTo<MyTicketsViewModel>();
                    return;
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Error in CompleteBookingAsync.", ex);
            StatusMessage = "The booking could not be completed.";
        }
        finally { IsBusy = false; }
    }

    private void ShowPaymentDialog(TicketDetails ticket, PaymentResult payment, bool isBankTransfer, DateTime holdExpiresAtUtc)
    {
        var window = new PaymentWindow(
            new[] { ticket.TicketCode },
            ticket.TotalPrice,
            payment.TransactionNo ?? string.Empty,
            payment.PaymentUrl,
            isBankTransfer,
            holdExpiresAtUtc)
        {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
        var holdDisplay = holdExpiresAtUtc.ToLocalTime().ToString("HH:mm");

        if (isBankTransfer)
        {
            if (window.DialogResult == true)
            {
                NotificationService.Instance.ShowSuccess(
                    LocalizationService.Instance.Format("ToastTransferSubmitted", ticket.TicketCode, holdDisplay));
            }
            else
            {
                NotificationService.Instance.ShowWarning(
                    LocalizationService.Instance.Format("ToastTransferNotSubmitted", ticket.TicketCode, holdDisplay));
            }
            return;
        }

        if (window.PaymentConfirmed)
        {
            NotificationService.Instance.ShowSuccess(
                LocalizationService.Instance.Format("ToastVnPayConfirmed", ticket.TicketCode));
            return;
        }

        NotificationService.Instance.ShowWarning(
            LocalizationService.Instance.Format("ToastVnPayPending", ticket.TicketCode, holdDisplay));
    }
}
