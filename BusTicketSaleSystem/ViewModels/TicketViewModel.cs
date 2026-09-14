using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BusTicketSaleSystem.ViewModels;

public sealed class TicketViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    public TicketDetails Ticket { get; }
    public BitmapImage QrCodeImage { get; }
    public ICommand BackToOverviewCommand { get; }
    public ICommand BackToMyTicketsCommand { get; }
    public ICommand PrintTicketCommand { get; }
    public ICommand ExportPngCommand { get; }
    public ICommand ExportPdfCommand { get; }

    public TicketViewModel(INavigationService navigationService, TicketDetails ticket)
    {
        _navigationService = navigationService;
        Ticket = ticket;
        QrCodeImage = TicketPrintService.GenerateQrCodeImage(ticket.TicketCode);
        BackToOverviewCommand = new RelayCommand(() => _navigationService.NavigateTo<HomeViewModel>());
        BackToMyTicketsCommand = new RelayCommand(() => _navigationService.NavigateTo<MyTicketsViewModel>());
        PrintTicketCommand = new RelayCommand(PrintTicket);
        ExportPngCommand = new RelayCommand(ExportPng);
        ExportPdfCommand = new RelayCommand(ExportPdf);
    }

    private TicketPrintModel GetPrintModel() => TicketPrintModel.FromTicket(Ticket);

    private void PrintTicket()
    {
        try
        {
            TicketPrintService.PrintTicket(GetPrintModel());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể in vé: {ex.Message}", "In vé", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPng()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = $"Ticket_{Ticket.TicketCode}.png"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            TicketPrintService.ExportToPng(GetPrintModel(), dialog.FileName);
            MessageBox.Show($"Đã xuất vé PNG tại:\n{dialog.FileName}", "Xuất vé", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xuất PNG: {ex.Message}", "Xuất vé", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPdf()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF Document (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = $"Ticket_{Ticket.TicketCode}.pdf"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            TicketPrintService.ExportToPdf(GetPrintModel(), dialog.FileName);
            MessageBox.Show($"Đã xuất vé PDF tại:\n{dialog.FileName}", "Xuất vé", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể xuất PDF: {ex.Message}", "Xuất vé", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
