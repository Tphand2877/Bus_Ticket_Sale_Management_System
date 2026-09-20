using System.Windows;
using System.Windows.Threading;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.Views;
using Serilog;

namespace BusTicketSaleSystem;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LocalizationService.Instance.Initialize();
        LoggerService.LogInfo("=== BusTicketSaleSystem starting ===");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var databaseReady = await DatabaseMigrator.MigrateAsync();
            if (!databaseReady)
            {
                LoggerService.LogError($"Database migration failed: {DatabaseMigrator.LastError}");
                MessageBox.Show(
                    $"The database could not be created or updated.\n\n{DatabaseMigrator.LastError}",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            await DatabaseMigrator.EnsureUpcomingTripsAsync();
            BackgroundJobScheduler.Start();
            VnPayCallbackListener.Start();

            var loginWindow = new LoginWindow();
            MainWindow = loginWindow;
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Application startup failed.", ex);
            MessageBox.Show($"The application could not start.\n\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Keeps the application alive for failures a point-of-sale operator can retry, and
    /// lets everything else terminate. Marking every exception handled meant the app
    /// carried on after arbitrary state corruption (AUD-E-011), and the dialog gave the
    /// user nothing to quote to support (AUD-G-005).
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        LoggerService.LogError($"Unhandled UI dispatcher exception [{correlationId}].", e.Exception);

        if (!IsRecoverable(e.Exception))
        {
            MessageBox.Show(
                $"The application must close after an unexpected error.\n\nReference: {correlationId}\nDetails were written to Logs/app-*.log.",
                "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
            Log.CloseAndFlush();
            return; // not handled: the process terminates rather than continuing in an unknown state
        }

        MessageBox.Show(
            $"A system error occurred.\n\nReference: {correlationId}\nDetails were written to Logs/app-*.log.",
            "System error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>
    /// Transient infrastructure faults leave the application's own state intact; a
    /// programming error or resource exhaustion does not.
    /// </summary>
    private static bool IsRecoverable(Exception exception) => exception is
        Microsoft.Data.SqlClient.SqlException or
        System.IO.IOException or
        TimeoutException or
        OperationCanceledException or
        ExternalServiceException;

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            LoggerService.LogError("Fatal unhandled exception.", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LoggerService.LogError("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        VnPayCallbackListener.Stop();
        BackgroundJobScheduler.Stop();
        LoggerService.LogInfo("=== BusTicketSaleSystem shutting down ===");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
