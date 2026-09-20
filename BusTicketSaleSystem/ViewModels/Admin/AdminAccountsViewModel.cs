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


public sealed class AdminAccountsViewModel : ViewModelBase, IRefreshable
{
    private string _message = "";
    private string _searchQuery = "";
    private string _newPassword = "";
    private bool _isLoading;
    private List<AccountAdminItem> _allAccounts = new();
    private AccountAdminItem? _resetTarget;
    private AccountAdminItem? _pendingDelete;

    public ObservableCollection<AccountAdminItem> Accounts { get; } = new();
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
    public bool HasAccounts => Accounts.Count > 0;
    public bool HasNoAccounts => !HasAccounts && !IsLoading;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value)) FilterAccounts();
        }
    }

    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }

    public AccountAdminItem? ResetTarget
    {
        get => _resetTarget;
        private set
        {
            if (SetProperty(ref _resetTarget, value))
            {
                OnPropertyChanged(nameof(IsResettingPassword));
                OnPropertyChanged(nameof(PasswordResetPrompt));
            }
        }
    }
    public bool IsResettingPassword => ResetTarget is not null;
    public string PasswordResetPrompt => ResetTarget is null
        ? ""
        : $"Set a new password for {ResetTarget.Username} ({ResetTarget.FullName}). Minimum 8 characters.";

    public AccountAdminItem? PendingDelete
    {
        get => _pendingDelete;
        private set
        {
            if (SetProperty(ref _pendingDelete, value))
            {
                OnPropertyChanged(nameof(IsConfirmingDelete));
                OnPropertyChanged(nameof(DeleteConfirmationPrompt));
            }
        }
    }
    public bool IsConfirmingDelete => PendingDelete is not null;
    public string DeleteConfirmationPrompt => PendingDelete is null
        ? ""
        : $"Delete account {PendingDelete.Username}? This cannot be undone.";

    public ICommand ToggleRoleCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand BeginPasswordResetCommand { get; }
    public ICommand ApplyPasswordResetCommand { get; }
    public ICommand CancelPasswordResetCommand { get; }
    public ICommand RequestDeleteCommand { get; }
    public ICommand ConfirmDeleteCommand { get; }
    public ICommand CancelDeleteCommand { get; }

    public AdminAccountsViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(async () => await RefreshAsync());
        ToggleRoleCommand = new AsyncRelayCommand(async p => await ToggleRoleAsync(p));
        BeginPasswordResetCommand = new RelayCommand(p =>
        {
            if (p is not AccountAdminItem account) return;
            PendingDelete = null;
            NewPassword = "";
            Message = "";
            ResetTarget = account;
        });
        CancelPasswordResetCommand = new RelayCommand(() =>
        {
            ResetTarget = null;
            NewPassword = "";
        });
        ApplyPasswordResetCommand = new AsyncRelayCommand(async () => await ApplyPasswordResetAsync(), () => IsResettingPassword);
        RequestDeleteCommand = new RelayCommand(p =>
        {
            if (p is not AccountAdminItem account) return;
            ResetTarget = null;
            Message = "";
            PendingDelete = account;
        });
        CancelDeleteCommand = new RelayCommand(() => PendingDelete = null);
        ConfirmDeleteCommand = new AsyncRelayCommand(async () => await DeleteAsync(), () => IsConfirmingDelete);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            _allAccounts = (await AdminDatabaseService.GetAccountsAsync()).ToList();
            FilterAccounts();
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Loading accounts");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasAccounts));
            OnPropertyChanged(nameof(HasNoAccounts));
        }
    }

    private void FilterAccounts()
    {
        Accounts.Clear();
        var query = SearchQuery.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allAccounts
            : _allAccounts.Where(a => a.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                      a.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                      (a.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                      (a.Phone?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (var a in filtered) Accounts.Add(a);
        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(HasNoAccounts));
    }

    private async Task ToggleRoleAsync(object? parameter)
    {
        if (parameter is not AccountAdminItem account) return;
        var targetRole = account.IsAdmin ? "Customer" : "Admin";
        try
        {
            var result = await AdminDatabaseService.UpdateAccountRoleAsync(account.AccountId, targetRole);
            Message = result.Error ?? $"{account.Username} is now a{(targetRole == "Admin" ? "n administrator" : " customer")}.";
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Updating the account role");
        }
    }

    private async Task ApplyPasswordResetAsync()
    {
        var target = ResetTarget;
        if (target is null) return;
        try
        {
            var result = await AdminDatabaseService.ResetAccountPasswordAsync(target.AccountId, NewPassword);
            Message = result.Error ?? $"Password reset for {target.Username}.";
            if (result.Success)
            {
                ResetTarget = null;
                NewPassword = "";
            }
        }
        catch (Exception ex)
        {
            Message = AdminFailure.Describe(ex, "Resetting the password");
        }
    }

    private async Task DeleteAsync()
    {
        var target = PendingDelete;
        if (target is null) return;
        try
        {
            var result = await AdminDatabaseService.DeleteAccountAsync(target.AccountId);
            Message = result.Error ?? $"Account {target.Username} deleted.";
            PendingDelete = null;
            if (result.Success) await RefreshAsync();
        }
        catch (Exception ex)
        {
            PendingDelete = null;
            Message = AdminFailure.Describe(ex, "Deleting the account");
        }
    }
}

