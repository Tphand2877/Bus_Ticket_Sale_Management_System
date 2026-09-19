using BusTicketSaleSystem.Models;
using BusTicketSaleSystem.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace BusTicketSaleSystem.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml.
    /// Runs as the initial window and owns nothing but window lifetime: authentication and role
    /// routing decisions live in <see cref="LoginViewModel"/>.
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel = new();

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel.LoginSucceeded += OnLoginSucceeded;
            _viewModel.SignUpRequested += OnSignUpRequested;
            DataContext = _viewModel;

            // Put the caret in the first field so the user can start typing immediately instead
            // of having to click or Tab into it.
            Loaded += (_, _) => TxtUsername.Focus();
        }

        /// <summary>
        /// WPF's <see cref="PasswordBox"/> deliberately does not expose a bindable password, so the
        /// value is pushed into the ViewModel here. This is the only logic in this code-behind
        /// besides window lifetime.
        /// </summary>
        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox box) _viewModel.Password = box.Password;
        }

        private void OnLoginSucceeded(UserSession account)
        {
            TxtPassword.Clear();

            Window next = string.Equals(account.Role, "Admin", StringComparison.OrdinalIgnoreCase)
                ? new AdminWindow()
                : new MainWindow();

            Application.Current.MainWindow = next;
            next.Show();
            Close();
        }

        private void OnSignUpRequested()
        {
            var signUpWindow = new SignUpWindow { Owner = this };
            signUpWindow.ShowDialog();
            if (signUpWindow.SuccessNotice is { Length: > 0 } notice) _viewModel.StatusMessage = notice;
        }
    }
}
