using BusTicketSaleSystem.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace BusTicketSaleSystem.Views;

/// <summary>
/// Interaction logic for SignUpWindow.xaml. Holds window lifetime only; validation and account
/// creation live in <see cref="SignUpViewModel"/>.
/// </summary>
public partial class SignUpWindow : Window
{
    private readonly SignUpViewModel _viewModel = new();

    /// <summary>
    /// Success notice for the sign-in screen, set when an account was created. Empty otherwise.
    /// </summary>
    public string SuccessNotice { get; private set; } = string.Empty;

    public SignUpWindow()
    {
        InitializeComponent();
        _viewModel.RegistrationSucceeded += OnRegistrationSucceeded;
        _viewModel.RegistrationCancelled += Close;
        DataContext = _viewModel;
    }

    /// <summary>
    /// WPF's <see cref="PasswordBox"/> deliberately does not expose a bindable password, so both
    /// values are pushed into the ViewModel here. This is the only logic in this code-behind.
    /// </summary>
    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;
        switch (box.Tag as string)
        {
            case "Password": _viewModel.Password = box.Password; break;
            case "ConfirmPassword": _viewModel.ConfirmPassword = box.Password; break;
        }
    }

    private void OnRegistrationSucceeded(string notice)
    {
        SuccessNotice = notice;
        TxtPassword.Clear();
        TxtConfirmPassword.Clear();
        Close();
    }
}
