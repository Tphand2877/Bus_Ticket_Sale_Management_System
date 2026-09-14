using BusTicketSaleSystem.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace BusTicketSaleSystem.Views;

public partial class ProfileView : UserControl
{
    public ProfileView() => InitializeComponent();

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProfileViewModel model || sender is not PasswordBox box) return;
        switch (box.Tag as string)
        {
            case "CurrentPassword": model.CurrentPassword = box.Password; break;
            case "NewPassword": model.NewPassword = box.Password; break;
            case "ConfirmPassword": model.ConfirmPassword = box.Password; break;
        }
    }
}
