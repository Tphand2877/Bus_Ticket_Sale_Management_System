using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;

namespace BusTicketSaleSystem.ViewModels;

/// <summary>
/// Adapts the singleton <see cref="NotificationService"/> to bindings for the shell toast.
/// </summary>
public sealed class NotificationHostViewModel : ViewModelBase, IDisposable
{
    private readonly NotificationService _notifications = NotificationService.Instance;

    public string Message => _notifications.Message;
    public NotificationKind Kind => _notifications.Kind;
    public bool IsVisible => _notifications.IsVisible;

    public ICommand DismissCommand { get; }

    public NotificationHostViewModel()
    {
        DismissCommand = new RelayCommand(() => _notifications.Dismiss());
        _notifications.Changed += OnNotificationChanged;
    }

    private void OnNotificationChanged()
    {
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(IsVisible));
    }

    /// <summary>
    /// Releases the singleton subscription. Each shell creation — every login and
    /// logout cycle — used to add a permanently rooted handler (AUD-E-003).
    /// </summary>
    public void Dispose() => _notifications.Changed -= OnNotificationChanged;
}
