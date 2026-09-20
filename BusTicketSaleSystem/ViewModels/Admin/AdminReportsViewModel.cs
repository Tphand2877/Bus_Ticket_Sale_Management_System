using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using Microsoft.Data.SqlClient;

namespace BusTicketSaleSystem.ViewModels;


/// <summary>Per-route revenue for a date range, computed from the payment ledger.</summary>
public sealed class AdminReportsViewModel : ViewModelBase, IRefreshable
{
    private DateTime _fromDate = DateTime.UtcNow.Date.AddDays(-29);
    private DateTime _toDate = DateTime.UtcNow.Date;
    private string _message = "";
    private bool _isLoading;

    public ObservableCollection<RevenueReportItem> Rows { get; } = new();
    public RevenueReportItem Totals { get; private set; } = new("All routes", 0, 0, 0);

    public DateTime FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage));
        }
    }
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasRows => Rows.Count > 0;
    public bool HasNoRows => !HasRows && !IsLoading;

    public string RangeDescription => $"{FromDate:dd/MM/yyyy} – {ToDate:dd/MM/yyyy} (UTC, inclusive)";

    public ICommand RunReportCommand { get; }
    public ICommand LastSevenDaysCommand { get; }
    public ICommand LastThirtyDaysCommand { get; }
    public ICommand ClearMessageCommand { get; }

    public AdminReportsViewModel()
    {
        RunReportCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        LastSevenDaysCommand = new AsyncRelayCommand(async () => await SetRangeAsync(7));
        LastThirtyDaysCommand = new AsyncRelayCommand(async () => await SetRangeAsync(30));
        ClearMessageCommand = new RelayCommand(() => Message = "");
    }

    private async Task SetRangeAsync(int days)
    {
        ToDate = DateTime.UtcNow.Date;
        FromDate = ToDate.AddDays(-(days - 1));
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (ToDate < FromDate)
        {
            Message = "The end date must not be earlier than the start date.";
            return;
        }

        IsLoading = true;
        try
        {
            var rows = await AdminDatabaseService.GetRevenueReportAsync(FromDate.Date, ToDate.Date.AddDays(1));
            Rows.Clear();
            foreach (var row in rows) Rows.Add(row);
            var totalTickets = rows.Sum(r => r.TicketsSold);
            var totalGross = rows.Sum(r => r.GrossRevenue);
            var totalRefunded = rows.Sum(r => r.RefundedAmount);
            var totalNet = totalGross - totalRefunded;
            var totalCommission = rows.Sum(r => r.CommissionAmount);
            var avgCommissionRate = totalNet > 0 ? totalCommission / totalNet : 0.10m;
            Totals = new RevenueReportItem(
                "All routes",
                totalTickets,
                totalGross,
                totalRefunded,
                avgCommissionRate);
            OnPropertyChanged(nameof(Totals));
            OnPropertyChanged(nameof(RangeDescription));
            Message = "";
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Building the revenue report");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(HasNoRows));
        }
    }
}

