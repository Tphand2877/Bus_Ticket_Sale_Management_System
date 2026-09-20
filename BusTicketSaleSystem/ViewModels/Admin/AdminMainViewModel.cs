using System;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.ViewModels;

public sealed class AdminMainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _activeTab = "Dashboard";
    public ViewModelBase? CurrentViewModel => _navigationService.CurrentViewModel;
    public string ActiveTab { get => _activeTab; private set => SetProperty(ref _activeTab, value); }
    public string AdminDisplayName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Quản trị viên";
    public string AdminRole => CurrentUser.Account?.Role ?? "Admin";
    public string CurrentLanguageCode => LocalizationService.Instance.IsVietnamese ? "VI" : "EN";
    public string CurrentLanguageDisplay => LocalizationService.Instance.IsVietnamese ? "🇻🇳 Tiếng Việt" : "🇬🇧 English";
    public string StationScope => LocationService.Instance.MachineLocation;
    public string StationScopeDisplay => $"📍 Bến xe {StationScope}";

    public ICommand DashboardCommand { get; }
    public ICommand RoutesCommand { get; }
    public ICommand BusesCommand { get; }
    public ICommand TripsCommand { get; }
    public ICommand PaymentsCommand { get; }
    public ICommand ReportsCommand { get; }
    public ICommand AccountsCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public event EventHandler? RequestLogout;

    public AdminMainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(INavigationService.CurrentViewModel))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
                ActiveTab = CurrentViewModel switch
                {
                    AdminRouteManagementViewModel => "Routes",
                    AdminBusManagementViewModel => "Buses",
                    AdminTripManagementViewModel => "Trips",
                    AdminPaymentsViewModel => "Payments",
                    AdminReportsViewModel => "Reports",
                    AdminAccountsViewModel => "Accounts",
                    _ => "Dashboard"
                };
            }
        };
        DashboardCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminDashboardViewModel>());
        RoutesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminRouteManagementViewModel>());
        BusesCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminBusManagementViewModel>());
        TripsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminTripManagementViewModel>());
        PaymentsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminPaymentsViewModel>());
        ReportsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminReportsViewModel>());
        AccountsCommand = new RelayCommand(() => _navigationService.NavigateTo<AdminAccountsViewModel>());
        LogoutCommand = new RelayCommand(() => RequestLogout?.Invoke(this, EventArgs.Empty));
        ToggleLanguageCommand = new RelayCommand(() => LocalizationService.Instance.ToggleLanguage());
        LocalizationService.Instance.LanguageChanged += _ =>
        {
            OnPropertyChanged(nameof(CurrentLanguageCode));
            OnPropertyChanged(nameof(CurrentLanguageDisplay));
        };
        LocationService.Instance.LocationChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(StationScope));
            OnPropertyChanged(nameof(StationScopeDisplay));
        };

        // The factory is supplied by the composition root (AppComposition.CreateShellScope).
        _navigationService.NavigateTo<AdminDashboardViewModel>();
    }
}
