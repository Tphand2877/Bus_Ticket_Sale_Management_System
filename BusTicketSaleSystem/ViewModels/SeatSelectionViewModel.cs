using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace BusTicketSaleSystem.ViewModels;

public sealed class SeatSelectionViewModel : ViewModelBase, IRefreshable, IDisposable
{
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(20);

    private readonly INavigationService _navigationService;
    private readonly IPaymentDialogService _paymentDialog;
    private readonly DispatcherTimer _autoRefreshTimer;
    private string _passengerName = CurrentUser.Account?.FullName ?? string.Empty;
    private string _passengerPhone = CurrentUser.Account?.Phone ?? string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private bool _isDisposed;

    public TripSearchResult Trip { get; }
    public ObservableCollection<SelectableSeatViewModel> Seats { get; } = new();

    public IReadOnlyList<SeatInfo> SelectedSeats =>
        Seats.Where(seat => seat.IsSelected).Select(seat => seat.Seat).ToArray();

    public string PassengerName { get => _passengerName; set => SetProperty(ref _passengerName, value); }
    public string PassengerPhone { get => _passengerPhone; set => SetProperty(ref _passengerPhone, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasNoSeats));
        }
    }

    public int AvailableSeatsCount => Seats.Count(s => !s.IsBooked);
    public int BookedSeatsCount => Seats.Count(s => s.IsBooked);
    public int SelectedSeatCount => Seats.Count(s => s.IsSelected);
    public bool HasSelectedSeat => SelectedSeatCount > 0;
    public bool HasNoSeats => !IsLoading && Seats.Count == 0;
    public string SelectedSeatNumber => HasSelectedSeat ? string.Join(", ", SelectedSeats.Select(seat => seat.SeatNumber)) : "None";
    public string TotalPriceDisplay => HasSelectedSeat ? $"{Trip.Price * SelectedSeatCount:N0} VND" : "0 VND";
    public string PriceDisplay => $"{Trip.Price:N0} VND";
    public string TripScheduleDisplay => $"{Trip.DepartureTime:dd/MM/yyyy HH:mm} - {Trip.ArrivalTime:HH:mm}";

    public ICommand GoToConfirmationCommand { get; }
    public ICommand BackToBookingCommand { get; }
    public ICommand RefreshCommand { get; }

    public SeatSelectionViewModel(INavigationService navigationService, IPaymentDialogService paymentDialog, TripSearchResult trip)
    {
        _navigationService = navigationService;
        _paymentDialog = paymentDialog;
        Trip = trip;

        GoToConfirmationCommand = new RelayCommand(() =>
        {
            var selectedSeats = SelectedSeats;
            if (selectedSeats.Count == 0)
            {
                StatusMessage = "Please select at least one seat on the seat map.";
                return;
            }
            if (selectedSeats.Any(seat => seat.IsBooked))
            {
                StatusMessage = "One of the selected seats has just been taken. Please choose again.";
                return;
            }
            if (string.IsNullOrWhiteSpace(PassengerName))
            {
                StatusMessage = "Please enter the passenger's full name.";
                return;
            }

            _navigationService.NavigateTo(new BookingConfirmationViewModel(
                _navigationService, _paymentDialog, Trip, selectedSeats, PassengerName.Trim(), PassengerPhone.Trim()));
        });

        BackToBookingCommand = new RelayCommand(() =>
        {
            Dispose();
            _navigationService.NavigateTo<BookingViewModel>();
        });

        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());

        _autoRefreshTimer = new DispatcherTimer { Interval = AutoRefreshInterval };
        _autoRefreshTimer.Tick += OnAutoRefreshTick;
    }

    public async Task RefreshAsync()
    {
        if (_isDisposed) return;
        await LoadSeatsAsync(auto: false);
        if (!_isDisposed) _autoRefreshTimer.Start();
    }

    /// <summary>Stops the live seat-map polling. Called from the view's Unloaded handler.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Tick -= OnAutoRefreshTick;
        foreach (var seat in Seats) seat.PropertyChanged -= OnSeatPropertyChanged;
    }

    private void OnAutoRefreshTick(object? sender, EventArgs e) => _ = LoadSeatsAsync(auto: true);

    private async Task LoadSeatsAsync(bool auto)
    {
        if (_isDisposed) return;
        IsLoading = true;
        try
        {
            var seats = await DatabaseHelper.GetSeatsAsync(Trip.TripId);
            if (_isDisposed) return;

            var previousSelectionNumbers = SelectedSeats
                .Select(seat => seat.SeatNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var previousAvailableNumbers = SelectedSeats
                .Where(seat => !seat.IsBooked)
                .Select(seat => seat.SeatNumber)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var seat in Seats) seat.PropertyChanged -= OnSeatPropertyChanged;
            Seats.Clear();
            foreach (var seat in seats)
            {
                var selectable = new SelectableSeatViewModel(seat)
                {
                    IsSelected = !seat.IsBooked && previousSelectionNumbers.Contains(seat.SeatNumber)
                };
                selectable.PropertyChanged += OnSeatPropertyChanged;
                Seats.Add(selectable);
            }

            var lostSelection = previousAvailableNumbers
                .Where(number => !Seats.Any(seat => string.Equals(seat.SeatNumber, number, StringComparison.OrdinalIgnoreCase) && !seat.IsBooked))
                .ToArray();
            StatusMessage = lostSelection.Length > 0
                ? $"Seat(s) {string.Join(", ", lostSelection)} were booked by another passenger. Please choose again."
                : Seats.Count == 0
                    ? "This departure has no seat map configured yet."
                    : $"{AvailableSeatsCount} seat(s) available, {BookedSeatsCount} already booked.";

            OnPropertyChanged(nameof(AvailableSeatsCount));
            OnPropertyChanged(nameof(BookedSeatsCount));
            OnPropertyChanged(nameof(SelectedSeats));
            OnPropertyChanged(nameof(SelectedSeatCount));
            OnPropertyChanged(nameof(HasSelectedSeat));
            OnPropertyChanged(nameof(SelectedSeatNumber));
            OnPropertyChanged(nameof(TotalPriceDisplay));
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Could not load the seat map for trip {Trip.TripId}.", ex);
            if (!auto) StatusMessage = "The seat map could not be loaded from the database.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoSeats));
        }
    }

    private void OnSeatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SelectableSeatViewModel.IsSelected)) return;
        OnPropertyChanged(nameof(SelectedSeats));
        OnPropertyChanged(nameof(SelectedSeatCount));
        OnPropertyChanged(nameof(HasSelectedSeat));
        OnPropertyChanged(nameof(SelectedSeatNumber));
        OnPropertyChanged(nameof(TotalPriceDisplay));
    }
}

public sealed class SelectableSeatViewModel : ViewModelBase
{
    private bool _isSelected;

    public SeatInfo Seat { get; }
    public int SeatId => Seat.SeatId;
    public string SeatNumber => Seat.SeatNumber;
    public bool IsBooked => Seat.IsBooked;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public SelectableSeatViewModel(SeatInfo seat) => Seat = seat;
}
