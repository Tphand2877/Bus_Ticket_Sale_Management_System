using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class MyTicketsViewModel : ViewModelBase, IRefreshable
{
    private readonly INavigationService _navigationService;
    private string _statusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading;

    public ObservableCollection<TicketItemViewModel> Tickets { get; } = new();
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }


    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                OnPropertyChanged(nameof(HasNoTickets));
        }
    }

    public bool HasNoTickets => !IsLoading && Tickets.Count == 0;

    /// <summary>Which irreversible action is awaiting confirmation.</summary>
    public enum PendingTicketAction
    {
        None,
        Cancel,
        Refund
    }

    private TicketItemViewModel? _pendingTicket;
    private PendingTicketAction _pendingAction = PendingTicketAction.None;

    /// <summary>
    /// Cancelling a seat and requesting a refund both move money and cannot be undone, so they
    /// are staged here and only executed by <see cref="ConfirmPendingActionCommand"/>. Mirrors the
    /// PendingDelete convention already used by the admin screens.
    /// </summary>
    public TicketItemViewModel? PendingTicket
    {
        get => _pendingTicket;
        private set
        {
            if (SetProperty(ref _pendingTicket, value))
            {
                OnPropertyChanged(nameof(IsConfirmingAction));
                OnPropertyChanged(nameof(ConfirmationPrompt));
                OnPropertyChanged(nameof(ConfirmActionLabel));
            }
        }
    }

    public bool IsConfirmingAction => _pendingTicket is not null && _pendingAction != PendingTicketAction.None;

    public string ConfirmationPrompt => (_pendingTicket, _pendingAction) switch
    {
        (null, _) => "",
        ({ } t, PendingTicketAction.Cancel) =>
            LocalizationService.Instance.Format("ConfirmCancelPrompt", t.TicketCode, t.Ticket.SeatNumbers),
        ({ } t, PendingTicketAction.Refund) =>
            LocalizationService.Instance.Format("ConfirmRefundPrompt", t.TicketCode, t.Price),
        _ => ""
    };

    public string ConfirmActionLabel => _pendingAction switch
    {
        PendingTicketAction.Cancel => LocalizationService.Instance.GetString("ConfirmCancelAction"),
        PendingTicketAction.Refund => LocalizationService.Instance.GetString("ConfirmRefundAction"),
        _ => LocalizationService.Instance.GetString("ConfirmGeneric")
    };

    public ICommand ViewDetailsCommand { get; }
    public ICommand CancelTicketCommand { get; }
    public ICommand RequestRefundCommand { get; }
    public ICommand BookNewTicketCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ConfirmPendingActionCommand { get; }
    public ICommand DismissPendingActionCommand { get; }

    public MyTicketsViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        ViewDetailsCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is TicketItemViewModel item)
                    _navigationService.NavigateTo(new TicketViewModel(_navigationService, item.Ticket));
            },
            parameter => parameter is TicketItemViewModel { CanViewETicket: true });

        // These two only *stage* the action; nothing is written until it is confirmed.
        CancelTicketCommand = new RelayCommand(
            parameter => StagePendingAction(parameter, PendingTicketAction.Cancel),
            parameter => parameter is TicketItemViewModel { CanCancel: true });

        RequestRefundCommand = new RelayCommand(
            parameter => StagePendingAction(parameter, PendingTicketAction.Refund),
            parameter => parameter is TicketItemViewModel { CanRequestRefund: true });

        ConfirmPendingActionCommand = new AsyncRelayCommand(
            ExecutePendingActionAsync,
            () => IsConfirmingAction);

        DismissPendingActionCommand = new RelayCommand(ClearPendingAction);

        BookNewTicketCommand = new RelayCommand(() => _navigationService.NavigateTo<BookingViewModel>());
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    private void StagePendingAction(object? parameter, PendingTicketAction action)
    {
        if (parameter is not TicketItemViewModel item) return;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        _pendingAction = action;
        PendingTicket = item;
    }

    private void ClearPendingAction()
    {
        _pendingAction = PendingTicketAction.None;
        PendingTicket = null;
    }

    private async Task ExecutePendingActionAsync()
    {
        var item = _pendingTicket;
        var action = _pendingAction;
        ClearPendingAction();

        if (item is null) return;

        switch (action)
        {
            case PendingTicketAction.Cancel:
                await CancelTicketAsync(item);
                break;
            case PendingTicketAction.Refund:
                await RequestRefundAsync(item);
                break;
        }
    }

    public Task RefreshAsync() => LoadAsync();

    private async Task CancelTicketAsync(TicketItemViewModel item)
    {
        if (CurrentUser.Account is null)
        {
            ErrorMessage = "Your session has expired. Please sign in again.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var result = await DatabaseHelper.CancelTicketAsync(item.Ticket.TicketId, CurrentUser.Account.AccountId);
            if (result.Success)
            {
                // Toast, not StatusMessage: LoadAsync() below re-renders the list and the
                // inline banner would be lost against the refreshed content.
                NotificationService.Instance.ShowSuccess(
                    LocalizationService.Instance.Format("ToastTicketCancelled", item.TicketCode));
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "This ticket could not be cancelled.";
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Cancelling ticket {item.Ticket.TicketId} failed.", ex);
            ErrorMessage = "The cancellation could not be processed. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RequestRefundAsync(TicketItemViewModel item)
    {
        if (CurrentUser.Account is null)
        {
            ErrorMessage = "Your session has expired. Please sign in again.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var result = await DatabaseHelper.RequestRefundAsync(item.Ticket.TicketId, CurrentUser.Account.AccountId);
            if (result.Success)
            {
                NotificationService.Instance.ShowSuccess(
                    LocalizationService.Instance.Format("ToastRefundRequested", item.TicketCode));
                await LoadAsync();
            }
            else
            {
                ErrorMessage = result.Error ?? "This refund request could not be submitted.";
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Requesting a refund for ticket {item.Ticket.TicketId} failed.", ex);
            ErrorMessage = "The refund request could not be submitted. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAsync()
    {
        if (CurrentUser.Account is null)
        {
            ErrorMessage = "Your session has expired. Please sign in again.";
            return;
        }

        IsLoading = true;
        try
        {
            var tickets = await DatabaseHelper.GetTicketsAsync(CurrentUser.Account.AccountId, false);

            Tickets.Clear();
            foreach (var ticket in tickets)
                Tickets.Add(new TicketItemViewModel(ticket));
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Could not load the active ticket list.", ex);
            Tickets.Clear();
            ErrorMessage = "Your tickets could not be loaded from the database.";
        }
        finally
        {
            IsLoading = false;
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
    public string SeatNumbers => Ticket.SeatNumbers;
    public int SeatCount => Ticket.SeatCount;
    public string BusType => string.IsNullOrWhiteSpace(Ticket.BusType) ? "Standard coach" : Ticket.BusType;
    public string Price => $"{Ticket.TotalPrice:N0} VND";

    /// <summary>Raw DB literal; rendered through the TicketStatusToDisplay converter.</summary>
    public string Status => Ticket.Status;

    /// <summary>Raw DB literal; rendered through the PaymentStatusToDisplay converter.</summary>
    public string PaymentStatus => Ticket.PaymentStatus;
    public bool CanViewETicket => Ticket.IsPaid;
    public bool IsPendingPayment => !CanViewETicket;

    /// <summary>An unpaid reservation can be released; a paid ticket must go through the refund flow.</summary>
    public bool CanCancel => Ticket.Status == TicketStatuses.Reserved && Ticket.DepartureTime > DateTime.UtcNow;

    public bool CanRequestRefund => Ticket.CanRequestRefund;

    public TicketItemViewModel(TicketDetails ticket) => Ticket = ticket;
}
