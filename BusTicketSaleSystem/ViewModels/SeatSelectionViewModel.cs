using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    // TODO: teammate will replace this with real logic
    /// <summary>
    /// Placeholder ViewModel for Seat Selection screen.
    /// Out of UI scope — to be implemented by teammate in Phase 2.
    /// </summary>
    public class SeatSelectionViewModel : ViewModelBase
    {
        private readonly INavigationService? _navigationService;

        public string Title { get; } = "Seat Selection";
        public string Note { get; } = "This section is assigned to teammate: Interactive seat map & seat picking.";

        public ICommand? GoToConfirmationCommand { get; }
        public ICommand? BackToBookingCommand { get; }

        public SeatSelectionViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            GoToConfirmationCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<BookingConfirmationViewModel>();
            });

            BackToBookingCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<BookingViewModel>();
            });
        }

        public SeatSelectionViewModel()
        {
        }
    }
}
