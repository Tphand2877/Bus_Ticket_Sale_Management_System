using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    /// <summary>
    /// ViewModel for Trip History screen.
    /// Displays past completed trips and tickets.
    /// </summary>
    public class HistoryViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        public ObservableCollection<PastTripItemViewModel> HistoryTrips { get; } = new();

        public string TotalCompletedTrips { get; } = "12 Trips";
        public string TotalMilesTraveled { get; } = "3,480 km";

        public ICommand BookAgainCommand { get; }

        public HistoryViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            BookAgainCommand = new RelayCommand(() =>
            {
                _navigationService.NavigateTo<BookingViewModel>();
            });

            LoadMockHistory();
        }

        private void LoadMockHistory()
        {
            HistoryTrips.Add(new PastTripItemViewModel
            {
                TicketCode = "TK-2026-7241",
                Route = "Saigon → Da Lat",
                Date = "Sep 02, 2026",
                Time = "11:00 PM",
                BusOperator = "Thanh Buoi Limousine",
                Seat = "B-04",
                Fare = "$18.50",
                Status = "Completed",
                Rating = "★★★★★"
            });

            HistoryTrips.Add(new PastTripItemViewModel
            {
                TicketCode = "TK-2026-6819",
                Route = "Da Lat → Saigon",
                Date = "Sep 06, 2026",
                Time = "01:30 PM",
                BusOperator = "Thanh Buoi Limousine",
                Seat = "A-12",
                Fare = "$18.50",
                Status = "Completed",
                Rating = "★★★★☆"
            });

            HistoryTrips.Add(new PastTripItemViewModel
            {
                TicketCode = "TK-2026-5502",
                Route = "Hanoi → Sapa",
                Date = "Jul 15, 2026",
                Time = "06:30 AM",
                BusOperator = "Inter Bus Lines",
                Seat = "Cabin-02",
                Fare = "$15.00",
                Status = "Completed",
                Rating = "★★★★★"
            });

            HistoryTrips.Add(new PastTripItemViewModel
            {
                TicketCode = "TK-2026-4310",
                Route = "Saigon → Vung Tau",
                Date = "May 20, 2026",
                Time = "09:00 AM",
                BusOperator = "Hoa Mai Express",
                Seat = "07",
                Fare = "$8.00",
                Status = "Completed",
                Rating = "★★★★☆"
            });
        }
    }

    public class PastTripItemViewModel : ViewModelBase
    {
        public string TicketCode { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string BusOperator { get; set; } = string.Empty;
        public string Seat { get; set; } = string.Empty;
        public string Fare { get; set; } = string.Empty;
        public string Status { get; set; } = "Completed";
        public string Rating { get; set; } = "★★★★★";
    }
}
