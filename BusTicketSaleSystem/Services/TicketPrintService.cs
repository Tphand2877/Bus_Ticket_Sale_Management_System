using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;
using QuestPDF.Fluent;
using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Services;

public sealed class TicketPrintModel
{
    public string TicketCode { get; init; } = "";
    public string PassengerName { get; init; } = "";
    public string PassengerPhone { get; init; } = "";
    public string RouteName { get; init; } = "";
    public string DepartureTime { get; init; } = "";
    public string ArrivalTime { get; init; } = "";
    public string SeatNumber { get; init; } = "";
    public string BusType { get; init; } = "";
    public string LicensePlate { get; init; } = "";
    public decimal Price { get; init; }
    public string Status { get; init; } = "";
    public DateTime BookingTime { get; init; }

    public static TicketPrintModel FromTicket(TicketDetails ticket) => new()
    {
        TicketCode = ticket.TicketCode,
        PassengerName = ticket.PassengerName,
        PassengerPhone = ticket.PassengerPhone ?? "",
        RouteName = ticket.Route,
        DepartureTime = ticket.DepartureTime.ToString("HH:mm - dd/MM/yyyy"),
        ArrivalTime = ticket.ArrivalTime.ToString("HH:mm - dd/MM/yyyy"),
        SeatNumber = ticket.SeatNumber,
        BusType = ticket.BusType,
        LicensePlate = ticket.LicensePlate,
        Price = ticket.Price,
        Status = ticket.Status,
        BookingTime = ticket.BookingTime
    };
}

public static class TicketPrintService
{
    static TicketPrintService() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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

    public static void PrintTicket(TicketPrintModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        dialog.PrintVisual(BuildTicketVisual(model), $"Vé xe - {model.TicketCode}");
    }

    public static void ExportToPng(TicketPrintModel model, string filePath)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
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
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(model.TicketCode, QRCodeGenerator.ECCLevel.Q);
        var qrBytes = new PngByteQRCode(data).GetGraphic(20);

        Document.Create(container => container.Page(page =>
        {
            page.Size(QuestPDF.Helpers.PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(10));
            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(text =>
                    {
                        text.Item().Text("VÉ XE KHÁCH - BUS TICKET").FontSize(17).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                        text.Item().Text($"Mã vé: {model.TicketCode}").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);
                    });
                    row.AutoItem().Text(model.Status.ToUpperInvariant()).Bold().FontColor(QuestPDF.Helpers.Colors.Green.Darken2);
                });
                column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten1);
                column.Item().Text(model.RouteName).FontSize(15).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    AddPdfField(table, "Hành khách", model.PassengerName);
                    AddPdfField(table, "Điện thoại", model.PassengerPhone);
                    AddPdfField(table, "Khởi hành", model.DepartureTime);
                    AddPdfField(table, "Đến nơi", model.ArrivalTime);
                    AddPdfField(table, "Số ghế", model.SeatNumber);
                    AddPdfField(table, "Biển số xe", model.LicensePlate);
                    AddPdfField(table, "Loại xe", model.BusType);
                    AddPdfField(table, "Giá vé", $"{model.Price:N0} VND");
                });
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(text =>
                    {
                        text.Item().Text("Có mặt tại bến trước giờ khởi hành 15 phút.").Italic().FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                        text.Item().Text($"Phát hành: {model.BookingTime:dd/MM/yyyy HH:mm}").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
                    });
                    row.ConstantItem(90).Image(qrBytes).FitArea();
                });
            });
        })).GeneratePdf(filePath);
    }

    public static FrameworkElement BuildTicketVisual(TicketPrintModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var card = new Border { Width = 550, Padding = new Thickness(25), Background = Brushes.White, BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(1) };
        var grid = new Grid();
        for (var i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "VÉ XE KHÁCH - BUS TICKET", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkBlue });
        title.Children.Add(new TextBlock { Text = $"Mã vé: {model.TicketCode}", FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(title, 0);
        header.Children.Add(title);
        var status = new TextBlock { Text = model.Status.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.Green, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(status, 1);
        header.Children.Add(status);
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var separator = new Border { Height = 1, Background = Brushes.LightGray, Margin = new Thickness(0, 15, 0, 15) };
        Grid.SetRow(separator, 1);
        grid.Children.Add(separator);

        var details = new Grid();
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel();
        AddDetailRow(left, "Hành khách:", model.PassengerName);
        AddDetailRow(left, "Điện thoại:", model.PassengerPhone);
        AddDetailRow(left, "Tuyến đường:", model.RouteName);
        AddDetailRow(left, "Khởi hành:", model.DepartureTime);
        Grid.SetColumn(left, 0);
        details.Children.Add(left);
        var right = new StackPanel { Margin = new Thickness(15, 0, 0, 0) };
        AddDetailRow(right, "Số ghế:", model.SeatNumber, true);
        AddDetailRow(right, "Biển số xe:", model.LicensePlate);
        AddDetailRow(right, "Loại xe:", model.BusType);
        AddDetailRow(right, "Giá vé:", $"{model.Price:N0} VND", true);
        Grid.SetColumn(right, 1);
        details.Children.Add(right);
        Grid.SetRow(details, 2);
        grid.Children.Add(details);

        var footer = new Grid { Margin = new Thickness(0, 20, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var notes = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        notes.Children.Add(new TextBlock { Text = "* Có mặt tại bến trước giờ khởi hành 15 phút.", FontSize = 11, FontStyle = FontStyles.Italic, Foreground = Brushes.Gray });
        notes.Children.Add(new TextBlock { Text = "* Xuất trình mã QR này để soát vé.", FontSize = 11, FontStyle = FontStyles.Italic, Foreground = Brushes.Gray, Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(notes, 0);
        footer.Children.Add(notes);
        var qr = new System.Windows.Controls.Image { Source = GenerateQrCodeImage(model.TicketCode), Width = 90, Height = 90, HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(qr, 1);
        footer.Children.Add(qr);
        Grid.SetRow(footer, 3);
        grid.Children.Add(footer);
        card.Child = grid;
        card.Measure(new System.Windows.Size(550, double.PositiveInfinity));
        card.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), card.DesiredSize));
        card.UpdateLayout();
        return card;
    }

    private static void AddDetailRow(StackPanel parent, string label, string? value, bool highlight = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(new TextBlock { Text = label, Width = 90, FontSize = 12, Foreground = Brushes.Gray });
        row.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(value) ? "-" : value, FontSize = highlight ? 13 : 12, FontWeight = highlight ? FontWeights.Bold : FontWeights.Normal, Foreground = highlight ? Brushes.Firebrick : Brushes.Black, TextWrapping = TextWrapping.Wrap });
        parent.Children.Add(row);
    }

    private static void AddPdfField(QuestPDF.Fluent.TableDescriptor table, string label, string? value)
    {
        table.Cell().PaddingBottom(7).Column(column =>
        {
            column.Item().Text(label).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Darken1);
            column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).Bold();
        });
    }
}
