using System.Windows;
using BusTicketSaleSystem.Data;
using BusTicketSaleSystem.Models;

namespace BusTicketSaleSystem.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// Runs as a separate initial window before MainWindow is opened.
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var usernameOrEmail = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                TxtError.Text = "Vui lòng nhập tên đăng nhập/email và mật khẩu.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtError.Visibility = Visibility.Collapsed;

            var loginResult = await DatabaseHelper.AuthenticateAsync(usernameOrEmail, password);
            if (!loginResult.Success)
            {
                TxtError.Text = loginResult.Error;
                TxtError.Visibility = Visibility.Visible;
                BtnLogin.IsEnabled = true;
                return;
            }

            CurrentUser.Account = loginResult.Account;
            if (CurrentUser.IsAdmin)
            {
                var adminWindow = new AdminWindow();
                Application.Current.MainWindow = adminWindow;
                adminWindow.Show();
                Close();
                return;
            }

            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();
            Close();
        }

        private void SignUpButton_Click(object sender, RoutedEventArgs e)
        {
            var signUpWindow = new SignUpWindow { Owner = this };
            signUpWindow.ShowDialog();
        }
    }
}
