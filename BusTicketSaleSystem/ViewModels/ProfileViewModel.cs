using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

public sealed class ProfileViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private string _fullName = CurrentUser.Account?.FullName ?? string.Empty;
    private string _email = CurrentUser.Account?.Email ?? string.Empty;
    private string _phone = CurrentUser.Account?.Phone ?? string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _profileStatusMessage = string.Empty;
    private string _passwordStatusMessage = string.Empty;

    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string CurrentPassword { get => _currentPassword; set => SetProperty(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public string ProfileStatusMessage { get => _profileStatusMessage; private set => SetProperty(ref _profileStatusMessage, value); }
    public string PasswordStatusMessage { get => _passwordStatusMessage; private set => SetProperty(ref _passwordStatusMessage, value); }
    public string MembershipTier => "Passenger";
    public ICommand SaveProfileCommand { get; }
    public ICommand ChangePasswordCommand { get; }

    public ProfileViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        SaveProfileCommand = new RelayCommand(async () => await SaveProfileAsync());
        ChangePasswordCommand = new RelayCommand(async () => await ChangePasswordAsync());
    }

    private async Task SaveProfileAsync()
    {
        if (CurrentUser.Account is null) { ProfileStatusMessage = "Phiên đăng nhập đã hết hạn."; return; }
        if (string.IsNullOrWhiteSpace(FullName)) { ProfileStatusMessage = "Họ tên không được để trống."; return; }
        if (!string.IsNullOrWhiteSpace(Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(Email)) { ProfileStatusMessage = "Email không hợp lệ."; return; }
        var result = await DatabaseHelper.UpdateProfileAsync(CurrentUser.Account, FullName.Trim(), string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(), string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim());
        if (!result.Success || result.Account is null) { ProfileStatusMessage = result.Error ?? "Không thể cập nhật."; return; }
        CurrentUser.Account = result.Account;
        ProfileStatusMessage = "Đã cập nhật thông tin tài khoản.";
    }

    private async Task ChangePasswordAsync()
    {
        PasswordStatusMessage = string.Empty;
        if (CurrentUser.Account is null) { PasswordStatusMessage = "Phiên đăng nhập đã hết hạn."; return; }
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword)) { PasswordStatusMessage = "Vui lòng nhập đầy đủ các trường mật khẩu."; return; }
        if (NewPassword.Length < 8) { PasswordStatusMessage = "Mật khẩu mới phải có ít nhất 8 ký tự."; return; }
        if (NewPassword != ConfirmPassword) { PasswordStatusMessage = "Mật khẩu xác nhận không khớp."; return; }
        var result = await DatabaseHelper.ChangePasswordAsync(CurrentUser.Account.AccountId, CurrentPassword, NewPassword);
        PasswordStatusMessage = result.Success ? "Đã đổi mật khẩu thành công." : result.Error ?? "Không thể đổi mật khẩu.";
        if (result.Success) CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
    }
}
