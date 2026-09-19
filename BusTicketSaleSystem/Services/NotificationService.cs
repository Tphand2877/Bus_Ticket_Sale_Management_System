using System;
using System.Windows.Threading;

namespace BusTicketSaleSystem.Services;

public enum NotificationKind
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Application-wide transient messages ("toasts").
/// <para>
/// Per-ViewModel status strings cannot report the outcome of an action that navigates away:
/// the ViewModel holding the message is replaced in the same call, so the text is never rendered.
/// Booking completion did exactly that — the user paid and saw nothing. Toasts are owned by the
/// shell instead, so they outlive the ViewModel that raised them.
/// </para>
/// </summary>
public sealed class NotificationService
{
    private static readonly Lazy<NotificationService> LazyInstance = new(() => new NotificationService());
    public static NotificationService Instance => LazyInstance.Value;

    /// <summary>Errors stay up longer — they usually carry a recovery instruction.</summary>
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ErrorLifetime = TimeSpan.FromSeconds(10);

    private readonly DispatcherTimer _dismissTimer;

    private string _message = string.Empty;
    private NotificationKind _kind = NotificationKind.Info;

    /// <summary>Raised on show and on dismiss; the shell binds to this.</summary>
    public event Action? Changed;

    public string Message => _message;
    public NotificationKind Kind => _kind;
    public bool IsVisible => !string.IsNullOrWhiteSpace(_message);

    private NotificationService()
    {
        _dismissTimer = new DispatcherTimer(DispatcherPriority.Normal);
        _dismissTimer.Tick += (_, _) => Dismiss();
    }

    public void ShowSuccess(string message) => Show(message, NotificationKind.Success);
    public void ShowError(string message) => Show(message, NotificationKind.Error);
    public void ShowWarning(string message) => Show(message, NotificationKind.Warning);
    public void ShowInfo(string message) => Show(message, NotificationKind.Info);

    public void Show(string message, NotificationKind kind, TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        _message = message.Trim();
        _kind = kind;

        // Restart the countdown so a newer toast always gets its full dwell time.
        _dismissTimer.Stop();
        _dismissTimer.Interval = lifetime
            ?? (kind == NotificationKind.Error ? ErrorLifetime : DefaultLifetime);
        _dismissTimer.Start();

        Changed?.Invoke();
    }

    public void Dismiss()
    {
        _dismissTimer.Stop();
        if (!IsVisible) return;

        _message = string.Empty;
        Changed?.Invoke();
    }
}
