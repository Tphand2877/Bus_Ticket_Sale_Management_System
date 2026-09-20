using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace BusTicketSaleSystem.ViewModels;

public sealed class TicketViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public TicketDetails Ticket { get; }
    public BitmapImage QrCodeImage { get; }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    /// <summary>Address the PDF will be sent to; empty when the account has no e-mail on file.</summary>
    public string RecipientEmail => CurrentUser.Account?.Email ?? string.Empty;

    public bool CanEmailTicket => !IsBusy && ExternalApiServices.IsEmailConfigured && !string.IsNullOrWhiteSpace(RecipientEmail);

    /// <summary>English explanation shown next to a disabled e-mail button.</summary>
    public string EmailAvailabilityNote =>
        !ExternalApiServices.IsEmailConfigured
            ? "E-mail delivery is not configured on this installation (BREVO_API_KEY / EMAIL_SENDER_ADDRESS are missing)."
            : string.IsNullOrWhiteSpace(RecipientEmail)
                ? "Add an e-mail address to your profile to have tickets delivered by e-mail."
                : $"The PDF ticket will be sent to {RecipientEmail}.";

    /// <summary>Headline describing the ticket's real state, never a blanket "confirmed".</summary>
    public string StateHeadline => Ticket.Status switch
    {
        TicketStatuses.Paid => "Ticket paid and confirmed",
        TicketStatuses.Reserved => "Seat reserved - payment outstanding",
        TicketStatuses.Used => "Ticket already used",
        TicketStatuses.Cancelled => "Ticket cancelled",
        _ => "Ticket issued"
    };

    public string StateDetail => Ticket.Status switch
    {
        TicketStatuses.Paid => "Present this boarding pass at the bus platform.",
        TicketStatuses.Reserved => "Complete the payment before the hold expires, otherwise the seat is released.",
        TicketStatuses.Used => "This ticket was checked in for boarding and can no longer be used.",
        TicketStatuses.Cancelled => "This ticket is no longer valid for travel.",
        _ => "Present this boarding pass at the bus platform."
    };

    /// <summary>
    /// Glyph matching the ticket state. Paired with the status-derived colours so state is not
    /// signalled by colour alone.
    /// </summary>
    public string StateGlyph => Ticket.Status switch
    {
        TicketStatuses.Paid => "✓",
        TicketStatuses.Reserved => "!",
        TicketStatuses.Used => "✓",
        TicketStatuses.Cancelled => "✕",
        _ => "i"
    };

    public ICommand BackToOverviewCommand { get; }
    public ICommand BackToMyTicketsCommand { get; }
    public ICommand PrintTicketCommand { get; }
    public ICommand ExportPngCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand EmailTicketCommand { get; }
    public ICommand ViewRouteOnMapCommand { get; }

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
        EmailTicketCommand = new AsyncRelayCommand(async () => await EmailTicketAsync(), () => CanEmailTicket);
        ViewRouteOnMapCommand = new RelayCommand(ViewRouteOnMap);
    }

    private TicketPrintModel GetPrintModel() => TicketPrintModel.FromTicket(Ticket);

    private void ClearMessages()
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
    }

    private void PrintTicket()
    {
        ClearMessages();
        try
        {
            TicketPrintService.PrintTicket(GetPrintModel());
            StatusMessage = "The ticket was sent to the printer.";
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Printing ticket {Ticket.TicketCode} failed.", ex);
            ErrorMessage = $"The ticket could not be printed: {ex.Message}";
        }
    }

    private void ExportPng()
    {
        ClearMessages();
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
            StatusMessage = $"Ticket image saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Exporting ticket {Ticket.TicketCode} to PNG failed.", ex);
            ErrorMessage = $"The PNG could not be saved: {ex.Message}";
        }
    }

    private void ExportPdf()
    {
        ClearMessages();
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
            StatusMessage = $"Ticket PDF saved to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Exporting ticket {Ticket.TicketCode} to PDF failed.", ex);
            ErrorMessage = $"The PDF could not be saved: {ex.Message}";
        }
    }

    private async Task EmailTicketAsync()
    {
        ClearMessages();
        var recipient = RecipientEmail;
        if (!CanEmailTicket)
        {
            ErrorMessage = EmailAvailabilityNote;
            return;
        }

        IsBusy = true;
        OnPropertyChanged(nameof(CanEmailTicket));
        StatusMessage = $"Sending the ticket to {recipient}...";
        try
        {
            await ExternalApiServices.SendTicketEmailAsync(Ticket, recipient);
            StatusMessage = $"Ticket {Ticket.TicketCode} was e-mailed to {recipient}.";
        }
        catch (ExternalServiceException ex)
        {
            LoggerService.LogError($"E-mailing ticket {Ticket.TicketCode} to {recipient} failed.", ex);
            StatusMessage = string.Empty;
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"E-mailing ticket {Ticket.TicketCode} to {recipient} failed unexpectedly.", ex);
            StatusMessage = string.Empty;
            ErrorMessage = "The ticket could not be e-mailed. Please try again later.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanEmailTicket));
        }
    }

    private void ViewRouteOnMap()
    {
        ClearMessages();
        try
        {
            ExternalApiServices.OpenRouteMap(Ticket.Origin, Ticket.Destination);
            StatusMessage = "The route was opened in your browser.";
        }
        catch (ExternalServiceException ex)
        {
            LoggerService.LogError($"Opening the route map for ticket {Ticket.TicketCode} failed.", ex);
            ErrorMessage = ex.Message;
        }
    }
}
