using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using System.Windows;

namespace BusTicketSaleSystem.Views;

public partial class AdminWindow : Window
{
    public AdminWindow()
    {
        InitializeComponent();
        var navigation = new NavigationService();
        var model = new AdminMainViewModel(navigation);
        model.RequestLogout += (_, _) =>
        {
            CurrentUser.Account = null;
            var login = new LoginWindow();
            Application.Current.MainWindow = login;
            login.Show();
            Close();
        };
        DataContext = model;
    }
}
