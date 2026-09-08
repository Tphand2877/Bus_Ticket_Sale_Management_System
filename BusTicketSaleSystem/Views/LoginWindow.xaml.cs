using System.Windows;

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

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Simple validation: fields not empty check
            if (string.IsNullOrWhiteSpace(TxtUsername.Text))
            {
                TxtError.Text = "Please enter a username or email address.";
                TxtError.Visibility = Visibility.Visible;
                return;
            }

            // Create and show MainWindow
            var mainWindow = new MainWindow();
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();

            // Close LoginWindow
            this.Close();
        }
    }
}
