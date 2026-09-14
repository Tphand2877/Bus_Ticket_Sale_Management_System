using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class AdminMainViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private string _activeTab = "Dashboard";
    public ViewModelBase? CurrentViewModel => _navigationService.CurrentViewModel;
    public string ActiveTab { get => _activeTab; private set => SetProperty(ref _activeTab, value); }
    public string AdminDisplayName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Administrator";
    public string AdminRole => CurrentUser.Account?.Role ?? "Admin";

    public ICommand DashboardCommand { get; }
    public ICommand RoutesCommand { get; }
    public ICommand BusesCommand { get; }
    public ICommand TripsCommand { get; }
    public ICommand AccountsCommand { get; }
    public ICommand LogoutCommand { get; }
    public event EventHandler? RequestLogout;

    public AdminMainViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationService.CurrentViewModel))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
                ActiveTab = CurrentViewModel switch
                {
                    AdminRouteManagementViewModel => "Routes",
                    AdminBusManagementViewModel => "Buses",
                    AdminTripManagementViewModel => "Trips",
                    AdminAccountsViewModel => "Accounts",
                    _ => "Dashboard"
                };
            }
        };
        DashboardCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminDashboardViewModel>());
        RoutesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminRouteManagementViewModel>());
        BusesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminBusManagementViewModel>());
        TripsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminTripManagementViewModel>());
        AccountsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminAccountsViewModel>());
        LogoutCommand = new RelayCommand(() => RequestLogout?.Invoke(this, EventArgs.Empty));
        _navigationService.SetFactory(type =>
            type == typeof(AdminDashboardViewModel) ? new AdminDashboardViewModel(_navigationService) :
            type == typeof(AdminRouteManagementViewModel) ? new AdminRouteManagementViewModel(_navigationService) :
            type == typeof(AdminBusManagementViewModel) ? new AdminBusManagementViewModel(_navigationService) :
            type == typeof(AdminTripManagementViewModel) ? new AdminTripManagementViewModel(_navigationService) :
            type == typeof(AdminAccountsViewModel) ? new AdminAccountsViewModel(_navigationService) :
            throw new InvalidOperationException($"No admin factory for {type.Name}"));
        _navigationService.NavigateTo<AdminDashboardViewModel>();
    }
}

public sealed class AdminDashboardViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private string _message = "";
    private bool _isLoading;
    private string _lastRefreshed = "";

    public AdminDashboardStats Stats { get; private set; } = new(0, 0, 0, 0, 0, 0);
    public ObservableCollection<RecentBookingAdminItem> RecentBookings { get; } = new();
    public ObservableCollection<TripAdminItem> UpcomingTrips { get; } = new();
    public ObservableCollection<BusAdminItem> BusFleet { get; } = new();

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string LastRefreshed
    {
        get => _lastRefreshed;
        private set => SetProperty(ref _lastRefreshed, value);
    }

    public bool HasRecentBookings => RecentBookings.Count > 0;
    public bool HasNoRecentBookings => !HasRecentBookings;

    public bool HasUpcomingTrips => UpcomingTrips.Count > 0;
    public bool HasNoUpcomingTrips => !HasUpcomingTrips;

    public bool HasBuses => BusFleet.Count > 0;
    public bool HasNoBuses => !HasBuses;

    public string AdminName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Administrator";
    public string AdminRole => CurrentUser.Account?.Role ?? "Admin";

    public ICommand RefreshCommand { get; }
    public ICommand NavigateRoutesCommand { get; }
    public ICommand NavigateBusesCommand { get; }
    public ICommand NavigateTripsCommand { get; }
    public ICommand ClearMessageCommand { get; }

    public AdminDashboardViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        NavigateRoutesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminRouteManagementViewModel>());
        NavigateBusesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminBusManagementViewModel>());
        NavigateTripsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminTripManagementViewModel>());
        ClearMessageCommand = new RelayCommand(() => Message = "");

        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Stats = await AdminDatabaseService.GetDashboardStatsAsync();
            OnPropertyChanged(nameof(Stats));

            var bookings = await AdminDatabaseService.GetRecentBookingsAsync(6);
            RecentBookings.Clear();
            foreach (var b in bookings) RecentBookings.Add(b);
            OnPropertyChanged(nameof(HasRecentBookings));
            OnPropertyChanged(nameof(HasNoRecentBookings));

            var trips = await AdminDatabaseService.GetUpcomingTripsAsync(5);
            UpcomingTrips.Clear();
            foreach (var t in trips) UpcomingTrips.Add(t);
            OnPropertyChanged(nameof(HasUpcomingTrips));
            OnPropertyChanged(nameof(HasNoUpcomingTrips));

            var buses = await AdminDatabaseService.GetBusesAsync();
            BusFleet.Clear();
            foreach (var bus in buses.Take(5)) BusFleet.Add(bus);
            OnPropertyChanged(nameof(HasBuses));
            OnPropertyChanged(nameof(HasNoBuses));

            LastRefreshed = $"Updated {DateTime.Now:HH:mm:ss}";
            Message = "";
        }
        catch (Exception ex)
        {
            Message = "Không thể tải dữ liệu bảng điều khiển: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public sealed class AdminRouteManagementViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private string _origin = "";
    private string _destination = "";
    private string _distance = "";
    private string _message = "";
    private bool _isEditing;
    private int _editingRouteId;

    public ObservableCollection<RouteAdminItem> Routes { get; } = new();
    public string Origin { get => _origin; set => SetProperty(ref _origin, value); }
    public string Destination { get => _destination; set => SetProperty(ref _destination, value); }
    public string Distance { get => _distance; set => SetProperty(ref _distance, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsEditing { get => _isEditing; private set => SetProperty(ref _isEditing, value); }

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand SelectForEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand DeleteCommand { get; }

    public AdminRouteManagementViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        AddCommand = new RelayCommand(async () => await AddAsync());
        UpdateCommand = new RelayCommand(async () => await UpdateAsync());
        CancelEditCommand = new RelayCommand(() =>
        {
            IsEditing = false;
            _editingRouteId = 0;
            Origin = Destination = Distance = "";
            Message = "";
        });
        SelectForEditCommand = new RelayCommand(p =>
        {
            if (p is RouteAdminItem r)
            {
                _editingRouteId = r.RouteId;
                Origin = r.Origin;
                Destination = r.Destination;
                Distance = r.DistanceKm?.ToString("F1") ?? "";
                IsEditing = true;
                Message = $"Đang sửa tuyến {r.DisplayName}";
            }
        });
        DeleteCommand = new RelayCommand(async p =>
        {
            if (p is RouteAdminItem r)
            {
                var result = await AdminDatabaseService.DeleteRouteAsync(r.RouteId);
                Message = result.Error ?? "Đã xóa tuyến.";
                await LoadAsync();
            }
        });
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Routes.Clear();
            foreach (var r in await AdminDatabaseService.GetRoutesAsync()) Routes.Add(r);
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private async Task AddAsync()
    {
        decimal? distance = null;
        if (!string.IsNullOrWhiteSpace(Distance))
        {
            if (!decimal.TryParse(Distance, out var value))
            {
                Message = "Khoảng cách không hợp lệ.";
                return;
            }
            distance = value;
        }
        var result = await AdminDatabaseService.AddRouteAsync(Origin, Destination, distance);
        Message = result.Error ?? "Đã thêm tuyến.";
        if (result.Success)
        {
            Origin = Destination = Distance = "";
            await LoadAsync();
        }
    }

    private async Task UpdateAsync()
    {
        if (_editingRouteId <= 0) return;
        decimal? distance = null;
        if (!string.IsNullOrWhiteSpace(Distance))
        {
            if (!decimal.TryParse(Distance, out var value))
            {
                Message = "Khoảng cách không hợp lệ.";
                return;
            }
            distance = value;
        }
        var result = await AdminDatabaseService.UpdateRouteDistanceAsync(_editingRouteId, distance);
        Message = result.Error ?? "Đã cập nhật khoảng cách tuyến.";
        if (result.Success)
        {
            IsEditing = false;
            _editingRouteId = 0;
            Origin = Destination = Distance = "";
            await LoadAsync();
        }
    }
}

public sealed class AdminBusManagementViewModel : ViewModelBase
{
    private string _plate = "";
    private string _seatCount = "40";
    private string _busType = "Ghế ngồi";
    private string _message = "";

    public ObservableCollection<BusAdminItem> Buses { get; } = new();
    public string Plate { get => _plate; set => SetProperty(ref _plate, value); }
    public string SeatCount { get => _seatCount; set => SetProperty(ref _seatCount, value); }
    public string BusType { get => _busType; set => SetProperty(ref _busType, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }

    public AdminBusManagementViewModel(NavigationService navigationService)
    {
        AddCommand = new RelayCommand(async () => await AddAsync());
        DeleteCommand = new RelayCommand(async p =>
        {
            if (p is BusAdminItem b)
            {
                var result = await AdminDatabaseService.DeleteBusAsync(b.BusId);
                Message = result.Error ?? "Đã xóa xe.";
                await LoadAsync();
            }
        });
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Buses.Clear();
            foreach (var b in await AdminDatabaseService.GetBusesAsync()) Buses.Add(b);
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private async Task AddAsync()
    {
        if (!int.TryParse(SeatCount, out var count))
        {
            Message = "Số ghế không hợp lệ.";
            return;
        }
        var result = await AdminDatabaseService.AddBusAsync(Plate, count, BusType);
        Message = result.Error ?? "Đã thêm xe và sinh ghế.";
        if (result.Success)
        {
            Plate = "";
            await LoadAsync();
        }
    }
}

public sealed class AdminTripManagementViewModel : ViewModelBase
{
    private readonly NavigationService _navigationService;
    private int _selectedRouteId;
    private int _selectedBusId;
    private DateTime _departure = DateTime.Today.AddDays(1).AddHours(7);
    private DateTime _arrival = DateTime.Today.AddDays(1).AddHours(10);
    private string _price = "150000";
    private string _message = "";
    private bool _isEditing;
    private int _editingTripId;

    public ObservableCollection<RouteAdminItem> Routes { get; } = new();
    public ObservableCollection<BusAdminItem> Buses { get; } = new();
    public ObservableCollection<TripAdminItem> Trips { get; } = new();

    public int SelectedRouteId { get => _selectedRouteId; set => SetProperty(ref _selectedRouteId, value); }
    public int SelectedBusId { get => _selectedBusId; set => SetProperty(ref _selectedBusId, value); }
    public DateTime Departure { get => _departure; set => SetProperty(ref _departure, value); }
    public DateTime Arrival { get => _arrival; set => SetProperty(ref _arrival, value); }
    public string Price { get => _price; set => SetProperty(ref _price, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsEditing { get => _isEditing; private set => SetProperty(ref _isEditing, value); }

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand SelectForEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand DeleteCommand { get; }

    public AdminTripManagementViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        AddCommand = new RelayCommand(async () => await AddAsync());
        UpdateCommand = new RelayCommand(async () => await UpdateAsync());
        CancelEditCommand = new RelayCommand(() =>
        {
            IsEditing = false;
            _editingTripId = 0;
            Message = "";
        });
        SelectForEditCommand = new RelayCommand(p =>
        {
            if (p is TripAdminItem t)
            {
                _editingTripId = t.TripId;
                SelectedRouteId = t.RouteId;
                SelectedBusId = t.BusId;
                Departure = t.DepartureTime;
                Arrival = t.ArrivalTime;
                Price = t.Price.ToString("F0");
                IsEditing = true;
                Message = $"Đang chỉnh sửa chuyến xe #{t.TripId}";
            }
        });
        DeleteCommand = new RelayCommand(async p =>
        {
            if (p is TripAdminItem t)
            {
                var result = await AdminDatabaseService.DeleteTripAsync(t.TripId);
                Message = result.Error ?? "Đã xóa chuyến.";
                await LoadAsync();
            }
        });
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Routes.Clear();
            Buses.Clear();
            Trips.Clear();
            foreach (var r in await AdminDatabaseService.GetRoutesAsync()) Routes.Add(r);
            foreach (var b in await AdminDatabaseService.GetBusesAsync()) Buses.Add(b);
            foreach (var t in await AdminDatabaseService.GetTripsAsync()) Trips.Add(t);
            if (SelectedRouteId == 0 && Routes.Count > 0) SelectedRouteId = Routes[0].RouteId;
            if (SelectedBusId == 0 && Buses.Count > 0) SelectedBusId = Buses[0].BusId;
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private async Task AddAsync()
    {
        if (!decimal.TryParse(Price, out var price))
        {
            Message = "Giá vé không hợp lệ.";
            return;
        }
        var result = await AdminDatabaseService.AddTripAsync(SelectedRouteId, SelectedBusId, Departure, Arrival, price);
        Message = result.Error ?? "Đã thêm chuyến thành công.";
        if (result.Success) await LoadAsync();
    }

    private async Task UpdateAsync()
    {
        if (_editingTripId <= 0) return;
        if (!decimal.TryParse(Price, out var price))
        {
            Message = "Giá vé không hợp lệ.";
            return;
        }
        var result = await AdminDatabaseService.UpdateTripAsync(_editingTripId, Departure, Arrival, price);
        Message = result.Error ?? "Đã cập nhật chuyến thành công.";
        if (result.Success)
        {
            IsEditing = false;
            _editingTripId = 0;
            await LoadAsync();
        }
    }
}

public sealed class AdminAccountsViewModel : ViewModelBase
{
    private string _message = "";
    private string _searchQuery = "";
    private List<AccountAdminItem> _allAccounts = new();

    public ObservableCollection<AccountAdminItem> Accounts { get; } = new();
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                FilterAccounts();
        }
    }

    public ICommand ToggleRoleCommand { get; }
    public ICommand RefreshCommand { get; }

    public AdminAccountsViewModel(NavigationService navigationService)
    {
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
        ToggleRoleCommand = new RelayCommand(async p =>
        {
            if (p is AccountAdminItem acc)
            {
                var targetRole = acc.Role == "Admin" ? "Customer" : "Admin";
                var result = await AdminDatabaseService.UpdateAccountRoleAsync(acc.AccountId, targetRole);
                Message = result.Error ?? $"Đã cập nhật quyền cho {acc.Username} thành {targetRole}.";
                await LoadAsync();
            }
        });
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            _allAccounts = (await AdminDatabaseService.GetAccountsAsync()).ToList();
            FilterAccounts();
        }
        catch (Exception ex)
        {
            Message = "Không thể tải danh sách tài khoản: " + ex.Message;
        }
    }

    private void FilterAccounts()
    {
        Accounts.Clear();
        var query = SearchQuery.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allAccounts
            : _allAccounts.Where(a => a.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                      a.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                      (a.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var a in filtered) Accounts.Add(a);
    }
}
