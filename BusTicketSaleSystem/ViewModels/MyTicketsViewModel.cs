using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class MyTicketsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public ObservableCollection<TicketItemViewModel> Tickets { get; } = new();
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(HasNoTickets));
                OnPropertyChanged(nameof(HasTickets));
            }
        }
    }
    public bool HasTickets => Tickets.Count > 0;
    public bool HasNoTickets => !IsBusy && Tickets.Count == 0;

    public ICommand ViewDetailsCommand { get; }
    public ICommand CancelTicketCommand { get; }
    public ICommand BookNewTicketCommand { get; }
    public ICommand RefreshCommand { get; }

    public MyTicketsViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        ViewDetailsCommand = new RelayCommand(parameter =>
        {
            if (parameter is TicketItemViewModel item)
            {
                _navigationService.NavigateTo(new TicketViewModel(_navigationService, item.Ticket));
            }
        });

        CancelTicketCommand = new RelayCommand(async parameter =>
        {
            if (parameter is TicketItemViewModel item)
            {
                await CancelTicketAsync(item.Ticket.TicketId);
            }
        });

        BookNewTicketCommand = new RelayCommand(() => _navigationService.NavigateTo<BookingViewModel>());
        RefreshCommand = new RelayCommand(async () => await LoadAsync());

        _ = LoadAsync();
    }

    private async Task CancelTicketAsync(int ticketId)
    {
        if (CurrentUser.Account is null) return;
        IsBusy = true;
        try
        {
            var result = await DatabaseHelper.CancelTicketAsync(ticketId, CurrentUser.Account.AccountId);
            if (result.Success)
            {
                StatusMessage = "Hủy vé thành công. Vé đã được chuyển sang phần Lịch sử chuyến đi.";
                await LoadAsync();
            }
            else
            {
                StatusMessage = result.Error ?? "Không thể hủy vé.";
            }
        }
        catch
        {
            StatusMessage = "Lỗi khi xử lý hủy vé.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync()
    {
        if (CurrentUser.Account is null) return;
        IsBusy = true;
        try
        {
            Tickets.Clear();
            var tickets = await DatabaseHelper.GetTicketsAsync(CurrentUser.Account.AccountId, false);
            foreach (var ticket in tickets)
            {
                Tickets.Add(new TicketItemViewModel(ticket));
            }
        }
        catch
        {
            StatusMessage = "Không thể tải danh sách vé từ cơ sở dữ liệu.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasTickets));
            OnPropertyChanged(nameof(HasNoTickets));
        }
    }
}

public sealed class TicketItemViewModel : ViewModelBase
{
    public TicketDetails Ticket { get; }
    public string TicketCode => Ticket.TicketCode;
    public string Route => Ticket.Route;
    public string Date => Ticket.DepartureTime.ToString("dd/MM/yyyy");
    public string Time => Ticket.DepartureTime.ToString("HH:mm");
    public string BusOperator => string.IsNullOrWhiteSpace(Ticket.BusType) ? "OmniBus Express" : Ticket.BusType;
    public string BusType => Ticket.BusType;
    public string SeatNumber => Ticket.SeatNumber;
    public string BoardingPoint => $"{Ticket.Origin} (Bến xe trung tâm)";
    public string Price => $"{Ticket.Price:N0} VND";
    public string Status => Ticket.Status;
    public bool CanCancel => Ticket.Status != "Đã hủy" && Ticket.Status != "Đã sử dụng";

    public TicketItemViewModel(TicketDetails ticket) => Ticket = ticket;
}
