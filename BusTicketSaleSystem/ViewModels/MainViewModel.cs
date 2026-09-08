using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
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

        // Current user static profile info
        public string UserName { get; set; } = "Alex Morgan";
        public string UserEmail { get; set; } = "alex.morgan@busxpress.com";
        public string UserRole { get; set; } = "Gold Passenger";
        public string UserInitials { get; set; } = "AM";

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
        }

        private void UpdateActiveTabFromCurrentViewModel()
        {
            if (CurrentViewModel is HomeViewModel) ActiveTab = "Overview";
            else if (CurrentViewModel is BookingViewModel) ActiveTab = "Booking";
            else if (CurrentViewModel is MyTicketsViewModel) ActiveTab = "MyTickets";
            else if (CurrentViewModel is HistoryViewModel) ActiveTab = "History";
            else if (CurrentViewModel is ProfileViewModel) ActiveTab = "Account";
            else if (CurrentViewModel is TicketViewModel) ActiveTab = "MyTickets";
        }
    }
}
