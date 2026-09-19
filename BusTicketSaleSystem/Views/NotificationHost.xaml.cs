using System.Windows.Controls;
using BusTicketSaleSystem.ViewModels;

namespace BusTicketSaleSystem.Views;

public partial class NotificationHost : UserControl
{
    public NotificationHost()
    {
        InitializeComponent();
        DataContext = new NotificationHostViewModel();
    }
}
