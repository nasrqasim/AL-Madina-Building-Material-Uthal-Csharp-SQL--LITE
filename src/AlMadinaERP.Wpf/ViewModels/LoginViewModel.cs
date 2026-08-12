using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Wpf.Views;

namespace AlMadinaERP.Wpf.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _isPasswordVisible;

        [ObservableProperty]
        private bool _isBusy;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
        }

        [RelayCommand]
        public async Task LoginAsync(Window? window)
        {
            if (IsBusy) return;

            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both Username and Password.";
                return;
            }

            try
            {
                IsBusy = true;
                var user = await _authService.AuthenticateAsync(Username.Trim(), Password.Trim());

                if (user != null)
                {
                    // Successful login
                    var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                    window?.Close();
                }
                else
                {
                    ErrorMessage = "Invalid Username or Password. Please try again.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }
    }
}
