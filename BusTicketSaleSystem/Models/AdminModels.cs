using BusTicketSaleSystem.Common;

namespace BusTicketSaleSystem.Models;

public sealed record RouteAdminItem(int RouteId, string Origin, string Destination, decimal? DistanceKm)
{
    public string DisplayName => $"{Origin} → {Destination}";
    public string DistanceDisplay => DistanceKm.HasValue ? $"{DistanceKm.Value:N1} km" : "Not set";
}

public sealed record BusAdminItem(
    int BusId,
    string LicensePlate,
    int SeatCount,
    string BusType,
    int UsedSeats,
    string CompanyName = "Sao Việt",
    string DriverName = "Nguyễn Văn Hùng",
    string DriverPhone = "0912 345 678",
    string DepartureTimeNote = "06:30, 09:00, 14:30",
    string RouteName = "Hà Nội → Thái Nguyên",
    string IntermediateStops = "Sân bay Nội Bài, Ngã tư Sóc Sơn, Phổ Yên, Bến xe Thái Nguyên",
    string Make = "Hyundai",
    string Model = "Universe",
    int ManufactureYear = 2023,
    string Color = "Trắng - Xanh",
    string RegistrationNumber = "29B-99881",
    string InspectionNumber = "KD-7890123",
    DateTime? RegistrationDate = null,
    DateTime? InspectionExpiryDate = null,
    DateTime? InsuranceExpiryDate = null,
    string RegisteredEntity = "Công ty CP Vận tải Sao Việt",
    string OperationType = "Tuyến cố định",
    decimal? FullPrice = null,
    decimal? StagePrice = null)
{
    public string Availability => $"{UsedSeats}/{SeatCount} seats booked";
    public int OccupancyPercentage => SeatCount > 0 ? (int)Math.Clamp(Math.Round((double)UsedSeats / SeatCount * 100), 0, 100) : 0;
    public string OccupancyText => $"{UsedSeats}/{SeatCount} ({OccupancyPercentage}%)";
    public string SpecsDisplay => $"{Make} {Model} ({ManufactureYear}) · {Color}";
    public string LegalStatusDisplay => $"ĐK: {RegistrationNumber} | KĐ: {InspectionNumber}";
    public string FareDisplay => StagePrice.HasValue && StagePrice.Value > 0
        ? $"Toàn tuyến: {FullPrice:N0}đ | Chặng: {StagePrice:N0}đ"
        : (FullPrice.HasValue && FullPrice.Value > 0 ? $"Giá vé: {FullPrice:N0}đ" : "Theo cự ly");
}

public sealed record BusRegistrationInput(
    string CompanyName,
    string DriverName,
    string DriverPhone,
    string LicensePlate,
    string DepartureTimeNote,
    int? RouteId,
    string IntermediateStops,
    int SeatCount,
    string BusType,
    string Make,
    string Model,
    int ManufactureYear,
    string Color,
    string RegistrationNumber,
    string InspectionNumber,
    DateTime? RegistrationDate,
    DateTime? InspectionExpiryDate,
    DateTime? InsuranceExpiryDate,
    string RegisteredEntity,
    string OperationType,
    decimal? FarePrice = null,
    string? Hotline = null,
    string? OperatingArea = null,
    string? Description = null,
    string? Amenities = null,
    decimal? StagePrice = null);

public sealed class BusDraftItem : ViewModelBase
{
    private string _plate = "";
    private string _seatCount = "40";
    private string _busType = "Limousine";
    private string _make = "Hyundai";
    private string _model = "Universe";
    private string _manufactureYear = "2023";
    private string _color = "Trắng - Xanh";
    private string _operationType = "Tuyến cố định";

    private string _driverName = "Nguyễn Văn Hùng";
    private string _driverPhone = "0912 345 678";

    private string _origin = "Hà Nội";
    private string _destination = "Thái Nguyên";
    private int? _routeId;
    private string _intermediateStops = "Sân bay Nội Bài, Ngã tư Sóc Sơn, Phổ Yên, Bến xe Thái Nguyên";
    private string _departureTimeNote = "06:00, 08:30, 11:00, 14:00, 16:30, 19:00";
    private string _farePrice = "150000";
    private string _stagePrice = "70000";

    private string _registrationNumber = "";
    private string _inspectionNumber = "KD-001";
    private DateTime? _registrationDate = DateTime.Today;
    private DateTime? _inspectionExpiryDate = DateTime.Today.AddYears(2);
    private DateTime? _insuranceExpiryDate = DateTime.Today.AddYears(1);

    public string Plate { get => _plate; set { SetProperty(ref _plate, value); OnPropertyChanged(nameof(VehicleDisplay)); } }
    public string SeatCount { get => _seatCount; set { SetProperty(ref _seatCount, value); OnPropertyChanged(nameof(VehicleDisplay)); } }
    public string BusType { get => _busType; set { SetProperty(ref _busType, value); OnPropertyChanged(nameof(VehicleDisplay)); } }
    public string Make { get => _make; set { SetProperty(ref _make, value); OnPropertyChanged(nameof(VehicleDisplay)); } }
    public string Model { get => _model; set { SetProperty(ref _model, value); OnPropertyChanged(nameof(VehicleDisplay)); } }
    public string ManufactureYear { get => _manufactureYear; set => SetProperty(ref _manufactureYear, value); }
    public string Color { get => _color; set => SetProperty(ref _color, value); }
    public string OperationType { get => _operationType; set => SetProperty(ref _operationType, value); }

    public string DriverName { get => _driverName; set { SetProperty(ref _driverName, value); OnPropertyChanged(nameof(DriverDisplay)); } }
    public string DriverPhone { get => _driverPhone; set { SetProperty(ref _driverPhone, value); OnPropertyChanged(nameof(DriverDisplay)); } }

    public string Origin { get => _origin; set { SetProperty(ref _origin, value); OnPropertyChanged(nameof(RouteDisplay)); } }
    public string Destination { get => _destination; set { SetProperty(ref _destination, value); OnPropertyChanged(nameof(RouteDisplay)); } }
    public int? RouteId { get => _routeId; set => SetProperty(ref _routeId, value); }
    public string IntermediateStops { get => _intermediateStops; set => SetProperty(ref _intermediateStops, value); }
    public string DepartureTimeNote { get => _departureTimeNote; set => SetProperty(ref _departureTimeNote, value); }
    public string FarePrice { get => _farePrice; set { SetProperty(ref _farePrice, value); OnPropertyChanged(nameof(PriceSummary)); } }
    public string StagePrice { get => _stagePrice; set { SetProperty(ref _stagePrice, value); OnPropertyChanged(nameof(PriceSummary)); } }

    public string RegistrationNumber { get => _registrationNumber; set => SetProperty(ref _registrationNumber, value); }
    public string InspectionNumber { get => _inspectionNumber; set => SetProperty(ref _inspectionNumber, value); }
    public DateTime? RegistrationDate { get => _registrationDate; set => SetProperty(ref _registrationDate, value); }
    public DateTime? InspectionExpiryDate { get => _inspectionExpiryDate; set => SetProperty(ref _inspectionExpiryDate, value); }
    public DateTime? InsuranceExpiryDate { get => _insuranceExpiryDate; set => SetProperty(ref _insuranceExpiryDate, value); }

    public string RouteDisplay => $"{Origin} → {Destination}";
    public string DriverDisplay => $"{DriverName} (SĐT: {DriverPhone})";
    public string VehicleDisplay => $"{Plate} · {BusType} ({SeatCount} chỗ) - {Make} {Model}";
    public string PriceSummary => $"Toàn tuyến: {FarePrice}đ | Chặng: {StagePrice}đ";
    public string ScheduleSummary => $"Khung giờ: {DepartureTimeNote}";
}

public sealed record TripAdminItem(
    int TripId,
    int RouteId,
    int BusId,
    string Route,
    string LicensePlate,
    DateTime DepartureTime,
    DateTime ArrivalTime,
    decimal Price,
    string Status = "Scheduled")
{
    public string Schedule => $"{DepartureTime.ToLocalTime():dd/MM/yyyy HH:mm} - {ArrivalTime.ToLocalTime():HH:mm}";
    public string PriceDisplay => $"{Price:N0} VND";
    public string DepartureFormatted => DepartureTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string ArrivalFormatted => ArrivalTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string TimeRangeDisplay => $"{DepartureTime.ToLocalTime():HH:mm} → {ArrivalTime.ToLocalTime():HH:mm}";
    public string DateDisplay => DepartureTime.ToLocalTime().ToString("dd/MM/yyyy");
    public bool IsCancelled => Status == "Cancelled";
    public bool IsDeparted => Status == "Departed";
    public bool CanCancel => Status == "Scheduled" && DepartureTime > DateTime.UtcNow;
    public string StatusDisplay => Status switch
    {
        "Cancelled" => "Đã hủy",
        "Departed" => "Đã xuất bến",
        _ => "Đang chạy"
    };
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
    public string SeatDisplay => $"Seat {SeatNumber}";
    public string PassengerDisplay => string.IsNullOrWhiteSpace(PassengerPhone) ? PassengerName : $"{PassengerName} ({PassengerPhone})";
}

/// <summary>
/// Dashboard headline figures. Revenue is payment-backed: only ledger rows that reached
/// <c>Success</c> count, minus everything that was refunded. Unpaid holds are reported
/// separately through the awaiting-settlement fields.
/// </summary>
public sealed record AdminDashboardStats(
    int RouteCount,
    int BusCount,
    int TripCount,
    int TicketsSold,
    decimal TotalRevenue,
    int CustomerCount,
    int PendingSettlementCount,
    decimal PendingSettlementAmount,
    decimal RefundedAmount)
{
    public static AdminDashboardStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    public string TotalRevenueDisplay => $"{TotalRevenue:N0} VND";
    public string PendingSettlementAmountDisplay => $"{PendingSettlementAmount:N0} VND";
    public string RefundedAmountDisplay => $"{RefundedAmount:N0} VND";
    public string PendingSettlementCaption => PendingSettlementCount == 1
        ? "1 payment awaiting confirmation"
        : $"{PendingSettlementCount} payments awaiting confirmation";
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
    public bool IsAdmin => Role == "Admin";
    public string RoleBadge => IsAdmin ? "Administrator" : "Customer";
    public string ToggleRoleCaption => IsAdmin ? "Demote to customer" : "Promote to admin";
    public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm");
}

/// <summary>
/// One row of the settlement console: the payment ledger entry joined to the ticket it pays for.
/// </summary>
public sealed record PaymentAdminItem(
    int PaymentId,
    int TicketId,
    string TicketCode,
    string? TransactionNo,
    string? ProviderTransactionNo,
    string PaymentMethod,
    string Provider,
    decimal Amount,
    string Status,
    string TicketStatus,
    string PassengerName,
    string RouteSummary,
    DateTime DepartureTime,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? RefundRequestedAt,
    DateTime? RefundedAt,
    string? ConfirmedBy,
    string? Notes,
    DateTime? PaymentExpiresAt)
{
    public string AmountDisplay => $"{Amount:N0} VND";
    public string StatusDisplay => PaymentStatusToDisplayConverter.ToDisplay(Status);
    public string TicketStatusDisplay => TicketStatusToDisplayConverter.ToDisplay(TicketStatus);
    public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm");
    public string DepartureDisplay => DepartureTime.ToString("dd/MM/yyyy HH:mm");
    public string PaidAtDisplay => PaidAt.HasValue ? PaidAt.Value.ToString("dd/MM/yyyy HH:mm") : "—";
    public string ReferenceDisplay => string.IsNullOrWhiteSpace(ProviderTransactionNo)
        ? (string.IsNullOrWhiteSpace(TransactionNo) ? "—" : TransactionNo)
        : ProviderTransactionNo;
    public string ChannelDisplay => string.Equals(PaymentMethod, Provider, StringComparison.OrdinalIgnoreCase)
        ? PaymentMethod
        : $"{PaymentMethod} · {Provider}";
    public string ConfirmedByDisplay => string.IsNullOrWhiteSpace(ConfirmedBy) ? "—" : ConfirmedBy;
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "—" : Notes;
    public string ExpiryDisplay => PaymentExpiresAt.HasValue
        ? $"Hold expires {PaymentExpiresAt.Value:dd/MM/yyyy HH:mm}"
        : "No hold deadline";

    public bool IsPending => Status == "Pending";
    public bool IsSettled => Status == "Success";
    public bool RefundRequested => RefundRequestedAt.HasValue;
    public bool CanConfirm => IsPending;
    public bool CanReject => IsPending;
    public bool CanApproveRefund => IsSettled && RefundRequested;
    public bool CanCheckIn => IsSettled && TicketStatus == "Đã thanh toán";
    public string RefundRequestDisplay => RefundRequestedAt.HasValue
        ? $"Refund requested {RefundRequestedAt.Value:dd/MM/yyyy HH:mm}"
        : "";
}

/// <summary>Ledger totals grouped by <c>Payments.Status</c>.</summary>
public sealed record AdminPaymentStats(
    int PendingCount,
    decimal PendingAmount,
    int SuccessCount,
    decimal SuccessAmount,
    int FailedCount,
    decimal FailedAmount,
    int RefundedCount,
    decimal RefundedAmount)
{
    public static AdminPaymentStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    public decimal NetRevenue => SuccessAmount - RefundedAmount;
    public string PendingAmountDisplay => $"{PendingAmount:N0} VND";
    public string SuccessAmountDisplay => $"{SuccessAmount:N0} VND";
    public string FailedAmountDisplay => $"{FailedAmount:N0} VND";
    public string RefundedAmountDisplay => $"{RefundedAmount:N0} VND";
    public string NetRevenueDisplay => $"{NetRevenue:N0} VND";
}

/// <summary>One route's contribution to revenue over the reported window.</summary>
public sealed record RevenueReportItem(
    string RouteSummary,
    int TicketsSold,
    decimal GrossRevenue,
    decimal RefundedAmount,
    decimal CommissionRate = 0.10m)
{
    public decimal NetRevenue => GrossRevenue - RefundedAmount;
    public decimal CommissionAmount => NetRevenue > 0 ? NetRevenue * CommissionRate : 0m;
    public decimal CompanyPayout => NetRevenue > 0 ? NetRevenue - CommissionAmount : 0m;
    public string GrossRevenueDisplay => $"{GrossRevenue:N0} VND";
    public string RefundedAmountDisplay => $"{RefundedAmount:N0} VND";
    public string NetRevenueDisplay => $"{NetRevenue:N0} VND";
    public string CommissionDisplay => $"{CommissionAmount:N0} VND ({CommissionRate * 100:0.#}%)";
    public string PayoutDisplay => $"{CompanyPayout:N0} VND";
}
