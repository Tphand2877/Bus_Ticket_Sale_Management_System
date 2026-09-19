using BusTicketSaleSystem.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace BusTicketSaleSystem.Views;

public partial class ProfileView : UserControl
{
    private ProfileViewModel? _subscribed;

    public ProfileView()
    {
        InitializeComponent();
        // The shell assigns the DataContext after construction, so subscribe on change.
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        if (e.NewValue is ProfileViewModel model)
        {
            model.PasswordFieldsCleared += ClearPasswordBoxes;
            _subscribed = model;
        }
    }

    private void Detach()
    {
        if (_subscribed is null) return;
        _subscribed.PasswordFieldsCleared -= ClearPasswordBoxes;
        _subscribed = null;
    }

    /// <summary>
    /// PasswordBox.Password cannot be data-bound, so a successful change has to clear the
    /// controls explicitly; otherwise the fields still show dots for the old password.
    /// </summary>
    private void ClearPasswordBoxes()
    {
        CurrentPasswordBox.Clear();
        NewPasswordBox.Clear();
        ConfirmPasswordBox.Clear();
    }

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
