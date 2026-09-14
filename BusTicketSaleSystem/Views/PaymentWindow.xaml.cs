using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;

namespace BusTicketSaleSystem.Views;

public partial class PaymentWindow : Window
{
    private readonly string? _paymentUrl;

    public bool IsBankTransfer { get; }
    public bool IsVnPay => !IsBankTransfer;
    public string TitleText => IsBankTransfer ? "THANH TOÁN QUA VIETQR" : "THANH TOÁN QUA VNPAY";
    public string DescriptionText => IsBankTransfer
        ? "Quét mã QR để chuyển khoản đúng số tiền vé"
        : "Giao dịch được xác minh bởi backend và VNPay";
    public string BankTransferInstruction =>
        "Nội dung chuyển khoản: mã vé hiển thị bên dưới. Giao dịch sẽ được đối soát trước khi vé chuyển sang Đã thanh toán.";
    public BitmapImage? BankQrImage { get; }
    public string AmountText { get; }
    public string TicketCodeText { get; }

    public PaymentWindow(int paymentId, int ticketId, string ticketCode, decimal amount, string? paymentUrl, bool isBankTransfer = false)
    {
        InitializeComponent();
        _paymentUrl = paymentUrl;
        IsBankTransfer = isBankTransfer;
        if (IsBankTransfer)
        {
            var qrPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "BankTransferQr.jpg");
            if (System.IO.File.Exists(qrPath))
                BankQrImage = new BitmapImage(new Uri(qrPath, UriKind.Absolute));
        }
        AmountText = $"{amount:N0} VND";
        TicketCodeText = IsBankTransfer
            ? $"Mã vé / nội dung chuyển khoản: {ticketCode}"
            : $"Mã vé: {ticketCode} | VNPay Sandbox";
        DataContext = this;
        if (!IsBankTransfer)
            Loaded += (_, _) => OpenGateway_Click(this, new RoutedEventArgs());
    }

    private void OpenGateway_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_paymentUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_paymentUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở cổng thanh toán: {ex.Message}", "Thanh toán", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void PaymentWindow_Closed(object? sender, EventArgs e) { }
}
