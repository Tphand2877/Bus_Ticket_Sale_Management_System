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

        // Current user profile info
        public string UserName { get; set; } = CurrentUser.Account?.Username ?? "Guest";
        public string UserEmail { get; set; } = CurrentUser.Account?.Email ?? string.Empty;
        public string UserRole { get; set; } = CurrentUser.Account?.Role ?? "Customer";
        public string UserInitials { get; set; } = GetInitials(CurrentUser.Account?.Username);

        private static string GetInitials(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return "GU";
            return username.Length == 1 ? username.ToUpperInvariant() : username[..2].ToUpperInvariant();
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

        public MainViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Listen to NavigationService changes to notify WPF ContentControl
            if (_navigationService is INotifyPropertyChanged notifyNav)
            {
                notifyNav.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(INavigationService.CurrentViewModel))
                    {
                        OnPropertyChanged(nameof(CurrentViewModel));
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
