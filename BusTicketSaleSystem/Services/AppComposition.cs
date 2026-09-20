using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BusTicketSaleSystem.Services;

/// <summary>
/// The application's single composition root (AUD-A-003).
/// </summary>
/// <remarks>
/// Object graphs used to be hand-wired in two places — a ternary chain in
/// <c>MainWindow</c>'s code-behind and another in <c>AdminMainViewModel</c> — so adding
/// a dependency to any ViewModel meant editing both, and neither factory could be
/// reused or tested.
/// <para>
/// Navigation is <b>scoped</b>: each shell window owns one <see cref="NavigationService"/>
/// and the pages it creates resolve that same instance, which is what lets the customer
/// and admin shells run their own navigation stacks.
/// </para>
/// </remarks>
public static class AppComposition
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // One navigator per shell.
        services.AddScoped<NavigationService>();
        services.AddScoped<INavigationService>(provider => provider.GetRequiredService<NavigationService>());
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IPaymentDialogService, Views.PaymentDialogService>();

        // Pages are transient so a screen never shows data captured at first construction.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<BookingViewModel>();
        services.AddTransient<BusCompanyDetailViewModel>();
        services.AddTransient<MyTicketsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<MainViewModel>();

        services.AddTransient<AdminMainViewModel>();
        services.AddTransient<AdminDashboardViewModel>();
        services.AddTransient<AdminRouteManagementViewModel>();
        services.AddTransient<AdminBusManagementViewModel>();
        services.AddTransient<AdminTripManagementViewModel>();
        services.AddTransient<AdminPaymentsViewModel>();
        services.AddTransient<AdminReportsViewModel>();
        services.AddTransient<AdminAccountsViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a shell scope and points its navigator at the scope's provider, so
    /// every page the shell navigates to is resolved with its dependencies.
    /// </summary>
    public static IServiceScope CreateShellScope(this IServiceProvider provider)
    {
        var scope = provider.CreateScope();
        var navigation = scope.ServiceProvider.GetRequiredService<NavigationService>();
        navigation.SetFactory(type => (ViewModelBase)scope.ServiceProvider.GetRequiredService(type));
        return scope;
    }
}
