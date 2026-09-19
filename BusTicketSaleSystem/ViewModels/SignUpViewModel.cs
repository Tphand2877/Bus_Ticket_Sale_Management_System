using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

/// <summary>
/// Registration screen logic. Validates input client-side, creates the account and reports the
/// outcome through <see cref="ErrorMessage"/> / <see cref="RegistrationSucceeded"/>.
/// Window lifetime stays in <c>SignUpWindow.xaml.cs</c>.
/// </summary>
public sealed partial class SignUpViewModel : ViewModelBase
{
    private const int MaxUsernameLength = 50;
    private const int MinPasswordLength = 8;

    private string _fullName = string.Empty;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string ConfirmPassword { get => _confirmPassword; set => SetProperty(ref _confirmPassword, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public ICommand CreateAccountCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>Raised with a success notice for the sign-in screen once the account exists.</summary>
    public event Action<string>? RegistrationSucceeded;

    /// <summary>Raised when the user abandons registration.</summary>
    public event Action? RegistrationCancelled;

    public SignUpViewModel()
    {
        CreateAccountCommand = new RelayCommand(async () => await CreateAccountAsync(), () => !IsBusy);
        CancelCommand = new RelayCommand(() => RegistrationCancelled?.Invoke(), () => !IsBusy);
    }

    private async Task CreateAccountAsync()
    {
        ErrorMessage = string.Empty;

        var fullName = FullName.Trim();
        var username = Username.Trim();
        var email = Email.Trim();
        var phone = Phone.Trim();

        if (!Validate(fullName, username, email, phone, out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await DatabaseHelper.CreateAccountAsync(
                username,
                Password,
                fullName,
                string.IsNullOrWhiteSpace(phone) ? null : phone,
                string.IsNullOrWhiteSpace(email) ? null : email);

            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "The account could not be created.";
                return;
            }

            LoggerService.LogInfo($"Account '{username}' was registered.");
            Password = ConfirmPassword = string.Empty;
            RegistrationSucceeded?.Invoke($"Account '{username}' was created. You can sign in now.");
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Creating account '{username}' failed.", ex);
            ErrorMessage = "Registration is unavailable right now. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool Validate(string fullName, string username, string email, string phone, out string error)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            error = "Full name is required.";
            return false;
        }
        if (fullName.Length > 100)
        {
            error = "Full name must be 100 characters or fewer.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(username))
        {
            error = "Username is required.";
            return false;
        }
        if (username.Length > MaxUsernameLength || username.Any(char.IsWhiteSpace))
        {
            error = $"Username must be {MaxUsernameLength} characters or fewer and contain no spaces.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            error = "Password is required.";
            return false;
        }
        if (Password.Length < MinPasswordLength || Password.Length > 128 || !Password.Any(char.IsUpper) || !Password.Any(char.IsLower) || !Password.Any(char.IsDigit))
        {
            error = "Password must be 8-128 characters and include an uppercase letter, a lowercase letter and a digit.";
            return false;
        }
        if (Password != ConfirmPassword)
        {
            error = "The confirmation password does not match.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email))
        {
            error = "Please enter a valid email address.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(phone) && !PhonePattern().IsMatch(phone))
        {
            error = "Please enter a valid phone number (8 to 15 digits, optional leading +).";
            return false;
        }

        error = string.Empty;
        return true;
    }

    [GeneratedRegex(@"^\+?[0-9]{8,15}$")]
    private static partial Regex PhonePattern();
}
