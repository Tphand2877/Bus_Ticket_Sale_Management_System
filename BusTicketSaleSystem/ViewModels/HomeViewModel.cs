using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class HomeViewModel : ViewModelBase, IRefreshable, IDisposable
{
    private readonly INavigationService _navigationService;
    private string _origin = string.Empty;
    private string _destination = string.Empty;
    private DateTime _departureDate = DateTime.Today.AddDays(1);
    private string _selectedPassengers = "1 Passenger";
    private string _totalTickets = "0";
    private string _upcomingTripsCount = "0";
    private string _totalSpent = "0 VND";
    private string _rewardTier = "Standard";
    private string _rewardTierSubtext = "Tier based on settled spend";
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private bool _originsLoaded;
    private int _destinationRequestVersion;

    public string UserName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Guest";

    public string Origin
    {
        get => _origin;
        set
        {
            if (SetProperty(ref _origin, value) && _originsLoaded)
                _ = LoadDestinationsAsync(value);
        }
    }

    public string Destination { get => _destination; set => SetProperty(ref _destination, value); }
    public DateTime DepartureDate { get => _departureDate; set => SetProperty(ref _departureDate, value); }

    /// <summary>Earliest selectable departure; the calendar greys out anything before today.</summary>
    public DateTime MinDepartureDate => DateTime.Today;
    public string SelectedPassengers { get => _selectedPassengers; set => SetProperty(ref _selectedPassengers, value); }
    public ObservableCollection<string> PassengerOptions { get; } = new() { "1 Passenger", "2 Passengers", "3 Passengers", "4 Passengers" };

    /// <summary>Origins served by at least one route, loaded from the Routes table.</summary>
    public ObservableCollection<string> Origins { get; } = new();

    /// <summary>Destinations reachable from <see cref="Origin"/>, reloaded whenever the origin changes.</summary>
    public ObservableCollection<string> Destinations { get; } = new();

    public string TotalTickets { get => _totalTickets; private set => SetProperty(ref _totalTickets, value); }
    public string UpcomingTripsCount { get => _upcomingTripsCount; private set => SetProperty(ref _upcomingTripsCount, value); }
    public string TotalSpent { get => _totalSpent; private set => SetProperty(ref _totalSpent, value); }
    public string RewardTier { get => _rewardTier; private set => SetProperty(ref _rewardTier, value); }
    public string RewardTierSubtext { get => _rewardTierSubtext; private set => SetProperty(ref _rewardTierSubtext, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasNoPopularRoutes));
        }
    }

    public ObservableCollection<PopularRouteItem> PopularRoutes { get; } = new();
    public bool HasNoPopularRoutes => !IsLoading && PopularRoutes.Count == 0;

    public ICommand SearchCommand { get; }
    public ICommand ViewTicketCommand { get; }
    public ICommand SwapLocationsCommand { get; }
    public ICommand QuickBookCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ViewCompanyCommand { get; }
    public HomeViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _origin = LocationService.Instance.CurrentLocation;
        LocationService.Instance.LocationChanged += OnLocationChanged;
        SearchCommand = new RelayCommand(() => _navigationService.NavigateTo<BookingViewModel>());
        QuickBookCommand = new RelayCommand(_ => _navigationService.NavigateTo<BookingViewModel>());
        ViewTicketCommand = new RelayCommand(() => _navigationService.NavigateTo<MyTicketsViewModel>());
        SwapLocationsCommand = new RelayCommand(() => (Origin, Destination) = (Destination, Origin));
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        ViewCompanyCommand = new AsyncRelayCommand(async parameter =>
        {
            string companyName = "Sao Việt";
            if (parameter is PopularRouteItem route) companyName = route.CompanyName;
            else if (parameter is string name && !string.IsNullOrWhiteSpace(name)) companyName = name;

            var company = await DatabaseHelper.GetBusCompanyByNameAsync(companyName) ?? new BusCompanyItem(
                1, companyName, "1900 1234", 4.8m, 1820, "Hà Nội - Thái Nguyên - Lào Cai - Sa Pa",
                $"Nhà xe {companyName} chuyên phục vụ hành khách các tuyến liên tỉnh chất lượng cao, an toàn và đúng giờ.",
                "Wifi tốc độ cao, Nước uống miễn phí, Khăn lạnh, Cổng sạc USB, Điều hòa hai chiều, Màn hình LCD giải trí",
                "SV", "#0F4C81");

            _navigationService.NavigateTo(new BusCompanyDetailViewModel(_navigationService, company));
        });
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await LoadOriginsAsync();
            await LoadStatsAsync();
            await LoadPopularRoutesAsync();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasNoPopularRoutes));
        }
    }

    private async Task LoadOriginsAsync()
    {
        try
        {
            var origins = await DatabaseHelper.GetDistinctOriginsAsync();
            var previousOrigin = Origin;
            Origins.Clear();
            foreach (var origin in origins) Origins.Add(origin);
            _originsLoaded = true;

            var targetOrigin = LocationService.Instance.CurrentLocation;
            var resolvedOrigin = Origins.FirstOrDefault(o => o.Contains(targetOrigin, StringComparison.OrdinalIgnoreCase))
                                 ?? Origins.FirstOrDefault(o => string.Equals(o, previousOrigin, StringComparison.OrdinalIgnoreCase))
                                 ?? Origins.FirstOrDefault()
                                 ?? string.Empty;
            if (!string.Equals(resolvedOrigin, _origin, StringComparison.Ordinal))
            {
                _origin = resolvedOrigin;
                OnPropertyChanged(nameof(Origin));
            }
            await LoadDestinationsAsync(_origin);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load origins for the dashboard search box.", ex);
            StatusMessage = "Route list is unavailable right now. Please try again.";
        }
    }

    private async Task LoadDestinationsAsync(string? origin)
    {
        var requestVersion = ++_destinationRequestVersion;
        try
        {
            var destinations = await DatabaseHelper.GetDestinationsByOriginAsync(origin);
            if (requestVersion != _destinationRequestVersion) return;
            var previousDestination = Destination;
            Destinations.Clear();
            foreach (var destination in destinations) Destinations.Add(destination);
            Destination = Destinations.FirstOrDefault(d => string.Equals(d, previousDestination, StringComparison.OrdinalIgnoreCase))
                          ?? Destinations.FirstOrDefault()
                          ?? string.Empty;
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load destinations for the dashboard search box.", ex);
            StatusMessage = "Destination list is unavailable right now. Please try again.";
        }
    }

    private async Task LoadStatsAsync()
    {
        if (CurrentUser.Account is null) return;
        try
        {
            var stats = await DatabaseHelper.GetDashboardStatsAsync(CurrentUser.Account.AccountId);
            TotalTickets = stats.TotalTickets.ToString("N0");
            UpcomingTripsCount = stats.UpcomingTrips.ToString("N0");
            TotalSpent = $"{stats.TotalSpent:N0} VND";
            (RewardTier, RewardTierSubtext) = stats.TotalSpent switch
            {
                >= 5_000_000m => ("Gold", "Tier based on settled spend"),
                >= 2_000_000m => ("Silver", "Tier based on settled spend"),
                >= 500_000m => ("Bronze", "Tier based on settled spend"),
                _ => ("Standard", "Tier based on settled spend")
            };
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load dashboard stats.", ex);
            StatusMessage = "Your travel statistics could not be loaded.";
        }
    }

    private async Task LoadPopularRoutesAsync()
    {
        try
        {
            var routes = await DatabaseHelper.GetPopularRoutesAsync(4);
            PopularRoutes.Clear();
            foreach (var route in routes) PopularRoutes.Add(route);
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load popular routes.", ex);
            StatusMessage = "Popular routes could not be loaded.";
        }
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        var target = LocationService.Instance.CurrentLocation;
        var match = Origins.FirstOrDefault(o => o.Contains(target, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            Origin = match;
        }
    }

    /// <summary>
    /// Releases the singleton subscription. Without it the process-lifetime
    /// LocationService kept one dead HomeViewModel per navigation alive, and each of
    /// them still ran a database query on every location change (AUD-E-002).
    /// </summary>
    public void Dispose() => LocationService.Instance.LocationChanged -= OnLocationChanged;
}
