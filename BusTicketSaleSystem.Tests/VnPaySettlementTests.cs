using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Services;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Settlement behaviour of the VNPay return handler against the real ledger.
/// Callbacks are signed locally with the configured hash secret, so these tests
/// exercise the production verification path end to end without the gateway.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class VnPaySettlementTests : IDisposable
{
    private const string TmnCode = "TESTTMN";
    private const string HashSecret = "integration-test-hash-secret";

    private readonly SqlServerFixture _fixture;

    public VnPaySettlementTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
        // The production code reads these on every access, so configuring the
        // gateway for the duration of one test needs no seam.
        Environment.SetEnvironmentVariable("VNPAY_TMN_CODE", TmnCode);
        Environment.SetEnvironmentVariable("VNPAY_HASH_SECRET", HashSecret);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("VNPAY_TMN_CODE", null);
        Environment.SetEnvironmentVariable("VNPAY_HASH_SECRET", null);
    }

    [SkippableFact]
    public async Task VerifyVnPayCallbackAsync_SettlesWhenBothStatusFieldsReportSuccess()
    {
        _fixture.SkipIfUnavailable();
        var (ticketId, transactionNo, amount) = await CreatePendingVnPayPaymentAsync();

        var result = await PaymentService.VerifyVnPayCallbackAsync(SignedCallback(transactionNo, amount, "00", "00"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Success", await PaymentStatusAsync(transactionNo));
        Assert.Equal("Đã thanh toán", await TicketStatusAsync(ticketId));
    }

    /// <summary>
    /// AUD-D-004: VNPay reports capture with both vnp_ResponseCode and
    /// vnp_TransactionStatus. A response code of 00 with an unsuccessful transaction
    /// status previously settled the ticket anyway.
    /// </summary>
    [SkippableTheory]
    [InlineData("02")]
    [InlineData("01")]
    [InlineData(null)]
    public async Task VerifyVnPayCallbackAsync_DoesNotSettleWhenTransactionStatusIsNotSuccess(string? transactionStatus)
    {
        _fixture.SkipIfUnavailable();
        var (ticketId, transactionNo, amount) = await CreatePendingVnPayPaymentAsync();

        var result = await PaymentService.VerifyVnPayCallbackAsync(SignedCallback(transactionNo, amount, "00", transactionStatus));

        Assert.False(result.IsSuccess);
        Assert.NotEqual("Success", await PaymentStatusAsync(transactionNo));
        Assert.NotEqual("Đã thanh toán", await TicketStatusAsync(ticketId));
    }

    /// <summary>
    /// AUD-C-004: a decline must release the seat, because the UI tells the customer
    /// it was released and the payable guard otherwise blocks a retry.
    /// </summary>
    [SkippableFact]
    public async Task VerifyVnPayCallbackAsync_DeclinedPaymentReleasesTheSeat()
    {
        _fixture.SkipIfUnavailable();
        var (ticketId, transactionNo, amount) = await CreatePendingVnPayPaymentAsync();
        var seatId = await SeatOfTicketAsync(ticketId);
        var tripId = await TripOfTicketAsync(ticketId);

        var result = await PaymentService.VerifyVnPayCallbackAsync(SignedCallback(transactionNo, amount, "24", "02"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed", await PaymentStatusAsync(transactionNo));
        Assert.Equal("Đã hủy", await TicketStatusAsync(ticketId));
        Assert.Equal(0, await _fixture.CountActiveSeatHoldersAsync(tripId, seatId));
    }

    /// <summary>
    /// AUD-C-001: money captured after the hold lapsed must not vanish. The payment
    /// is recorded as settled and queued for an operator refund instead of being left
    /// Pending until the sweep marks it Failed with the customer charged.
    /// </summary>
    [SkippableFact]
    public async Task VerifyVnPayCallbackAsync_LateCaptureIsRecordedAndQueuedForRefund()
    {
        _fixture.SkipIfUnavailable();
        var (ticketId, transactionNo, amount) = await CreatePendingVnPayPaymentAsync();

        // The hold lapses while the customer is still on the gateway.
        await _fixture.ExecuteAsync(
            $"UPDATE dbo.Tickets SET PaymentExpiresAt = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE TicketId = {ticketId};");

        var result = await PaymentService.VerifyVnPayCallbackAsync(SignedCallback(transactionNo, amount, "00", "00"));

        Assert.False(result.IsSuccess);
        Assert.Contains("refund", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("Success", await PaymentStatusAsync(transactionNo));
        Assert.NotNull(await _fixture.ScalarAsync<string>(
            $"SELECT CONVERT(varchar(30), RefundRequestedAt, 126) FROM dbo.Payments WHERE TransactionNo = N'{transactionNo}';"));
        Assert.NotNull(await _fixture.ScalarAsync<string>(
            $"SELECT RawResponse FROM dbo.Payments WHERE TransactionNo = N'{transactionNo}';"));
    }

    private static IReadOnlyDictionary<string, string> SignedCallback(
        string transactionNo, decimal amount, string responseCode, string? transactionStatus)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = TmnCode,
            ["vnp_TransactionNo"] = "99999999",
            ["vnp_TxnRef"] = transactionNo
        };
        if (transactionStatus is not null) fields["vnp_TransactionStatus"] = transactionStatus;

        var signed = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            ["vnp_SecureHash"] = PaymentService.BuildSignature(fields, HashSecret)
        };
        return signed;
    }

    private async Task<(int TicketId, string TransactionNo, decimal Amount)> CreatePendingVnPayPaymentAsync()
    {
        var (tripId, seatIds) = await _fixture.CreateTripAsync();
        var booking = await DatabaseHelper.BookTicketAsync(_fixture.AccountIds[0], tripId, seatIds[0], "VNPay Test", null);
        Assert.True(booking.Success, booking.Error);

        var ticket = booking.Ticket!;
        var payment = await PaymentService.CreatePaymentAsync(new PaymentRequest(ticket.TicketId, ticket.TicketCode, "VNPay"));
        Assert.True(payment.IsSuccess, payment.ErrorMessage);

        var amount = await _fixture.ScalarAsync<decimal>(
            $"SELECT Amount FROM dbo.Payments WHERE TransactionNo = N'{payment.TransactionNo}';");
        return (ticket.TicketId, payment.TransactionNo!, amount);
    }

    private Task<string?> PaymentStatusAsync(string transactionNo) =>
        _fixture.ScalarAsync<string>($"SELECT Status FROM dbo.Payments WHERE TransactionNo = N'{transactionNo}';");

    private Task<string?> TicketStatusAsync(int ticketId) =>
        _fixture.ScalarAsync<string>($"SELECT Status FROM dbo.Tickets WHERE TicketId = {ticketId};");

    private Task<int> SeatOfTicketAsync(int ticketId) =>
        _fixture.ScalarAsync<int>($"SELECT TOP (1) SeatId FROM dbo.TicketSeats WHERE TicketId = {ticketId};");

    private Task<int> TripOfTicketAsync(int ticketId) =>
        _fixture.ScalarAsync<int>($"SELECT TripId FROM dbo.Tickets WHERE TicketId = {ticketId};");
}
