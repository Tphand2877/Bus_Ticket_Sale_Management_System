namespace BusTicketSaleSystem.Services;

/// <summary>
/// Runs the recurring housekeeping the booking flow depends on: expiring abandoned seat holds and
/// closing out tickets whose coach has already left. One pass per minute, never overlapping.
/// </summary>
/// <remarks>
/// The loop runs on the thread pool rather than on a <c>DispatcherTimer</c>: this is pure
/// database work, and scheduling it on the UI dispatcher made its continuations compete with
/// rendering every minute. The previous <c>async void</c> tick also let any exception type
/// outside the two it caught escape to the dispatcher handler (AUD-E-006).
/// </remarks>
public static class BackgroundJobScheduler
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static CancellationTokenSource? _cancellation;
    private static Task? _loop;

    public static void Start()
    {
        if (_cancellation is not null) return;
        _cancellation = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cancellation.Token));
        LoggerService.LogInfo($"Background job scheduler started (every {Interval.TotalSeconds:F0}s).");
    }

    public static void Stop()
    {
        var cancellation = _cancellation;
        if (cancellation is null) return;
        _cancellation = null;

        cancellation.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex)
        {
            LoggerService.LogWarning($"Background job scheduler ended with: {ex.GetBaseException().Message}");
        }
        finally
        {
            cancellation.Dispose();
            _loop = null;
            LoggerService.LogInfo("Background job scheduler stopped.");
        }
    }

    private static async Task RunAsync(CancellationToken token)
    {
        // One immediate pass, then one per interval. PeriodicTimer cannot overlap
        // passes by construction, so no re-entrancy guard is needed.
        await RunPassAsync();

        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                await RunPassAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private static async Task RunPassAsync()
    {
        try
        {
            var expired = await PaymentService.ExpirePendingPaymentsAsync();
            if (expired > 0) LoggerService.LogInfo($"Background maintenance expired {expired} abandoned seat hold(s).");

            var used = await TicketLifecycleService.MarkDepartedTicketsUsedAsync();
            if (used > 0) LoggerService.LogInfo($"Background maintenance marked {used} departed ticket(s) as used.");
        }
        catch (Exception ex)
        {
            // Caught at the loop boundary: a failed pass must never end the schedule,
            // and no exception type may escape into the dispatcher from here.
            LoggerService.LogError("Background maintenance pass failed.", ex);
        }
    }
}
