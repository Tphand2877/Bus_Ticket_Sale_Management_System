using BusTicketSaleSystem.Common;
using BusTicketSaleSystem.Services;
using System;
using System.Windows.Input;

namespace BusTicketSaleSystem.ViewModels
{
    /// <summary>
    /// ViewModel for User Account / Profile screen.
    /// Displays personal profile data and provides a placeholder for password changes.
    /// </summary>
    public class ProfileViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;

        // Personal Information
        private string _fullName = "Alex Morgan";
        private string _email = "alex.morgan@busxpress.com";
        private string _phone = "+84 912 345 678";
        private string _nationalId = "079201004821";
        private string _address = "128 Nguyen Hue Blvd, District 1, Ho Chi Minh City";
        private string _emergencyContact = "Taylor Morgan (+84 987 654 321)";

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string NationalId
        {
            get => _nationalId;
            set => SetProperty(ref _nationalId, value);
        }

        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        public string EmergencyContact
        {
            get => _emergencyContact;
            set => SetProperty(ref _emergencyContact, value);
        }

        // Account Tier info
        public string MembershipTier { get; set; } = "Gold Passenger";
        public string PointsBalance { get; set; } = "1,250 Points";
        public string MemberSince { get; set; } = "February 2025";
        public string TotalTrips { get; set; } = "14 Completed Trips";

        // Password change fields (prototype placeholders)
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;
        private string _passwordStatusMessage = string.Empty;
        private bool _isPasswordMessageSuccess = false;

        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public string PasswordStatusMessage
        {
            get => _passwordStatusMessage;
            set => SetProperty(ref _passwordStatusMessage, value);
        }

        public bool IsPasswordMessageSuccess
        {
            get => _isPasswordMessageSuccess;
            set => SetProperty(ref _isPasswordMessageSuccess, value);
        }

        private string _profileStatusMessage = string.Empty;
        public string ProfileStatusMessage
        {
            get => _profileStatusMessage;
            set => SetProperty(ref _profileStatusMessage, value);
        }

        // Commands
        public ICommand SaveProfileCommand { get; }
        public ICommand ChangePasswordCommand { get; }

        public ProfileViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            SaveProfileCommand = new RelayCommand(() =>
            {
                ProfileStatusMessage = "Profile updated successfully! (Prototype)";
            });

            ChangePasswordCommand = new RelayCommand(() =>
            {
                // Placeholder mock change password logic
                if (string.IsNullOrWhiteSpace(NewPassword) && string.IsNullOrWhiteSpace(ConfirmPassword))
                {
                    PasswordStatusMessage = "Password change simulated successfully! (Phase 1 prototype)";
                    IsPasswordMessageSuccess = true;
                }
                else if (NewPassword != ConfirmPassword)
                {
                    PasswordStatusMessage = "New passwords do not match.";
                    IsPasswordMessageSuccess = false;
                }
                else
                {
                    PasswordStatusMessage = "Password changed successfully! (Phase 1 prototype)";
                    IsPasswordMessageSuccess = true;
                    CurrentPassword = string.Empty;
                    NewPassword = string.Empty;
                    ConfirmPassword = string.Empty;
                }
            });
        }
    }
}
