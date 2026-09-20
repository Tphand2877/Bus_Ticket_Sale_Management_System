using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;
using BusTicketSaleSystem.Models;

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

    /// <summary>
    /// Navigating without a factory used to fall back to Activator, producing a
    /// ViewModel wired to a detached NavigationService: the screen rendered but none
    /// of its navigation worked, with no exception and no log entry (AUD-A-010).
    /// </summary>
    [Fact]
    public void NavigationService_ThrowsADiagnosableErrorWhenNoFactoryIsSet()
    {
        var nav = new NavigationService();

        var error = Assert.Throws<InvalidOperationException>(() => nav.NavigateTo<DummyViewModelA>());

        Assert.Contains(nameof(DummyViewModelA), error.Message);
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

    [Theory]
    [InlineData("short1A", false, "Password must be 8-128 characters")]
    [InlineData("alllowercase123", false, "Password must be 8-128 characters and include an uppercase letter, a lowercase letter and a digit.")]
    [InlineData("ALLUPPERCASE123", false, "Password must be 8-128 characters and include an uppercase letter, a lowercase letter and a digit.")]
    [InlineData("NoDigitsInPassword", false, "Password must be 8-128 characters and include an uppercase letter, a lowercase letter and a digit.")]
    public void SignUpViewModel_ValidatesPasswordComplexity(string password, bool expectedValid, string expectedSubstring)
    {
        var vm = new SignUpViewModel
        {
            FullName = "Nguyen Van Test",
            Username = "testuser",
            Password = password,
            ConfirmPassword = password
        };

        vm.CreateAccountCommand.Execute(null);

        if (!expectedValid)
        {
            Assert.Contains(expectedSubstring, vm.ErrorMessage);
        }
        else
        {
            Assert.Empty(vm.ErrorMessage);
        }
    }

    [Fact]
    public void SignUpViewModel_RejectsPasswordConfirmationMismatch()
    {
        var vm = new SignUpViewModel
        {
            FullName = "Nguyen Van Test",
            Username = "testuser",
            Password = "Password123",
            ConfirmPassword = "DifferentPassword123"
        };

        vm.CreateAccountCommand.Execute(null);
        Assert.Equal("The confirmation password does not match.", vm.ErrorMessage);
    }

    [Fact]
    public void AdminBusManagementViewModel_FormInitiallyHiddenAndToggledByCommand()
    {
        var vm = new AdminBusManagementViewModel();

        Assert.False(vm.IsFormVisible);
        Assert.Equal("+ Đăng ký xe mới", vm.FormToggleButtonText);

        vm.ToggleFormCommand.Execute(null);
        Assert.True(vm.IsFormVisible);
        Assert.Equal("✕ Đóng form", vm.FormToggleButtonText);

        vm.ToggleFormCommand.Execute(null);
        Assert.False(vm.IsFormVisible);
        Assert.Equal("+ Đăng ký xe mới", vm.FormToggleButtonText);
    }

    [Fact]
    public void AdminBusManagementViewModel_HasRequestedBusTypesAndOperationTypes()
    {
        var vm = new AdminBusManagementViewModel();

        Assert.Contains("Limousine", vm.BusTypes);
        Assert.Contains("Ghế ngồi", vm.BusTypes);
        Assert.Contains("Ghế nằm", vm.BusTypes);
        Assert.Contains("Cabin", vm.BusTypes);

        Assert.DoesNotContain("Limousine VIP", vm.OperationTypes);
        Assert.Contains("Tuyến cố định", vm.OperationTypes);
        Assert.Contains("Hợp đồng", vm.OperationTypes);
        Assert.Contains("Du lịch", vm.OperationTypes);
    }

    [Fact]
    public void AdminBusManagementViewModel_UpdatesRouteWhenOriginAndDestinationSelected()
    {
        var vm = new AdminBusManagementViewModel();
        vm.AvailableRoutes.Add(new RouteAdminItem(10, "Hà Nội", "Thái Nguyên", 80));
        vm.AvailableRoutes.Add(new RouteAdminItem(11, "Hà Nội", "Hải Phòng", 120));

        vm.SelectedOrigin = "Hà Nội";
        vm.SelectedDestination = "Thái Nguyên";

        Assert.NotNull(vm.SelectedRoute);
        Assert.Equal(10, vm.SelectedRoute.RouteId);

        vm.SelectedDestination = "Hải Phòng";
        Assert.NotNull(vm.SelectedRoute);
        Assert.Equal(11, vm.SelectedRoute.RouteId);
    }

    [Fact]
    public void BookingViewModel_SelectCompanyCommand_NavigatesToBusCompanyDetailViewModel()
    {
        var nav = new NavigationService();
        var bookingVm = new BookingViewModel(nav);
        nav.NavigateTo(bookingVm);

        var company = new BusCompanyItem(
            1, "Sao Việt", "1900 1234", 4.8m, 1250, "Hà Nội - Hải Phòng",
            "Mô tả", "Wifi", "SV", "#0F4C81", 140000m, 5);

        bookingVm.SelectCompanyCommand.Execute(company);

        Assert.NotNull(nav.CurrentViewModel);
        var detailVm = Assert.IsType<BusCompanyDetailViewModel>(nav.CurrentViewModel);
        Assert.Equal("Sao Việt", detailVm.Company.CompanyName);
    }

    [Fact]
    public void BusCompanyDetailViewModel_BackCommand_NavigatesBackToBookingViewModel()
    {
        var nav = new NavigationService();
        var company = new BusCompanyItem(
            1, "Sao Việt", "1900 1234", 4.8m, 1250, "Hà Nội - Hải Phòng",
            "Mô tả", "Wifi", "SV", "#0F4C81");
        var detailVm = new BusCompanyDetailViewModel(nav, company);
        nav.NavigateTo(detailVm);

        detailVm.BackCommand.Execute(null);

        Assert.NotNull(nav.CurrentViewModel);
        Assert.IsType<BookingViewModel>(nav.CurrentViewModel);
    }

    [Fact]
    public void BusCompanyDetailViewModel_BookTripCommand_NavigatesToSeatSelectionForAvailableTrip()
    {
        var nav = new NavigationService();
        var company = new BusCompanyItem(
            1, "Sao Việt", "1900 1234", 4.8m, 1250, "Hà Nội - Hải Phòng",
            "Mô tả", "Wifi", "SV", "#0F4C81");
        var detailVm = new BusCompanyDetailViewModel(nav, company);
        nav.NavigateTo(detailVm);

        // 1. Available trip -> Should navigate to SeatSelectionViewModel
        var availableTrip = new TripSearchResult(
            1, "Hà Nội", "Hải Phòng", "Ghế ngồi",
            DateTime.Now.AddHours(3), DateTime.Now.AddHours(5),
            140000m, 20);

        detailVm.BookTripCommand.Execute(availableTrip);
        Assert.IsType<SeatSelectionViewModel>(nav.CurrentViewModel);

        // 2. Unavailable past trip -> Should reject and not navigate
        nav.NavigateTo(detailVm);
        var pastTrip = new TripSearchResult(
            2, "Hà Nội", "Hải Phòng", "Ghế ngồi",
            DateTime.Now.AddHours(-3), DateTime.Now.AddHours(-1),
            140000m, 20);

        detailVm.BookTripCommand.Execute(pastTrip);
        Assert.IsType<BusCompanyDetailViewModel>(nav.CurrentViewModel);
        Assert.Contains("không thể đặt", detailVm.StatusMessage);
    }
    private static readonly ManualResetEventSlim _appReady = new(false);
    private static int _appCreating;
    /// <summary>Shared Application bootstrap — safe to call from any STA thread.</summary>
    internal static void EnsureWpfApp()
    {
        if (Interlocked.CompareExchange(ref _appCreating, 1, 0) == 0)
        {
            var app = new System.Windows.Application();
            app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri("/BusTicketSaleSystem;component/Theme.xaml", UriKind.RelativeOrAbsolute)
            });
            app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri("/BusTicketSaleSystem;component/Resources/Strings.en-US.xaml", UriKind.RelativeOrAbsolute)
            });
            _appReady.Set();
        }
        else
        {
            _appReady.Wait(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void BusCompanyDetailView_CanBeInstantiatedWithoutResourceError()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApp();
                var view = new BusTicketSaleSystem.Views.BusCompanyDetailView();
                Assert.NotNull(view);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadException);
    }

    [Fact]
    public void BookingView_CanBeInstantiatedWithoutResourceError()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApp();
                var view = new BusTicketSaleSystem.Views.BookingView();
                Assert.NotNull(view);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadException);
    }

    [Fact]
    public void AdminBusManagementViewModel_QuickPresets_SetExpectedTimeSlots()
    {
        var vm = new AdminBusManagementViewModel();
        vm.ClearTimeSlotsCommand.Execute(null);
        Assert.Equal("", vm.DepartureTimeNote);

        vm.PresetMorningCommand.Execute(null);
        Assert.Equal("06:00, 08:30", vm.DepartureTimeNote);

        vm.PresetEveningCommand.Execute(null);
        Assert.Equal("06:00, 08:30, 19:30, 21:00", vm.DepartureTimeNote);

        vm.PresetContinuousHourlyCommand.Execute(null);
        Assert.Contains("06:00", vm.DepartureTimeNote);
        Assert.Contains("12:00", vm.DepartureTimeNote);
        Assert.Contains("20:00", vm.DepartureTimeNote);
    }

    [Fact]
    public void AdminBusManagementViewModel_FleetFilter_FiltersByCompanyAndSearchQuery()
    {
        var vm = new AdminBusManagementViewModel();
        vm.Buses.Add(new BusAdminItem(1, "20B-001.23", 9, "Limousine", 0, "Hà Lan Buslines", "Tài xế 1", "0911", "06:00", "Thái Nguyên → Hà Nội"));
        vm.Buses.Add(new BusAdminItem(2, "20B-002.34", 16, "Limousine", 0, "Hà Lan Buslines", "Tài xế 2", "0922", "07:00", "Thái Nguyên → Bắc Ninh"));
        vm.Buses.Add(new BusAdminItem(3, "29B-999.88", 40, "Ghế ngồi", 0, "Sao Việt", "Tài xế 3", "0933", "08:00", "Hà Nội → Lào Cai"));

        vm.AvailableCompanies.Add("Hà Lan Buslines");
        vm.AvailableCompanies.Add("Sao Việt");

        // Filter by Hà Lan Buslines
        vm.SelectedCompanyFilter = "Hà Lan Buslines";
        Assert.Equal(2, vm.FilteredBuses.Count);
        Assert.All(vm.FilteredBuses, b => Assert.Equal("Hà Lan Buslines", b.CompanyName));
        Assert.Contains("Nhà xe Hà Lan Buslines", vm.FleetSummaryText);

        // Search by plate
        vm.FleetSearchText = "002.34";
        Assert.Single(vm.FilteredBuses);
        Assert.Equal("20B-002.34", vm.FilteredBuses[0].LicensePlate);

        // Reset filter
        vm.SelectedCompanyFilter = "Tất cả nhà xe";
        vm.FleetSearchText = "";
        Assert.Equal(3, vm.FilteredBuses.Count);
    }

    [Fact]
    public void AdminBusManagementViewModel_RouteSelection_SuggestsFarePrice()
    {
        var vm = new AdminBusManagementViewModel();
        vm.AvailableRoutes.Add(new RouteAdminItem(10, "Thái Nguyên", "Hà Nội", 80));
        vm.FarePrice = "";

        vm.SelectedOrigin = "Thái Nguyên";
        vm.SelectedDestination = "Hà Nội";

        Assert.NotNull(vm.SelectedRoute);
        Assert.False(string.IsNullOrWhiteSpace(vm.FarePrice));
        Assert.True(decimal.TryParse(vm.FarePrice, out var price) && price >= 100000);
    }

    [Fact]
    public void AdminBusManagementView_CanBeInstantiatedWithoutResourceError()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApp();
                var view = new BusTicketSaleSystem.Views.AdminBusManagementView();
                Assert.NotNull(view);
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadException);
    }

    [Fact]
    public void AdminBusManagementViewModel_MultiVehicleDraft_SupportsDifferentRoutesAndDrivers()
    {
        var vm = new AdminBusManagementViewModel();
        vm.CompanyName = "Hà Lan Buslines";
        vm.CompanyHotline = "1900 6868";
        vm.CompanyOperatingArea = "Thái Nguyên - Hà Nội - Tuyên Quang";

        // 1. Configure Vehicle 1: Hà Nội -> Thái Nguyên with Driver A
        vm.Plate = "20B-001.23";
        vm.SeatCount = "9";
        vm.BusType = "Limousine";
        vm.DriverName = "Nguyễn Văn A";
        vm.DriverPhone = "0911.111.111";
        vm.SelectedOrigin = "Hà Nội";
        vm.SelectedDestination = "Thái Nguyên";
        vm.IntermediateStops = "Sân bay Nội Bài, Phổ Yên";
        vm.FarePrice = "160000";
        vm.StagePrice = "80000";
        vm.DepartureTimeNote = "06:00, 08:30, 11:00, 14:00";
        vm.SaveVehicleDraftCommand.Execute(null);

        Assert.Single(vm.DraftBuses);
        Assert.True(vm.HasDraftBuses);
        Assert.Equal("20B-001.23", vm.DraftBuses[0].Plate);
        Assert.Equal("Nguyễn Văn A", vm.DraftBuses[0].DriverName);
        Assert.Equal("Hà Nội → Thái Nguyên", vm.DraftBuses[0].RouteDisplay);
        Assert.Equal("160000", vm.DraftBuses[0].FarePrice);
        Assert.Equal("80000", vm.DraftBuses[0].StagePrice);

        // 2. Configure Vehicle 2: Thái Nguyên -> Tuyên Quang with Driver B
        vm.StartNewVehicleDraftCommand.Execute(null);
        vm.Plate = "20B-002.34";
        vm.SeatCount = "16";
        vm.BusType = "Limousine";
        vm.DriverName = "Trần Văn B";
        vm.DriverPhone = "0922.222.222";
        vm.SelectedOrigin = "Thái Nguyên";
        vm.SelectedDestination = "Tuyên Quang";
        vm.IntermediateStops = "Đại Từ, Sơn Dương";
        vm.FarePrice = "120000";
        vm.StagePrice = "50000";
        vm.DepartureTimeNote = "07:00, 10:00, 13:30, 17:00";
        vm.SaveVehicleDraftCommand.Execute(null);

        Assert.Equal(2, vm.DraftBuses.Count);
        Assert.Equal("Trần Văn B", vm.DraftBuses[1].DriverName);
        Assert.Equal("Thái Nguyên → Tuyên Quang", vm.DraftBuses[1].RouteDisplay);

        // 3. Configure Vehicle 3: Hà Nội -> Tuyên Quang with Driver C
        vm.StartNewVehicleDraftCommand.Execute(null);
        vm.Plate = "20B-003.45";
        vm.SeatCount = "29";
        vm.BusType = "Ghế ngồi";
        vm.DriverName = "Lê Văn C";
        vm.DriverPhone = "0933.333.333";
        vm.SelectedOrigin = "Hà Nội";
        vm.SelectedDestination = "Tuyên Quang";
        vm.IntermediateStops = "Nội Bài, Vĩnh Yên, Tam Đảo, Sơn Dương";
        vm.FarePrice = "180000";
        vm.StagePrice = "90000";
        vm.DepartureTimeNote = "06:30, 13:00";
        vm.SaveVehicleDraftCommand.Execute(null);

        Assert.Equal(3, vm.DraftBuses.Count);
        Assert.Equal("Lê Văn C", vm.DraftBuses[2].DriverName);
        Assert.Equal("Hà Nội → Tuyên Quang", vm.DraftBuses[2].RouteDisplay);
        Assert.Equal("180000", vm.DraftBuses[2].FarePrice);
        Assert.Equal("90000", vm.DraftBuses[2].StagePrice);
    }

    [Fact]
    public void AdminBusManagementViewModel_IntermediateStops_ComboBoxPresetsAndAppend()
    {
        var vm = new AdminBusManagementViewModel();
        vm.SelectedOrigin = "Thái Nguyên";
        vm.SelectedDestination = "Tuyên Quang";

        // Check ComboBox presets for Thái Nguyên -> Tuyên Quang
        Assert.Contains(vm.AvailableStopPresets, s => s.Contains("Sơn Dương"));
        Assert.Contains(vm.AvailableStopPresets, s => s.Contains("Đại Từ"));

        // Append stop via command
        vm.IntermediateStops = "Bến xe Thái Nguyên";
        vm.AddStopToRouteCommand.Execute("Đại Từ");
        Assert.Equal("Bến xe Thái Nguyên, Đại Từ", vm.IntermediateStops);

        vm.AddStopToRouteCommand.Execute("Sơn Dương");
        Assert.Equal("Bến xe Thái Nguyên, Đại Từ, Sơn Dương", vm.IntermediateStops);
    }

    [Fact]
    public void PaymentWindowViewModel_BankTransfer_InitializesAndHandlesSubmit()
    {
        bool? closedResult = null;
        var holdExpiresAt = DateTime.UtcNow.AddMinutes(15);
        using var vm = new PaymentWindowViewModel(
            ticketCodes: new[] { "TK-1001", "TK-1002" },
            amount: 320000m,
            transactionNo: "TXN-2026-001",
            paymentUrl: null,
            isBankTransfer: true,
            holdExpiresAtUtc: holdExpiresAt);

        vm.RequestClose += r => closedResult = r;

        Assert.True(vm.IsBankTransfer);
        Assert.False(vm.IsVnPay);
        Assert.Equal("320,000 VND", vm.AmountText);
        Assert.Equal("TK-1001, TK-1002", vm.ReferenceText);
        Assert.Contains("CÁC MÃ NỘI DUNG CHUYỂN KHOẢN", vm.ReferenceCaption);
        Assert.Contains("THANH TOÁN CHUYỂN KHOẢN", vm.TitleText);
        Assert.False(string.IsNullOrWhiteSpace(vm.CountdownText));

        vm.SubmitTransferCommand.Execute(null);
        Assert.True(closedResult);
    }

    [Fact]
    public void PaymentWindowViewModel_VnPay_InitializesCorrectly()
    {
        bool? closedResult = null;
        var holdExpiresAt = DateTime.UtcNow.AddMinutes(15);
        using var vm = new PaymentWindowViewModel(
            ticketCodes: new[] { "TK-9999" },
            amount: 150000m,
            transactionNo: "TXN-VNPAY-001",
            paymentUrl: "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?test=1",
            isBankTransfer: false,
            holdExpiresAtUtc: holdExpiresAt);

        vm.RequestClose += r => closedResult = r;

        Assert.False(vm.IsBankTransfer);
        Assert.True(vm.IsVnPay);
        Assert.Equal("150,000 VND", vm.AmountText);
        Assert.Equal("MÃ VÉ", vm.ReferenceCaption);
        Assert.Contains("VNPAY", vm.TitleText);

        vm.CloseCommand.Execute(null);
        Assert.False(closedResult ?? false);
    }

    [Fact]
    public void LocalizationService_LanguageSwitching_WorksAndFiresEvent()
    {
        var service = LocalizationService.Instance;
        string? lastChangedLanguage = null;
        service.LanguageChanged += lang => lastChangedLanguage = lang;

        // Switch to English
        service.SetLanguage(LocalizationService.English);
        Assert.Equal(LocalizationService.English, service.CurrentLanguage);
        Assert.True(service.IsEnglish);
        Assert.False(service.IsVietnamese);
        Assert.Equal(LocalizationService.English, lastChangedLanguage);

        // Toggle to Vietnamese
        service.ToggleLanguage();
        Assert.Equal(LocalizationService.Vietnamese, service.CurrentLanguage);
        Assert.True(service.IsVietnamese);
        Assert.False(service.IsEnglish);
        Assert.Equal(LocalizationService.Vietnamese, lastChangedLanguage);

        // Toggle back to English
        service.ToggleLanguage();
        Assert.Equal(LocalizationService.English, service.CurrentLanguage);
        Assert.True(service.IsEnglish);

        // Reset back to Vietnamese as default
        service.SetLanguage(LocalizationService.Vietnamese);
        Assert.True(service.IsVietnamese);
    }

    [Fact]
    public async Task AsyncRelayCommand_RefusesReentryWhileHandlerIsStillRunning()
    {
        // Guards against duplicate bookings: RelayCommand with an async lambda is async void, so
        // it returned at the first await and a second click ran the handler again.
        var gate = new TaskCompletionSource();
        var invocations = 0;

        var command = new AsyncRelayCommand(async () =>
        {
            invocations++;
            await gate.Task;
        });

        command.Execute(null);
        Assert.Equal(1, invocations);
        Assert.True(command.IsRunning);
        Assert.False(command.CanExecute(null));

        // Second and third clicks while the first is in flight must be dropped.
        command.Execute(null);
        command.Execute(null);
        Assert.Equal(1, invocations);

        gate.SetResult();
        await Task.Yield();

        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));

        command.Execute(null);
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task AsyncRelayCommand_SurvivesAFaultingHandlerAndReportsIt()
    {
        // A faulted async void command used to crash the dispatcher.
        Exception? reported = null;
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("boom"),
            onError: ex => reported = ex);

        command.Execute(null);
        await Task.Yield();

        Assert.IsType<InvalidOperationException>(reported);
        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void TicketStatusPalette_DistinguishesCancelledFromPaid()
    {
        // The ticket banner was hard-coded success-green, so a cancelled ticket showed a green
        // tick. Paid and cancelled must never resolve to the same palette.
        var paid = StatusPalette.ForTicketStatus(TicketStatuses.Paid);
        var cancelled = StatusPalette.ForTicketStatus(TicketStatuses.Cancelled);
        var reserved = StatusPalette.ForTicketStatus(TicketStatuses.Reserved);

        Assert.Equal(StatusPalette.Success, paid);
        Assert.Equal(StatusPalette.Danger, cancelled);
        Assert.Equal(StatusPalette.Warning, reserved);
        Assert.NotEqual(paid, cancelled);
    }

    [Fact]
    public void MyTicketsViewModel_CancelAndRefundRequireExplicitConfirmation()
    {
        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                EnsureWpfApp();

                var nav = new NavigationService();
                var vm = new MyTicketsViewModel(nav);
                var ticket = new TicketDetails(
                    TicketId: 42,
                    TicketCode: "TK-42",
                    Route: "Hà Nội - Thái Nguyên",
                    DepartureTime: DateTime.Now.AddDays(2),
                    ArrivalTime: DateTime.Now.AddDays(2).AddHours(2),
                    BusType: "Limousine",
                    SeatNumbers: "A1, A2",
                    Price: 320000m,
                    Status: TicketStatuses.Paid,
                    BookingTime: DateTime.Now);
                var item = new TicketItemViewModel(ticket);

                // Nothing is staged to begin with.
                Assert.False(vm.IsConfirmingAction);
                Assert.False(vm.ConfirmPendingActionCommand.CanExecute(null));

                // Clicking refund only stages the action; it must not submit anything yet.
                vm.RequestRefundCommand.Execute(item);
                Assert.True(vm.IsConfirmingAction);
                Assert.Same(item, vm.PendingTicket);
                Assert.Contains("TK-42", vm.ConfirmationPrompt);
                Assert.True(vm.ConfirmPendingActionCommand.CanExecute(null));

                // Backing out clears the staged action without executing it.
                vm.DismissPendingActionCommand.Execute(null);
                Assert.False(vm.IsConfirmingAction);
                Assert.Null(vm.PendingTicket);
                Assert.False(vm.ConfirmPendingActionCommand.CanExecute(null));

                // Cancel stages a distinctly worded prompt, so the user knows which action they confirm.
                vm.CancelTicketCommand.Execute(item);
                Assert.True(vm.IsConfirmingAction);
                Assert.Contains("release", vm.ConfirmationPrompt, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) { threadException = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadException != null) throw threadException;
    }

    [Fact]
    public void PaymentWindowViewModel_RefusesTransferSubmissionOnceTheHoldHasExpired()
    {
        // An expired hold means the seats are already back on sale; submitting anyway used to
        // return DialogResult=true and tell the user their booking was pending.
        bool? closeResult = null;
        using var vm = new PaymentWindowViewModel(
            ticketCodes: new[] { "TK-99" },
            amount: 150000m,
            transactionNo: "TXN-EXPIRED",
            paymentUrl: null,
            isBankTransfer: true,
            holdExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

        vm.RequestClose += r => closeResult = r;

        Assert.True(vm.IsHoldExpired);
        Assert.False(vm.CanSubmitTransfer);
        Assert.False(vm.SubmitTransferCommand.CanExecute(null));

        vm.SubmitTransfer();

        Assert.Null(closeResult);
    }

    [Fact]
    public void LocationService_NormalizesProvincesAndAliasesCorrectly()
    {
        Assert.Equal("Thái Nguyên", LocationService.NormalizeToVietnamProvince("Bến xe Thái Nguyên"));
        Assert.Equal("Thái Nguyên", LocationService.NormalizeToVietnamProvince("thai nguyen"));
        Assert.Equal("Hà Nội", LocationService.NormalizeToVietnamProvince("Hanoi"));
        Assert.Equal("TP. Hồ Chí Minh", LocationService.NormalizeToVietnamProvince("saigon"));
        Assert.Equal("Đà Nẵng", LocationService.NormalizeToVietnamProvince("Da Nang"));
        Assert.Equal("Hải Phòng", LocationService.NormalizeToVietnamProvince("haiphong"));
        Assert.Contains(LocationService.NormalizeToVietnamProvince("Cần Thơ"), LocationService.Instance.AvailableLocations);
    }

    [Fact]
    public void LocationService_CustomerCanChangeLocation_AdminIsLocked()
    {
        var locService = LocationService.Instance;
        locService.SetFixedMachineLocation("Thái Nguyên");

        // 1. As Customer: user can change location freely
        CurrentUser.Account = new UserSession(1, "customer", "Khách hàng", "khach@gmail.com", "0912345678", "Customer");
        var changed = locService.SetUserLocation("Hà Nội");
        Assert.True(changed);
        Assert.Equal("Hà Nội", locService.CurrentLocation);
        Assert.Equal("Thái Nguyên", locService.MachineLocation); // Machine station remains fixed

        // 2. As Admin: attempting to change location is rejected (locked to machine)
        CurrentUser.Account = new UserSession(2, "admin", "Quản trị viên", "admin@gmail.com", "0987654321", "Admin");
        var adminChanged = locService.SetUserLocation("Đà Nẵng");
        Assert.False(adminChanged);
        // Admin's active location is strictly locked to MachineLocation
        Assert.Equal("Thái Nguyên", locService.CurrentLocation);
        Assert.Equal("Thái Nguyên", locService.MachineLocation);

        // Reset to customer for other tests
        CurrentUser.Account = null;
    }

    [Fact]
    public void AdminRouteManagementViewModel_RejectsRouteOutsideStationScope()
    {
        LocationService.Instance.SetFixedMachineLocation("Thái Nguyên");
        CurrentUser.Account = new UserSession(2, "admin", "Admin", null, null, "Admin");

        var vm = new AdminRouteManagementViewModel();
        vm.Origin = "Hà Nội";
        vm.Destination = "Hải Phòng"; // Neither is Thái Nguyên!
        vm.Distance = "120";

        vm.AddCommand.Execute(null);

        Assert.Contains("Thao tác bị từ chối", vm.Message);
        Assert.Contains("Thái Nguyên", vm.Message);

        CurrentUser.Account = null;
    }
}
