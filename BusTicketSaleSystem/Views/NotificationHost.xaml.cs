using System.Windows.Controls;
using BusTicketSaleSystem.ViewModels;

namespace BusTicketSaleSystem.Views;

public partial class NotificationHost : UserControl
{
    public NotificationHost()
    {
        InitializeComponent();
        DataContext = new NotificationHostViewModel();
        // The host subscribes to a process-lifetime singleton, so every shell it is
        // placed in must release it or each login cycle roots another one (AUD-E-003).
        Unloaded += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
