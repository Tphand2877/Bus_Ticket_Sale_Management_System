using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class SeatSelectionViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _passengerName = CurrentUser.Account?.FullName ?? string.Empty;
    private string _passengerPhone = CurrentUser.Account?.Phone ?? string.Empty;
    private SeatInfo? _selectedSeat;
    private string _statusMessage = string.Empty;

    public TripSearchResult Trip { get; }
    public ObservableCollection<SeatInfo> Seats { get; } = new();
    public SeatInfo? SelectedSeat
    {
        get => _selectedSeat;
        set
        {
            if (SetProperty(ref _selectedSeat, value))
            {
                OnPropertyChanged(nameof(HasSelectedSeat));
                OnPropertyChanged(nameof(SelectedSeatNumber));
                OnPropertyChanged(nameof(TotalPriceDisplay));
            }
        }
    }
    public string PassengerName { get => _passengerName; set => SetProperty(ref _passengerName, value); }
    public string PassengerPhone { get => _passengerPhone; set => SetProperty(ref _passengerPhone, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public int AvailableSeatsCount => Seats.Count(s => !s.IsBooked);
    public int BookedSeatsCount => Seats.Count(s => s.IsBooked);
    public bool HasSelectedSeat => SelectedSeat != null;
    public string SelectedSeatNumber => SelectedSeat?.SeatNumber ?? "Chưa chọn";
    public string TotalPriceDisplay => SelectedSeat != null ? $"{Trip.Price:N0} VND" : "0 VND";

    public ICommand GoToConfirmationCommand { get; }
    public ICommand BackToBookingCommand { get; }
    public ICommand SelectSeatCommand { get; }

    public SeatSelectionViewModel(INavigationService navigationService, TripSearchResult trip)
    {
        _navigationService = navigationService;
        Trip = trip;

        SelectSeatCommand = new RelayCommand(parameter =>
        {
            if (parameter is SeatInfo seat)
            {
                if (seat.IsBooked)
                {
                    StatusMessage = $"Ghế số {seat.SeatNumber} đã có người đặt trước.";
                    return;
                }
                SelectedSeat = seat;
                StatusMessage = $"Đã chọn ghế số {seat.SeatNumber}. Vui lòng kiểm tra thông tin hành khách.";
            }
        });

        GoToConfirmationCommand = new RelayCommand(() =>
        {
            if (SelectedSeat is null) { StatusMessage = "Vui lòng chọn một ghế trên sơ đồ xe."; return; }
            if (SelectedSeat.IsBooked) { StatusMessage = $"Ghế số {SelectedSeat.SeatNumber} đã có người đặt. Vui lòng chọn ghế khác."; return; }
            if (string.IsNullOrWhiteSpace(PassengerName)) { StatusMessage = "Vui lòng nhập họ và tên hành khách."; return; }
            _navigationService.NavigateTo(new BookingConfirmationViewModel(_navigationService, Trip, SelectedSeat, PassengerName.Trim(), PassengerPhone.Trim()));
        });
        BackToBookingCommand = new RelayCommand(() => _navigationService.NavigateTo(new BookingViewModel(_navigationService, Trip.Origin, Trip.Destination, Trip.DepartureTime.Date)));
        _ = LoadSeatsAsync();
    }

    private async Task LoadSeatsAsync()
    {
        try
        {
            Seats.Clear();
            var seats = await DatabaseHelper.GetSeatsAsync(Trip.TripId);
            foreach (var seat in seats) Seats.Add(seat);
            OnPropertyChanged(nameof(AvailableSeatsCount));
            OnPropertyChanged(nameof(BookedSeatsCount));
            if (Seats.Count == 0) StatusMessage = "Chuyến xe này chưa có dữ liệu sơ đồ ghế.";
            else StatusMessage = $"Có {AvailableSeatsCount} ghế còn trống, {BookedSeatsCount} ghế đã đặt.";
        }
        catch (Exception) { StatusMessage = "Không thể tải sơ đồ ghế từ cơ sở dữ liệu."; }
    }
}
