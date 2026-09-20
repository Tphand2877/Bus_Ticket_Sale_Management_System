using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.ViewModels;

public sealed class AdminDashboardViewModel : ViewModelBase, IRefreshable
{
    private static readonly (string Server, string Catalog) DatabaseTarget = ReadDatabaseTarget();

    private readonly NavigationService _navigationService;
    private string _message = "";
    private bool _isLoading;
    private string _lastRefreshed = "Chưa tải dữ liệu";

    public AdminDashboardStats Stats { get; private set; } = AdminDashboardStats.Empty;
    public ObservableCollection<RecentBookingAdminItem> RecentBookings { get; } = new();
    public ObservableCollection<TripAdminItem> UpcomingTrips { get; } = new();
    public ObservableCollection<BusAdminItem> BusFleet { get; } = new();

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage));
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

    public string AdminName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Quản trị viên";
    public string AdminRole => CurrentUser.Account?.Role ?? "Admin";

    /// <summary>The SQL Server instance the app is actually configured against.</summary>
    public string DatabaseServer => DatabaseTarget.Server;

    /// <summary>The catalog (database) name taken from the configured connection string.</summary>
    public string DatabaseCatalog => DatabaseTarget.Catalog;

    public ICommand RefreshCommand { get; }
    public ICommand NavigateRoutesCommand { get; }
    public ICommand NavigateBusesCommand { get; }
    public ICommand NavigateTripsCommand { get; }
    public ICommand NavigatePaymentsCommand { get; }
    public ICommand NavigateReportsCommand { get; }
    public ICommand ClearMessageCommand { get; }

    public AdminDashboardViewModel(NavigationService navigationService)
    {
        _navigationService = navigationService;
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        NavigateRoutesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminRouteManagementViewModel>());
        NavigateBusesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminBusManagementViewModel>());
        NavigateTripsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminTripManagementViewModel>());
        NavigatePaymentsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminPaymentsViewModel>());
        NavigateReportsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminReportsViewModel>());
        ClearMessageCommand = new RelayCommand(() => Message = "");
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            // Four independent reads, each on its own connection: issued together the
            // landing screen costs one round trip instead of four (AUD-E-012).
            var statsTask = AdminDatabaseService.GetDashboardStatsAsync();
            var bookingsTask = AdminDatabaseService.GetRecentBookingsAsync(6);
            var tripsTask = AdminDatabaseService.GetUpcomingTripsAsync(5);
            var busesTask = AdminDatabaseService.GetBusesAsync();
            await Task.WhenAll(statsTask, bookingsTask, tripsTask, busesTask);

            Stats = await statsTask;
            OnPropertyChanged(nameof(Stats));

            RecentBookings.Clear();
            foreach (var b in await bookingsTask) RecentBookings.Add(b);
            OnPropertyChanged(nameof(HasRecentBookings));
            OnPropertyChanged(nameof(HasNoRecentBookings));

            UpcomingTrips.Clear();
            foreach (var t in await tripsTask) UpcomingTrips.Add(t);
            OnPropertyChanged(nameof(HasUpcomingTrips));
            OnPropertyChanged(nameof(HasNoUpcomingTrips));

            BusFleet.Clear();
            foreach (var bus in (await busesTask).Take(5)) BusFleet.Add(bus);
            OnPropertyChanged(nameof(HasBuses));
            OnPropertyChanged(nameof(HasNoBuses));

            LastRefreshed = $"Dữ liệu lúc {DateTime.Now:HH:mm:ss}";
            Message = "";
        }
        catch (Exception ex)
        {
            LastRefreshed = "Không tải được dữ liệu";
            Message = AdminFailure.Describe(ex, "Tải trang tổng quan");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static (string Server, string Catalog) ReadDatabaseTarget()
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(DatabaseOptions.ConnectionString);
            return (
                string.IsNullOrWhiteSpace(builder.DataSource) ? "Chưa cấu hình" : builder.DataSource,
                string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "Chưa cấu hình" : builder.InitialCatalog);
        }
        catch (ArgumentException ex)
        {
            LoggerService.LogError("Không thể phân tích chuỗi kết nối đã cấu hình.", ex);
            return ("Chuỗi kết nối không hợp lệ", "Chuỗi kết nối không hợp lệ");
        }
    }
}
