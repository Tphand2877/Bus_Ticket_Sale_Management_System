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
                    $"Không thể khởi tạo hoặc cập nhật cơ sở dữ liệu.\n\n{DatabaseMigrator.LastError}",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var loginWindow = new LoginWindow();
            MainWindow = loginWindow;
            loginWindow.Show();
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Application startup failed.", ex);
            MessageBox.Show($"Không thể khởi động ứng dụng.\n\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LoggerService.LogError("Unhandled UI dispatcher exception.", e.Exception);
        MessageBox.Show("Đã xảy ra lỗi hệ thống. Chi tiết đã được ghi vào Logs/app-*.log.", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

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
        LoggerService.LogInfo("=== BusTicketSaleSystem shutting down ===");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
