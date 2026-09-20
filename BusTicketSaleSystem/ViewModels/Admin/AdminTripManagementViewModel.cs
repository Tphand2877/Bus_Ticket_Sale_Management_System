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


public sealed class AdminTripManagementViewModel : ViewModelBase, IRefreshable
{
    private int _selectedRouteId;
    private int _selectedBusId;
    private DateTime _departure = DateTime.Today.AddDays(1).AddHours(7);
    private DateTime _arrival = DateTime.Today.AddDays(1).AddHours(10);
    private string _price = "150000";
    private string _message = "";
    private bool _isEditing;
    private bool _isLoading;
    private int _editingTripId;
    private TripAdminItem? _pendingDelete;

    public ObservableCollection<RouteAdminItem> Routes { get; } = new();
    public ObservableCollection<BusAdminItem> Buses { get; } = new();
    public ObservableCollection<TripAdminItem> Trips { get; } = new();

    public string StationScope => LocationService.Instance.CurrentLocation;
    public string StationScopeDisplay => $"📍 Quản lý phạm vi: Bến xe {StationScope} (Cố định theo máy trạm)";
    public int SelectedRouteId { get => _selectedRouteId; set => SetProperty(ref _selectedRouteId, value); }
    public int SelectedBusId { get => _selectedBusId; set => SetProperty(ref _selectedBusId, value); }
    public DateTime Departure { get => _departure; set => SetProperty(ref _departure, value); }
    public DateTime Arrival { get => _arrival; set => SetProperty(ref _arrival, value); }
    public string Price { get => _price; set => SetProperty(ref _price, value); }
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
    public bool HasTrips => Trips.Count > 0;
    public bool HasNoTrips => !HasTrips && !IsLoading;

    public TripAdminItem? PendingDelete
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
        : $"Delete the {PendingDelete.Route} departure on {PendingDelete.DepartureFormatted}? This cannot be undone.";

    public ICommand AddCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand SelectForEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }
    public ICommand RefreshCommand { get; }

    public AdminTripManagementViewModel()
    {
        AddCommand = new AsyncRelayCommand(async () => await AddAsync());
        UpdateCommand = new AsyncRelayCommand(async () => await UpdateAsync(), () => IsEditing);
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        CancelEditCommand = new RelayCommand(() =>
        {
            IsEditing = false;
            _editingTripId = 0;
            Message = "";
        });
        SelectForEditCommand = new RelayCommand(p =>
        {
            if (p is not TripAdminItem t) return;
            PendingDelete = null;
            _editingTripId = t.TripId;
            SelectedRouteId = t.RouteId;
            SelectedBusId = t.BusId;
            Departure = t.DepartureTime;
            Arrival = t.ArrivalTime;
            Price = t.Price.ToString("F0");
            IsEditing = true;
            Message = $"Editing the {t.Route} departure on {t.DepartureFormatted}.";
        });
        RequestDeleteCommand = new RelayCommand(p =>
        {
            if (p is not TripAdminItem t) return;
            Message = "";
            PendingDelete = t;
        });
        CancelDeleteCommand = new RelayCommand(() => PendingDelete = null);
        ConfirmDeleteCommand = new AsyncRelayCommand(async () => await DeleteAsync(), () => IsConfirmingDelete);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var station = StationScope;
            var routes = await AdminDatabaseService.GetRoutesAsync();
            var buses = await AdminDatabaseService.GetBusesAsync();
            var trips = await AdminDatabaseService.GetTripsAsync();

            // Chỉ hiển thị các tuyến xe và chuyến xe đi/đến từ bến xe của máy Admin này
            var scopedRoutes = routes.Where(r =>
                r.Origin.Contains(station, StringComparison.OrdinalIgnoreCase) ||
                r.Destination.Contains(station, StringComparison.OrdinalIgnoreCase)).ToList();

            var scopedTrips = trips.Where(t =>
                t.Route.Contains(station, StringComparison.OrdinalIgnoreCase)).ToList();
            Routes.Clear();
            foreach (var r in scopedRoutes) Routes.Add(r);
            Buses.Clear();
            foreach (var b in buses) Buses.Add(b);
            Trips.Clear();
            foreach (var t in scopedTrips) Trips.Add(t);

            if (SelectedRouteId == 0 && Routes.Count > 0) SelectedRouteId = Routes[0].RouteId;
            if (SelectedBusId == 0 && Buses.Count > 0) SelectedBusId = Buses[0].BusId;
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Loading trip schedules");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasTrips));
            OnPropertyChanged(nameof(HasNoTrips));
        }
    }

    private async Task AddAsync()
    {
        var route = Routes.FirstOrDefault(r => r.RouteId == SelectedRouteId);
        if (route != null &&
            !route.Origin.Contains(StationScope, StringComparison.OrdinalIgnoreCase) &&
            !route.Destination.Contains(StationScope, StringComparison.OrdinalIgnoreCase))
        {
            Message = $"Thao tác bị từ chối: Máy trạm này chỉ có quyền xếp lịch cho các tuyến thuộc Bến xe {StationScope}.";
            return;
        }

        if (!decimal.TryParse(Price, out var price))
        {
            Message = "Fare must be a number.";
            return;
        }
        try
        {
            var result = await AdminDatabaseService.AddTripAsync(SelectedRouteId, SelectedBusId, Departure, Arrival, price);
            Message = result.Error ?? "Trip scheduled.";
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Scheduling the trip");
        }
    }

    private async Task UpdateAsync()
    {
        if (_editingTripId <= 0) return;
        var route = Routes.FirstOrDefault(r => r.RouteId == SelectedRouteId);
        if (route != null &&
            !route.Origin.Contains(StationScope, StringComparison.OrdinalIgnoreCase) &&
            !route.Destination.Contains(StationScope, StringComparison.OrdinalIgnoreCase))
        {
            Message = $"Thao tác bị từ chối: Tuyến này không thuộc phạm vi Bến xe {StationScope}.";
            return;
        }
        if (!decimal.TryParse(Price, out var price))
        {
            Message = "Fare must be a number.";
            return;
        }
        try
        {
            var result = await AdminDatabaseService.UpdateTripAsync(_editingTripId, SelectedRouteId, SelectedBusId, Departure, Arrival, price);
            Message = result.Error ?? "Trip updated.";
            if (result.Success)
            {
                IsEditing = false;
                _editingTripId = 0;
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Updating the trip");
        }
    }

    private async Task DeleteAsync()
    {
        var target = PendingDelete;
        if (target is null) return;
        try
        {
            var result = await AdminDatabaseService.DeleteTripAsync(target.TripId);
            Message = result.Error ?? "Trip deleted.";
            PendingDelete = null;
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            PendingDelete = null;
            Message = AdminFailure.Describe(ex, "Deleting the trip");
        }
    }
}

