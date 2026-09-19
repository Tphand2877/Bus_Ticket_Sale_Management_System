using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;

namespace BusTicketSaleSystem.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var navigationService = new NavigationService();
        navigationService.SetFactory(type => type == typeof(HomeViewModel) ? new HomeViewModel(navigationService)
            : type == typeof(BookingViewModel) ? new BookingViewModel(navigationService)
            : type == typeof(MyTicketsViewModel) ? new MyTicketsViewModel(navigationService)
            : type == typeof(HistoryViewModel) ? new HistoryViewModel(navigationService)
            : type == typeof(ProfileViewModel) ? new ProfileViewModel(navigationService)
            : throw new InvalidOperationException($"No factory registered for {type.Name}"));
        var mainViewModel = new MainViewModel(navigationService);
        mainViewModel.RequestLogout += (_, _) =>
        {
            CurrentUser.Account = null;
            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        };

        // A new page must start at the top. The shell reuses a single ScrollViewer for every
        // page, so without this the user lands mid-page after navigating away from a scrolled one.
        mainViewModel.PropertyChanged += OnShellPropertyChanged;

        DataContext = mainViewModel;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentViewModel))
        {
            ContentScroller.ScrollToTop();
        }
    }
}
