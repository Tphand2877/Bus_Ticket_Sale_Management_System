using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;

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
            SeatNumber: "A05",
            Price: 180000m,
            Status: "Đã thanh toán",
            BookingTime: bookingTime,
            Origin: "Hà Nội",
            Destination: "Hải Phòng",
            DistanceKm: 120m)
        {
            PassengerName = "Nguyễn Văn A",
            PassengerPhone = "0912345678",
            LicensePlate = "29B-999.88"
        };

        var printModel = TicketPrintModel.FromTicket(ticket);

        Assert.Equal("BT-20261015-1234", printModel.TicketCode);
        Assert.Equal("Nguyễn Văn A", printModel.PassengerName);
        Assert.Equal("0912345678", printModel.PassengerPhone);
        Assert.Equal("Hà Nội → Hải Phòng", printModel.RouteName);
        Assert.Equal("A05", printModel.SeatNumber);
        Assert.Equal("Limousine VIP", printModel.BusType);
        Assert.Equal("29B-999.88", printModel.LicensePlate);
        Assert.Equal(180000m, printModel.Price);
        Assert.Equal("Đã thanh toán", printModel.Status);
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
        Assert.Equal("12 chỗ trống", trip.AvailableSeatsDisplay);
    }
}
