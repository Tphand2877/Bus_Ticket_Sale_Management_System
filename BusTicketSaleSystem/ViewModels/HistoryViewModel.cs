using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class HistoryViewModel : ViewModelBase, IRefreshable
{
    private readonly INavigationService _navigationService;
    private string _totalCompletedTripsText = "0 Trips Completed";
    private string _totalDistanceTraveledText = "0 km Traveled";
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public ObservableCollection<PastTripItemViewModel> HistoryTrips { get; } = new();
    public string TotalCompletedTripsText { get => _totalCompletedTripsText; private set => SetProperty(ref _totalCompletedTripsText, value); }
    public string TotalDistanceTraveledText { get => _totalDistanceTraveledText; private set => SetProperty(ref _totalDistanceTraveledText, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasNoHistory));
        }
    }

    public bool HasNoHistory => !IsLoading && HistoryTrips.Count == 0;

    public ICommand BookAgainCommand { get; }
    public ICommand RefreshCommand { get; }

    public HistoryViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        BookAgainCommand = new RelayCommand(_ => _navigationService.NavigateTo<BookingViewModel>());

        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        if (CurrentUser.Account is null)
        {
            ErrorMessage = "Your session has expired. Please sign in again.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var tickets = await DatabaseHelper.GetTicketsAsync(CurrentUser.Account.AccountId, true);
            HistoryTrips.Clear();
            foreach (var ticket in tickets) HistoryTrips.Add(new PastTripItemViewModel(ticket));

            var (completedCount, totalKm) = await DatabaseHelper.GetHistoryStatsAsync(CurrentUser.Account.AccountId);
            TotalCompletedTripsText = $"{completedCount:N0} Trips Completed";
            TotalDistanceTraveledText = $"{totalKm:N0} km Traveled";
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load travel history.", ex);
            HistoryTrips.Clear();
            ErrorMessage = "Your travel history could not be loaded from the database.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoHistory));
        }
    }
}

public sealed class PastTripItemViewModel : ViewModelBase
{
    private readonly TicketDetails _ticket;
    public string TicketCode => _ticket.TicketCode;
    public string Route => _ticket.Route;
    public string Origin => _ticket.Origin;
    public string Destination => _ticket.Destination;
    public decimal DistanceKm => _ticket.DistanceKm;
    public string Date => _ticket.DepartureTime.ToString("dd/MM/yyyy");
    public string Time => _ticket.DepartureTime.ToString("HH:mm");
    public string BusType => string.IsNullOrWhiteSpace(_ticket.BusType) ? "Standard coach" : _ticket.BusType;
    public string Seats => _ticket.SeatNumbers;
    public int SeatCount => _ticket.SeatCount;
    public string Fare => $"{_ticket.TotalPrice:N0} VND";

    /// <summary>Raw DB literal; rendered through the TicketStatusToDisplay converter.</summary>
    public string Status => _ticket.Status;

    /// <summary>Raw DB literal; rendered through the PaymentStatusToDisplay converter.</summary>
    public string PaymentStatus => _ticket.PaymentStatus;

    public PastTripItemViewModel(TicketDetails ticket) => _ticket = ticket;
}
