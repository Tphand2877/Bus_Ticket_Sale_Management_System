namespace BusTicketSaleSystem.Models;

public sealed record RouteAdminItem(int RouteId, string Origin, string Destination, decimal? DistanceKm)
{
    public string DisplayName => $"{Origin} → {Destination}";
    public string DistanceDisplay => DistanceKm.HasValue ? $"{DistanceKm.Value:N1} km" : "N/A";
}

public sealed record BusAdminItem(int BusId, string LicensePlate, int SeatCount, string BusType, int UsedSeats)
{
    public string Availability => $"{UsedSeats}/{SeatCount} ghế đã đặt";
    public int OccupancyPercentage => SeatCount > 0 ? (int)Math.Clamp(Math.Round((double)UsedSeats / SeatCount * 100), 0, 100) : 0;
    public string OccupancyText => $"{UsedSeats}/{SeatCount} ({OccupancyPercentage}%)";
}

public sealed record TripAdminItem(int TripId, int RouteId, int BusId, string Route, string LicensePlate, DateTime DepartureTime, DateTime ArrivalTime, decimal Price)
{
    public string Schedule => $"{DepartureTime:dd/MM/yyyy HH:mm} - {ArrivalTime:HH:mm}";
    public string PriceDisplay => $"{Price:N0} VND";
    public string DepartureFormatted => DepartureTime.ToString("dd/MM/yyyy HH:mm");
    public string ArrivalFormatted => ArrivalTime.ToString("dd/MM/yyyy HH:mm");
    public string TimeRangeDisplay => $"{DepartureTime:HH:mm} → {ArrivalTime:HH:mm}";
    public string DateDisplay => DepartureTime.ToString("dd/MM/yyyy");
}

public sealed record RecentBookingAdminItem(
    int TicketId,
    string TicketCode,
    string RouteName,
    string PassengerName,
    string? PassengerPhone,
    string SeatNumber,
    decimal Price,
    string Status,
    DateTime BookingTime)
{
    public string PriceDisplay => $"{Price:N0} VND";
    public string BookingTimeDisplay => BookingTime.ToString("dd/MM/yyyy HH:mm");
    public string SeatDisplay => $"Ghế {SeatNumber}";
    public string PassengerDisplay => string.IsNullOrWhiteSpace(PassengerPhone) ? PassengerName : $"{PassengerName} ({PassengerPhone})";
}

public sealed record AdminDashboardStats(
    int RouteCount,
    int BusCount,
    int TripCount,
    int TicketCount,
    decimal Revenue,
    int CustomerCount = 0)
{
    public string RevenueDisplay => $"{Revenue:N0} VND";
}

public sealed record AccountAdminItem(
    int AccountId,
    string Username,
    string FullName,
    string? Email,
    string? Phone,
    string Role,
    DateTime CreatedAt)
{
    public string RoleBadge => Role == "Admin" ? "Quản trị viên" : "Khách hàng";
    public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm");
}
