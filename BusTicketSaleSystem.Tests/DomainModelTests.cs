using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using BusTicketSaleSystem.ViewModels;

namespace BusTicketSaleSystem.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void VietnamProvinces_ContainsAllSixtyThreeProvincesWithNoDuplicates()
    {
        var provinces = VietnamProvinces.List;

        Assert.Equal(63, provinces.Count);
        Assert.Equal(63, provinces.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("Hà Nội", provinces);
        Assert.Contains("TP. Hồ Chí Minh", provinces);
        Assert.Contains("Đà Nẵng", provinces);
        Assert.Contains("Hải Phòng", provinces);
    }

    [Fact]
    public void TicketPrintModel_MapsAllFieldsCorrectlyFromTicketDetails()
    {
        var departure = new DateTime(2026, 10, 15, 8, 30, 0);
        var arrival = new DateTime(2026, 10, 15, 11, 0, 0);
        var bookingTime = new DateTime(2026, 10, 10, 14, 0, 0);

        var ticket = new TicketDetails(
            TicketId: 101,
            TicketCode: "BT-20261015-1234",
            Route: "Hà Nội → Hải Phòng",
            DepartureTime: departure,
            ArrivalTime: arrival,
            BusType: "Limousine VIP",
            SeatNumbers: "A05, A06",
            Price: 180000m,
            Status: "Đã thanh toán",
            BookingTime: bookingTime,
            Origin: "Hà Nội",
            Destination: "Hải Phòng",
            DistanceKm: 120m)
        {
            SeatCount = 2,
            PassengerName = "Nguyễn Văn A",
            PassengerPhone = "0912345678",
            LicensePlate = "29B-999.88"
        };

        var printModel = TicketPrintModel.FromTicket(ticket);

        Assert.Equal("BT-20261015-1234", printModel.TicketCode);
        Assert.Equal("Nguyễn Văn A", printModel.PassengerName);
        Assert.Equal("0912345678", printModel.PassengerPhone);
        Assert.Equal("Hà Nội → Hải Phòng", printModel.RouteName);
        Assert.Equal("A05, A06", printModel.SeatNumbers);
        Assert.Equal(2, printModel.SeatCount);
        Assert.Equal("Limousine VIP", printModel.BusType);
        Assert.Equal("29B-999.88", printModel.LicensePlate);
        Assert.Equal(360000m, printModel.TotalPrice);
        Assert.Equal("Đã thanh toán", printModel.Status);
    }

    [Fact]
    public void TicketPrintService_ExportsValidPdfAndPngFilesMatchingNewReceiptDesign()
    {
        var ticket = new TicketDetails(
            105,
            "VX260916000128",
            "Thái Nguyên → Hà Nội",
            new DateTime(2026, 9, 16, 9, 0, 0),
            new DateTime(2026, 9, 16, 11, 30, 0),
            "Sao Việt",
            "A12",
            95000m,
            TicketStatuses.Paid,
            new DateTime(2026, 9, 16, 8, 32, 0),
            Origin: "Thái Nguyên",
            Destination: "Hà Nội")
        {
            PassengerName = "Nguyễn Văn A",
            PassengerPhone = "0987 654 321",
            LicensePlate = "20A-123.45"
        };

        var model = TicketPrintModel.FromTicket(ticket);
        var tempPdf = Path.Combine(Path.GetTempPath(), $"ticket_test_{Guid.NewGuid():N}.pdf");
        var tempPng = Path.Combine(Path.GetTempPath(), $"ticket_test_{Guid.NewGuid():N}.png");

        try
        {
            TicketPrintService.ExportToPdf(model, tempPdf);
            Assert.True(File.Exists(tempPdf));
            Assert.True(new FileInfo(tempPdf).Length > 1000);

            TicketPrintService.ExportToPng(model, tempPng);
            Assert.True(File.Exists(tempPng));
            Assert.True(new FileInfo(tempPng).Length > 1000);
        }
        finally
        {
            if (File.Exists(tempPdf)) File.Delete(tempPdf);
            if (File.Exists(tempPng)) File.Delete(tempPng);
        }
    }

    [Fact]
    public void PendingPayment_ShowsPendingActionInsteadOfETicket()
    {
        var pending = new TicketDetails(
            102,
            "BT-PENDING",
            "Hà Nội → Hải Phòng",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(3),
            "Limousine VIP",
            "A06",
            180000m,
            TicketStatuses.Reserved,
            DateTime.UtcNow)
        {
            PaymentStatus = TicketStatuses.Unpaid,
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        var pendingItem = new TicketItemViewModel(pending);
        Assert.True(pending.IsAwaitingPayment);
        Assert.False(pendingItem.CanViewETicket);
        Assert.True(pendingItem.IsPendingPayment);

        var paidItem = new TicketItemViewModel(pending with
        {
            Status = TicketStatuses.Paid,
            PaymentStatus = TicketStatuses.Paid
        });
        Assert.True(paidItem.CanViewETicket);
        Assert.False(paidItem.IsPendingPayment);
    }

    [Fact]
    public void BookingConfirmation_SummarizesMultipleSeatsAndTotalFare()
    {
        var trip = new TripSearchResult(
            7,
            "Hà Nội",
            "Hải Phòng",
            "Limousine VIP",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(3),
            180000m,
            10);
        var seats = new[]
        {
            new SeatInfo(11, 3, "A01", false),
            new SeatInfo(12, 3, "A02", false)
        };

        var confirmation = new BookingConfirmationViewModel(
            new NavigationService(),
            new FakePaymentDialogService(),
            trip,
            seats,
            "Nguyễn Văn A",
            "0900000000");

        Assert.Equal(2, confirmation.SeatCount);
        Assert.Equal("A01, A02", confirmation.SelectedSeatNumbers);
        Assert.Equal((trip.Price * 2).ToString("N0") + " VND", confirmation.PriceDisplay);
    }

    [Fact]
    public void TicketDetails_ComputesTotalPriceFromSeatCountAndUnitPrice()
    {
        var ticket = new TicketDetails(
            103,
            "BT-FAMILY",
            "Hà Nội → Đà Nẵng",
            DateTime.UtcNow.AddDays(2),
            DateTime.UtcNow.AddDays(2).AddHours(14),
            "Giường nằm",
            "01, 02, 05, 06",
            350000m,
            TicketStatuses.Reserved,
            DateTime.UtcNow)
        {
            SeatCount = 4
        };

        Assert.Equal(4, ticket.SeatCount);
        Assert.Equal(1400000m, ticket.TotalPrice);
        Assert.Equal("01, 02, 05, 06", ticket.SeatNumbers);
    }

    [Fact]
    public void BusAdminItem_CalculatesOccupancyPercentageCorrectly()
    {
        var emptyBus = new BusAdminItem(1, "29B-111.11", 40, "Ghế ngồi", 0);
        var halfBus = new BusAdminItem(2, "29B-222.22", 40, "Ghế ngồi", 20);
        var fullBus = new BusAdminItem(3, "29B-333.33", 40, "Ghế ngồi", 40);

        Assert.Equal(0, emptyBus.OccupancyPercentage);
        Assert.Equal(50, halfBus.OccupancyPercentage);
        Assert.Equal(100, fullBus.OccupancyPercentage);
        Assert.Equal("20/40 (50%)", halfBus.OccupancyText);
    }

    [Fact]
    public void TripSearchResult_FormatsPriceAndScheduleCorrectly()
    {
        var dep = new DateTime(2026, 9, 20, 7, 0, 0);
        var arr = new DateTime(2026, 9, 20, 9, 30, 0);
        var trip = new TripSearchResult(1, "Hà Nội", "Hải Phòng", "Ghế ngồi", dep, arr, 150000m, 12);

        Assert.Equal(trip.Price.ToString("N0") + " VND", trip.PriceDisplay);
        Assert.Equal("20/09/2026 07:00 - 09:30", trip.Schedule);
        Assert.Equal("Hà Nội → Hải Phòng", trip.Route);
        Assert.Equal("12 seats left", trip.AvailableSeatsDisplay);
        Assert.Equal($"{150000:N0} VND / vé", trip.FormattedFare);
    }

    [Fact]
    public void BusCompanyItem_CalculatesRatingsAndMinPriceCorrectly()
    {
        var company = new BusCompanyItem(
            1, "Sao Việt", "1900 1234", 4.8m, 1250, "Hà Nội - Sa Pa",
            "Mô tả nhà xe", "Wifi, Nước uống", "SV", "#0F4C81",
            MinPrice: 140000m, ActiveTripsCount: 5);

        Assert.Equal($"★ {4.8m:F1} / 5.0", company.RatingDisplay);
        Assert.Equal($"({1250:N0} lượt đánh giá)", company.ReviewCountDisplay);
        Assert.Equal($"Giá chỉ từ {140000m:N0} VND", company.MinPriceDisplay);
        Assert.True(company.HasActiveTrips);
        Assert.Equal("● 5 chuyến đang chạy", company.TripStatusDisplay);
    }

    [Fact]
    public void TripSearchResult_CalculatesAvailabilityAndOpacityCorrectly()
    {
        var futureDep = DateTime.Now.AddHours(2);
        var futureArr = futureDep.AddHours(2);
        var pastDep = DateTime.Now.AddHours(-2);
        var pastArr = pastDep.AddHours(2);

        // Available future trip with seats
        var activeTrip = new TripSearchResult(1, "Hà Nội", "Hải Phòng", "Ghế ngồi", futureDep, futureArr, 140000m, 20);
        Assert.True(activeTrip.IsAvailable);
        Assert.Equal(1.0, activeTrip.CardOpacity);
        Assert.Contains("Còn 20 chỗ", activeTrip.StatusBadgeText);

        // Past trip
        var pastTrip = new TripSearchResult(2, "Hà Nội", "Hải Phòng", "Ghế ngồi", pastDep, pastArr, 140000m, 20);
        Assert.False(pastTrip.IsAvailable);
        Assert.Equal(0.45, pastTrip.CardOpacity);
        Assert.Equal("Đã khởi hành", pastTrip.StatusBadgeText);

        // Fully booked trip
        var fullTrip = new TripSearchResult(3, "Hà Nội", "Hải Phòng", "Ghế ngồi", futureDep, futureArr, 140000m, 0);
        Assert.False(fullTrip.IsAvailable);
        Assert.Equal(0.45, fullTrip.CardOpacity);
        Assert.Equal("Hết chỗ", fullTrip.StatusBadgeText);
    }
}
