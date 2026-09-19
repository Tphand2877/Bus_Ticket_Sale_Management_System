using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using QRCoder;
using QuestPDF.Fluent;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Common;

namespace BusTicketSaleSystem.Services;

public sealed class TicketPrintModel
{
    public string TicketCode { get; init; } = "";
    public string OrderCode { get; init; } = "";
    public string StationName { get; init; } = "BẾN XE THÁI NGUYÊN";
    public string StationAddress { get; init; } = "Số 1, Đường Minh Cầu, TP. Thái Nguyên, Thái Nguyên";
    public string Slogan { get; init; } = "An toàn - Nhanh chóng - Tiện lợi";
    public string PassengerName { get; init; } = "";
    public string PassengerPhone { get; init; } = "";
    public string PassengerType { get; init; } = "Người lớn";
    public string RouteName { get; init; } = "";
    public string RouteOrigin { get; init; } = "";
    public string RouteDestination { get; init; } = "";
    public string OriginStation { get; init; } = "";
    public string DestinationStation { get; init; } = "";
    public string DepartureDateFormatted { get; init; } = "";
    public string DepartureTimeFormatted { get; init; } = "";
    public string ArrivalTimeFormatted { get; init; } = "";
    public string SeatNumbers { get; init; } = "";
    public int SeatCount { get; init; } = 1;
    public string BusCompanyName { get; init; } = "Sao Việt";
    public string BusCompanyPhone { get; init; } = "1900 1234";
    public string BusType { get; init; } = "";
    public string LicensePlate { get; init; } = "";
    public string DriverName { get; init; } = "Nguyễn Văn Hùng";
    public decimal UnitPrice { get; init; }
    public decimal BaseFare { get; init; }
    public decimal ServiceFee { get; init; } = 0m;
    public decimal Discount { get; init; } = 0m;
    public decimal RefundAmount { get; init; } = 0m;
    public decimal TotalPrice { get; init; }
    public string PaymentMethod { get; init; } = "Tiền mặt";
    public string Status { get; init; } = "";
    public DateTime BookingTime { get; init; }
    public string IssuedAt { get; init; } = "";
    public string StaffCode { get; init; } = "NV015";

    // Backward compatibility with previous API
    public string DepartureTime => DepartureTimeFormatted;
    public string ArrivalTime => ArrivalTimeFormatted;
    public decimal Price => TotalPrice;

    public static TicketPrintModel FromTicket(TicketDetails ticket)
    {
        var origin = string.IsNullOrWhiteSpace(ticket.Origin) ? "Thái Nguyên" : ticket.Origin.Trim();
        var destination = string.IsNullOrWhiteSpace(ticket.Destination) ? "Hà Nội" : ticket.Destination.Trim();
        var originUpper = origin.ToUpperInvariant();
        var destUpper = destination.ToUpperInvariant();

        var departureDate = FormatVietnameseDate(ticket.DepartureTime);
        var departureTime = ticket.DepartureTime.ToString("HH:mm");
        var arrivalTime = ticket.ArrivalTime.ToString("HH:mm");

        var orderCode = "BK" + ticket.BookingTime.ToString("yyMMdd") + (ticket.TicketId > 0 ? ticket.TicketId.ToString("D6") : "000085");
        var busCompany = string.IsNullOrWhiteSpace(ticket.BusType) ? "Sao Việt" : ticket.BusType;

        var paymentMethod = ticket.IsPaid ? "Chuyển khoản (VietQR)" : "Tiền mặt";

        return new TicketPrintModel
        {
            TicketCode = ticket.TicketCode,
            OrderCode = orderCode,
            StationName = $"BẾN XE {originUpper}",
            StationAddress = $"Số 1, Đường Minh Cầu, TP. {origin}",
            Slogan = "An toàn - Nhanh chóng - Tiện lợi",
            PassengerName = string.IsNullOrWhiteSpace(ticket.PassengerName) ? "Nguyễn Văn A" : ticket.PassengerName,
            PassengerPhone = string.IsNullOrWhiteSpace(ticket.PassengerPhone) ? "0987 654 321" : ticket.PassengerPhone,
            PassengerType = "Người lớn",
            RouteName = $"{origin} → {destination}",
            RouteOrigin = originUpper,
            RouteDestination = destUpper,
            OriginStation = $"Bến xe {origin}",
            DestinationStation = $"Bến xe {destination}",
            DepartureDateFormatted = departureDate,
            DepartureTimeFormatted = departureTime,
            ArrivalTimeFormatted = arrivalTime,
            SeatNumbers = string.IsNullOrWhiteSpace(ticket.SeatNumbers) ? "A12" : ticket.SeatNumbers,
            SeatCount = Math.Max(1, ticket.SeatCount),
            BusCompanyName = busCompany,
            BusCompanyPhone = "1900 1234",
            BusType = ticket.BusType,
            LicensePlate = string.IsNullOrWhiteSpace(ticket.LicensePlate) ? "20A-123.45" : ticket.LicensePlate,
            DriverName = "Nguyễn Văn Hùng",
            UnitPrice = ticket.Price,
            BaseFare = ticket.TotalPrice,
            ServiceFee = 0m,
            Discount = 0m,
            RefundAmount = 0m,
            TotalPrice = ticket.TotalPrice,
            PaymentMethod = paymentMethod,
            Status = ticket.Status,
            BookingTime = ticket.BookingTime,
            IssuedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            StaffCode = "NV015"
        };
    }

    public static string FormatVietnameseDate(DateTime date)
    {
        var dayOfWeek = date.DayOfWeek switch
        {
            DayOfWeek.Monday => "Thứ Hai",
            DayOfWeek.Tuesday => "Thứ Ba",
            DayOfWeek.Wednesday => "Thứ Tư",
            DayOfWeek.Thursday => "Thứ Năm",
            DayOfWeek.Friday => "Thứ Sáu",
            DayOfWeek.Saturday => "Thứ Bảy",
            DayOfWeek.Sunday => "Chủ Nhật",
            _ => ""
        };
        return $"{date:dd/MM/yyyy} ({dayOfWeek})";
    }
}

internal static class Barcode128
{
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131",
        "211412", // 103 Start A
        "211214", // 104 Start B
        "211232", // 105 Start C
        "2331112" // 106 Stop
    ];

    public static bool[] Encode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) text = "TICKET";
        var sequence = new List<int> { 104 }; // Start B
        int checksum = 104;
        for (int i = 0; i < text.Length; i++)
        {
            int val = text[i] - 32;
            if (val < 0 || val > 95) val = 0;
            sequence.Add(val);
            checksum += val * (i + 1);
        }
        sequence.Add(checksum % 103);
        sequence.Add(106); // Stop

        var bits = new List<bool>();
        for (int q = 0; q < 8; q++) bits.Add(false);

        foreach (var code in sequence)
        {
            string pattern = Patterns[code];
            bool bar = true;
            foreach (char c in pattern)
            {
                int width = c - '0';
                for (int w = 0; w < width; w++) bits.Add(bar);
                bar = !bar;
            }
        }
        bits.Add(true);
        bits.Add(true);
        for (int q = 0; q < 8; q++) bits.Add(false);
        return bits.ToArray();
    }

    public static BitmapSource GenerateBitmap(string text, int height = 36, int moduleWidth = 2)
    {
        var bits = Encode(text);
        int width = bits.Length * moduleWidth;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        for (int x = 0; x < bits.Length; x++)
        {
            byte color = bits[x] ? (byte)0 : (byte)255;
            for (int mx = 0; mx < moduleWidth; mx++)
            {
                int px = (x * moduleWidth) + mx;
                for (int y = 0; y < height; y++)
                {
                    int offset = (y * stride) + (px * 4);
                    pixels[offset] = color;
                    pixels[offset + 1] = color;
                    pixels[offset + 2] = color;
                    pixels[offset + 3] = 255;
                }
            }
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static byte[] GeneratePngBytes(string text, int height = 36, int moduleWidth = 2)
    {
        var bitmap = GenerateBitmap(text, height, moduleWidth);
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        return stream.ToArray();
    }
}

internal static class TicketIcons
{
    public const string Bus = "M 4,6 C 4,4.3 5.3,3 7,3 L 17,3 C 18.7,3 20,4.3 20,6 L 20,17 C 20,18.7 18.7,20 17,20 L 17,21.5 C 17,22.3 16.3,23 15.5,23 L 14.5,23 C 13.7,23 13,22.3 13,21.5 L 13,20 L 11,20 L 11,21.5 C 11,22.3 10.3,23 9.5,23 L 8.5,23 C 7.7,23 7,22.3 7,21.5 L 7,20 L 7,20 C 5.3,20 4,18.7 4,17 Z M 6,8 L 18,8 L 18,12 L 6,12 Z M 7.5,17 C 8.3,17 9,16.3 9,15.5 C 9,14.7 8.3,14 7.5,14 C 6.7,14 6,14.7 6,15.5 C 6,16.3 6.7,17 7.5,17 Z M 16.5,17 C 17.3,17 18,16.3 18,15.5 C 18,14.7 17.3,14 16.5,14 C 15.7,14 15,14.7 15,15.5 C 15,16.3 15.7,17 16.5,17 Z";
    public const string Pin = "M 12,2 C 8.13,2 5,5.13 5,9 C 5,14.25 12,22 12,22 C 12,22 19,14.25 19,9 C 19,5.13 15.87,2 12,2 Z M 12,11.5 C 10.62,11.5 9.5,10.38 9.5,9 C 9.5,7.62 10.62,6.5 12,6.5 C 13.38,6.5 14.5,7.62 14.5,9 C 14.5,10.38 13.38,11.5 12,11.5 Z";
    public const string Calendar = "M 19,3 L 18,3 L 18,1 L 16,1 L 16,3 L 8,3 L 8,1 L 6,1 L 6,3 L 5,3 C 3.89,3 3,3.9 3,5 L 3,19 C 3,20.1 3.89,21 5,21 L 19,21 C 20.1,21 21,20.1 21,19 L 21,5 C 21,3.9 20.1,3 19,3 Z M 19,19 L 5,19 L 5,8 L 19,8 L 19,19 Z";
    public const string Clock = "M 12,2 C 6.5,2 2,6.5 2,12 C 2,17.5 6.5,22 12,22 C 17.5,22 22,17.5 22,12 C 22,6.5 17.5,2 12,2 Z M 12,20 C 7.59,20 4,16.41 4,12 C 4,7.59 7.59,4 12,4 C 16.41,4 20,7.59 20,12 C 20,16.41 16.41,20 12,20 Z M 12.5,7 L 11,7 L 11,13 L 16.2,16.2 L 17,14.9 L 12.5,12.2 Z";
    public const string Seat = "M 7,3 C 5.9,3 5,3.9 5,5 L 5,13 C 5,14.1 5.9,15 7,15 L 17,15 C 18.1,15 19,14.1 19,13 L 19,5 C 19,3.9 18.1,3 17,3 Z M 4,16 L 20,16 C 20.6,16 21,16.4 21,17 C 21,17.6 20.6,18 20,18 L 4,18 C 3.4,18 3,17.6 3,17 C 3,16.4 3.4,16 4,16 Z M 6,18 L 6,21 L 8,21 L 8,18 Z M 16,18 L 16,21 L 18,21 L 18,18 Z";
    public const string User = "M 12,12 C 14.21,12 16,10.21 16,8 C 16,5.79 14.21,4 12,4 C 9.79,4 8,5.79 8,8 C 8,10.21 9.79,12 12,12 Z M 12,14 C 9.33,14 4,15.34 4,18 L 4,20 L 20,20 L 20,18 C 20,15.34 14.67,14 12,14 Z";
    public const string Phone = "M 6.62,10.79 C 8.06,13.62 10.38,15.94 13.21,17.38 L 15.41,15.18 C 15.69,14.9 16.08,14.82 16.43,14.93 C 17.55,15.3 18.75,15.5 20,15.5 C 20.55,15.5 21,15.95 21,16.5 L 21,20 C 21,20.55 20.55,21 20,21 C 10.61,21 3,13.39 3,4 C 3,3.45 3.45,3 4,3 L 7.5,3 C 8.05,3 8.5,3.45 8.5,4 C 8.5,5.25 8.7,6.45 9.07,7.57 C 9.18,7.92 9.1,8.31 8.82,8.59 L 6.62,10.79 Z";
    public const string Ticket = "M 21.41,11.58 L 12.41,2.58 C 12.05,2.22 11.55,2 11,2 L 4,2 C 2.9,2 2,2.9 2,4 L 2,11 C 2,11.55 2.22,12.05 2.59,12.42 L 11.59,21.42 C 11.95,21.78 12.45,22 13,22 C 13.55,22 14.05,21.78 14.41,21.41 L 21.41,14.41 C 21.78,14.05 22,13.55 22,13 C 22,12.45 21.77,11.94 21.41,11.58 Z M 6.5,8 C 5.67,8 5,7.33 5,6.5 C 5,5.67 5.67,5 6.5,5 C 7.33,5 8,5.67 8,6.5 C 8,7.33 7.33,8 6.5,8 Z";
    public const string BusFront = "M 4,16 L 4,6 C 4,4.3 5.3,3 7,3 L 17,3 C 18.7,3 20,4.3 20,6 L 20,16 C 20,17.7 18.7,19 17,19 L 17,20.5 C 17,21.3 16.3,22 15.5,22 L 14.5,22 C 13.7,22 13,21.3 13,20.5 L 13,19 L 11,19 L 11,20.5 C 11,21.3 10.3,22 9.5,22 L 8.5,22 C 7.7,22 7,21.3 7,20.5 L 7,19 C 5.3,19 4,17.7 4,16 Z M 6,6 L 18,6 L 18,11 L 6,11 Z M 7.5,16 C 8.3,16 9,15.3 9,14.5 C 9,13.7 8.3,13 7.5,13 C 6.7,13 6,13.7 6,14.5 C 6,15.3 6.7,16 7.5,16 Z M 16.5,16 C 17.3,16 18,15.3 18,14.5 C 18,13.7 17.3,13 16.5,13 C 15.7,13 15,13.7 15,14.5 C 15,15.3 15.7,16 16.5,16 Z";
    public const string Driver = "M 12,2 C 6.5,2 2,4.5 2,7 L 2,10 L 22,10 L 22,7 C 22,4.5 17.5,2 12,2 Z M 12,12 C 14.2,12 16,10.2 16,8 L 8,8 C 8,10.2 9.8,12 12,12 Z M 12,14 C 9.3,14 4,15.3 4,18 L 4,20 L 20,20 L 20,18 C 20,15.3 14.7,14 12,14 Z";
    public const string Card = "M 20,4 L 4,4 C 2.89,4 2.01,4.89 2.01,6 L 2,18 C 2,19.11 2.89,20 4,20 L 20,20 C 21.11,20 22,19.11 22,18 L 22,6 C 22,4.89 21.11,4 20,4 Z M 20,18 L 4,18 L 4,12 L 20,12 L 20,18 Z M 20,8 L 4,8 L 4,6 L 20,6 L 20,8 Z";
    public const string Cash = "M 21,6 L 3,6 C 1.9,6 1,6.9 1,8 L 1,16 C 1,17.1 1.9,18 3,18 L 21,18 C 22.1,18 23,17.1 23,16 L 23,8 C 23,6.9 22.1,6 21,6 Z M 21,16 L 3,16 L 3,8 L 21,8 L 21,16 Z M 12,9 C 10.3,9 9,10.3 9,12 C 9,13.7 10.3,15 12,15 C 13.7,15 15,13.7 15,12 C 15,10.3 13.7,9 12,9 Z";

    public static System.Windows.Shapes.Path CreateWpfIcon(string pathData, Brush brush, double size = 14)
    {
        return new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(pathData),
            Fill = brush,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
    }
}

public static class TicketPrintService
{
    private static readonly CultureInfo VnCulture = new("vi-VN");
    private static readonly SolidColorBrush NavyBrush = new(Color.FromRgb(0x0F, 0x4C, 0x81));
    private static readonly SolidColorBrush SlateDarkBrush = new(Color.FromRgb(0x0F, 0x17, 0x2A));
    private static readonly SolidColorBrush SlateBodyBrush = new(Color.FromRgb(0x33, 0x41, 0x55));
    private static readonly SolidColorBrush SlateMutedBrush = new(Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly SolidColorBrush CardBgBrush = new(Color.FromRgb(0xF0, 0xF4, 0xF9));
    private static readonly SolidColorBrush BadgeBgBrush = new(Color.FromRgb(0xE0, 0xED, 0xF8));
    private static readonly SolidColorBrush BorderLineBrush = new(Color.FromRgb(0xCB, 0xD5, 0xE1));
    private static readonly SolidColorBrush DividerLineBrush = new(Color.FromRgb(0xE2, 0xE8, 0xF0));

    static TicketPrintService()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        NavyBrush.Freeze();
        SlateDarkBrush.Freeze();
        SlateBodyBrush.Freeze();
        SlateMutedBrush.Freeze();
        CardBgBrush.Freeze();
        BadgeBgBrush.Freeze();
        BorderLineBrush.Freeze();
        DividerLineBrush.Freeze();
    }

    public static BitmapImage GenerateQrCodeImage(string ticketCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketCode);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticketCode, QRCodeGenerator.ECCLevel.Q);
        var bytes = new PngByteQRCode(data).GetGraphic(20);
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static byte[] GenerateQrCodePngBytes(string ticketCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketCode);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(ticketCode, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(20);
    }

    public static void PrintTicket(TicketPrintModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        var visual = BuildTicketVisual(model);
        dialog.PrintVisual(visual, $"Bus ticket - {model.TicketCode}");
    }

    public static void ExportToPng(TicketPrintModel model, string filePath)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            RenderVisualToPng(model, filePath);
        }
        else
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { RenderVisualToPng(model, filePath); }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw new InvalidOperationException("Failed to export PNG on STA thread.", error);
        }
    }

    private static void RenderVisualToPng(TicketPrintModel model, string filePath)
    {
        var visual = BuildTicketVisual(model);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(visual.DesiredSize.Width)),
            Math.Max(1, (int)Math.Ceiling(visual.DesiredSize.Height)),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    public static void ExportToPdf(TicketPrintModel model, string filePath)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var qrBytes = GenerateQrCodePngBytes(model.TicketCode);
        var barcodeBytes = Barcode128.GeneratePngBytes(model.TicketCode, height: 40, moduleWidth: 2);

        // Aspect ratio matches standard thermal ticket receipt ~400 x 720 points
        var receiptPageSize = new QuestPDF.Helpers.PageSize(410, 730);

        Document.Create(container => container.Page(page =>
        {
            page.Size(receiptPageSize);
            page.Margin(20);
            page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(9).FontColor("#0F172A"));
            page.Content().Column(column =>
            {
                column.Spacing(8);

                // 1. Header: Station Brand on Left, Barcode on Right
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(headerCol =>
                    {
                        headerCol.Item().Text(model.StationName).FontSize(14).Bold().FontColor("#0F4C81");
                        headerCol.Item().Text(model.Slogan).FontSize(8.5f).FontColor("#475569");
                        headerCol.Item().PaddingTop(2).Text(model.StationAddress).FontSize(7.5f).FontColor("#334155");
                    });

                    row.ConstantItem(150).Column(barCol =>
                    {
                        barCol.Item().Height(28).Image(barcodeBytes).FitArea();
                        barCol.Item().AlignCenter().Text($"Mã vé: {model.TicketCode}").FontSize(8).Bold().FontColor("#0F172A");
                    });
                });

                // Divider line
                column.Item().LineHorizontal(1).LineColor("#CBD5E1");

                // 2. Title & Order Badge
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(titleCol =>
                    {
                        titleCol.Item().Text("VÉ XE KHÁCH").FontSize(20).Bold().FontColor("#0F4C81");
                        titleCol.Item().Text("(Vé hành khách liên tỉnh)").FontSize(9.5f).FontColor("#334155");
                    });

                    row.AutoItem().Background("#E0EDF8").PaddingVertical(6).PaddingHorizontal(14).Column(badgeCol =>
                    {
                        badgeCol.Item().AlignCenter().Text("Mã đơn").FontSize(8).FontColor("#475569");
                        badgeCol.Item().AlignCenter().Text(model.OrderCode).FontSize(10.5f).Bold().FontColor("#0F172A");
                    });
                });

                // 3. Quick Trip Info Strip (3 columns)
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Ngày đi").FontSize(8.5f).FontColor("#64748B");
                        col.Item().Text(model.DepartureDateFormatted).FontSize(10).Bold().FontColor("#0F172A");
                    });

                    row.ConstantItem(75).Column(col =>
                    {
                        col.Item().Text("Giờ đi").FontSize(8.5f).FontColor("#64748B");
                        col.Item().Text(model.DepartureTimeFormatted).FontSize(12).Bold().FontColor("#0F172A");
                    });

                    row.ConstantItem(85).Column(col =>
                    {
                        col.Item().Text("Ghế").FontSize(8.5f).FontColor("#64748B");
                        col.Item().Text(model.SeatNumbers).FontSize(12).Bold().FontColor("#0F172A");
                    });
                });

                // 4. Section 1: Passenger Information
                column.Item().Background("#F0F4F9").Padding(10).Column(box =>
                {
                    box.Spacing(5);
                    box.Item().Text("THÔNG TIN HÀNH KHÁCH").FontSize(9.5f).Bold().FontColor("#0F4C81");
                    box.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Họ tên").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.PassengerName).FontSize(10).Bold().FontColor("#0F172A");
                            c.Item().PaddingTop(3).Text("SĐT").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.PassengerPhone).FontSize(10).Bold().FontColor("#0F172A");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Loại khách").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.PassengerType).FontSize(10).Bold().FontColor("#0F172A");
                            c.Item().PaddingTop(3).Text("Số vé").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.SeatCount.ToString()).FontSize(10).Bold().FontColor("#0F172A");
                        });
                    });
                });

                // 5. Center QR Code
                column.Item().AlignCenter().Column(qrCol =>
                {
                    qrCol.Item().AlignCenter().Width(115).Height(115).Image(qrBytes).FitArea();
                    qrCol.Item().AlignCenter().PaddingTop(3).Text("Quét mã QR để kiểm tra vé hoặc lên xe").FontSize(8.5f).FontColor("#334155");
                });

                // 6. Section 2: Bus Operator Information
                column.Item().Background("#F0F4F9").Padding(10).Column(box =>
                {
                    box.Spacing(5);
                    box.Item().Text("THÔNG TIN NHÀ XE").FontSize(9.5f).Bold().FontColor("#0F4C81");
                    box.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Nhà xe").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.BusCompanyName).FontSize(10).Bold().FontColor("#0F172A");
                            c.Item().PaddingTop(3).Text("SĐT nhà xe").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.BusCompanyPhone).FontSize(10).Bold().FontColor("#0F172A");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Biển số").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.LicensePlate).FontSize(10).Bold().FontColor("#0F172A");
                            c.Item().PaddingTop(3).Text("Tài xế").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.DriverName).FontSize(10).Bold().FontColor("#0F172A");
                        });
                    });
                });

                // 7. Section 3: Route
                column.Item().Column(routeCol =>
                {
                    routeCol.Item().Text("TUYẾN ĐƯỜNG").FontSize(9.5f).Bold().FontColor("#0F4C81");
                    routeCol.Item().PaddingTop(1).Text($"{model.RouteOrigin}  →  {model.RouteDestination}").FontSize(14).Bold().FontColor("#0F4C81");
                    routeCol.Item().Text($"Bến đi: {model.OriginStation}   |   Bến đến: {model.DestinationStation}").FontSize(8).FontColor("#475569");
                    routeCol.Item().PaddingTop(5).LineHorizontal(1).LineColor("#E2E8F0");
                });

                // 8. Section 4: Payment Breakdown
                column.Item().Column(payCol =>
                {
                    payCol.Item().Text("THÔNG TIN THANH TOÁN").FontSize(9.5f).Bold().FontColor("#0F4C81");
                    payCol.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem(1.3f).Column(c =>
                        {
                            c.Item().Row(r => { r.RelativeItem().Text("Giá vé").FontSize(8.5f).FontColor("#334155"); r.AutoItem().Text(model.BaseFare.ToString("#,##0", VnCulture) + "đ").FontSize(8.5f).FontColor("#334155"); });
                            c.Item().Row(r => { r.RelativeItem().Text("Phí dịch vụ").FontSize(8.5f).FontColor("#334155"); r.AutoItem().Text(model.ServiceFee.ToString("#,##0", VnCulture) + "đ").FontSize(8.5f).FontColor("#334155"); });
                            c.Item().Row(r => { r.RelativeItem().Text("Giảm giá").FontSize(8.5f).FontColor("#334155"); r.AutoItem().Text(model.Discount.ToString("#,##0", VnCulture) + "đ").FontSize(8.5f).FontColor("#334155"); });
                            c.Item().PaddingTop(5).Text("Phương thức thanh toán").FontSize(7.5f).FontColor("#64748B");
                            c.Item().Text(model.PaymentMethod).FontSize(9.5f).Bold().FontColor("#0F172A");
                        });

                        row.ConstantItem(12).AlignCenter().LineVertical(1).LineColor("#E2E8F0");

                        row.RelativeItem(1.0f).Column(c =>
                        {
                            c.Item().Row(r => { r.RelativeItem().Text("Hoàn tiền").FontSize(8.5f).FontColor("#334155"); r.AutoItem().Text(model.RefundAmount.ToString("#,##0", VnCulture) + "đ").FontSize(8.5f).FontColor("#334155"); });
                            c.Item().PaddingTop(10).Text("Tổng cộng").FontSize(10).Bold().FontColor("#0F4C81");
                            c.Item().Text(model.TotalPrice.ToString("#,##0", VnCulture) + "đ").FontSize(17).Bold().FontColor("#0F4C81");
                        });
                    });
                });

                // 9. Footer
                column.Item().LineHorizontal(1).LineColor("#CBD5E1");
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Cảm ơn quý khách!").FontSize(9.5f).Bold().Italic().FontColor("#0F4C81");
                        c.Item().Text("Chúc quý khách thượng lộ bình an!").FontSize(8.5f).Italic().FontColor("#475569");
                    });

                    row.AutoItem().Column(c =>
                    {
                        c.Item().AlignRight().Text($"In lúc: {model.IssuedAt}").FontSize(7.5f).FontColor("#64748B");
                        c.Item().AlignRight().Text($"NV: {model.StaffCode}").FontSize(7.5f).FontColor("#64748B");
                    });
                });
            });
        })).GeneratePdf(filePath);
    }

    public static FrameworkElement BuildTicketVisual(TicketPrintModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var outerContainer = new Border
        {
            Width = 540,
            Background = Brushes.White,
            BorderBrush = BorderLineBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(24, 18, 24, 18),
            CornerRadius = new CornerRadius(4)
        };

        var root = new StackPanel();

        // 1. Header: Station on Left, Barcode on Right
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerLeft = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Bus, NavyBrush, 24));
        var brandStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        brandStack.Children.Add(new TextBlock { Text = model.StationName, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = NavyBrush });
        brandStack.Children.Add(new TextBlock { Text = model.Slogan, FontSize = 10, Foreground = SlateMutedBrush, Margin = new Thickness(0, 1, 0, 0) });
        titleRow.Children.Add(brandStack);
        headerLeft.Children.Add(titleRow);

        var addressRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        addressRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Pin, NavyBrush, 12));
        addressRow.Children.Add(new TextBlock { Text = model.StationAddress, FontSize = 9, Foreground = SlateBodyBrush, Margin = new Thickness(5, 0, 0, 0) });
        headerLeft.Children.Add(addressRow);
        Grid.SetColumn(headerLeft, 0);
        headerGrid.Children.Add(headerLeft);

        var headerRight = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        headerRight.Children.Add(new System.Windows.Controls.Image
        {
            Source = Barcode128.GenerateBitmap(model.TicketCode, height: 34, moduleWidth: 1),
            Width = 155,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        headerRight.Children.Add(new TextBlock
        {
            Text = $"Mã vé: {model.TicketCode}",
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = SlateDarkBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(headerRight, 1);
        headerGrid.Children.Add(headerRight);

        root.Children.Add(headerGrid);

        // Separator line
        root.Children.Add(new Border { Height = 1, Background = BorderLineBrush, Margin = new Thickness(0, 10, 0, 10) });

        // 2. Title & Order Code Badge
        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(new TextBlock { Text = "VÉ XE KHÁCH", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = NavyBrush });
        titleBlock.Children.Add(new TextBlock { Text = "(Vé hành khách liên tỉnh)", FontSize = 11, Foreground = SlateBodyBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(titleBlock, 0);
        titleGrid.Children.Add(titleBlock);

        var badgeBorder = new Border
        {
            Background = BadgeBgBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 6, 14, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        badgeStack.Children.Add(new TextBlock { Text = "Mã đơn", FontSize = 9, Foreground = SlateMutedBrush, HorizontalAlignment = HorizontalAlignment.Center });
        badgeStack.Children.Add(new TextBlock { Text = model.OrderCode, FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, HorizontalAlignment = HorizontalAlignment.Center });
        badgeBorder.Child = badgeStack;
        Grid.SetColumn(badgeBorder, 1);
        titleGrid.Children.Add(badgeBorder);

        root.Children.Add(titleGrid);

        // 3. Quick Trip Info Strip (3 columns)
        var tripStrip = new Grid { Margin = new Thickness(0, 10, 0, 10) };
        tripStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        tripStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
        tripStrip.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });

        // Date col
        var dateCol = new StackPanel();
        var dateHeader = new StackPanel { Orientation = Orientation.Horizontal };
        dateHeader.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Calendar, NavyBrush, 13));
        dateHeader.Children.Add(new TextBlock { Text = "Ngày đi", FontSize = 9.5, Foreground = SlateMutedBrush, Margin = new Thickness(5, 0, 0, 0) });
        dateCol.Children.Add(dateHeader);
        dateCol.Children.Add(new TextBlock { Text = model.DepartureDateFormatted, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(dateCol, 0);
        tripStrip.Children.Add(dateCol);

        // Time col
        var timeCol = new StackPanel();
        var timeHeader = new StackPanel { Orientation = Orientation.Horizontal };
        timeHeader.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Clock, NavyBrush, 13));
        timeHeader.Children.Add(new TextBlock { Text = "Giờ đi", FontSize = 9.5, Foreground = SlateMutedBrush, Margin = new Thickness(5, 0, 0, 0) });
        timeCol.Children.Add(timeHeader);
        timeCol.Children.Add(new TextBlock { Text = model.DepartureTimeFormatted, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(timeCol, 1);
        tripStrip.Children.Add(timeCol);

        // Seat col
        var seatCol = new StackPanel();
        var seatHeader = new StackPanel { Orientation = Orientation.Horizontal };
        seatHeader.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Seat, NavyBrush, 13));
        seatHeader.Children.Add(new TextBlock { Text = "Ghế", FontSize = 9.5, Foreground = SlateMutedBrush, Margin = new Thickness(5, 0, 0, 0) });
        seatCol.Children.Add(seatHeader);
        seatCol.Children.Add(new TextBlock { Text = model.SeatNumbers, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(seatCol, 2);
        tripStrip.Children.Add(seatCol);

        root.Children.Add(tripStrip);

        // 4. Section 1: Passenger Information
        var passCard = new Border
        {
            Background = CardBgBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var passStack = new StackPanel();
        var passTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        passTitle.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.User, NavyBrush, 13));
        passTitle.Children.Add(new TextBlock { Text = "THÔNG TIN HÀNH KHÁCH", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = NavyBrush, Margin = new Thickness(6, 0, 0, 0) });
        passStack.Children.Add(passTitle);

        var passGrid = new Grid();
        passGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        passGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var passLeft = new StackPanel();
        passLeft.Children.Add(new TextBlock { Text = "Họ tên", FontSize = 9, Foreground = SlateMutedBrush });
        passLeft.Children.Add(new TextBlock { Text = model.PassengerName, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 6) });
        var phoneRow = new StackPanel { Orientation = Orientation.Horizontal };
        phoneRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Phone, SlateMutedBrush, 11));
        phoneRow.Children.Add(new TextBlock { Text = "SĐT", FontSize = 9, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        passLeft.Children.Add(phoneRow);
        passLeft.Children.Add(new TextBlock { Text = model.PassengerPhone, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(passLeft, 0);
        passGrid.Children.Add(passLeft);

        var passRight = new StackPanel();
        var typeRow = new StackPanel { Orientation = Orientation.Horizontal };
        typeRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.User, SlateMutedBrush, 11));
        typeRow.Children.Add(new TextBlock { Text = "Loại khách", FontSize = 9, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        passRight.Children.Add(typeRow);
        passRight.Children.Add(new TextBlock { Text = model.PassengerType, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 6) });
        var countRow = new StackPanel { Orientation = Orientation.Horizontal };
        countRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Ticket, SlateMutedBrush, 11));
        countRow.Children.Add(new TextBlock { Text = "Số vé", FontSize = 9, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        passRight.Children.Add(countRow);
        passRight.Children.Add(new TextBlock { Text = model.SeatCount.ToString(), FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(passRight, 1);
        passGrid.Children.Add(passRight);

        passStack.Children.Add(passGrid);
        passCard.Child = passStack;
        root.Children.Add(passCard);

        // 5. Center QR Code
        var qrPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 8) };
        qrPanel.Children.Add(new System.Windows.Controls.Image
        {
            Source = GenerateQrCodeImage(model.TicketCode),
            Width = 140,
            Height = 140,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        qrPanel.Children.Add(new TextBlock
        {
            Text = "Quét mã QR để kiểm tra vé\nhoặc lên xe",
            FontSize = 9.5,
            FontWeight = FontWeights.Medium,
            Foreground = SlateBodyBrush,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(qrPanel);

        // 6. Section 2: Bus Operator Information
        var busCard = new Border
        {
            Background = CardBgBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var busStack = new StackPanel();
        var busTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        busTitle.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Bus, NavyBrush, 13));
        busTitle.Children.Add(new TextBlock { Text = "THÔNG TIN NHÀ XE", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = NavyBrush, Margin = new Thickness(6, 0, 0, 0) });
        busStack.Children.Add(busTitle);

        var busGrid = new Grid();
        busGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        busGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var busLeft = new StackPanel();
        busLeft.Children.Add(new TextBlock { Text = "Nhà xe", FontSize = 9, Foreground = SlateMutedBrush });
        busLeft.Children.Add(new TextBlock { Text = model.BusCompanyName, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 6) });
        var busPhoneRow = new StackPanel { Orientation = Orientation.Horizontal };
        busPhoneRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Phone, SlateMutedBrush, 11));
        busPhoneRow.Children.Add(new TextBlock { Text = "SĐT nhà xe", FontSize = 9, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        busLeft.Children.Add(busPhoneRow);
        busLeft.Children.Add(new TextBlock { Text = model.BusCompanyPhone, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(busLeft, 0);
        busGrid.Children.Add(busLeft);

        var busRight = new StackPanel();
        var plateRow = new StackPanel { Orientation = Orientation.Horizontal };
        plateRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.BusFront, SlateMutedBrush, 11));
        plateRow.Children.Add(new TextBlock { Text = "Biển số", FontSize = 9, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        busRight.Children.Add(plateRow);
        busRight.Children.Add(new TextBlock { Text = model.LicensePlate, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 6) });
        var driverRow = new StackPanel { Orientation = Orientation.Horizontal };
        driverRow.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Driver, SlateMutedBrush, 11));
        driverRow.Children.Add(new TextBlock { Text = "Tài xế", FontSize = 9, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        busRight.Children.Add(driverRow);
        busRight.Children.Add(new TextBlock { Text = model.DriverName, FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(busRight, 1);
        busGrid.Children.Add(busRight);

        busStack.Children.Add(busGrid);
        busCard.Child = busStack;
        root.Children.Add(busCard);

        // 7. Section 3: Route
        var routePanel = new StackPanel { Margin = new Thickness(0, 2, 0, 8) };
        var routeTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        routeTitle.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Pin, NavyBrush, 12));
        routeTitle.Children.Add(new TextBlock { Text = "TUYẾN ĐƯỜNG", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = NavyBrush, Margin = new Thickness(5, 0, 0, 0) });
        routePanel.Children.Add(routeTitle);

        routePanel.Children.Add(new TextBlock
        {
            Text = $"{model.RouteOrigin}   →   {model.RouteDestination}",
            FontSize = 16.5,
            FontWeight = FontWeights.Bold,
            Foreground = NavyBrush,
            Margin = new Thickness(0, 2, 0, 2)
        });
        routePanel.Children.Add(new TextBlock
        {
            Text = $"Bến đi: {model.OriginStation}   |   Bến đến: {model.DestinationStation}",
            FontSize = 9.5,
            Foreground = SlateBodyBrush
        });
        routePanel.Children.Add(new Border { Height = 1, Background = DividerLineBrush, Margin = new Thickness(0, 8, 0, 0) });
        root.Children.Add(routePanel);

        // 8. Section 4: Payment Breakdown
        var payPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 8) };
        var payTitle = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        payTitle.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Card, NavyBrush, 12));
        payTitle.Children.Add(new TextBlock { Text = "THÔNG TIN THANH TOÁN", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = NavyBrush, Margin = new Thickness(5, 0, 0, 0) });
        payPanel.Children.Add(payTitle);

        var payGrid = new Grid();
        payGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        payGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        payGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

        var payLeft = new StackPanel();
        payLeft.Children.Add(CreatePriceRow("Giá vé", model.BaseFare.ToString("#,##0", VnCulture) + "đ"));
        payLeft.Children.Add(CreatePriceRow("Phí dịch vụ", model.ServiceFee.ToString("#,##0", VnCulture) + "đ"));
        payLeft.Children.Add(CreatePriceRow("Giảm giá", model.Discount.ToString("#,##0", VnCulture) + "đ"));

        var methodBox = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        var methodHeader = new StackPanel { Orientation = Orientation.Horizontal };
        methodHeader.Children.Add(TicketIcons.CreateWpfIcon(TicketIcons.Cash, NavyBrush, 11));
        methodHeader.Children.Add(new TextBlock { Text = "Phương thức thanh toán", FontSize = 8.5, Foreground = SlateMutedBrush, Margin = new Thickness(4, 0, 0, 0) });
        methodBox.Children.Add(methodHeader);
        methodBox.Children.Add(new TextBlock { Text = model.PaymentMethod, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = SlateDarkBrush, Margin = new Thickness(0, 1, 0, 0) });
        payLeft.Children.Add(methodBox);
        Grid.SetColumn(payLeft, 0);
        payGrid.Children.Add(payLeft);

        var vertDivider = new Border { Width = 1, Background = DividerLineBrush, Margin = new Thickness(10, 0, 14, 0) };
        Grid.SetColumn(vertDivider, 1);
        payGrid.Children.Add(vertDivider);

        var payRight = new StackPanel();
        payRight.Children.Add(CreatePriceRow("Hoàn tiền", model.RefundAmount.ToString("#,##0", VnCulture) + "đ"));
        payRight.Children.Add(new TextBlock { Text = "Tổng cộng", FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = NavyBrush, Margin = new Thickness(0, 12, 0, 0) });
        payRight.Children.Add(new TextBlock { Text = model.TotalPrice.ToString("#,##0", VnCulture) + "đ", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = NavyBrush });
        Grid.SetColumn(payRight, 2);
        payGrid.Children.Add(payRight);

        payPanel.Children.Add(payGrid);
        root.Children.Add(payPanel);

        // 9. Footer
        root.Children.Add(new Border { Height = 1, Background = BorderLineBrush, Margin = new Thickness(0, 4, 0, 8) });

        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var footerLeft = new StackPanel();
        footerLeft.Children.Add(new TextBlock { Text = "Cảm ơn quý khách!", FontSize = 10.5, FontStyle = FontStyles.Italic, FontWeight = FontWeights.Bold, Foreground = NavyBrush });
        footerLeft.Children.Add(new TextBlock { Text = "Chúc quý khách thượng lộ bình an!", FontSize = 9, FontStyle = FontStyles.Italic, Foreground = SlateBodyBrush, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(footerLeft, 0);
        footerGrid.Children.Add(footerLeft);

        var footerRight = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        footerRight.Children.Add(new TextBlock { Text = $"In lúc: {model.IssuedAt}", FontSize = 8, Foreground = SlateMutedBrush, HorizontalAlignment = HorizontalAlignment.Right });
        footerRight.Children.Add(new TextBlock { Text = $"NV: {model.StaffCode}", FontSize = 8, Foreground = SlateMutedBrush, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1, 0, 0) });
        Grid.SetColumn(footerRight, 1);
        footerGrid.Children.Add(footerRight);

        root.Children.Add(footerGrid);

        outerContainer.Child = root;

        outerContainer.Measure(new System.Windows.Size(520, double.PositiveInfinity));
        outerContainer.Arrange(new Rect(new System.Windows.Point(0, 0), outerContainer.DesiredSize));
        outerContainer.UpdateLayout();
        return outerContainer;
    }

    private static Grid CreatePriceRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock { Text = label, FontSize = 9, Foreground = SlateBodyBrush };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var valueBlock = new TextBlock { Text = value, FontSize = 9, Foreground = SlateBodyBrush };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);

        return grid;
    }
}
