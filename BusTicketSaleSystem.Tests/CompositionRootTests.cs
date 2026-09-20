using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Every ViewModel the shells navigate to must be resolvable from the single
/// composition root, and each shell must get its own navigator (AUD-A-003).
/// </summary>
public sealed class CompositionRootTests
{
    private static readonly Type[] NavigableViewModels =
    [
        typeof(HomeViewModel), typeof(BookingViewModel), typeof(MyTicketsViewModel),
        typeof(HistoryViewModel), typeof(ProfileViewModel), typeof(MainViewModel),
        typeof(AdminDashboardViewModel), typeof(AdminRouteManagementViewModel),
        typeof(AdminBusManagementViewModel), typeof(AdminTripManagementViewModel),
        typeof(AdminPaymentsViewModel), typeof(AdminReportsViewModel),
        typeof(AdminAccountsViewModel)
    ];

    [Theory]
    [MemberData(nameof(NavigableViewModelData))]
    public void EveryNavigableViewModelResolvesFromTheCompositionRoot(Type viewModelType)
    {
        using var provider = AppComposition.Build();
        using var scope = ((IServiceProvider)provider).CreateShellScope();

        var resolved = scope.ServiceProvider.GetRequiredService(viewModelType);

        Assert.IsAssignableFrom<ViewModelBase>(resolved);
    }

    public static TheoryData<Type> NavigableViewModelData()
    {
        var data = new TheoryData<Type>();
        foreach (var type in NavigableViewModels) data.Add(type);
        return data;
    }

    /// <summary>
    /// The customer and admin shells run independent navigation stacks, so they must
    /// not share a navigator.
    /// </summary>
    [Fact]
    public void EachShellScopeOwnsItsOwnNavigator()
    {
        using var provider = AppComposition.Build();
        using var first = ((IServiceProvider)provider).CreateShellScope();
        using var second = ((IServiceProvider)provider).CreateShellScope();

        var a = first.ServiceProvider.GetRequiredService<INavigationService>();
        var b = second.ServiceProvider.GetRequiredService<INavigationService>();

        Assert.NotSame(a, b);
        Assert.Same(a, first.ServiceProvider.GetRequiredService<NavigationService>());
    }

    /// <summary>
    /// A page resolved through the shell scope must be able to navigate, i.e. it must
    /// receive the shell's navigator rather than a detached one.
    /// </summary>
    [Fact]
    public void PagesResolvedInAScopeShareTheShellNavigator()
    {
        using var provider = AppComposition.Build();
        using var scope = ((IServiceProvider)provider).CreateShellScope();
        var navigation = scope.ServiceProvider.GetRequiredService<NavigationService>();

        navigation.NavigateTo<BookingViewModel>();

        Assert.IsType<BookingViewModel>(navigation.CurrentViewModel);
    }

    [Fact]
    public void SignOutClearsTheSessionAndNotifiesOnce()
    {
        using var provider = AppComposition.Build();
        var session = provider.GetRequiredService<ISessionService>();
        session.SignIn(new BusTicketSaleSystem.Models.UserSession(1, "u", "User", null, null, "Customer"));

        var notifications = 0;
        session.SignedOut += (_, _) => notifications++;
        session.SignOut();

        Assert.Null(session.Account);
        Assert.False(session.IsAdmin);
        Assert.Equal(1, notifications);
    }
}
