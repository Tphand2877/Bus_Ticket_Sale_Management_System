using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.ComponentModel.DataAnnotations;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class ProfileViewModel : ViewModelBase, IRefreshable
{
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _profileStatusMessage = string.Empty;
    private string _passwordStatusMessage = string.Empty;
    private bool _isBusy;

    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public string ProfileStatusMessage { get => _profileStatusMessage; private set => SetProperty(ref _profileStatusMessage, value); }
    public string PasswordStatusMessage { get => _passwordStatusMessage; private set => SetProperty(ref _passwordStatusMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public string Username => CurrentUser.Account?.Username ?? string.Empty;
    public string AccountRole => CurrentUser.Account?.Role ?? "Customer";
    public bool IsVietnamese => LocalizationService.Instance.IsVietnamese;
    public bool IsEnglish => LocalizationService.Instance.IsEnglish;
    public string CurrentLanguageName => IsVietnamese ? "Tiếng Việt" : "English";

    public ICommand SaveProfileCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand SelectVietnameseCommand { get; }
    public ICommand SelectEnglishCommand { get; }
    public ICommand ToggleLanguageCommand { get; }

    /// <summary>
    /// Raised after a successful password change. <c>PasswordBox.Password</c> is deliberately
    /// not a bindable DependencyProperty, so clearing the ViewModel strings does not clear the
    /// on-screen dots; the view handles this event to reset the boxes.
    /// </summary>
    public event Action? PasswordFieldsCleared;
    public ProfileViewModel(INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        SaveProfileCommand = new RelayCommand(async () => await SaveProfileAsync(), () => !IsBusy);
        ChangePasswordCommand = new RelayCommand(async () => await ChangePasswordAsync(), () => !IsBusy);
        SelectVietnameseCommand = new RelayCommand(() => LocalizationService.Instance.SetLanguage(LocalizationService.Vietnamese));
        SelectEnglishCommand = new RelayCommand(() => LocalizationService.Instance.SetLanguage(LocalizationService.English));
        ToggleLanguageCommand = new RelayCommand(() => LocalizationService.Instance.ToggleLanguage());
        LocalizationService.Instance.LanguageChanged += _ =>
        {
            OnPropertyChanged(nameof(IsVietnamese));
            OnPropertyChanged(nameof(IsEnglish));
            OnPropertyChanged(nameof(CurrentLanguageName));
        };
    }
    /// <summary>Re-reads the signed-in account so the form never shows values from an earlier session.</summary>
    public Task RefreshAsync()
    {
        FullName = CurrentUser.Account?.FullName ?? string.Empty;
        Email = CurrentUser.Account?.Email ?? string.Empty;
        Phone = CurrentUser.Account?.Phone ?? string.Empty;
        CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        ProfileStatusMessage = string.Empty;
        PasswordStatusMessage = string.Empty;
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(AccountRole));
        return Task.CompletedTask;
    }

    private async Task SaveProfileAsync()
    {
        if (CurrentUser.Account is null) { ProfileStatusMessage = "Your session has expired. Please sign in again."; return; }
        if (string.IsNullOrWhiteSpace(FullName)) { ProfileStatusMessage = "Full name is required."; return; }
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email)) { ProfileStatusMessage = "Please enter a valid email address."; return; }

        IsBusy = true;
        try
        {
            var result = await DatabaseHelper.UpdateProfileAsync(
                CurrentUser.Account,
                FullName.Trim(),
                string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim());

            if (!result.Success || result.Account is null)
            {
                ProfileStatusMessage = result.Error ?? "Your profile could not be updated.";
                return;
            }

            CurrentUser.Account = result.Account;
            // Success goes to the toast; the inline banners carry failures only, so their red
            // styling is always correct. Previously one success was rendered in danger-red.
            NotificationService.Instance.ShowSuccess(LocalizationService.Instance.GetString("ToastProfileUpdated"));
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Updating the account profile failed.", ex);
            ProfileStatusMessage = "Your profile could not be updated. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ChangePasswordAsync()
    {
        PasswordStatusMessage = string.Empty;
        if (CurrentUser.Account is null) { PasswordStatusMessage = "Your session has expired. Please sign in again."; return; }
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            PasswordStatusMessage = "Please fill in all three password fields.";
            return;
        }
        if (NewPassword.Length < 8) { PasswordStatusMessage = "The new password must be at least 8 characters long."; return; }
        if (NewPassword != ConfirmPassword) { PasswordStatusMessage = "The confirmation password does not match."; return; }

        IsBusy = true;
        try
        {
            var result = await DatabaseHelper.ChangePasswordAsync(CurrentUser.Account.AccountId, CurrentPassword, NewPassword);
            if (result.Success)
            {
                NotificationService.Instance.ShowSuccess(LocalizationService.Instance.GetString("ToastPasswordChanged"));
                CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
                // PasswordBox.Password is not bindable, so the view must be told to clear it —
                // otherwise the boxes keep showing dots while the ViewModel is already empty.
                PasswordFieldsCleared?.Invoke();
            }
            else
            {
                PasswordStatusMessage = result.Error ?? "Your password could not be changed.";
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError("Changing the account password failed.", ex);
            PasswordStatusMessage = "Your password could not be changed. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
