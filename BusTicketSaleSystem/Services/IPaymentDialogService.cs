namespace BusTicketSaleSystem.Services;

/// <summary>What the customer did in the payment dialog.</summary>
public enum PaymentDialogOutcome
{
    /// <summary>The customer dismissed the dialog without completing the payment.</summary>
    NotCompleted,

    /// <summary>The customer declared the bank transfer as sent; an operator must confirm it.</summary>
    TransferSubmitted,

    /// <summary>The gateway reported the payment settled while the dialog was open.</summary>
    GatewayConfirmed
}

/// <summary>Everything the dialog needs; no WPF type crosses this boundary.</summary>
public sealed record PaymentDialogRequest(
    string TicketCode,
    decimal Amount,
    string TransactionNo,
    string? PaymentUrl,
    bool IsBankTransfer,
    DateTime HoldExpiresAtUtc);

/// <summary>
/// Presents the payment dialog on behalf of a ViewModel (AUD-A-001).
/// </summary>
/// <remarks>
/// <c>BookingConfirmationViewModel</c> used to construct <c>PaymentWindow</c> and call
/// <c>ShowDialog()</c> itself, so the revenue path could not run without a live WPF
/// <c>Application</c> and could not be tested headlessly.
/// </remarks>
public interface IPaymentDialogService
{
    Task<PaymentDialogOutcome> ShowAsync(PaymentDialogRequest request);
}
