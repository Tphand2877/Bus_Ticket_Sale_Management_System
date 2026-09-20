using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class BookingViewModel : ViewModelBase, IRefreshable
{
    private readonly INavigationService _navigationService;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public ObservableCollection<BusCompanyItem> Companies { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(HasNoCompanies));
        }
    }

    public bool HasNoCompanies => !IsBusy && Companies.Count == 0;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ActiveLocation => LocationService.Instance.CurrentLocation;
    public string LocationBanner => $"📍 Vị trí khởi hành của bạn: {ActiveLocation}";
    public ICommand RefreshCommand { get; }
    public ICommand SelectCompanyCommand { get; }
    public ICommand BackToHomeCommand { get; }
    public BookingViewModel() : this(new NavigationService())
    {
    }

    public BookingViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        LocationService.Instance.LocationChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ActiveLocation));
            OnPropertyChanged(nameof(LocationBanner));
        };
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        SelectCompanyCommand = new RelayCommand(parameter =>
        {
            if (parameter is not BusCompanyItem company) return;
            _navigationService.NavigateTo(new BusCompanyDetailViewModel(_navigationService, company));
        });
        BackToHomeCommand = new RelayCommand(() => _navigationService.NavigateTo<HomeViewModel>());
    }

    public async Task RefreshAsync()
    {
        await LoadCompaniesAsync();
    }

    private async Task LoadCompaniesAsync()
    {
        IsBusy = true;
        try
        {
            var companies = await DatabaseHelper.GetBookingCompaniesAsync(null, null);
            Companies.Clear();
            foreach (var c in companies) Companies.Add(c);

            StatusMessage = Companies.Count == 0
                ? "Không tìm thấy nhà xe nào đang hoạt động."
                : $"{Companies.Count} nhà xe đang hoạt động.";
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load booking companies.", ex);
            Companies.Clear();
            StatusMessage = "Không thể tải danh sách nhà xe từ cơ sở dữ liệu.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasNoCompanies));
        }
    }
}
