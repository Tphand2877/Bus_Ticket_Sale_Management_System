using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.ViewModels;


/// <summary>
/// Settlement console. Every state change is delegated to the payment state machine in
/// <see cref="PaymentService"/> / <see cref="TicketLifecycleService"/>; this ViewModel never
/// writes payment or ticket state itself.
/// </summary>
public sealed class AdminPaymentsViewModel : ViewModelBase, IRefreshable
{
    private string _selectedStatusFilter = "All";
    private string _searchQuery = "";
    private string _providerReference = "";
    private string _actionNote = "";
    private string _rejectReason = "";
    private string _message = "";
    private bool _isLoading;
    private bool _isConfirmingReject;
    private PaymentAdminItem? _selectedPayment;

    public ObservableCollection<PaymentAdminItem> Payments { get; } = new();
    public IReadOnlyList<string> StatusFilters { get; } = new[] { "All", "Pending", "Success", "Failed", "Refunded" };
    public AdminPaymentStats Stats { get; private set; } = AdminPaymentStats.Empty;

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value)) _ = RefreshAsync();
        }
    }

    public string SearchQuery { get => _searchQuery; set => SetProperty(ref _searchQuery, value); }
    public string ProviderReference { get => _providerReference; set => SetProperty(ref _providerReference, value); }
    public string ActionNote { get => _actionNote; set => SetProperty(ref _actionNote, value); }
    public string RejectReason { get => _rejectReason; set => SetProperty(ref _rejectReason, value); }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage));
        }
    }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasPayments => Payments.Count > 0;
    public bool HasNoPayments => !HasPayments && !IsLoading;

    public PaymentAdminItem? SelectedPayment
    {
        get => _selectedPayment;
        private set
        {
            if (SetProperty(ref _selectedPayment, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectionHeadline));
            }
        }
    }
    public bool HasSelection => SelectedPayment is not null;
    public string SelectionHeadline => SelectedPayment is null
        ? ""
        : $"Payment #{SelectedPayment.PaymentId} · ticket {SelectedPayment.TicketCode} · {SelectedPayment.AmountDisplay} · {SelectedPayment.StatusDisplay}";

    public bool IsConfirmingReject
    {
        get => _isConfirmingReject;
        private set => SetProperty(ref _isConfirmingReject, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand SelectPaymentCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ConfirmPaymentCommand { get; }
    public ICommand RequestRejectCommand { get; }
    public ICommand RejectPaymentCommand { get; }
    public ICommand CancelRejectCommand { get; }
    public ICommand ApproveRefundCommand { get; }
    public ICommand CheckInTicketCommand { get; }
    public ICommand ClearMessageCommand { get; }

    public AdminPaymentsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        SearchCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        ClearMessageCommand = new RelayCommand(() => Message = "");
        SelectPaymentCommand = new RelayCommand(p =>
        {
            if (p is not PaymentAdminItem payment) return;
            IsConfirmingReject = false;
            RejectReason = "";
            ProviderReference = payment.ProviderTransactionNo ?? "";
            ActionNote = "";
            Message = "";
            SelectedPayment = payment;
        });
        ClearSelectionCommand = new RelayCommand(() =>
        {
            SelectedPayment = null;
            IsConfirmingReject = false;
            ProviderReference = ActionNote = RejectReason = "";
        });
        ConfirmPaymentCommand = new RelayCommand(
            async () => await ConfirmAsync(),
            () => SelectedPayment?.CanConfirm == true);
        RequestRejectCommand = new RelayCommand(
            () => IsConfirmingReject = true,
            () => SelectedPayment?.CanReject == true);
        CancelRejectCommand = new RelayCommand(() =>
        {
            IsConfirmingReject = false;
            RejectReason = "";
        });
        RejectPaymentCommand = new RelayCommand(
            async () => await RejectAsync(),
            () => IsConfirmingReject && SelectedPayment?.CanReject == true);
        ApproveRefundCommand = new RelayCommand(
            async () => await ApproveRefundAsync(),
            () => SelectedPayment?.CanApproveRefund == true);
        CheckInTicketCommand = new RelayCommand(
            async () => await CheckInAsync(),
            () => SelectedPayment?.CanCheckIn == true);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var filter = SelectedStatusFilter == "All" ? null : SelectedStatusFilter;
            var rows = await AdminDatabaseService.GetPaymentsAsync(filter, SearchQuery);
            var previouslySelected = SelectedPayment?.PaymentId;

            Payments.Clear();
            foreach (var row in rows) Payments.Add(row);
            Stats = await AdminDatabaseService.GetPaymentStatsAsync();
            OnPropertyChanged(nameof(Stats));

            SelectedPayment = previouslySelected is null
                ? null
                : Payments.FirstOrDefault(p => p.PaymentId == previouslySelected.Value);
            if (SelectedPayment is null) IsConfirmingReject = false;
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Loading payments");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasPayments));
            OnPropertyChanged(nameof(HasNoPayments));
        }
    }

    private static bool TryGetAdminAccountId(out int accountId, out string? error)
    {
        accountId = CurrentUser.Account?.AccountId ?? 0;
        if (accountId > 0)
        {
            error = null;
            return true;
        }
        error = "Your administrator session has expired. Please sign in again.";
        return false;
    }

    private async Task ConfirmAsync()
    {
        var payment = SelectedPayment;
        if (payment is null || !payment.CanConfirm) return;
        if (!TryGetAdminAccountId(out var adminId, out var sessionError))
        {
            Message = sessionError!;
            return;
        }
        var reference = string.IsNullOrWhiteSpace(ProviderReference) ? null : ProviderReference.Trim();
        var note = string.IsNullOrWhiteSpace(ActionNote) ? null : ActionNote.Trim();
        try
        {
            var result = await PaymentService.ConfirmManualPaymentAsync(payment.PaymentId, adminId, reference, note);
            Message = result.Error ?? $"Payment #{payment.PaymentId} settled; ticket {payment.TicketCode} is now paid.";
            if (result.Success)
            {
                ProviderReference = ActionNote = "";
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Confirming the payment");
        }
    }

    private async Task RejectAsync()
    {
        var payment = SelectedPayment;
        if (payment is null || !payment.CanReject) return;
        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            Message = "Enter a reason before rejecting the payment.";
            return;
        }
        if (!TryGetAdminAccountId(out var adminId, out var sessionError))
        {
            Message = sessionError!;
            return;
        }
        try
        {
            var result = await PaymentService.RejectManualPaymentAsync(payment.PaymentId, adminId, RejectReason.Trim());
            Message = result.Error ?? $"Payment #{payment.PaymentId} rejected; ticket {payment.TicketCode} was cancelled.";
            if (result.Success)
            {
                IsConfirmingReject = false;
                RejectReason = "";
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Rejecting the payment");
        }
    }

    private async Task ApproveRefundAsync()
    {
        var payment = SelectedPayment;
        if (payment is null || !payment.CanApproveRefund) return;
        if (!TryGetAdminAccountId(out var adminId, out var sessionError))
        {
            Message = sessionError!;
            return;
        }
        var note = string.IsNullOrWhiteSpace(ActionNote) ? null : ActionNote.Trim();
        try
        {
            var result = await PaymentService.ApproveRefundAsync(payment.PaymentId, adminId, note);
            Message = result.Error ?? $"Refund approved for payment #{payment.PaymentId} ({payment.AmountDisplay}).";
            if (result.Success)
            {
                ActionNote = "";
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Approving the refund");
        }
    }

    private async Task CheckInAsync()
    {
        var payment = SelectedPayment;
        if (payment is null || !payment.CanCheckIn) return;
        if (!TryGetAdminAccountId(out var adminId, out var sessionError))
        {
            Message = sessionError!;
            return;
        }
        try
        {
            var result = await TicketLifecycleService.CheckInTicketAsync(payment.TicketId, adminId);
            Message = result.Error ?? $"Ticket {payment.TicketCode} checked in for boarding.";
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Checking the passenger in");
        }
    }
}

