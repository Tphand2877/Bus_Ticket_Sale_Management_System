using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    /// <summary>
    /// ViewModel for My Tickets screen.
    /// Displays prototype mock data of active and upcoming booked tickets.
    /// </summary>
    public class MyTicketsViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private TicketItemViewModel? _selectedTicket;

        public ObservableCollection<TicketItemViewModel> Tickets { get; } = new();

        public TicketItemViewModel? SelectedTicket
        {
            get => _selectedTicket;
            set => SetProperty(ref _selectedTicket, value);
        }

        public ICommand ViewDetailsCommand { get; }
        public ICommand BookNewTicketCommand { get; }

        public MyTicketsViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            ViewDetailsCommand = new RelayCommand(() =>
            {
                _navigationService.NavigateTo<TicketViewModel>();
            });

            BookNewTicketCommand = new RelayCommand(() =>
            {
                _navigationService.NavigateTo<BookingViewModel>();
            });

            LoadMockTickets();
        }

        private void LoadMockTickets()
        {
            Tickets.Add(new TicketItemViewModel
            {
                TicketCode = "TK-2026-8921",
                Route = "Hanoi → Da Nang",
                Date = "Oct 16, 2026",
                Time = "07:30 PM",
                BusOperator = "Phuong Trang Express (FUTA)",
                BusType = "VIP Sleeper 34 Beds",
                SeatNumber = "A-08",
                BoardingPoint = "My Dinh Terminal, Bay 4B",
                Price = "$28.50",
                Status = "Confirmed",
                StatusColor = "#10B981" // Green
            });

            Tickets.Add(new TicketItemViewModel
            {
                TicketCode = "TK-2026-9042",
                Route = "Da Nang → Hoi An",
                Date = "Oct 19, 2026",
                Time = "02:00 PM",
                BusOperator = "Hoi An Shuttle Bus",
                BusType = "Comfort Mini-Coach",
                SeatNumber = "12",
                BoardingPoint = "Cham Museum Stop",
                Price = "$6.00",
                Status = "Confirmed",
                StatusColor = "#10B981"
            });

            Tickets.Add(new TicketItemViewModel
            {
                TicketCode = "TK-2026-9115",
                Route = "Da Nang → Hanoi",
                Date = "Oct 24, 2026",
                Time = "08:00 PM",
                BusOperator = "Hoang Long Bus",
                BusType = "Royal Sleeper Limousine",
                SeatNumber = "B-03",
                BoardingPoint = "Da Nang Central Station",
                Price = "$29.00",
                Status = "Payment Pending",
                StatusColor = "#F59E0B" // Amber
            });

            SelectedTicket = Tickets[0];
        }
    }

    /// <summary>
    /// Lightweight ViewModel representation of a ticket item for the list.
    /// </summary>
    public class TicketItemViewModel : ViewModelBase
    {
        public string TicketCode { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string BusOperator { get; set; } = string.Empty;
        public string BusType { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string BoardingPoint { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#10B981";
    }
}
