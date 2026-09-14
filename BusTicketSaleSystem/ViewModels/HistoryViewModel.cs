using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _totalCompletedTripsText = "0 Trips Completed";
    private string _totalDistanceTraveledText = "0 km Traveled";
    private bool _isBusy;

    public ObservableCollection<PastTripItemViewModel> HistoryTrips { get; } = new();
    public string TotalCompletedTripsText { get => _totalCompletedTripsText; private set => SetProperty(ref _totalCompletedTripsText, value); }
    public string TotalDistanceTraveledText { get => _totalDistanceTraveledText; private set => SetProperty(ref _totalDistanceTraveledText, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(HasNoHistory));
                OnPropertyChanged(nameof(HasHistory));
            }
        }
    }
    public bool HasHistory => HistoryTrips.Count > 0;
    public bool HasNoHistory => !IsBusy && HistoryTrips.Count == 0;

    public ICommand BookAgainCommand { get; }
    public ICommand RefreshCommand { get; }

    public HistoryViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        BookAgainCommand = new RelayCommand(parameter =>
        {
            if (parameter is PastTripItemViewModel item && !string.IsNullOrWhiteSpace(item.Origin) && !string.IsNullOrWhiteSpace(item.Destination))
            {
                _navigationService.NavigateTo(new BookingViewModel(_navigationService, item.Origin, item.Destination, DateTime.Today.AddDays(1)));
            }
            else
            {
                _navigationService.NavigateTo<BookingViewModel>();
            }
        });

        RefreshCommand = new RelayCommand(async () => await LoadAsync());

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (CurrentUser.Account is null) return;
        IsBusy = true;
        try
        {
            HistoryTrips.Clear();
            var tickets = await DatabaseHelper.GetTicketsAsync(CurrentUser.Account.AccountId, true);
            foreach (var ticket in tickets)
            {
                HistoryTrips.Add(new PastTripItemViewModel(ticket));
            }

            var (completedCount, totalKm) = await DatabaseHelper.GetHistoryStatsAsync(CurrentUser.Account.AccountId);
            TotalCompletedTripsText = $"{completedCount} Trips Completed";
            TotalDistanceTraveledText = $"{totalKm:N0} km Traveled";
        }
        catch { }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasHistory));
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
    public string BusOperator => string.IsNullOrWhiteSpace(_ticket.BusType) ? "OmniBus Express" : _ticket.BusType;
    public string Seat => _ticket.SeatNumber;
    public string Fare => $"{_ticket.Price:N0} VND";
    public string Status => _ticket.Status;
    public string Rating => _ticket.Status == "Đã hủy" ? "Đã hủy" : "5.0 ★";

    public PastTripItemViewModel(TicketDetails ticket) => _ticket = ticket;
}
