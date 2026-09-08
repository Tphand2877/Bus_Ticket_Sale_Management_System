using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using System;
using System.Windows;

namespace BusTicketSaleSystem.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// Thin code-behind shell: delegates all navigation and data handling to MainViewModel.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Set up shared NavigationService and ViewModels
            var navigationService = new NavigationService();

            // Factory for resolving ViewModels with dependencies
            navigationService.SetFactory(type =>
            {
                if (type == typeof(HomeViewModel)) return new HomeViewModel(navigationService);
                if (type == typeof(BookingViewModel)) return new BookingViewModel(navigationService);
                if (type == typeof(SeatSelectionViewModel)) return new SeatSelectionViewModel(navigationService);
                if (type == typeof(BookingConfirmationViewModel)) return new BookingConfirmationViewModel(navigationService);
                if (type == typeof(TicketViewModel)) return new TicketViewModel(navigationService);
                if (type == typeof(MyTicketsViewModel)) return new MyTicketsViewModel(navigationService);
                if (type == typeof(HistoryViewModel)) return new HistoryViewModel(navigationService);
                if (type == typeof(ProfileViewModel)) return new ProfileViewModel(navigationService);

                return (Common.ViewModelBase)Activator.CreateInstance(type)!;
            });

            var mainViewModel = new MainViewModel(navigationService);

            // Handle user logout request from MainViewModel
            mainViewModel.RequestLogout += (s, e) =>
            {
                var loginWindow = new LoginWindow();
                Application.Current.MainWindow = loginWindow;
                loginWindow.Show();
                this.Close();
            };

            DataContext = mainViewModel;
        }
    }
}
