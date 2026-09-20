using BusTicketSaleSystem.Models;
using Xunit;

namespace BusTicketSaleSystem.Tests;

/// <summary>
/// Verifies the commission rate and bus company settlement calculations (Task 2.3).
/// </summary>
public sealed class CommissionReportTests
{
    [Fact]
    public void RevenueReportItem_CalculatesCommissionAndCompanyPayoutCorrectly()
    {
        // 10 tickets @ 200,000 = 2,000,000 gross. 1 refunded ticket @ 200,000 = 200,000 refunded.
        // Net = 1,800,000. Commission = 10% (0.1000) = 180,000. Payout to bus company = 1,620,000.
        var report = new RevenueReportItem(
            RouteSummary: "Hà Nội → Hải Phòng",
            TicketsSold: 10,
            GrossRevenue: 2_000_000m,
            RefundedAmount: 200_000m,
            CommissionRate: 0.10m);

        Assert.Equal(1_800_000m, report.NetRevenue);
        Assert.Equal(180_000m, report.CommissionAmount);
        Assert.Equal(1_620_000m, report.CompanyPayout);
        Assert.Equal("1,800,000 VND", report.NetRevenueDisplay);
        Assert.Equal("180,000 VND (10%)", report.CommissionDisplay);
        Assert.Equal("1,620,000 VND", report.PayoutDisplay);
    }

    [Fact]
    public void RevenueReportItem_ZeroNetRevenueYieldsZeroCommissionAndPayout()
    {
        var report = new RevenueReportItem(
            RouteSummary: "Hà Nội → Nam Định",
            TicketsSold: 1,
            GrossRevenue: 100_000m,
            RefundedAmount: 100_000m,
            CommissionRate: 0.15m);

        Assert.Equal(0m, report.NetRevenue);
        Assert.Equal(0m, report.CommissionAmount);
        Assert.Equal(0m, report.CompanyPayout);
    }
}
