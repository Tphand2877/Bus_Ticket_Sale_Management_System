using BusTicketSaleSystem.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace BusTicketSaleSystem.Views
{
    /// <summary>
    /// Interaction logic for SeatSelectionView.xaml. Stops the live seat-map polling as soon as
    /// the view leaves the visual tree.
    /// </summary>
    public partial class SeatSelectionView : UserControl
    {
        public SeatSelectionView()
        {
            InitializeComponent();
        }

        private void SeatSelectionView_OnUnloaded(object sender, RoutedEventArgs e)
        {
            (DataContext as SeatSelectionViewModel)?.Dispose();
        }
    }
}
