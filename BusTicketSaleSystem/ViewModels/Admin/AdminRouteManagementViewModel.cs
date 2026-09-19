using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
namespace BusTicketSaleSystem.ViewModels;

public sealed class AdminRouteManagementViewModel : ViewModelBase, IRefreshable
{
    private string _origin = "";
    private string _destination = "";
    private string _distance = "";
    private string _message = "";
    private bool _isEditing;
    private bool _isLoading;
    private int _editingRouteId;
    private RouteAdminItem? _pendingDelete;

    public ObservableCollection<RouteAdminItem> Routes { get; } = new();
    public string StationScope => LocationService.Instance.CurrentLocation;
    public string StationScopeDisplay => $"📍 Quản lý phạm vi: Bến xe {StationScope} (Cố định theo máy trạm)";

    public string Origin { get => _origin; set => SetProperty(ref _origin, value); }
    public string Destination { get => _destination; set => SetProperty(ref _destination, value); }
    public string Distance { get => _distance; set => SetProperty(ref _distance, value); }
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
    public bool HasRoutes => Routes.Count > 0;
    public bool HasNoRoutes => !HasRoutes && !IsLoading;

    public RouteAdminItem? PendingDelete
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
        : $"Xóa tuyến đường {PendingDelete.DisplayName}? Thao tác này không thể hoàn tác.";

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand SelectForEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand RefreshCommand { get; }

    public AdminRouteManagementViewModel()
    {
        AddCommand = new RelayCommand(async () => await AddAsync());
        UpdateCommand = new RelayCommand(async () => await UpdateAsync(), () => IsEditing);
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        CancelEditCommand = new RelayCommand(ResetForm);
        SelectForEditCommand = new RelayCommand(p =>
        {
            if (p is not RouteAdminItem r) return;
            PendingDelete = null;
            _editingRouteId = r.RouteId;
            Origin = r.Origin;
            Destination = r.Destination;
            Distance = r.DistanceKm?.ToString("F1") ?? "";
            IsEditing = true;
            Message = $"Đang chỉnh sửa tuyến {r.DisplayName}.";
        });
        RequestDeleteCommand = new RelayCommand(p =>
        {
            if (p is not RouteAdminItem r) return;
            Message = "";
            PendingDelete = r;
        });
        CancelDeleteCommand = new RelayCommand(() => PendingDelete = null);
        ConfirmDeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => IsConfirmingDelete);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var station = StationScope;
            var routes = await AdminDatabaseService.GetRoutesAsync();
            var scoped = routes.Where(r =>
                r.Origin.Contains(station, StringComparison.OrdinalIgnoreCase) ||
                r.Destination.Contains(station, StringComparison.OrdinalIgnoreCase)).ToList();

            Routes.Clear();
            foreach (var r in scoped) Routes.Add(r);
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Tải danh sách tuyến đường");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasRoutes));
            OnPropertyChanged(nameof(HasNoRoutes));
        }
    }

    private void ResetForm()
    {
        IsEditing = false;
        _editingRouteId = 0;
        Origin = StationScope;
        Destination = Distance = "";
        Message = "";
    }

    private bool TryReadDistance(out decimal? distance)
    {
        distance = null;
        if (string.IsNullOrWhiteSpace(Distance)) return true;
        if (!decimal.TryParse(Distance, out var value))
        {
            Message = "Khoảng cách phải là số đo theo kilomet.";
            return false;
        }
        distance = value;
        return true;
    }

    private async Task AddAsync()
    {
        var station = StationScope;
        if (!Origin.Contains(station, StringComparison.OrdinalIgnoreCase) &&
            !Destination.Contains(station, StringComparison.OrdinalIgnoreCase))
        {
            Message = $"Thao tác bị từ chối: Tuyến đường mới phải có điểm đi hoặc điểm đến tại Bến xe {station}.";
            return;
        }
        if (!TryReadDistance(out var distance)) return;
        try
        {
            var result = await AdminDatabaseService.AddRouteAsync(Origin, Destination, distance);
            Message = result.Error ?? "Tạo tuyến đường thành công.";
            if (result.Success)
            {
                Origin = Destination = Distance = "";
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Tạo tuyến đường mới");
        }
    }

    private async Task UpdateAsync()
    {
        if (_editingRouteId <= 0) return;
        var station = StationScope;
        if (!Origin.Contains(station, StringComparison.OrdinalIgnoreCase) &&
            !Destination.Contains(station, StringComparison.OrdinalIgnoreCase))
        {
            Message = $"Thao tác bị từ chối: Tuyến đường phải có điểm đi hoặc điểm đến tại Bến xe {station}.";
            return;
        }
        if (!TryReadDistance(out var distance)) return;
        try
        {
            var result = await AdminDatabaseService.UpdateRouteAsync(_editingRouteId, Origin, Destination, distance);
            Message = result.Error ?? "Cập nhật tuyến đường thành công.";
            if (result.Success)
            {
                IsEditing = false;
                _editingRouteId = 0;
                Origin = Destination = Distance = "";
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Cập nhật tuyến đường");
        }
    }

    private async Task DeleteAsync()
    {
        var target = PendingDelete;
        if (target is null) return;
        try
        {
            var result = await AdminDatabaseService.DeleteRouteAsync(target.RouteId);
            Message = result.Error ?? $"Đã xóa tuyến {target.DisplayName}.";
            PendingDelete = null;
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            PendingDelete = null;
            Message = AdminFailure.Describe(ex, "Xóa tuyến đường");
        }
    }
}
