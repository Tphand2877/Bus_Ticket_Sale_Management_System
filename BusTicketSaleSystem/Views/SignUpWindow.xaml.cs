using BusTicketSaleSystem.Data;
using System.ComponentModel.DataAnnotations;
using System.Windows;

namespace BusTicketSaleSystem.Views;

public partial class SignUpWindow : Window
{
    public SignUpWindow() => InitializeComponent();

    private async void CreateAccountButton_Click(object sender, RoutedEventArgs e)
    {
        var fullName=TxtFullName.Text.Trim(); var username=TxtUsername.Text.Trim(); var email=TxtEmail.Text.Trim(); var phone=TxtPhone.Text.Trim(); var password=TxtPassword.Password;
        if (string.IsNullOrWhiteSpace(fullName)||string.IsNullOrWhiteSpace(username)||string.IsNullOrWhiteSpace(password)){ShowError("Full name, username và password là bắt buộc.");return;}
        if (username.Length>50||username.Any(char.IsWhiteSpace)){ShowError("Username tối đa 50 ký tự và không chứa khoảng trắng.");return;}
        if (password.Length<8){ShowError("Mật khẩu phải có ít nhất 8 ký tự.");return;}
        if (!string.IsNullOrWhiteSpace(email)&&!new EmailAddressAttribute().IsValid(email)){ShowError("Email không hợp lệ.");return;}
        if (password!=TxtConfirmPassword.Password){ShowError("Mật khẩu xác nhận không khớp.");return;}
        BtnCreate.IsEnabled=false; TxtError.Visibility=Visibility.Collapsed;
        var result=await DatabaseHelper.CreateAccountAsync(username,password,fullName,string.IsNullOrWhiteSpace(phone)?null:phone,string.IsNullOrWhiteSpace(email)?null:email);
        if(!result.Success){ShowError(result.Error??"Không thể tạo tài khoản.");BtnCreate.IsEnabled=true;return;}
        MessageBox.Show("Tạo tài khoản thành công. Bạn có thể đăng nhập ngay bây giờ.","Success",MessageBoxButton.OK,MessageBoxImage.Information);Close();
    }
    private void ShowError(string message){TxtError.Text=message;TxtError.Visibility=Visibility.Visible;}
}
