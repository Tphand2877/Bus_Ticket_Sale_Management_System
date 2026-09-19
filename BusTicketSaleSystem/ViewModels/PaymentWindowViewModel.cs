using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.ViewModels;

public sealed class PaymentWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollBudget = TimeSpan.FromMinutes(10);

    private readonly int _referenceCount;
    private readonly string? _paymentUrl;
    private readonly string _transactionNo;
    private readonly DateTime _holdExpiresAtUtc;
    private readonly DispatcherTimer? _countdownTimer;
    private readonly DispatcherTimer? _pollTimer;
    private readonly Dispatcher _dispatcher;

    private DateTime _pollDeadlineUtc;
    private bool _pollInFlight;
    private bool _settled;
    private bool _disposed;

    private string _statusText = string.Empty;
    private string _countdownText = string.Empty;
    private string _closeButtonText = "Đóng";
    private bool _isHoldExpired;

    public event Action<bool?>? RequestClose;

    public bool IsBankTransfer { get; }
    public bool IsVnPay => !IsBankTransfer;
    public bool PaymentConfirmed { get; private set; }

    public string TitleText => IsBankTransfer ? "THANH TOÁN CHUYỂN KHOẢN NGÂN HÀNG" : "THANH TOÁN QUA CỔNG VNPAY";
    public string DescriptionText => IsBankTransfer
        ? "Quét mã QR hoặc chuyển khoản chính xác số tiền với nội dung bên dưới."
        : "Hoàn tất thanh toán trong cửa sổ trình duyệt vừa mở.";

    public BitmapImage? BankQrImage { get; }
    public string AmountText { get; }
    public string ReferenceCaption => IsBankTransfer
        ? (_referenceCount == 1 ? "MÃ NỘI DUNG CHUYỂN KHOẢN" : "CÁC MÃ NỘI DUNG CHUYỂN KHOẢN")
        : "MÃ VÉ";
    public string ReferenceText { get; }

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string CountdownText { get => _countdownText; set => SetProperty(ref _countdownText, value); }
    public string CloseButtonText { get => _closeButtonText; set => SetProperty(ref _closeButtonText, value); }

    /// <summary>
    /// True once the seat hold has lapsed. Submission is refused from this point: the seats are
    /// back on sale, so reporting a pending transfer would be a lie.
    /// </summary>
    public bool IsHoldExpired
    {
        get => _isHoldExpired;
        private set
        {
            if (SetProperty(ref _isHoldExpired, value))
                OnPropertyChanged(nameof(CanSubmitTransfer));
        }
    }

    public bool CanSubmitTransfer => IsBankTransfer && !_isHoldExpired;

    public ICommand SubmitTransferCommand { get; }
    public ICommand OpenGatewayCommand { get; }
    public ICommand CloseCommand { get; }

    public PaymentWindowViewModel(IReadOnlyList<string> ticketCodes, decimal amount, string transactionNo, string? paymentUrl, bool isBankTransfer, DateTime holdExpiresAtUtc, Dispatcher? dispatcher = null)
    {
        if (ticketCodes == null || ticketCodes.Count == 0)
            throw new ArgumentException("Cần ít nhất một mã vé để thanh toán.", nameof(ticketCodes));

        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _paymentUrl = paymentUrl;
        _transactionNo = transactionNo;
        _holdExpiresAtUtc = holdExpiresAtUtc;
        _referenceCount = ticketCodes.Count;
        IsBankTransfer = isBankTransfer;
        AmountText = $"{amount:N0} VND";
        ReferenceText = string.Join(", ", ticketCodes);

        SubmitTransferCommand = new RelayCommand(SubmitTransfer, () => CanSubmitTransfer);
        OpenGatewayCommand = new RelayCommand(OpenGateway);
        CloseCommand = new RelayCommand(CloseWindow);

        if (IsBankTransfer)
        {
            var qrPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BankTransferQr.jpg");
            if (File.Exists(qrPath))
            {
                try
                {
                    BankQrImage = new BitmapImage(new Uri(qrPath, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Không thể nạp ảnh mã QR chuyển khoản.", ex);
                }
            }

            _statusText = HoldStatusText();
            _closeButtonText = "Đóng — Tôi sẽ chuyển khoản sau";
            _countdownTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += OnCountdownTick;
            _countdownTimer.Start();
            UpdateCountdown();
        }
        else
        {
            _statusText = VnPayCallbackListener.IsListening
                ? "Đang chờ VNPay xác nhận thanh toán. Cửa sổ này sẽ tự động cập nhật."
                : "Đang chờ VNPay xác nhận thanh toán. Trạng thái sẽ được kiểm tra lại mỗi vài giây.";
            _closeButtonText = "Đóng — Giao dịch đang xử lý";
            _pollDeadlineUtc = DateTime.UtcNow.Add(PollBudget);
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = PollInterval };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
            VnPayCallbackListener.PaymentSettled += OnGatewaySettled;

            // Automatically open payment gateway
            OpenGateway();
        }
    }

    private string HoldStatusText() =>
        $"Chỗ của bạn được giữ tạm thời đến {_holdExpiresAtUtc.ToLocalTime():HH:mm 'ngày' dd/MM/yyyy}. Nhân viên sẽ duyệt sau khi nhận chuyển khoản.";

    private void OnCountdownTick(object? sender, EventArgs e) => UpdateCountdown();

    private void UpdateCountdown()
    {
        var remaining = _holdExpiresAtUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            CountdownText = "Thời gian giữ chỗ đã hết.";
            StatusText = "Vé đã được hủy giữ chỗ vì hết hạn thanh toán. Vui lòng đặt vé lại.";
            _countdownTimer?.Stop();
            // Must disable submission: the seats are already released, so accepting a transfer
            // here would tell the user their booking is pending when it no longer exists.
            IsHoldExpired = true;
            CloseButtonText = "Đóng";
            return;
        }
        CountdownText = remaining.TotalHours >= 1
            ? $"Chỗ được giữ thêm {(int)remaining.TotalHours} giờ {remaining.Minutes:00} phút"
            : $"Chỗ được giữ thêm {remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    private async void OnPollTick(object? sender, EventArgs e)
    {
        if (_pollInFlight || _settled) return;
        if (DateTime.UtcNow > _pollDeadlineUtc)
        {
            _pollTimer?.Stop();
            StatusText = "VNPay chưa xác nhận thanh toán kịp thời gian. Bạn có thể kiểm tra mục 'Vé của tôi' sau hoặc liên hệ hỗ trợ.";
            return;
        }

        _pollInFlight = true;
        try
        {
            var status = await PaymentService.GetPaymentStatusAsync(_transactionNo);
            switch (status)
            {
                case "Success":
                    Settle(true);
                    break;
                case "Failed":
                    Settle(false);
                    break;
            }
        }
        catch (SqlException ex)
        {
            LoggerService.LogError($"Lỗi kiểm tra trạng thái thanh toán cho mã {_transactionNo}.", ex);
        }
        catch (InvalidOperationException ex)
        {
            LoggerService.LogError($"Trạng thái không hợp lệ khi kiểm tra thanh toán cho mã {_transactionNo}.", ex);
        }
        finally
        {
            _pollInFlight = false;
        }
    }

    private void OnGatewaySettled(string transactionNo, bool success)
    {
        if (!string.Equals(transactionNo, _transactionNo, StringComparison.Ordinal)) return;
        _dispatcher.BeginInvoke(() => Settle(success));
    }

    private void Settle(bool success)
    {
        if (_settled) return;
        _settled = true;
        _pollTimer?.Stop();
        if (success)
        {
            PaymentConfirmed = true;
            StatusText = "Thanh toán thành công! Vé của bạn đã được xuất.";
            RequestClose?.Invoke(true);
            return;
        }
        StatusText = "Cổng VNPay thông báo thanh toán không thành công. Ghế đã được giải phóng để bạn có thể đặt lại.";
        CloseButtonText = "Đóng";
    }

    public void SubmitTransfer()
    {
        // Defence in depth: CanExecute already blocks this, but a lapsed hold must never be
        // reported back to the caller as a submitted transfer.
        if (!CanSubmitTransfer)
        {
            StatusText = "Thời gian giữ chỗ đã hết nên không thể ghi nhận chuyển khoản. Vui lòng đặt vé lại.";
            return;
        }

        StatusText = HoldStatusText();
        RequestClose?.Invoke(true);
    }

    public void OpenGateway()
    {
        if (string.IsNullOrWhiteSpace(_paymentUrl) ||
            !Uri.TryCreate(_paymentUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            StatusText = "Địa chỉ cổng thanh toán không hợp lệ, không thể mở trình duyệt.";
            LoggerService.LogError($"Từ chối mở địa chỉ VNPay không hợp lệ: {_paymentUrl}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Không thể mở trình duyệt để thanh toán VNPay.", ex);
        }
    }

    public void CloseWindow()
    {
        RequestClose?.Invoke(PaymentConfirmed);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_countdownTimer != null)
        {
            _countdownTimer.Stop();
            _countdownTimer.Tick -= OnCountdownTick;
        }

        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer.Tick -= OnPollTick;
        }

        if (IsVnPay)
        {
            VnPayCallbackListener.PaymentSettled -= OnGatewaySettled;
        }
    }
}
