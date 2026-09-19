namespace BusTicketSaleSystem.Models;

/// <summary>
/// The exact database literals used by <c>Tickets.Status</c> and
/// <c>Tickets.PaymentStatus</c>. They are CHECK-constrained in SQL Server and
/// therefore stay Vietnamese; the UI renders them through the status converters.
/// </summary>
public static class TicketStatuses
{
    /// <summary><c>Tickets.Status</c>: seat held, payment outstanding.</summary>
    public const string Reserved = "Đã đặt";

    /// <summary><c>Tickets.Status</c> and <c>Tickets.PaymentStatus</c>: settled.</summary>
    public const string Paid = "Đã thanh toán";

    /// <summary><c>Tickets.Status</c>: seat released.</summary>
    public const string Cancelled = "Đã hủy";

    /// <summary><c>Tickets.Status</c>: boarded or trip departed.</summary>
    public const string Used = "Đã sử dụng";

    /// <summary><c>Tickets.PaymentStatus</c>: nothing collected yet.</summary>
    public const string Unpaid = "Chưa thanh toán";

    /// <summary><c>Tickets.PaymentStatus</c>: money returned to the customer.</summary>
    public const string Refunded = "Hoàn tiền";


}
public sealed record BusCompanyItem(
    int CompanyId,
    string CompanyName,
    string Hotline,
    decimal Rating,
    int ReviewCount,
    string OperatingArea,
    string Description,
    string Amenities,
    string LogoText,
    string BrandColor,
    decimal MinPrice = 120000m,
    int ActiveTripsCount = 0)
{
    public string RatingDisplay => $"★ {Rating:F1} / 5.0";
    public string ReviewCountDisplay => $"({ReviewCount:N0} lượt đánh giá)";
    public string MinPriceDisplay => $"Giá chỉ từ {MinPrice:N0} VND";
    public bool HasActiveTrips => ActiveTripsCount > 0;
    public string TripStatusDisplay => ActiveTripsCount > 0 ? $"● {ActiveTripsCount} chuyến đang chạy" : "○ Tạm hết chuyến";
    public IReadOnlyList<string> AmenityList =>
        (Amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record TripSearchResult(
    int TripId,
    string Origin,
    string Destination,
    string BusType,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    decimal Price,
    int AvailableSeats,
    string CompanyName = "Sao Việt",
    string BrandColor = "#0F4C81",
    string LogoText = "SV",
    string IntermediateStops = "",
    string DriverName = "Nguyễn Văn Hùng",
    string DriverPhone = "1900 1234",
    string LicensePlate = "20A-123.45")
{
    public string Route => $"{Origin} → {Destination}";
    public string RouteWithStops => string.IsNullOrWhiteSpace(IntermediateStops)
        ? Route
        : $"{Route} (Dừng: {IntermediateStops})";
    public string Schedule => $"{DepartureTime:dd/MM/yyyy HH:mm} - {ArrivalTime:HH:mm}";
    public string PriceDisplay => $"{Price:N0} VND";
    public string FormattedFare => $"{Price:N0} VND / vé";
    public string AvailableSeatsDisplay => AvailableSeats == 1 ? "1 seat left" : $"{AvailableSeats} seats left";
    public bool IsAvailable => AvailableSeats > 0 && DepartureTime > DateTime.Now;
    public double CardOpacity => IsAvailable ? 1.0 : 0.45;
    public string StatusBadgeText => DepartureTime <= DateTime.Now ? "Đã khởi hành" : (AvailableSeats > 0 ? $"● Còn {AvailableSeats} chỗ" : "Hết chỗ");

    public IReadOnlyList<string> StopsList =>
        new[] { Origin }
            .Concat(string.IsNullOrWhiteSpace(IntermediateStops)
                ? Array.Empty<string>()
                : IntermediateStops.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(new[] { Destination })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record SeatInfo(int SeatId, int BusId, string SeatNumber, bool IsBooked)
{
    public string DisplayName => IsBooked ? $"{SeatNumber} (booked)" : SeatNumber;
}

public sealed record TicketDetails(
    int TicketId,
    string TicketCode,
    string Route,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    string BusType,
    string SeatNumbers,
    decimal Price,
    string Status,
    DateTime BookingTime,
    string Origin = "",
    string Destination = "",
    decimal DistanceKm = 0)
{
    /// <summary>Number of seats in this booking (≥ 1).</summary>
    public int SeatCount { get; init; } = 1;

    /// <summary>Total fare for all seats: <c>Price × SeatCount</c>.</summary>
    public decimal TotalPrice => Price * SeatCount;

    public string PassengerName { get; init; } = "";
    public string? PassengerPhone { get; init; }
    public string LicensePlate { get; init; } = "";

    /// <summary>Raw <c>Tickets.PaymentStatus</c> value; render via <c>PaymentStatusToDisplay</c>.</summary>
    public string PaymentStatus { get; init; } = TicketStatuses.Unpaid;

    /// <summary>Deadline for an unpaid hold; null once the ticket is settled or closed.</summary>
    public DateTime? PaymentExpiresAt { get; init; }

    /// <summary>Set when the ticket was checked in or its trip departed.</summary>
    public DateTime? UsedAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public bool IsPaid => PaymentStatus == TicketStatuses.Paid;

    public bool IsUsed => Status == TicketStatuses.Used;

    public bool IsCancelled => Status == TicketStatuses.Cancelled;

    /// <summary>True while the hold is still awaiting payment and has not expired.</summary>
    public bool IsAwaitingPayment =>
        Status == TicketStatuses.Reserved && PaymentStatus == TicketStatuses.Unpaid &&
        (PaymentExpiresAt is null || PaymentExpiresAt > DateTime.UtcNow);

    /// <summary>A refund can only be requested for a settled ticket before departure.</summary>
    public bool CanRequestRefund =>
        Status == TicketStatuses.Paid && PaymentStatus == TicketStatuses.Paid && DepartureTime > DateTime.UtcNow;
}

public sealed record PopularRouteItem(
    int RouteId,
    string From,
    string To,
    string Duration,
    string Price,
    string Tag,
    decimal MinPrice,
    string CompanyName = "Sao Việt",
    string BrandColor = "#0F4C81",
    string LogoText = "SV",
    string IntermediateStops = "")
{
    public string RouteDisplay => $"{From} → {To}";
    public string StopsSummary => string.IsNullOrWhiteSpace(IntermediateStops)
        ? "Tuyến chạy thẳng cao tốc"
        : $"Dừng: {IntermediateStops}";
}

public sealed record RouteSummaryItem(
    int RouteId,
    string Origin,
    string Destination,
    decimal? DistanceKm);

