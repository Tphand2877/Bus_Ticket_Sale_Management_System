using System.Windows;
using System.Windows.Controls;

namespace BusTicketSaleSystem.Views;

public partial class AdminCheckInView : UserControl
{
    public AdminCheckInView()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtScannerInput.Focus();
    }
}
