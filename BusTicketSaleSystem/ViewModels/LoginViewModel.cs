using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.Services;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels;

/// <summary>
/// Sign-in screen logic. Owns credential validation, authentication and role routing decisions.
/// Window lifetime is intentionally left to <c>LoginWindow.xaml.cs</c>, which subscribes to
/// <see cref="LoginSucceeded"/> and <see cref="SignUpRequested"/>.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private string _usernameOrEmail = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public string UsernameOrEmail { get => _usernameOrEmail; set => SetProperty(ref _usernameOrEmail, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool IsVietnamese => LocalizationService.Instance.IsVietnamese;
    public bool IsEnglish => LocalizationService.Instance.IsEnglish;
    public string CurrentLanguageName => IsVietnamese ? "Tiếng Việt" : "English";

    public ICommand SignInCommand { get; }
    public ICommand GoToSignUpCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand SetVietnameseCommand { get; }
    public ICommand SetEnglishCommand { get; }
    /// <summary>Raised with the authenticated session once sign-in succeeds.</summary>
    public event Action<UserSession>? LoginSucceeded;

    /// <summary>Raised when the user asks for the registration screen.</summary>
    public event Action? SignUpRequested;

    public LoginViewModel()
    {
        SignInCommand = new AsyncRelayCommand(async () => await SignInAsync(), () => !IsBusy);
        GoToSignUpCommand = new RelayCommand(() =>
        {
            ErrorMessage = string.Empty;
            SignUpRequested?.Invoke();
        }, () => !IsBusy);
        ToggleLanguageCommand = new RelayCommand(() => LocalizationService.Instance.ToggleLanguage());
        SetVietnameseCommand = new RelayCommand(() => LocalizationService.Instance.SetLanguage(LocalizationService.Vietnamese));
        SetEnglishCommand = new RelayCommand(() => LocalizationService.Instance.SetLanguage(LocalizationService.English));
        LocalizationService.Instance.LanguageChanged += _ =>
        {
            OnPropertyChanged(nameof(IsVietnamese));
            OnPropertyChanged(nameof(IsEnglish));
            OnPropertyChanged(nameof(CurrentLanguageName));
        };
    }
    private async Task SignInAsync()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        var login = UsernameOrEmail.Trim();
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your username or email and your password.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await DatabaseHelper.AuthenticateAsync(login, Password);
            if (!result.Success || result.Account is null)
            {
                ErrorMessage = result.Error ?? "Sign-in failed. Please check your credentials.";
                return;
            }

            CurrentUser.Account = result.Account;
            Password = string.Empty;
            LoggerService.LogInfo($"Account '{result.Account.Username}' signed in as {result.Account.Role}.");
            LoginSucceeded?.Invoke(result.Account);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Authentication failed for login '{login}'.", ex);
            ErrorMessage = "Sign-in is unavailable right now. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
