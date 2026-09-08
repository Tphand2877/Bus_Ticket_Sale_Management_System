using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    // TODO: teammate will replace this with real logic
    /// <summary>
    /// Placeholder ViewModel for Ticket View (E-Ticket boarding pass details & QR/Barcode).
    /// Out of UI scope — to be implemented by teammate in Phase 2.
    /// </summary>
    public class TicketViewModel : ViewModelBase
    {
        private readonly INavigationService? _navigationService;

        public string Title { get; } = "E-Ticket Boarding Pass Details";
        public string Note { get; } = "This section is assigned to teammate: Full boarding pass rendering, QR code, and print/download.";

        public ICommand? BackToOverviewCommand { get; }
        public ICommand? BackToMyTicketsCommand { get; }

        public TicketViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            BackToOverviewCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<HomeViewModel>();
            });

            BackToMyTicketsCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<MyTicketsViewModel>();
            });
        }

        public TicketViewModel()
        {
        }
    }
}
