using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.Views;
using System.Windows;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class BookingConfirmationViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public TripSearchResult Trip { get; }
    public SeatInfo Seat { get; }
    public string PassengerName { get; }
    public string PassengerPhone { get; }
    public IReadOnlyList<string> PaymentMethods { get; } =
    [
        "VNPay (Cổng thanh toán điện tử / Thẻ ATM)",
        "Tiền mặt (Thanh toán khi lên xe)",
        "Chuyển khoản (VietQR)"
    ];
    private string _selectedPaymentMethod = "VNPay (Cổng thanh toán điện tử / Thẻ ATM)";
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public string SelectedPaymentMethod { get => _selectedPaymentMethod; set => SetProperty(ref _selectedPaymentMethod, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string PriceDisplay => $"{Trip.Price:N0} VND";
    public ICommand CompleteBookingCommand { get; }
    public ICommand BackToSeatsCommand { get; }

    public BookingConfirmationViewModel(INavigationService navigationService, TripSearchResult trip, SeatInfo seat, string passengerName, string passengerPhone)
    {
        _navigationService = navigationService;
        Trip = trip;
        Seat = seat;
        PassengerName = passengerName;
        PassengerPhone = passengerPhone;
        CompleteBookingCommand = new RelayCommand(async () => await CompleteBookingAsync());
        BackToSeatsCommand = new RelayCommand(() => _navigationService.NavigateTo(new SeatSelectionViewModel(_navigationService, Trip)));
    }

    private async Task CompleteBookingAsync()
    {
        if (CurrentUser.Account is null) { StatusMessage = "Phiên đăng nhập đã hết hạn."; return; }
        IsBusy = true;
        try
        {
            var booking = await DatabaseHelper.BookTicketAsync(CurrentUser.Account.AccountId, Trip.TripId, Seat.SeatId, PassengerName, PassengerPhone);
            if (!booking.Success || booking.Ticket is null) { StatusMessage = booking.Error ?? "Không thể đặt vé."; return; }

            var isCash = SelectedPaymentMethod.Contains("Tiền mặt", StringComparison.OrdinalIgnoreCase) ||
                         SelectedPaymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase);
            var isBankTransfer = SelectedPaymentMethod.Contains("Chuyển khoản", StringComparison.OrdinalIgnoreCase) ||
                                 SelectedPaymentMethod.Contains("VietQR", StringComparison.OrdinalIgnoreCase) ||
                                 SelectedPaymentMethod.Contains("BankTransfer", StringComparison.OrdinalIgnoreCase);

            var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(
                booking.Ticket.TicketId,
                booking.Ticket.TicketCode,
                isCash ? "Cash" : isBankTransfer ? "BankTransfer" : SelectedPaymentMethod));

            if (!payment.IsSuccess)
            {
                await DatabaseHelper.CancelTicketAsync(booking.Ticket.TicketId, CurrentUser.Account.AccountId);
                StatusMessage = payment.ErrorMessage ?? "Không thể khởi tạo giao dịch thanh toán.";
                return;
            }

            if (isCash)
            {
                if (!string.IsNullOrWhiteSpace(CurrentUser.Account.Email))
                {
                    try
                    {
                        await ExternalApiServices.SendTicketEmailAsync(booking.Ticket, CurrentUser.Account.Email);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError("Could not send ticket email.", ex);
                    }
                }

                _navigationService.NavigateTo(new TicketViewModel(_navigationService, booking.Ticket));
                return;
            }

            if (isBankTransfer)
            {
                var bankPaymentWindow = new PaymentWindow(
                    payment.PaymentId,
                    booking.Ticket.TicketId,
                    booking.Ticket.TicketCode,
                    booking.Ticket.Price,
                    null,
                    isBankTransfer: true)
                {
                    Owner = Application.Current.MainWindow
                };
                bankPaymentWindow.ShowDialog();
                StatusMessage = "Đã tạo yêu cầu chuyển khoản. Vé đang chờ xác minh và được giữ trong 15 phút.";
                return;
            }

            if (string.IsNullOrWhiteSpace(payment.PaymentUrl))
            {
                await DatabaseHelper.CancelTicketAsync(booking.Ticket.TicketId, CurrentUser.Account.AccountId);
                StatusMessage = "Không có đường dẫn thanh toán hợp lệ.";
                return;
            }

            var paymentWindow = new PaymentWindow(payment.PaymentId, booking.Ticket.TicketId,
                booking.Ticket.TicketCode, booking.Ticket.Price, payment.PaymentUrl)
            {
                Owner = Application.Current.MainWindow
            };
            paymentWindow.ShowDialog();
            StatusMessage = "Đang chờ cổng thanh toán xác minh...";
            var paymentStatus = await WaitForPaymentAsync(payment.TransactionNo!);
            if (paymentStatus == "Success")
            {
                if (!string.IsNullOrWhiteSpace(CurrentUser.Account.Email))
                {
                    try
                    {
                        await ExternalApiServices.SendTicketEmailAsync(booking.Ticket, CurrentUser.Account.Email);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError("Could not send ticket email.", ex);
                    }
                }
                _navigationService.NavigateTo(new TicketViewModel(_navigationService, booking.Ticket));
            }
            else
            {
                StatusMessage = "Chưa nhận được xác nhận thanh toán. Vé của bạn đã được giữ chỗ trong mục 'Vé của tôi' (hiệu lực 15 phút).";
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Error in CompleteBookingAsync.", ex);
            StatusMessage = "Không thể hoàn tất đặt vé và thanh toán.";
        }
        finally { IsBusy = false; }
    }

    private static async Task<string?> WaitForPaymentAsync(string transactionNo)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var status = await PaymentService.GetPaymentStatusAsync(transactionNo);
            if (status is "Success" or "Failed") return status;
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return null;
    }
}
