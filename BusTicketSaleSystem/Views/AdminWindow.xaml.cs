using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;

namespace BusTicketSaleSystem.Views;

public partial class AdminWindow : Window
{
    private readonly IServiceScope _scope;

    public AdminWindow()
    {
        InitializeComponent();

        _scope = App.Services.CreateShellScope();
        var model = _scope.ServiceProvider.GetRequiredService<AdminMainViewModel>();
        model.RequestLogout += (_, _) => App.Services.GetRequiredService<ISessionService>().SignOut();

        // Each admin page must start at the top; the shell reuses one ScrollViewer.
        model.PropertyChanged += OnShellPropertyChanged;

        DataContext = model;
        Closed += (_, _) => _scope.Dispose();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdminMainViewModel.CurrentViewModel))
        {
            ContentScroller.ScrollToTop();
        }
    }
}
