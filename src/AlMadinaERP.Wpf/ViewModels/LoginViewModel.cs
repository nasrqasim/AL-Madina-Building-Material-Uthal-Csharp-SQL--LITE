using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Services;
using AlMadinaERP.Wpf.Views;

namespace AlMadinaERP.Wpf.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _username = "Superadmin";

        [ObservableProperty]
        private string _password = "admin1234";

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
            IsBusy = true; // Synchronously set before any await to prevent double execution on rapid clicks

            ErrorMessage = string.Empty;

            var uStr = string.IsNullOrWhiteSpace(Username) ? "Superadmin" : Username.Trim();
            var pStr = string.IsNullOrWhiteSpace(Password) ? "admin1234" : Password.Trim();

            try
            {
                var user = await _authService.AuthenticateAsync(uStr, pStr);

                if (user == null)
                {
                    ErrorMessage = "Invalid username or password.";
                    IsBusy = false;
                    return;
                }

                var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();

                if (window != null)
                {
                    window.Close();
                }
                else
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is LoginWindow)
                        {
                            win.Close();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login Error: {ex.Message}";
                MessageBox.Show($"Login Exception: {ex.Message}\n\n{ex.StackTrace}", "Login Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
