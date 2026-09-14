namespace BusTicketSaleSystem.Models;

public sealed record TripSearchResult(
    int TripId,
    string Origin,
    string Destination,
    string BusType,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    decimal Price,
    int AvailableSeats)
{
    public string Route => $"{Origin} → {Destination}";
    public string Schedule => $"{DepartureTime:dd/MM/yyyy HH:mm} - {ArrivalTime:HH:mm}";
    public string PriceDisplay => $"{Price:N0} VND";
    public string AvailableSeatsDisplay => $"{AvailableSeats} chỗ trống";
}

public sealed record SeatInfo(int SeatId, int BusId, string SeatNumber, bool IsBooked)
{
    public string DisplayName => IsBooked ? $"{SeatNumber} (đã đặt)" : SeatNumber;
}

public sealed record TicketDetails(
    int TicketId,
    string TicketCode,
    string Route,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    string BusType,
    string SeatNumber,
    decimal Price,
    string Status,
    DateTime BookingTime,
    string Origin = "",
    string Destination = "",
    decimal DistanceKm = 0)
{
    public string PassengerName { get; init; } = "";
    public string? PassengerPhone { get; init; }
    public string LicensePlate { get; init; } = "";
}

public sealed record PopularRouteItem(
    int RouteId,
    string From,
    string To,
    string Duration,
    string Price,
    string Tag,
    decimal MinPrice);

public sealed record RouteSummaryItem(
    int RouteId,
    string Origin,
    string Destination,
    decimal? DistanceKm);

