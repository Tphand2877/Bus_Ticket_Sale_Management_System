using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    /// <summary>
    /// ViewModel for the main Dashboard screen (HomeView).
    /// Displays trip search bar, upcoming trip card, and user personal stat cards.
    /// Uses static mock data defined internally without depending on teammate models.
    /// </summary>
    public class HomeViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        // Trip search box fields
        private string _origin = "Hanoi (My Dinh Terminal)";
        private string _destination = "Da Nang (Central Station)";
        private DateTime _departureDate = DateTime.Today.AddDays(2);
        private string _selectedPassengers = "1 Passenger";

        public string Origin
        {
            get => _origin;
            set => SetProperty(ref _origin, value);
        }

        public string Destination
        {
            get => _destination;
            set => SetProperty(ref _destination, value);
        }

        public DateTime DepartureDate
        {
            get => _departureDate;
            set => SetProperty(ref _departureDate, value);
        }

        public string SelectedPassengers
        {
            get => _selectedPassengers;
            set => SetProperty(ref _selectedPassengers, value);
        }

        // Available Passenger options for ComboBox
        public ObservableCollection<string> PassengerOptions { get; } = new()
        {
            "1 Passenger",
            "2 Passengers",
            "3 Passengers",
            "4+ Passengers"
        };

        // Personal Stats (Static prototype numbers)
        public string TotalTickets { get; set; } = "14";
        public string UpcomingTripsCount { get; set; } = "2";
        public string TotalSpent { get; set; } = "$340.00";
        public string LoyaltyPoints { get; set; } = "1,250 pts";

        // Upcoming Trip Card Static Data
        public string UpcomingTicketCode { get; set; } = "TK-2026-8921";
        public string UpcomingRoute { get; set; } = "Hanoi → Da Nang";
        public string UpcomingDepartureStation { get; set; } = "My Dinh Terminal, Gate 4B";
        public string UpcomingArrivalStation { get; set; } = "Da Nang Central Station";
        public string UpcomingDate { get; set; } = "Friday, Oct 16, 2026";
        public string UpcomingDepartureTime { get; set; } = "07:30 PM";
        public string UpcomingArrivalTime { get; set; } = "08:15 AM (Next Day)";
        public string UpcomingDuration { get; set; } = "12h 45m";
        public string UpcomingBusType { get; set; } = "VIP Sleeper Limousine • 34 Berths";
        public string UpcomingSeat { get; set; } = "A-08 (Lower Deck)";
        public string UpcomingStatus { get; set; } = "Confirmed";
        public string UpcomingPrice { get; set; } = "$28.50";

        // Popular Destination Cards for quick exploration
        public ObservableCollection<PopularRouteItem> PopularRoutes { get; } = new()
        {
            new PopularRouteItem("Hanoi", "Sapa", "5h 30m", "From $14.00", "Mountain Express"),
            new PopularRouteItem("Saigon", "Da Lat", "6h 45m", "From $18.50", "Pine Forest Sleeper"),
            new PopularRouteItem("Da Nang", "Hue", "2h 15m", "From $8.00", "Heritage Shuttle"),
            new PopularRouteItem("Saigon", "Nha Trang", "8h 00m", "From $22.00", "Coastal Limousine")
        };

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand ViewTicketCommand { get; }
        public ICommand SwapLocationsCommand { get; }
        public ICommand QuickBookCommand { get; }

        public HomeViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Clicking Search navigates to the Booking screen
            SearchCommand = new RelayCommand(() =>
            {
                _navigationService.NavigateTo<BookingViewModel>();
            });

            // Clicking View Ticket navigates to TicketView placeholder
            ViewTicketCommand = new RelayCommand(() =>
            {
                _navigationService.NavigateTo<TicketViewModel>();
            });

            // Swaps Origin and Destination
            SwapLocationsCommand = new RelayCommand(() =>
            {
                var temp = Origin;
                Origin = Destination;
                Destination = temp;
            });

            QuickBookCommand = new RelayCommand(() =>
            {
                _navigationService.NavigateTo<BookingViewModel>();
            });
        }
    }

    /// <summary>
    /// Simple helper record for popular route display cards.
    /// </summary>
    public record PopularRouteItem(string From, string To, string Duration, string Price, string Tag);
}
