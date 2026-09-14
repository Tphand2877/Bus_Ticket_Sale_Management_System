using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class BookingViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _origin = "Hà Nội";
    private string _destination = "Hải Phòng";
    private DateTime _travelDate = DateTime.Today.AddDays(1);
    private TripSearchResult? _selectedTrip;
    private bool _isBusy;
    private bool _locationsLoaded;
    private int _locationRequestVersion;
    private string _statusMessage = string.Empty;

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
    public DateTime TravelDate { get => _travelDate; set => SetProperty(ref _travelDate, value); }
    public ObservableCollection<TripSearchResult> Trips { get; } = new();
    public ObservableCollection<string> AvailableOrigins { get; } = new();
    public ObservableCollection<string> AvailableDestinations { get; } = new();
    public ObservableCollection<string> Origins => AvailableOrigins;
    public ObservableCollection<string> Destinations => AvailableDestinations;
    public TripSearchResult? SelectedTrip { get => _selectedTrip; set => SetProperty(ref _selectedTrip, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(HasNoTrips));
        }
    }

    public bool HasNoTrips => !IsBusy && Trips.Count == 0;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public ICommand SearchCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowAllTripsCommand { get; }
    public ICommand SwapLocationsCommand { get; }
    public ICommand SelectTripCommand { get; }
    public ICommand BackToHomeCommand { get; }

    public BookingViewModel(INavigationService navigationService)
        : this(navigationService, "Hà Nội", "Hải Phòng", DateTime.Today.AddDays(1))
    {
    }

    public BookingViewModel(INavigationService navigationService, string origin, string destination, DateTime travelDate)
    {
        _navigationService = navigationService;
        _origin = origin;
        _destination = destination;
        _travelDate = travelDate;

        SearchCommand = new RelayCommand(async () => await SearchAsync());
        RefreshCommand = new RelayCommand(async () => await SearchAsync());
        ShowAllTripsCommand = new RelayCommand(async () =>
        {
            Origin = string.Empty;
            Destination = string.Empty;
            await SearchAsync();
        });
        SwapLocationsCommand = new RelayCommand(() => (Origin, Destination) = (Destination, Origin));
        SelectTripCommand = new RelayCommand(parameter =>
        {
            if (parameter is not TripSearchResult trip) return;
            if (trip.AvailableSeats <= 0)
            {
                StatusMessage = "Chuyến xe này đã hết ghế trống. Vui lòng chọn chuyến khác.";
                return;
            }
            _navigationService.NavigateTo(new SeatSelectionViewModel(_navigationService, trip));
        });
        BackToHomeCommand = new RelayCommand(() => _navigationService.NavigateTo<HomeViewModel>());
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await SearchAsync();
    }

    private async Task LoadLocationsAsync()
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
            LoggerService.LogError("Could not load route locations.", ex);
            StatusMessage = "Không thể tải danh sách điểm đi và điểm đến.";
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
            LoggerService.LogError("Could not load destinations for origin.", ex);
        }
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            Trips.Clear();
            var results = await DatabaseHelper.SearchTripsAsync(Origin, Destination, TravelDate);
            foreach (var trip in results) Trips.Add(trip);
            StatusMessage = Trips.Count == 0
                ? "Không tìm thấy chuyến xe nào trên tuyến này vào ngày đã chọn."
                : $"Tìm thấy {Trips.Count} chuyến xe khả dụng.";
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not search trips.", ex);
            StatusMessage = "Không thể tải danh sách chuyến xe từ cơ sở dữ liệu.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasNoTrips));
        }
    }
}
