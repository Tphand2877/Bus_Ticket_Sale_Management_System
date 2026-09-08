using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    // TODO: teammate will replace this with real logic
    /// <summary>
    /// Placeholder ViewModel for Booking flow (trip search results & booking initiation).
    /// Out of UI scope — to be implemented by teammate in Phase 2.
    /// </summary>
    public class BookingViewModel : ViewModelBase
    {
        private readonly INavigationService? _navigationService;

        public string Title { get; } = "Booking Flow — Available Routes";
        public string Note { get; } = "This section is assigned to teammate: Search results, filter trips, and select bus.";

        public ICommand? GoToSeatSelectionCommand { get; }
        public ICommand? BackToHomeCommand { get; }

        public BookingViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            GoToSeatSelectionCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<SeatSelectionViewModel>();
            });

            BackToHomeCommand = new RelayCommand(() =>
            {
                _navigationService?.NavigateTo<HomeViewModel>();
            });
        }

        // Parameterless constructor for fallback activation
        public BookingViewModel()
        {
        }
    }
}
