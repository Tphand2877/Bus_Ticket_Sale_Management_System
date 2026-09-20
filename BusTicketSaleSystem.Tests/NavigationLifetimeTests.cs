using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// The navigator replaces ViewModels, so it owns their end of life. Before AUD-W2-02
/// every screen that subscribed to a singleton service leaked one live instance per
/// navigation, and those dead instances kept reacting to service events.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class NavigationLifetimeTests
{
    private sealed class TrackingViewModel : ViewModelBase, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public void NavigatingAway_DisposesTheOutgoingViewModel()
    {
        var navigation = new NavigationService();
        var first = new TrackingViewModel();
        var second = new TrackingViewModel();

        navigation.NavigateTo(first);
        Assert.Equal(0, first.DisposeCount);

        navigation.NavigateTo(second);

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);
        Assert.Same(second, navigation.CurrentViewModel);
    }

    [Fact]
    public void NavigatingToTheSameInstance_DoesNotDisposeIt()
    {
        var navigation = new NavigationService();
        var viewModel = new TrackingViewModel();

        navigation.NavigateTo(viewModel);
        navigation.NavigateTo(viewModel);

        Assert.Equal(0, viewModel.DisposeCount);
    }

    /// <summary>A shell-owned singleton must survive navigation away and back.</summary>
    [Fact]
    public void RegisteredSingletons_AreNeverDisposedByNavigation()
    {
        var navigation = new NavigationService();
        var shellOwned = new TrackingViewModel();
        navigation.Register(shellOwned);

        navigation.NavigateTo(shellOwned);
        navigation.NavigateTo(new TrackingViewModel());

        Assert.Equal(0, shellOwned.DisposeCount);
    }
    /// <summary>
    /// The concrete defect: repeated navigation must not accumulate handlers on the
    /// process-lifetime LocationService.
    /// </summary>
    [Fact]
    public void RepeatedNavigation_DoesNotAccumulateLocationSubscribers()
    {
        var navigation = new NavigationService();
        var dialog = new FakePaymentDialogService();
        navigation.SetFactory(type => type == typeof(HomeViewModel)
            ? new HomeViewModel(navigation, dialog)
            : new BookingViewModel(navigation, dialog));

        var before = LocationSubscriberCount();

        for (var i = 0; i < 10; i++)
        {
            navigation.NavigateTo<HomeViewModel>();
            navigation.NavigateTo<BookingViewModel>();
        }
        navigation.NavigateTo(new TrackingViewModel());

        Assert.Equal(before, LocationSubscriberCount());
    }

    /// <summary>
    /// Reads the invocation list of the singleton's event. A leaked ViewModel shows up
    /// here as an extra handler that no navigation will ever remove.
    /// </summary>
    private static int LocationSubscriberCount()
    {
        var field = typeof(LocationService).GetField("LocationChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return (field!.GetValue(LocationService.Instance) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }
}
