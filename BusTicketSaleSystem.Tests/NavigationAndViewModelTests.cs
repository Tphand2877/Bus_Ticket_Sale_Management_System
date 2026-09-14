using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;

namespace BusTicketSaleSystem.Tests;

public sealed class NavigationAndViewModelTests
{
    private sealed class DummyViewModelA : ViewModelBase { }
    private sealed class DummyViewModelB : ViewModelBase { }

    [Fact]
    public void NavigationService_NavigatesAndRaisesPropertyChanged()
    {
        var nav = new NavigationService();
        nav.SetFactory(type =>
            type == typeof(DummyViewModelA) ? new DummyViewModelA() :
            type == typeof(DummyViewModelB) ? new DummyViewModelB() :
            throw new InvalidOperationException());

        var propertiesChanged = new List<string>();
        nav.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) propertiesChanged.Add(e.PropertyName);
        };

        nav.NavigateTo<DummyViewModelA>();
        Assert.IsType<DummyViewModelA>(nav.CurrentViewModel);

        nav.NavigateTo<DummyViewModelB>();
        Assert.IsType<DummyViewModelB>(nav.CurrentViewModel);

        Assert.Equal(2, propertiesChanged.Count(p => p == nameof(NavigationService.CurrentViewModel)));
    }

    private sealed class DummyViewModelWithDependency : ViewModelBase
    {
        public DummyViewModelWithDependency(string _) { }
    }

    [Fact]
    public void NavigationService_FallbackActivatesParameterlessViewModelWhenNoFactory()
    {
        var nav = new NavigationService();
        nav.NavigateTo<DummyViewModelA>();
        Assert.NotNull(nav.CurrentViewModel);
        Assert.IsType<DummyViewModelA>(nav.CurrentViewModel);
    }

    [Fact]
    public void NavigationService_ThrowsWhenViewModelRequiresDependenciesWithoutFactory()
    {
        var nav = new NavigationService();
        Assert.Throws<MissingMethodException>(() => nav.NavigateTo<DummyViewModelWithDependency>());
    }

    [Fact]
    public void NavigationService_ThrowsWhenNullViewModelPassed()
    {
        var nav = new NavigationService();
        Assert.Throws<ArgumentNullException>(() => nav.NavigateTo(null!));
    }

    [Fact]
    public async Task PaymentService_CreatePaymentAsync_RejectsInvalidInput()
    {
        var result1 = await PaymentService.CreatePaymentAsync(new PaymentRequest(0, "BT-123"));
        Assert.False(result1.IsSuccess);

        var result2 = await PaymentService.CreatePaymentAsync(new PaymentRequest(10, "   "));
        Assert.False(result2.IsSuccess);

        var result3 = await PaymentService.CreatePaymentAsync(new PaymentRequest(-5, "BT-123"));
        Assert.False(result3.IsSuccess);
    }
}
