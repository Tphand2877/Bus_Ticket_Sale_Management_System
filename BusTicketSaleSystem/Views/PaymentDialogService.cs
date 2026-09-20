using System.Windows;
using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.Views;

/// <summary>
/// The WPF half of <see cref="IPaymentDialogService"/>: owns <see cref="PaymentWindow"/>
/// so no ViewModel has to.
/// </summary>
public sealed class PaymentDialogService : IPaymentDialogService
{
    public Task<PaymentDialogOutcome> ShowAsync(PaymentDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var window = new PaymentWindow(
            [request.TicketCode],
            request.Amount,
            request.TransactionNo,
            request.PaymentUrl,
            request.IsBankTransfer,
            request.HoldExpiresAtUtc)
        {
            Owner = Application.Current?.MainWindow
        };

        window.ShowDialog();

        var outcome = request.IsBankTransfer
            ? window.DialogResult == true ? PaymentDialogOutcome.TransferSubmitted : PaymentDialogOutcome.NotCompleted
            : window.PaymentConfirmed ? PaymentDialogOutcome.GatewayConfirmed : PaymentDialogOutcome.NotCompleted;

        return Task.FromResult(outcome);
    }
}
