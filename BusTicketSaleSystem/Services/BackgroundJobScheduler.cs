using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.Services;

/// <summary>
/// Runs the recurring housekeeping the booking flow depends on: expiring abandoned seat holds and
/// closing out tickets whose coach has already left. One pass per minute, never overlapping.
/// </summary>
public static class BackgroundJobScheduler
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private static DispatcherTimer? _timer;
    private static int _passInProgress;

    public static void Start()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Interval };
        _timer.Tick += OnTick;
        _timer.Start();
        LoggerService.LogInfo($"Background job scheduler started (every {Interval.TotalSeconds:F0}s).");
        OnTick(_timer, EventArgs.Empty);
    }

    public static void Stop()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
        LoggerService.LogInfo("Background job scheduler stopped.");
    }

    private static async void OnTick(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref _passInProgress, 1) == 1)
        {
            LoggerService.LogWarning("Background maintenance pass skipped: the previous pass is still running.");
            return;
        }
        try
        {
            var expired = await PaymentService.ExpirePendingPaymentsAsync();
            if (expired > 0) LoggerService.LogInfo($"Background maintenance expired {expired} abandoned payment/ticket row(s).");

            var used = await TicketLifecycleService.MarkDepartedTicketsUsedAsync();
            if (used > 0) LoggerService.LogInfo($"Background maintenance marked {used} departed ticket(s) as used.");
        }
        catch (SqlException ex)
        {
            LoggerService.LogError("Background maintenance pass failed against the database.", ex);
        }
        catch (InvalidOperationException ex)
        {
            LoggerService.LogError("Background maintenance pass failed in an invalid state.", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _passInProgress, 0);
        }
    }
}
