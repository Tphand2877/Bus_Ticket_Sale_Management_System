using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.ViewModels;


public sealed class AdminBusManagementViewModel : ViewModelBase, IRefreshable
{
    // Form fields
    private string _companyName = "";
    private string _companyHotline = "1900 6868";
    private string _companyOperatingArea = "Thái Nguyên - Hà Nội";
    private string _companyDescription = "";
    private string _companyAmenities = "Wifi tốc độ cao, Nước uống, Khăn lạnh, Điều hòa, Cổng sạc USB";
    private string _farePrice = "150000";
    private string _driverName = "Nguyễn Văn Hùng";
    private string _driverPhone = "0912 345 678";
    private string _plate = "";
    private string _departureTimeNote = "06:30, 09:00, 14:30";
    private string _selectedOrigin = "";
    private string _selectedDestination = "";
    private RouteAdminItem? _selectedRoute;
    private string _intermediateStops = "";
    private string _seatCount = "40";
    private string _busType = "Limousine";
    private string _make = "Hyundai";
    private string _model = "Universe";
    private string _manufactureYear = "2023";
    private string _color = "Trắng - Xanh";
    private string _registrationNumber = "";
    private string _inspectionNumber = "";
    private DateTime? _registrationDate = DateTime.Today;
    private DateTime? _inspectionExpiryDate = DateTime.Today.AddYears(2);
    private DateTime? _insuranceExpiryDate = DateTime.Today.AddYears(1);
    private string _registeredEntity = "Công ty CP Vận tải Sao Việt";
    private string _operationType = "Tuyến cố định";
    private string _message = "";
    private bool _isEditing;
    private bool _isLoading;
    private bool _isFormVisible;
    private int _editingBusId;
    private BusAdminItem? _pendingDelete;

    // Fleet Filtering
    private string _selectedCompanyFilter = "Tất cả nhà xe";
    private string _fleetSearchText = "";
    private string _fleetSummaryText = "";

    public ObservableCollection<BusAdminItem> Buses { get; } = new();
    public ObservableCollection<BusAdminItem> FilteredBuses { get; } = new();
    public ObservableCollection<string> CompanyFilterOptions { get; } = new();
    public ObservableCollection<string> AvailableCompanies { get; } = new();
    public ObservableCollection<RouteAdminItem> AvailableRoutes { get; } = new();
    public ObservableCollection<string> AvailableOrigins { get; } = new();
    public ObservableCollection<string> AvailableDestinations { get; } = new();
    public ObservableCollection<string> BusTypes { get; } = new(new[] { "Limousine", "Ghế ngồi", "Ghế nằm", "Cabin" });
    public ObservableCollection<string> OperationTypes { get; } = new(new[] { "Tuyến cố định", "Hợp đồng", "Du lịch" });

    // Company Profile Properties
    public string CompanyHotline { get => _companyHotline; set => SetProperty(ref _companyHotline, value); }
    public string CompanyOperatingArea { get => _companyOperatingArea; set => SetProperty(ref _companyOperatingArea, value); }
    public string CompanyDescription { get => _companyDescription; set => SetProperty(ref _companyDescription, value); }
    public string CompanyAmenities { get => _companyAmenities; set => SetProperty(ref _companyAmenities, value); }
    public string FarePrice { get => _farePrice; set => SetProperty(ref _farePrice, value); }
    private string _stagePrice = "70000";
    public string StagePrice { get => _stagePrice; set => SetProperty(ref _stagePrice, value); }

    // Intermediate Stops ComboBox
    private string _selectedStopOption = "";
    public string SelectedStopOption
    {
        get => _selectedStopOption;
        set
        {
            if (SetProperty(ref _selectedStopOption, value) && !string.IsNullOrWhiteSpace(value))
            {
                IntermediateStops = value;
            }
        }
    }
    public ObservableCollection<string> AvailableStopPresets { get; } = new();

    // Multi-vehicle Draft List
    public ObservableCollection<BusDraftItem> DraftBuses { get; } = new();
    public bool HasDraftBuses => DraftBuses.Count > 0;
    private bool _isVehiclePanelVisible = true;
    public bool IsVehiclePanelVisible { get => _isVehiclePanelVisible; set => SetProperty(ref _isVehiclePanelVisible, value); }
    // Fleet Filter Properties
    public string SelectedCompanyFilter
    {
        get => _selectedCompanyFilter;
        set
        {
            if (SetProperty(ref _selectedCompanyFilter, value))
            {
                ApplyFleetFilter();
            }
        }
    }

    public string FleetSearchText
    {
        get => _fleetSearchText;
        set
        {
            if (SetProperty(ref _fleetSearchText, value))
            {
                ApplyFleetFilter();
            }
        }
    }

    public string FleetSummaryText
    {
        get => _fleetSummaryText;
        private set => SetProperty(ref _fleetSummaryText, value);
    }
    // Properties
    public string CompanyName
    {
        get => _companyName;
        set
        {
            if (SetProperty(ref _companyName, value) && !IsEditing && !string.IsNullOrWhiteSpace(value))
            {
                _ = AutoFillCompanyDetailsAsync(value.Trim());
            }
        }
    }
    public string DriverName { get => _driverName; set => SetProperty(ref _driverName, value); }
    public string DriverPhone { get => _driverPhone; set => SetProperty(ref _driverPhone, value); }
    public string Plate { get => _plate; set => SetProperty(ref _plate, value); }
    public string DepartureTimeNote { get => _departureTimeNote; set => SetProperty(ref _departureTimeNote, value); }
    public bool IsFormVisible
    {
        get => _isFormVisible;
        set
        {
            if (SetProperty(ref _isFormVisible, value))
            {
                OnPropertyChanged(nameof(FormToggleButtonText));
            }
        }
    }
    public string FormToggleButtonText => IsFormVisible ? "✕ Đóng form" : "+ Đăng ký xe mới";

    public string SelectedOrigin
    {
        get => _selectedOrigin;
        set
        {
            if (SetProperty(ref _selectedOrigin, value))
            {
                UpdateAvailableDestinations();
                UpdateMatchedRoute();
            }
        }
    }

    public string SelectedDestination
    {
        get => _selectedDestination;
        set
        {
            if (SetProperty(ref _selectedDestination, value))
            {
                UpdateMatchedRoute();
            }
        }
    }

    private void UpdateAvailableDestinations()
    {
        var currentDest = _selectedDestination;
        AvailableDestinations.Clear();
        foreach (var p in VietnamProvinces.List)
        {
            if (!string.Equals(p, _selectedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                AvailableDestinations.Add(p);
            }
        }
        if (!string.IsNullOrWhiteSpace(currentDest) && AvailableDestinations.Contains(currentDest))
        {
            SelectedDestination = currentDest;
        }
    }

    private void UpdateMatchedRoute()
    {
        if (string.IsNullOrWhiteSpace(_selectedOrigin) || string.IsNullOrWhiteSpace(_selectedDestination))
        {
            SelectedRoute = null;
            return;
        }
        SelectedRoute = AvailableRoutes.FirstOrDefault(r =>
            string.Equals(r.Origin, _selectedOrigin, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Destination, _selectedDestination, StringComparison.OrdinalIgnoreCase));
        UpdateIntermediateStopPresets();
    }

    public RouteAdminItem? SelectedRoute
    {
        get => _selectedRoute;
        set
        {
            if (SetProperty(ref _selectedRoute, value) && value != null)
            {
                UpdateIntermediateStopPresets();
                if (string.IsNullOrWhiteSpace(FarePrice) || FarePrice == "150000")
                {
                    var dist = value.DistanceKm ?? 100m;
                    var estPrice = Math.Max(100000m, Math.Round((dist * 1200m) / 10000m) * 10000m);
                    FarePrice = estPrice.ToString("F0");
                    var estStage = Math.Max(50000m, Math.Round((estPrice * 0.45m) / 10000m) * 10000m);
                    StagePrice = estStage.ToString("F0");
                }
            }
        }
    }
    public string IntermediateStops { get => _intermediateStops; set => SetProperty(ref _intermediateStops, value); }
    public string SeatCount { get => _seatCount; set => SetProperty(ref _seatCount, value); }
    public string BusType { get => _busType; set => SetProperty(ref _busType, value); }
    public string Make { get => _make; set => SetProperty(ref _make, value); }
    public string Model { get => _model; set => SetProperty(ref _model, value); }
    public string ManufactureYear { get => _manufactureYear; set => SetProperty(ref _manufactureYear, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public string RegistrationNumber { get => _registrationNumber; set => SetProperty(ref _registrationNumber, value); }
    public string InspectionNumber { get => _inspectionNumber; set => SetProperty(ref _inspectionNumber, value); }
    public DateTime? RegistrationDate { get => _registrationDate; set => SetProperty(ref _registrationDate, value); }
    public DateTime? InspectionExpiryDate { get => _inspectionExpiryDate; set => SetProperty(ref _inspectionExpiryDate, value); }
    public DateTime? InsuranceExpiryDate { get => _insuranceExpiryDate; set => SetProperty(ref _insuranceExpiryDate, value); }
    public string RegisteredEntity { get => _registeredEntity; set => SetProperty(ref _registeredEntity, value); }
    public string OperationType { get => _operationType; set => SetProperty(ref _operationType, value); }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage));
        }
    }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool IsEditing { get => _isEditing; private set => SetProperty(ref _isEditing, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasBuses => Buses.Count > 0;
    public bool HasNoBuses => !HasBuses && !IsLoading;

    public BusAdminItem? PendingDelete
    {
        get => _pendingDelete;
        private set
        {
            if (SetProperty(ref _pendingDelete, value))
            {
                OnPropertyChanged(nameof(IsConfirmingDelete));
                OnPropertyChanged(nameof(DeleteConfirmationPrompt));
            }
        }
    }
    public bool IsConfirmingDelete => PendingDelete is not null;
    public string DeleteConfirmationPrompt => PendingDelete is null
        ? ""
        : $"Delete bus {PendingDelete.LicensePlate} and its seat map? This cannot be undone.";

    public ICommand ToggleFormCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand AddAndContinueCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand SelectForEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand RefreshCommand { get; }

    // Time Slot Preset Commands
    public ICommand PresetMorningCommand { get; }
    public ICommand PresetNoonCommand { get; }
    public ICommand PresetAfternoonCommand { get; }
    public ICommand PresetEveningCommand { get; }
    public ICommand PresetContinuousHourlyCommand { get; }
    public ICommand PresetContinuousTwoHourlyCommand { get; }
    public ICommand ClearTimeSlotsCommand { get; }

    // Multi-vehicle Draft Commands
    public ICommand StartNewVehicleDraftCommand { get; }
    public ICommand SaveVehicleDraftCommand { get; }
    public ICommand CancelVehicleDraftCommand { get; }
    public ICommand EditDraftVehicleCommand { get; }
    public ICommand RemoveDraftVehicleCommand { get; }
    public ICommand SaveAllBusesCommand { get; }
    public ICommand AddStopToRouteCommand { get; }

    public AdminBusManagementViewModel()
    {
        ToggleFormCommand = new RelayCommand(() =>
        {
            if (IsFormVisible && !IsEditing)
            {
                IsFormVisible = false;
            }
            else
            {
                ResetForm();
                IsFormVisible = true;
            }
        });
        AddCommand = new AsyncRelayCommand(async () => await AddAsync(keepFormOpenForNextBus: false));
        AddAndContinueCommand = new AsyncRelayCommand(async () => await AddAsync(keepFormOpenForNextBus: true));
        UpdateCommand = new AsyncRelayCommand(async () => await UpdateAsync(), () => IsEditing);
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());

        PresetMorningCommand = new RelayCommand(() => AppendOrSetTimeSlot("06:00, 08:30"));
        PresetNoonCommand = new RelayCommand(() => AppendOrSetTimeSlot("11:00, 13:30"));
        PresetAfternoonCommand = new RelayCommand(() => AppendOrSetTimeSlot("15:30, 17:30"));
        PresetEveningCommand = new RelayCommand(() => AppendOrSetTimeSlot("19:30, 21:00"));
        PresetContinuousHourlyCommand = new RelayCommand(() =>
        {
            DepartureTimeNote = "06:00, 07:00, 08:00, 09:00, 10:00, 11:00, 12:00, 13:00, 14:00, 15:00, 16:00, 17:00, 18:00, 19:00, 20:00";
        });
        PresetContinuousTwoHourlyCommand = new RelayCommand(() =>
        {
            DepartureTimeNote = "06:00, 08:00, 10:00, 12:00, 14:00, 16:00, 18:00, 20:00";
        });
        ClearTimeSlotsCommand = new RelayCommand(() => DepartureTimeNote = "");

        // Multi-vehicle Draft Wireup
        StartNewVehicleDraftCommand = new RelayCommand(() =>
        {
            ResetVehicleFieldsOnly();
            IsVehiclePanelVisible = true;
            Message = "Nhập thông tin phương tiện mới và bấm 'Lưu xe này vào danh sách'.";
        });
        SaveVehicleDraftCommand = new RelayCommand(() => SaveVehicleDraft());
        CancelVehicleDraftCommand = new RelayCommand(() =>
        {
            if (DraftBuses.Count > 0)
                IsVehiclePanelVisible = false;
        });
        EditDraftVehicleCommand = new RelayCommand(p =>
        {
            if (p is BusDraftItem draft)
                EditDraftVehicle(draft);
        });
        RemoveDraftVehicleCommand = new RelayCommand(p =>
        {
            if (p is BusDraftItem draft)
            {
                DraftBuses.Remove(draft);
                OnPropertyChanged(nameof(HasDraftBuses));
                Message = $"Đã xóa xe {draft.Plate} khỏi danh sách chờ.";
            }
        });
        SaveAllBusesCommand = new AsyncRelayCommand(async () => await SaveAllBusesAsync());
        AddStopToRouteCommand = new RelayCommand(p =>
        {
            var stop = p as string ?? SelectedStopOption;
            AddStopToRoute(stop);
        });
        CancelEditCommand = new RelayCommand(() =>
        {
            ResetForm();
            IsFormVisible = false;
        });
        SelectForEditCommand = new RelayCommand(p =>
        {
            if (p is not BusAdminItem b) return;
            PendingDelete = null;
            _editingBusId = b.BusId;
            CompanyName = b.CompanyName;
            DriverName = b.DriverName;
            DriverPhone = b.DriverPhone;
            Plate = b.LicensePlate;
            DepartureTimeNote = b.DepartureTimeNote;
            IntermediateStops = b.IntermediateStops;
            SeatCount = b.SeatCount.ToString();
            BusType = b.BusType;
            Make = b.Make;
            Model = b.Model;
            ManufactureYear = b.ManufactureYear.ToString();
            Color = b.Color;
            RegistrationNumber = b.RegistrationNumber;
            InspectionNumber = b.InspectionNumber;
            RegistrationDate = b.RegistrationDate ?? DateTime.Today;
            InspectionExpiryDate = b.InspectionExpiryDate ?? DateTime.Today.AddYears(2);
            InsuranceExpiryDate = b.InsuranceExpiryDate ?? DateTime.Today.AddYears(1);
            RegisteredEntity = b.RegisteredEntity;
            OperationType = b.OperationType;
            var route = AvailableRoutes.FirstOrDefault(r => r.DisplayName.Contains(b.RouteName));
            if (route != null)
            {
                SelectedOrigin = route.Origin;
                SelectedDestination = route.Destination;
                SelectedRoute = route;
            }
            else if (b.RouteName.Contains(" → "))
            {
                var parts = b.RouteName.Split(" → ", 2);
                SelectedOrigin = parts[0].Trim();
                SelectedDestination = parts[1].Trim();
            }
            IsEditing = true;
            IsFormVisible = true;
            Message = $"Editing bus {b.LicensePlate}.";
        });
        RequestDeleteCommand = new RelayCommand(p =>
        {
            if (p is not BusAdminItem b) return;
            Message = "";
            PendingDelete = b;
        });
        CancelDeleteCommand = new RelayCommand(() => PendingDelete = null);
        ConfirmDeleteCommand = new AsyncRelayCommand(async () => await DeleteAsync(), () => IsConfirmingDelete);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var busesTask = AdminDatabaseService.GetBusesAsync();
            var companiesTask = AdminDatabaseService.GetCompaniesListAsync();
            var routesTask = AdminDatabaseService.GetRoutesAsync();

            await Task.WhenAll(busesTask, companiesTask, routesTask);

            Buses.Clear();
            foreach (var b in await busesTask) Buses.Add(b);

            AvailableCompanies.Clear();
            foreach (var c in await companiesTask) AvailableCompanies.Add(c);

            CompanyFilterOptions.Clear();
            CompanyFilterOptions.Add("Tất cả nhà xe");
            foreach (var c in AvailableCompanies) CompanyFilterOptions.Add(c);

            AvailableRoutes.Clear();
            foreach (var r in await routesTask) AvailableRoutes.Add(r);

            AvailableOrigins.Clear();
            foreach (var p in VietnamProvinces.List) AvailableOrigins.Add(p);
            UpdateAvailableDestinations();
            ApplyFleetFilter();
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Loading the bus fleet");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasBuses));
            OnPropertyChanged(nameof(HasNoBuses));
        }
    }

    private void ResetForm()
    {
        IsEditing = false;
        _editingBusId = 0;
        Plate = "";
        SeatCount = "40";
        BusType = "Limousine";
        CompanyName = "";
        CompanyHotline = "1900 6868";
        CompanyOperatingArea = "Thái Nguyên - Hà Nội";
        FarePrice = "150000";
        StagePrice = "70000";
        DraftBuses.Clear();
        OnPropertyChanged(nameof(HasDraftBuses));
        IsVehiclePanelVisible = true;
        DriverName = "Nguyễn Văn Hùng";
        DriverPhone = "0912 345 678";
        DepartureTimeNote = "06:30, 09:00, 14:30";
        Make = "Hyundai";
        Model = "Universe";
        ManufactureYear = "2023";
        Color = "Trắng - Xanh";
        RegistrationNumber = "";
        InspectionNumber = "";
        RegistrationDate = DateTime.Today;
        InspectionExpiryDate = DateTime.Today.AddYears(2);
        InsuranceExpiryDate = DateTime.Today.AddYears(1);
        RegisteredEntity = "Công ty CP Vận tải Sao Việt";
        OperationType = "Tuyến cố định";
        IntermediateStops = "";
        SelectedOrigin = "";
        SelectedDestination = "";
        SelectedRoute = null;
        Message = "";
    }

    private void ResetVehicleFieldsOnly()
    {
        IsEditing = false;
        _editingBusId = 0;
        Plate = "";
        RegistrationNumber = "";
        InspectionNumber = "";
        DriverName = "Chưa phân công";
        DriverPhone = string.IsNullOrWhiteSpace(CompanyHotline) ? "0912 345 678" : CompanyHotline;
    }

    private async Task AutoFillCompanyDetailsAsync(string name)
    {
        var existingBus = Buses.FirstOrDefault(b => string.Equals(b.CompanyName, name, StringComparison.OrdinalIgnoreCase));
        if (existingBus != null)
        {
            if (!string.IsNullOrWhiteSpace(existingBus.RegisteredEntity))
                RegisteredEntity = existingBus.RegisteredEntity;
            if (!string.IsNullOrWhiteSpace(existingBus.DriverPhone))
                CompanyHotline = existingBus.DriverPhone;
        }

        try
        {
            var company = await DatabaseHelper.GetBusCompanyByNameAsync(name);
            if (company != null)
            {
                if (!string.IsNullOrWhiteSpace(company.Hotline))
                    CompanyHotline = company.Hotline;
                if (!string.IsNullOrWhiteSpace(company.OperatingArea))
                    CompanyOperatingArea = company.OperatingArea;
                if (!string.IsNullOrWhiteSpace(company.Description))
                    CompanyDescription = company.Description;
                if (!string.IsNullOrWhiteSpace(company.Amenities))
                    CompanyAmenities = company.Amenities;
            }
        }
        catch
        {
            // Background autofill failsafe
        }
    }

    private void AppendOrSetTimeSlot(string slots)
    {
        var existing = (DepartureTimeNote ?? "")
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var incoming = slots
            .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var slot in incoming)
        {
            if (!existing.Contains(slot, StringComparer.OrdinalIgnoreCase))
                existing.Add(slot);
        }
        existing.Sort();
        DepartureTimeNote = string.Join(", ", existing);
    }

    private void ApplyFleetFilter()
    {
        FilteredBuses.Clear();
        var query = Buses.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedCompanyFilter) && SelectedCompanyFilter != "Tất cả nhà xe")
        {
            query = query.Where(b => string.Equals(b.CompanyName, SelectedCompanyFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(FleetSearchText))
        {
            var term = FleetSearchText.Trim();
            query = query.Where(b =>
                b.LicensePlate.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.RouteName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.DriverName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.BusType.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var b in query) FilteredBuses.Add(b);

        if (!string.IsNullOrWhiteSpace(SelectedCompanyFilter) && SelectedCompanyFilter != "Tất cả nhà xe")
        {
            var routesCount = FilteredBuses.Select(b => b.RouteName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            FleetSummaryText = $"Nhà xe {SelectedCompanyFilter}: {FilteredBuses.Count} xe · {routesCount} tuyến khai thác";
        }
        else
        {
            var companyCount = Buses.Select(b => b.CompanyName).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            FleetSummaryText = $"Tổng cộng: {FilteredBuses.Count} xe · {companyCount} nhà xe hoạt động";
        }
    }
    private void UpdateIntermediateStopPresets()
    {
        AvailableStopPresets.Clear();
        var orig = (SelectedOrigin ?? "").Trim();
        var dest = (SelectedDestination ?? "").Trim();

        if (string.IsNullOrWhiteSpace(orig) || string.IsNullOrWhiteSpace(dest))
        {
            AvailableStopPresets.Add("Trạm dừng chân cao tốc, Nút giao liên tỉnh");
            return;
        }

        if ((orig.Contains("Hà Nội") && dest.Contains("Thái Nguyên")) || (orig.Contains("Thái Nguyên") && dest.Contains("Hà Nội")))
        {
            AvailableStopPresets.Add("Sân bay Nội Bài, Ngã tư Sóc Sơn, Nút giao Yên Bình, Phổ Yên, Bến xe Thái Nguyên");
            AvailableStopPresets.Add("Sân bay Nội Bài, Phổ Yên, KCN Sông Công, ĐH Sư phạm Thái Nguyên");
            AvailableStopPresets.Add("Sân bay Nội Bài");
            AvailableStopPresets.Add("Ngã tư Sóc Sơn");
            AvailableStopPresets.Add("Nút giao Yên Bình (Phổ Yên)");
            AvailableStopPresets.Add("KCN Sông Công");
            AvailableStopPresets.Add("Bến xe Thái Nguyên");
        }
        else if ((orig.Contains("Thái Nguyên") && dest.Contains("Tuyên Quang")) || (orig.Contains("Tuyên Quang") && dest.Contains("Thái Nguyên")))
        {
            AvailableStopPresets.Add("Bến xe Thái Nguyên, Ngã ba Bờ Đậu, Đại Từ, Sơn Dương, Cầu Chả, Bến xe Tuyên Quang");
            AvailableStopPresets.Add("Ngã ba Bờ Đậu");
            AvailableStopPresets.Add("Đại Từ");
            AvailableStopPresets.Add("Sơn Dương");
            AvailableStopPresets.Add("Cầu Chả");
            AvailableStopPresets.Add("Bến xe Tuyên Quang");
        }
        else if ((orig.Contains("Hà Nội") && dest.Contains("Tuyên Quang")) || (orig.Contains("Tuyên Quang") && dest.Contains("Hà Nội")))
        {
            AvailableStopPresets.Add("Bến xe Mỹ Đình, Sân bay Nội Bài, Vĩnh Yên, Tam Đảo, Sơn Dương, Bến xe Tuyên Quang");
            AvailableStopPresets.Add("Sân bay Nội Bài");
            AvailableStopPresets.Add("Vĩnh Yên");
            AvailableStopPresets.Add("Tam Đảo");
            AvailableStopPresets.Add("Sơn Dương");
            AvailableStopPresets.Add("Bến xe Tuyên Quang");
        }
        else if ((orig.Contains("Hà Nội") && dest.Contains("Hải Phòng")) || (orig.Contains("Hải Phòng") && dest.Contains("Hà Nội")))
        {
            AvailableStopPresets.Add("Nút giao Vĩnh Bảo, Tiên Lãng, Quán Toan, Bến xe Vĩnh Niệm");
            AvailableStopPresets.Add("Nút giao Vĩnh Bảo");
            AvailableStopPresets.Add("Tiên Lãng");
            AvailableStopPresets.Add("Quán Toan");
            AvailableStopPresets.Add("Bến xe Vĩnh Niệm");
        }
        else if ((orig.Contains("Hà Nội") && dest.Contains("Lào Cai")) || (orig.Contains("Lào Cai") && dest.Contains("Hà Nội")) || dest.Contains("Sa Pa") || orig.Contains("Sa Pa"))
        {
            AvailableStopPresets.Add("Sân bay Nội Bài, Vĩnh Phúc, Phú Thọ, Yên Bái, Bảo Hà, Bến xe Lào Cai, Sa Pa");
            AvailableStopPresets.Add("Sân bay Nội Bài");
            AvailableStopPresets.Add("Yên Bái");
            AvailableStopPresets.Add("Bảo Hà");
            AvailableStopPresets.Add("Bến xe Lào Cai");
            AvailableStopPresets.Add("Sa Pa");
        }
        else
        {
            AvailableStopPresets.Add($"{orig} (Bến xuất phát), Trạm dừng chân cao tốc, {dest} (Bến trả khách)");
            AvailableStopPresets.Add("Trạm dừng nghỉ cao tốc");
            AvailableStopPresets.Add("Nút giao trung tâm");
            AvailableStopPresets.Add($"Bến xe {dest}");
        }

        if (AvailableStopPresets.Count > 0 && string.IsNullOrWhiteSpace(IntermediateStops))
        {
            IntermediateStops = AvailableStopPresets[0];
            SelectedStopOption = AvailableStopPresets[0];
        }
    }

    private void AddStopToRoute(string stopName)
    {
        if (string.IsNullOrWhiteSpace(stopName)) return;
        var stop = stopName.Trim();
        var existing = (IntermediateStops ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (!existing.Contains(stop, StringComparer.OrdinalIgnoreCase))
        {
            existing.Add(stop);
            IntermediateStops = string.Join(", ", existing);
            Message = $"✓ Đã thêm chặng dừng: {stop}";
        }
    }

    private void SaveVehicleDraft()
    {
        if (string.IsNullOrWhiteSpace(Plate))
        {
            Message = "Biển kiểm soát không được để trống.";
            return;
        }
        if (!int.TryParse(SeatCount, out var count) || count is < 1 or > 200)
        {
            Message = "Số ghế phải là số nguyên từ 1 đến 200.";
            return;
        }

        var draft = new BusDraftItem
        {
            Plate = Plate.Trim(),
            SeatCount = count.ToString(),
            BusType = BusType,
            Make = Make,
            Model = Model,
            ManufactureYear = ManufactureYear,
            Color = Color,
            OperationType = OperationType,
            DriverName = DriverName,
            DriverPhone = DriverPhone,
            Origin = string.IsNullOrWhiteSpace(SelectedOrigin) ? "Hà Nội" : SelectedOrigin.Trim(),
            Destination = string.IsNullOrWhiteSpace(SelectedDestination) ? "Thái Nguyên" : SelectedDestination.Trim(),
            RouteId = SelectedRoute?.RouteId,
            IntermediateStops = IntermediateStops,
            DepartureTimeNote = DepartureTimeNote,
            FarePrice = FarePrice,
            StagePrice = StagePrice,
            RegistrationNumber = RegistrationNumber,
            InspectionNumber = InspectionNumber,
            RegistrationDate = RegistrationDate,
            InspectionExpiryDate = InspectionExpiryDate,
            InsuranceExpiryDate = InsuranceExpiryDate
        };

        DraftBuses.Add(draft);
        OnPropertyChanged(nameof(HasDraftBuses));
        Message = $"✓ Đã thêm xe {draft.Plate} ({draft.RouteDisplay}, TX: {draft.DriverName}) vào danh sách. Bạn có thể bấm '+ Thêm xe mới vào nhà xe' để thêm xe tiếp theo!";
        ResetVehicleFieldsOnly();
        IsVehiclePanelVisible = false;
    }

    private void EditDraftVehicle(BusDraftItem draft)
    {
        Plate = draft.Plate;
        SeatCount = draft.SeatCount;
        BusType = draft.BusType;
        Make = draft.Make;
        Model = draft.Model;
        ManufactureYear = draft.ManufactureYear;
        Color = draft.Color;
        OperationType = draft.OperationType;
        DriverName = draft.DriverName;
        DriverPhone = draft.DriverPhone;
        SelectedOrigin = draft.Origin;
        SelectedDestination = draft.Destination;
        IntermediateStops = draft.IntermediateStops;
        DepartureTimeNote = draft.DepartureTimeNote;
        FarePrice = draft.FarePrice;
        StagePrice = draft.StagePrice;
        RegistrationNumber = draft.RegistrationNumber;
        InspectionNumber = draft.InspectionNumber;
        RegistrationDate = draft.RegistrationDate;
        InspectionExpiryDate = draft.InspectionExpiryDate;
        InsuranceExpiryDate = draft.InsuranceExpiryDate;

        DraftBuses.Remove(draft);
        OnPropertyChanged(nameof(HasDraftBuses));
        IsVehiclePanelVisible = true;
        Message = $"Đang chỉnh sửa xe {draft.Plate}. Chỉnh sửa xong bấm 'Lưu xe này vào danh sách'.";
    }

    private async Task SaveAllBusesAsync()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            Message = "Vui lòng nhập tên nhà xe.";
            return;
        }

        if (DraftBuses.Count > 0)
        {
            IsLoading = true;
            int successCount = 0;
            try
            {
                foreach (var draft in DraftBuses.ToList())
                {
                    int.TryParse(draft.SeatCount, out var count);
                    int.TryParse(draft.ManufactureYear, out var year);
                    decimal.TryParse(draft.FarePrice, out var price);
                    decimal.TryParse(draft.StagePrice, out var sPrice);

                    int? routeId = draft.RouteId;
                    if (routeId == null && !string.IsNullOrWhiteSpace(draft.Origin) && !string.IsNullOrWhiteSpace(draft.Destination))
                    {
                        var addRoute = await AdminDatabaseService.AddRouteAsync(draft.Origin.Trim(), draft.Destination.Trim(), null);
                        var routes = await AdminDatabaseService.GetRoutesAsync();
                        var matched = routes.FirstOrDefault(r => string.Equals(r.Origin, draft.Origin.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(r.Destination, draft.Destination.Trim(), StringComparison.OrdinalIgnoreCase));
                        routeId = matched?.RouteId;
                    }

                    var input = new BusRegistrationInput(
                        CompanyName: CompanyName.Trim(),
                        DriverName: string.IsNullOrWhiteSpace(draft.DriverName) ? "Chưa phân công" : draft.DriverName.Trim(),
                        DriverPhone: string.IsNullOrWhiteSpace(draft.DriverPhone) ? "—" : draft.DriverPhone.Trim(),
                        LicensePlate: draft.Plate.Trim(),
                        DepartureTimeNote: string.IsNullOrWhiteSpace(draft.DepartureTimeNote) ? "Theo biểu đồ bến" : draft.DepartureTimeNote.Trim(),
                        RouteId: routeId,
                        IntermediateStops: draft.IntermediateStops.Trim(),
                        SeatCount: count is >= 1 and <= 200 ? count : 40,
                        BusType: string.IsNullOrWhiteSpace(draft.BusType) ? $"{draft.Make} {draft.Model}".Trim() : draft.BusType.Trim(),
                        Make: string.IsNullOrWhiteSpace(draft.Make) ? "Hyundai" : draft.Make.Trim(),
                        Model: string.IsNullOrWhiteSpace(draft.Model) ? "Universe" : draft.Model.Trim(),
                        ManufactureYear: year > 1900 ? year : 2023,
                        Color: string.IsNullOrWhiteSpace(draft.Color) ? "Trắng" : draft.Color.Trim(),
                        RegistrationNumber: string.IsNullOrWhiteSpace(draft.RegistrationNumber) ? draft.Plate.Trim() : draft.RegistrationNumber.Trim(),
                        InspectionNumber: string.IsNullOrWhiteSpace(draft.InspectionNumber) ? "KD-001" : draft.InspectionNumber.Trim(),
                        RegistrationDate: draft.RegistrationDate ?? DateTime.Today,
                        InspectionExpiryDate: draft.InspectionExpiryDate ?? DateTime.Today.AddYears(2),
                        InsuranceExpiryDate: draft.InsuranceExpiryDate ?? DateTime.Today.AddYears(1),
                        RegisteredEntity: string.IsNullOrWhiteSpace(RegisteredEntity) ? "Đơn vị vận tải" : RegisteredEntity.Trim(),
                        OperationType: string.IsNullOrWhiteSpace(draft.OperationType) ? "Tuyến cố định" : draft.OperationType.Trim(),
                        FarePrice: price > 0 ? price : null,
                        Hotline: string.IsNullOrWhiteSpace(CompanyHotline) ? null : CompanyHotline.Trim(),
                        OperatingArea: string.IsNullOrWhiteSpace(CompanyOperatingArea) ? null : CompanyOperatingArea.Trim(),
                        Description: string.IsNullOrWhiteSpace(CompanyDescription) ? null : CompanyDescription.Trim(),
                        Amenities: string.IsNullOrWhiteSpace(CompanyAmenities) ? null : CompanyAmenities.Trim(),
                        StagePrice: sPrice > 0 ? sPrice : null);

                    var res = await AdminDatabaseService.RegisterBusAsync(input);
                    if (res.Success) successCount++;
                }

                await RefreshAsync();
                DraftBuses.Clear();
                OnPropertyChanged(nameof(HasDraftBuses));
                ResetForm();
                IsFormVisible = false;
                Message = $"✓ Đã đăng ký thành công toàn bộ {successCount} phương tiện cho nhà xe {CompanyName} vào bến!";
            }
            catch (Exception ex)
            {
                Message = AdminFailure.Describe(ex, "Đăng ký đội xe vào bến");
            }
            finally
            {
                IsLoading = false;
            }
        }
        else
        {
            await AddAsync(keepFormOpenForNextBus: false);
        }
    }
    private BusRegistrationInput BuildInput()
    {
        int.TryParse(SeatCount, out var count);
        int.TryParse(ManufactureYear, out var year);
        decimal.TryParse(FarePrice, out var price);
        decimal.TryParse(StagePrice, out var stagePrice);
        return new BusRegistrationInput(
            CompanyName: string.IsNullOrWhiteSpace(CompanyName) ? "Sao Việt" : CompanyName.Trim(),
            DriverName: string.IsNullOrWhiteSpace(DriverName) ? "Chưa phân công" : DriverName.Trim(),
            DriverPhone: string.IsNullOrWhiteSpace(DriverPhone) ? "—" : DriverPhone.Trim(),
            LicensePlate: Plate.Trim(),
            DepartureTimeNote: string.IsNullOrWhiteSpace(DepartureTimeNote) ? "Theo biểu đồ bến" : DepartureTimeNote.Trim(),
            RouteId: SelectedRoute?.RouteId,
            IntermediateStops: IntermediateStops.Trim(),
            SeatCount: count is >= 1 and <= 200 ? count : 40,
            BusType: string.IsNullOrWhiteSpace(BusType) ? $"{Make} {Model}".Trim() : BusType.Trim(),
            Make: string.IsNullOrWhiteSpace(Make) ? "Hyundai" : Make.Trim(),
            Model: string.IsNullOrWhiteSpace(Model) ? "Universe" : Model.Trim(),
            ManufactureYear: year > 1900 ? year : 2023,
            Color: string.IsNullOrWhiteSpace(Color) ? "Trắng" : Color.Trim(),
            RegistrationNumber: string.IsNullOrWhiteSpace(RegistrationNumber) ? Plate.Trim() : RegistrationNumber.Trim(),
            InspectionNumber: string.IsNullOrWhiteSpace(InspectionNumber) ? "KD-001" : InspectionNumber.Trim(),
            RegistrationDate: RegistrationDate ?? DateTime.Today,
            InspectionExpiryDate: InspectionExpiryDate ?? DateTime.Today.AddYears(2),
            InsuranceExpiryDate: InsuranceExpiryDate ?? DateTime.Today.AddYears(1),
            RegisteredEntity: string.IsNullOrWhiteSpace(RegisteredEntity) ? "Đơn vị vận tải đăng ký bến" : RegisteredEntity.Trim(),
            OperationType: string.IsNullOrWhiteSpace(OperationType) ? "Tuyến cố định" : OperationType.Trim(),
            FarePrice: price > 0 ? price : null,
            Hotline: string.IsNullOrWhiteSpace(CompanyHotline) ? null : CompanyHotline.Trim(),
            OperatingArea: string.IsNullOrWhiteSpace(CompanyOperatingArea) ? null : CompanyOperatingArea.Trim(),
            Amenities: string.IsNullOrWhiteSpace(CompanyAmenities) ? null : CompanyAmenities.Trim(),
            StagePrice: stagePrice > 0 ? stagePrice : null);
    }

    private async Task AddAsync(bool keepFormOpenForNextBus = false)
    {
        if (string.IsNullOrWhiteSpace(Plate))
        {
            Message = "Biển kiểm soát không được để trống.";
            return;
        }
        if (!int.TryParse(SeatCount, out var count) || count is < 1 or > 200)
        {
            Message = "Số ghế phải là số nguyên từ 1 đến 200.";
            return;
        }
        try
        {
            if (SelectedRoute == null && !string.IsNullOrWhiteSpace(SelectedOrigin) && !string.IsNullOrWhiteSpace(SelectedDestination))
            {
                var addRoute = await AdminDatabaseService.AddRouteAsync(SelectedOrigin.Trim(), SelectedDestination.Trim(), null);
                if (addRoute.Success)
                {
                    var routes = await AdminDatabaseService.GetRoutesAsync();
                    AvailableRoutes.Clear();
                    foreach (var r in routes) AvailableRoutes.Add(r);
                    UpdateMatchedRoute();
                }
            }

            var input = BuildInput();
            var result = await AdminDatabaseService.RegisterBusAsync(input);
            var registeredPlate = Plate.Trim();
            var registeredCompany = CompanyName.Trim();

            if (result.Success)
            {
                await RefreshAsync();
                if (keepFormOpenForNextBus)
                {
                    ResetVehicleFieldsOnly();
                    IsFormVisible = true;
                    Message = $"✓ Đã lưu xe {registeredPlate} thành công! Mời bạn tiếp tục nhập thông tin xe tiếp theo cho nhà xe {registeredCompany}.";
                }
                else
                {
                    ResetForm();
                    IsFormVisible = false;
                    Message = result.Error ?? $"Xe {registeredPlate} đã đăng ký vào bến thành công cho nhà xe {registeredCompany}. Sơ đồ {count} ghế đã được tạo tự động.";
                }
            }
            else
            {
                Message = result.Error ?? "Không thể đăng ký xe vào bến.";
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Đăng ký xe vào bến");
        }
    }

    private async Task UpdateAsync()
    {
        if (_editingBusId <= 0) return;
        if (string.IsNullOrWhiteSpace(Plate))
        {
            Message = "Biển kiểm soát không được để trống.";
            return;
        }
        if (!int.TryParse(SeatCount, out var count) || count is < 1 or > 200)
        {
            Message = "Số ghế phải là số nguyên từ 1 đến 200.";
            return;
        }
        try
        {
            if (SelectedRoute == null && !string.IsNullOrWhiteSpace(SelectedOrigin) && !string.IsNullOrWhiteSpace(SelectedDestination))
            {
                var addRoute = await AdminDatabaseService.AddRouteAsync(SelectedOrigin.Trim(), SelectedDestination.Trim(), null);
                if (addRoute.Success)
                {
                    var routes = await AdminDatabaseService.GetRoutesAsync();
                    AvailableRoutes.Clear();
                    foreach (var r in routes) AvailableRoutes.Add(r);
                    UpdateMatchedRoute();
                }
            }

            var input = BuildInput();
            var result = await AdminDatabaseService.UpdateBusDetailsAsync(_editingBusId, input);
            Message = result.Error ?? $"Cập nhật hồ sơ xe {Plate} thành công.";
            if (result.Success)
            {
                ResetForm();
                IsFormVisible = false;
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Cập nhật hồ sơ xe");
        }
    }

    private async Task DeleteAsync()
    {
        var target = PendingDelete;
        if (target is null) return;
        try
        {
            var result = await AdminDatabaseService.DeleteBusAsync(target.BusId);
            Message = result.Error ?? $"Bus {target.LicensePlate} deleted.";
            PendingDelete = null;
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            PendingDelete = null;
            Message = AdminFailure.Describe(ex, "Deleting the bus");
        }
    }
}

