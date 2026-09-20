using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.ViewModels;

public sealed record CheckedInTicketItem(
    string TicketCode,
    string PassengerName,
    string Route,
    string SeatNumbers,
    string TimeCheckedIn);

/// <summary>
/// Dedicated Check-in / Barcode & QR Scanner terminal for bus station staff.
/// Receives scanned ticket codes, displays passenger and route information,
/// and moves paid tickets to 'Đã sử dụng' (Used).
/// </summary>
public sealed class AdminCheckInViewModel : ViewModelBase, IRefreshable
{
    private string _ticketCodeInput = string.Empty;
    private TicketDetails? _foundTicket;
    private bool _isSearching;
    private bool _isCheckingIn;
    private string _message = string.Empty;
    private string _messageKind = "Info";

    public ObservableCollection<CheckedInTicketItem> RecentCheckIns { get; } = new();

    public string TicketCodeInput
    {
        get => _ticketCodeInput;
        set => SetProperty(ref _ticketCodeInput, value);
    }

    public TicketDetails? FoundTicket
    {
        get => _foundTicket;
        private set
        {
            if (SetProperty(ref _foundTicket, value))
            {
                OnPropertyChanged(nameof(HasTicket));
                OnPropertyChanged(nameof(CanCheckIn));
                OnPropertyChanged(nameof(TicketStatusBadge));
                OnPropertyChanged(nameof(TicketStatusColor));
            }
        }
    }

    public bool HasTicket => FoundTicket is not null;
    public bool IsSearching { get => _isSearching; private set => SetProperty(ref _isSearching, value); }
    public bool IsCheckingIn { get => _isCheckingIn; private set => SetProperty(ref _isCheckingIn, value); }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
                OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public string MessageKind { get => _messageKind; private set => SetProperty(ref _messageKind, value); }

    public bool CanCheckIn =>
        FoundTicket is not null &&
        FoundTicket.Status == TicketStatuses.Paid &&
        FoundTicket.PaymentStatus == TicketStatuses.Paid &&
        !IsCheckingIn;

    public string TicketStatusBadge => FoundTicket switch
    {
        null => "",
        { Status: TicketStatuses.Used } => "ĐÃ SỬ DỤNG",
        { Status: TicketStatuses.Cancelled } => "ĐÃ HỦY",
        { PaymentStatus: TicketStatuses.Unpaid } => "CHƯA THANH TOÁN",
        { Status: TicketStatuses.Paid, PaymentStatus: TicketStatuses.Paid } => "HỢP LỆ — CHỜ SOÁT VÉ",
        _ => FoundTicket.Status
    };

    public string TicketStatusColor => FoundTicket switch
    {
        null => "#64748B",
        { Status: TicketStatuses.Used } => "#3B82F6",
        { Status: TicketStatuses.Cancelled } => "#EF4444",
        { PaymentStatus: TicketStatuses.Unpaid } => "#F59E0B",
        { Status: TicketStatuses.Paid, PaymentStatus: TicketStatuses.Paid } => "#10B981",
        _ => "#64748B"
    };

    public ICommand SearchCommand { get; }
    public ICommand CheckInCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RefreshCommand { get; }

    public AdminCheckInViewModel()
    {
        SearchCommand = new AsyncRelayCommand(SearchTicketAsync);
        CheckInCommand = new AsyncRelayCommand(CheckInAsync, () => CanCheckIn);
        ClearCommand = new RelayCommand(Clear);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public Task RefreshAsync()
    {
        Clear();
        return Task.CompletedTask;
    }

    public void Clear()
    {
        TicketCodeInput = string.Empty;
        FoundTicket = null;
        Message = string.Empty;
        MessageKind = "Info";
    }

    private async Task SearchTicketAsync()
    {
        var code = TicketCodeInput.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            Message = "Vui lòng nhập hoặc quét mã vé trên vé xe.";
            MessageKind = "Warning";
            return;
        }

        IsSearching = true;
        Message = string.Empty;
        try
        {
            var ticket = await DatabaseHelper.GetTicketByCodeAsync(code);
            if (ticket is null)
            {
                FoundTicket = null;
                Message = $"Không tìm thấy vé với mã '{code}'. Vui lòng kiểm tra lại.";
                MessageKind = "Danger";
                return;
            }

            FoundTicket = ticket;
            if (ticket.Status == TicketStatuses.Used)
            {
                Message = $"Vé {code} đã được check-in lúc {ticket.UsedAt?.ToLocalTime():HH:mm dd/MM/yyyy}.";
                MessageKind = "Warning";
            }
            else if (ticket.Status == TicketStatuses.Cancelled)
            {
                Message = $"Vé {code} đã bị hủy, không thể check-in.";
                MessageKind = "Danger";
            }
            else if (ticket.PaymentStatus != TicketStatuses.Paid)
            {
                Message = $"Vé {code} chưa thanh toán. Yêu cầu hành khách thanh toán trước khi soát vé lên xe.";
                MessageKind = "Warning";
            }
            else
            {
                Message = $"Tìm thấy vé hợp lệ cho hành khách {ticket.PassengerName}. Sẵn sàng soát vé.";
                MessageKind = "Success";
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Lỗi tra cứu vé '{code}'.", ex);
            Message = "Không thể tra cứu thông tin vé. Vui lòng thử lại.";
            MessageKind = "Danger";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task CheckInAsync()
    {
        var ticket = FoundTicket;
        if (ticket is null || !CanCheckIn) return;

        var adminId = CurrentUser.Account?.AccountId ?? 1;
        IsCheckingIn = true;
        try
        {
            var (success, error) = await TicketLifecycleService.CheckInTicketAsync(ticket.TicketId, adminId);
            if (success)
            {
                var timeStr = DateTime.Now.ToString("HH:mm:ss");
                RecentCheckIns.Insert(0, new CheckedInTicketItem(
                    ticket.TicketCode,
                    ticket.PassengerName,
                    ticket.Route,
                    ticket.SeatNumbers,
                    timeStr));

                Message = $"✓ SOÁT VÉ THÀNH CÔNG: Vé {ticket.TicketCode} - Khách {ticket.PassengerName} (Ghế {ticket.SeatNumbers}).";
                MessageKind = "Success";

                // Re-fetch ticket to update UI badge
                FoundTicket = await DatabaseHelper.GetTicketByCodeAsync(ticket.TicketCode);
                TicketCodeInput = string.Empty;
            }
            else
            {
                Message = error ?? "Soát vé thất bại.";
                MessageKind = "Danger";
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Lỗi check-in vé {ticket.TicketCode}.", ex);
            Message = "Có lỗi xảy ra trong quá trình soát vé.";
            MessageKind = "Danger";
        }
        finally
        {
            IsCheckingIn = false;
        }
    }
}
