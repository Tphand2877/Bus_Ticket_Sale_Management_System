using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class BusCompanyDetailViewModel : ViewModelBase, IRefreshable
{
    private readonly INavigationService _navigationService;
    private readonly string _originFilter;
    private readonly string _destinationFilter;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private BusCompanyItem _company;
    private DateTime? _selectedDate = DateTime.Today;
    private TripSearchResult? _selectedTrip;
    private string _selectedPickupPoint = "";
    private string _selectedDropoffPoint = "";
    private string _statusMessage = "";
    private bool _isLoading;

    public BusCompanyItem Company { get => _company; private set => SetProperty(ref _company, value); }
    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
                _ = LoadTripsAsync();
        }
    }

    public ObservableCollection<TripSearchResult> AvailableTrips { get; } = new();
    public ObservableCollection<string> AvailablePickupPoints { get; } = new();
    public ObservableCollection<string> AvailableDropoffPoints { get; } = new();

    public TripSearchResult? SelectedTrip
    {
        get => _selectedTrip;
        set
        {
            if (SetProperty(ref _selectedTrip, value))
            {
                UpdateStopsForSelectedTrip();
                OnPropertyChanged(nameof(HasSelectedTrip));
            }
        }
    }

    public bool HasSelectedTrip => SelectedTrip is not null;
    public string SelectedPickupPoint { get => _selectedPickupPoint; set => SetProperty(ref _selectedPickupPoint, value); }
    public string SelectedDropoffPoint { get => _selectedDropoffPoint; set => SetProperty(ref _selectedDropoffPoint, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasTrips => AvailableTrips.Count > 0;
    public bool HasNoTrips => !HasTrips && !IsLoading;
    public int ActiveTripsCount => AvailableTrips.Count(t => t.IsAvailable);

    public ICommand SelectTripCommand { get; }
    public ICommand BookTripCommand { get; }
    public ICommand BookSelectedTripCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand RefreshCommand { get; }

    public BusCompanyDetailViewModel(
        INavigationService navigationService,
        BusCompanyItem company,
        string? origin = null,
        string? destination = null,
        DateTime? travelDate = null)
    {
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _company = company ?? new BusCompanyItem(
            1, "Sao Việt", "1900 1234", 4.8m, 1250, "Hà Nội - Sa Pa",
            "Nhà xe chất lượng cao", "Wifi, Nước uống", "SV", "#0F4C81");
        _originFilter = origin ?? "";
        _destinationFilter = destination ?? "";
        _selectedDate = travelDate ?? DateTime.Today;
        SelectTripCommand = new RelayCommand(p =>
        {
            if (p is TripSearchResult trip)
                SelectedTrip = trip;
        });

        BookTripCommand = new RelayCommand(parameter =>
        {
            var trip = parameter as TripSearchResult ?? SelectedTrip;
            if (trip is null) return;
            if (!trip.IsAvailable)
            {
                StatusMessage = "Chuyến xe này không thể đặt do đã hết chỗ hoặc đã khởi hành.";
                return;
            }
            _navigationService.NavigateTo(new SeatSelectionViewModel(_navigationService, trip));
        });

        BookSelectedTripCommand = new RelayCommand(
            () =>
            {
                if (SelectedTrip is null || !SelectedTrip.IsAvailable) return;
                _navigationService.NavigateTo(new SeatSelectionViewModel(_navigationService, SelectedTrip));
            },
            () => HasSelectedTrip && SelectedTrip?.IsAvailable == true);

        BackCommand = new RelayCommand(() =>
        {
            _navigationService.NavigateTo(new BookingViewModel(_navigationService));
        });
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
    }
    public Task RefreshAsync() => LoadTripsAsync();

    private async Task LoadTripsAsync()
    {
        if (!await _loadLock.WaitAsync(0)) return;
        IsLoading = true;
        StatusMessage = "";
        try
        {
            var companyName = Company?.CompanyName ?? "Sao Việt";
            var trips = await DatabaseHelper.GetTripsByCompanyAsync(companyName, SelectedDate);
            AvailableTrips.Clear();
            foreach (var t in trips)
            {
                if (!string.IsNullOrWhiteSpace(_originFilter) && !t.Origin.Contains(_originFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(_destinationFilter) && !t.Destination.Contains(_destinationFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                AvailableTrips.Add(t);
            }

            if (AvailableTrips.Count == 0 && trips.Count > 0)
            {
                foreach (var t in trips) AvailableTrips.Add(t);
            }

            SelectedTrip = AvailableTrips.FirstOrDefault(t => t.IsAvailable) ?? AvailableTrips.FirstOrDefault();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Could not load trips for company {Company?.CompanyName}.", ex);
            StatusMessage = "Không thể tải danh sách chuyến xe. Vui lòng thử lại.";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasTrips));
            OnPropertyChanged(nameof(HasNoTrips));
            OnPropertyChanged(nameof(ActiveTripsCount));
            _loadLock.Release();
        }
    }
    private void UpdateStopsForSelectedTrip()
    {
        AvailablePickupPoints.Clear();
        AvailableDropoffPoints.Clear();

        if (SelectedTrip is null) return;

        var stops = SelectedTrip.StopsList;
        foreach (var stop in stops)
        {
            AvailablePickupPoints.Add(stop);
            AvailableDropoffPoints.Add(stop);
        }

        SelectedPickupPoint = AvailablePickupPoints.Count > 0 ? AvailablePickupPoints[0] : SelectedTrip.Origin;
        SelectedDropoffPoint = AvailableDropoffPoints.Count > 1 ? AvailableDropoffPoints[^1] : SelectedTrip.Destination;
    }
}
