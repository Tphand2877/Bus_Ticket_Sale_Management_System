using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.Models;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    /// <summary>
    /// Shell ViewModel for MainWindow.
    /// Hosts the CurrentViewModel for ViewModel-first navigation and handles sidebar actions.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private string _activeTab = "Overview";

        public ViewModelBase? CurrentViewModel => _navigationService.CurrentViewModel;

        public string ActiveTab
        {
            get => _activeTab;
            set => SetProperty(ref _activeTab, value);
        }

        // Current user profile info, read live so a profile edit is reflected immediately.
        public string UserName => CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username ?? "Guest";
        public string UserRole => CurrentUser.Account?.Role ?? "Customer";
        public string UserInitials => GetInitials(CurrentUser.Account?.FullName ?? CurrentUser.Account?.Username);
        public string CurrentLanguageCode => LocalizationService.Instance.IsVietnamese ? "VI" : "EN";
        public string CurrentLanguageDisplay => LocalizationService.Instance.IsVietnamese ? "🇻🇳 Tiếng Việt" : "🇬🇧 English";
        public string CurrentLocation
        {
            get => LocationService.Instance.CurrentLocation;
            set
            {
                if (LocationService.Instance.SetUserLocation(value))
                {
                    OnPropertyChanged(nameof(CurrentLocation));
                    OnPropertyChanged(nameof(LocationDisplay));
                }
            }
        }
        public string LocationDisplay => $"📍 {CurrentLocation}";
        public IReadOnlyList<string> AvailableLocations => LocationService.Instance.AvailableLocations;
        private static string GetInitials(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "GU";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "GU";
            if (parts.Length >= 2) return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
            return parts[0].Length == 1 ? parts[0].ToUpperInvariant() : parts[0][..2].ToUpperInvariant();
        }

        // Navigation Commands
        public ICommand NavigateHomeCommand { get; }
        public ICommand NavigateBookingCommand { get; }
        public ICommand NavigateMyTicketsCommand { get; }
        public ICommand NavigateHistoryCommand { get; }
        public ICommand NavigateProfileCommand { get; }

        // Event to request logout and switch back to LoginWindow
        public event EventHandler? RequestLogout;
        public ICommand LogoutCommand { get; }
        public ICommand ToggleLanguageCommand { get; }
        public ICommand SelectLocationCommand { get; }

        public MainViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            SelectLocationCommand = new RelayCommand(p =>
            {
                if (p is string loc) CurrentLocation = loc;
            });
            LocationService.Instance.LocationChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CurrentLocation));
                OnPropertyChanged(nameof(LocationDisplay));
            };
            ToggleLanguageCommand = new RelayCommand(() => LocalizationService.Instance.ToggleLanguage());
            LocalizationService.Instance.LanguageChanged += _ =>
            {
                OnPropertyChanged(nameof(CurrentLanguageCode));
                OnPropertyChanged(nameof(CurrentLanguageDisplay));
            };


            // Listen to NavigationService changes to notify WPF ContentControl
            if (_navigationService is INotifyPropertyChanged notifyNav)
            {
                notifyNav.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(INavigationService.CurrentViewModel))
                    {
                        OnPropertyChanged(nameof(CurrentViewModel));
                        OnPropertyChanged(nameof(UserName));
                        OnPropertyChanged(nameof(UserRole));
                        OnPropertyChanged(nameof(UserInitials));
                        UpdateActiveTabFromCurrentViewModel();
                    }
                };
            }

            NavigateHomeCommand = new RelayCommand(() =>
            {
                ActiveTab = "Overview";
                _navigationService.NavigateTo<HomeViewModel>();
            });

            NavigateBookingCommand = new RelayCommand(() =>
            {
                ActiveTab = "Booking";
                _navigationService.NavigateTo<BookingViewModel>();
            });

            NavigateMyTicketsCommand = new RelayCommand(() =>
            {
                ActiveTab = "MyTickets";
                _navigationService.NavigateTo<MyTicketsViewModel>();
            });

            NavigateHistoryCommand = new RelayCommand(() =>
            {
                ActiveTab = "History";
                _navigationService.NavigateTo<HistoryViewModel>();
            });

            NavigateProfileCommand = new RelayCommand(() =>
            {
                ActiveTab = "Account";
                _navigationService.NavigateTo<ProfileViewModel>();
            });

            LogoutCommand = new RelayCommand(() =>
            {
                RequestLogout?.Invoke(this, EventArgs.Empty);
            });

            _navigationService.NavigateTo<HomeViewModel>();
        }

        private void UpdateActiveTabFromCurrentViewModel()
        {
            if (CurrentViewModel is HomeViewModel) ActiveTab = "Overview";
            else if (CurrentViewModel is BookingViewModel or SeatSelectionViewModel or BookingConfirmationViewModel) ActiveTab = "Booking";
            else if (CurrentViewModel is MyTicketsViewModel or TicketViewModel) ActiveTab = "MyTickets";
            else if (CurrentViewModel is HistoryViewModel) ActiveTab = "History";
            else if (CurrentViewModel is ProfileViewModel) ActiveTab = "Account";
        }
    }
}
