using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;

namespace BusTicketSaleSystem.Views;

public partial class MainWindow : Window
{
    private readonly IServiceScope _scope;

    public MainWindow()
    {
        InitializeComponent();

        // One navigation scope per shell; the composition root owns every graph.
        _scope = App.Services.CreateShellScope();
        var mainViewModel = _scope.ServiceProvider.GetRequiredService<MainViewModel>();
        mainViewModel.RequestLogout += (_, _) => App.Services.GetRequiredService<ISessionService>().SignOut();

        // A new page must start at the top. The shell reuses a single ScrollViewer for every
        // page, so without this the user lands mid-page after navigating away from a scrolled one.
        mainViewModel.PropertyChanged += OnShellPropertyChanged;

        DataContext = mainViewModel;
        Closed += (_, _) => _scope.Dispose();
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentViewModel))
        {
            ContentScroller.ScrollToTop();
        }
    }
}
