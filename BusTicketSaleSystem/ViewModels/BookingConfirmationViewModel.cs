using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    // TODO: teammate will replace this with real logic
    /// <summary>
    /// Placeholder ViewModel for Booking Confirmation screen.
    /// Out of UI scope — to be implemented by teammate in Phase 2.
    /// </summary>
    public class BookingConfirmationViewModel : ViewModelBase
    {
        private readonly INavigationService? _navigationService;

        public string Title { get; } = "Booking Confirmation & Payment";
        public string Note { get; } = "This section is assigned to teammate: Passenger details form, payment method, order summary.";

        public ICommand? CompleteBookingCommand { get; }
        public ICommand? BackToSeatsCommand { get; }

        public BookingConfirmationViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            CompleteBookingCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<TicketViewModel>();
            });

            BackToSeatsCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<SeatSelectionViewModel>();
            });
        }

        public BookingConfirmationViewModel()
        {
        }
    }
}
