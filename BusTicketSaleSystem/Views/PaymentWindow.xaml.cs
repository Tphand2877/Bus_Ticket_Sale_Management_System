using System;
using System.Collections.Generic;
using System.Windows;
using BusTicketSaleSystem.ViewModels;

namespace BusTicketSaleSystem.Views;

public partial class PaymentWindow : Window, IDisposable
{
    private readonly PaymentWindowViewModel _viewModel;

    public bool PaymentConfirmed => _viewModel.PaymentConfirmed;
    public PaymentWindowViewModel ViewModel => _viewModel;

    public PaymentWindow(string ticketCode, decimal amount, string transactionNo, string? paymentUrl, bool isBankTransfer, DateTime holdExpiresAtUtc)
        : this(new[] { ticketCode }, amount, transactionNo, paymentUrl, isBankTransfer, holdExpiresAtUtc)
    {
    }

    public PaymentWindow(IReadOnlyList<string> ticketCodes, decimal amount, string transactionNo, string? paymentUrl, bool isBankTransfer, DateTime holdExpiresAtUtc)
    {
        InitializeComponent();
        _viewModel = new PaymentWindowViewModel(ticketCodes, amount, transactionNo, paymentUrl, isBankTransfer, holdExpiresAtUtc, Dispatcher);
        _viewModel.RequestClose += OnRequestClose;
        DataContext = _viewModel;
    }

    private void OnRequestClose(bool? dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    private void PaymentWindow_Closed(object? sender, EventArgs e) => Dispose();

    /// <summary>
    /// The window owns its ViewModel's timers and its subscription to the static
    /// settlement event, so closing must release them.
    /// </summary>
    public void Dispose()
    {
        _viewModel.RequestClose -= OnRequestClose;
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
