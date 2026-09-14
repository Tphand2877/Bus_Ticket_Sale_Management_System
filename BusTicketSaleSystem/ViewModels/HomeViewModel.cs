using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _origin = "Hà Nội";
    private string _destination = "Hải Phòng";
    private DateTime _departureDate = DateTime.Today.AddDays(1);
    private string _selectedPassengers = "1 Passenger";
    private string _totalTickets = "0";
    private string _upcomingTripsCount = "0";
    private string _totalSpent = "0 VND";
    private string _rewardTier = "Standard";
    private string _rewardTierSubtext = "New Member";
    private string _loyaltyPoints = "0 pts";
    private bool _locationsLoaded;
    private int _locationRequestVersion;

    public string UserName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Guest";
    public string Origin
    {
        get => _origin;
        set
        {
            if (SetProperty(ref _origin, value) && _locationsLoaded)
                _ = LoadDestinationsAsync(value);
        }
    }
    public string Destination { get => _destination; set => SetProperty(ref _destination, value); }
    public List<string> ProvinceList => VietnamProvinces.List;
    public DateTime DepartureDate { get => _departureDate; set => SetProperty(ref _departureDate, value); }
    public string SelectedPassengers { get => _selectedPassengers; set => SetProperty(ref _selectedPassengers, value); }
    public ObservableCollection<string> PassengerOptions { get; } = new() { "1 Passenger", "2 Passengers", "3 Passengers", "4 Passengers" };
    public ObservableCollection<string> AvailableOrigins { get; } = new();
    public ObservableCollection<string> AvailableDestinations { get; } = new();
    public ObservableCollection<string> Origins => AvailableOrigins;
    public ObservableCollection<string> Destinations => AvailableDestinations;

    public string TotalTickets { get => _totalTickets; private set => SetProperty(ref _totalTickets, value); }
    public string UpcomingTripsCount { get => _upcomingTripsCount; private set => SetProperty(ref _upcomingTripsCount, value); }
    public string TotalSpent { get => _totalSpent; private set => SetProperty(ref _totalSpent, value); }
    public string RewardTier { get => _rewardTier; private set => SetProperty(ref _rewardTier, value); }
    public string RewardTierSubtext { get => _rewardTierSubtext; private set => SetProperty(ref _rewardTierSubtext, value); }
    public string LoyaltyPoints { get => _loyaltyPoints; private set => SetProperty(ref _loyaltyPoints, value); }
    public ObservableCollection<PopularRouteItem> PopularRoutes { get; } = new();

    public ICommand SearchCommand { get; }
    public ICommand ViewTicketCommand { get; }
    public ICommand SwapLocationsCommand { get; }
    public ICommand QuickBookCommand { get; }

    public HomeViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        SearchCommand = new RelayCommand(() => _navigationService.NavigateTo(new BookingViewModel(_navigationService, Origin, Destination, DepartureDate)));
        QuickBookCommand = new RelayCommand(parameter =>
        {
            if (parameter is PopularRouteItem route)
                _navigationService.NavigateTo(new BookingViewModel(_navigationService, route.From, route.To, DepartureDate));
            else
                _navigationService.NavigateTo(new BookingViewModel(_navigationService, Origin, Destination, DepartureDate));
        });
        ViewTicketCommand = new RelayCommand(() => _navigationService.NavigateTo<MyTicketsViewModel>());
        SwapLocationsCommand = new RelayCommand(() => (Origin, Destination) = (Destination, Origin));
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        await LoadStatsAsync();
        await LoadPopularRoutesAsync();
    }

    private async Task LoadRouteLocationsAsync()
    {
        try
        {
            AvailableOrigins.Clear();
            foreach (var origin in await DatabaseHelper.GetDistinctOriginsAsync()) AvailableOrigins.Add(origin);
            _locationsLoaded = true;
            await LoadDestinationsAsync(Origin);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load home route locations.", ex);
        }
    }

    private async Task LoadDestinationsAsync(string? origin)
    {
        var requestVersion = ++_locationRequestVersion;
        try
        {
            var destinations = await DatabaseHelper.GetDestinationsByOriginAsync(origin);
            if (requestVersion != _locationRequestVersion) return;
            AvailableDestinations.Clear();
            foreach (var destination in destinations) AvailableDestinations.Add(destination);
            if (!string.IsNullOrWhiteSpace(Destination) && !AvailableDestinations.Contains(Destination, StringComparer.OrdinalIgnoreCase))
                Destination = string.Empty;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load home destinations.", ex);
        }
    }

    private async Task LoadStatsAsync()
    {
        if (CurrentUser.Account is null) return;
        try
        {
            var stats = await DatabaseHelper.GetDashboardStatsAsync(CurrentUser.Account.AccountId);
            TotalTickets = stats.TotalTickets.ToString();
            UpcomingTripsCount = stats.UpcomingTrips.ToString();
            TotalSpent = $"{stats.TotalSpent:N0} VND";
            LoyaltyPoints = $"{stats.TotalTickets * 50} pts";
            (RewardTier, RewardTierSubtext) = stats.TotalTickets switch
            {
                >= 10 => ("VIP Gold", "Gold Tier Member"),
                >= 5 => ("Silver", "Silver Tier Member"),
                >= 1 => ("Bronze", "Active Traveler"),
                _ => ("Standard", "Standard Member Tier")
            };
        }
        catch (Exception ex) { LoggerService.LogError("Could not load dashboard stats.", ex); }
    }

    private async Task LoadPopularRoutesAsync()
    {
        try
        {
            PopularRoutes.Clear();
            foreach (var route in await DatabaseHelper.GetPopularRoutesAsync(4)) PopularRoutes.Add(route);
        }
        catch (Exception ex) { LoggerService.LogError("Could not load popular routes.", ex); }
    }
}
