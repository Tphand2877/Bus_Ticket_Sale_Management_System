using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Stands in for the WPF payment dialog so the booking path can be driven without an
/// STA thread or a live Application (AUD-A-001).
/// </summary>
public sealed class FakePaymentDialogService(PaymentDialogOutcome outcome = PaymentDialogOutcome.NotCompleted)
    : IPaymentDialogService
{
    public List<PaymentDialogRequest> Requests { get; } = [];

    public Task<PaymentDialogOutcome> ShowAsync(PaymentDialogRequest request)
    {
        Requests.Add(request);
        return Task.FromResult(outcome);
    }
}
