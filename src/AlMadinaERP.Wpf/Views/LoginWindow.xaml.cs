using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AlMadinaERP.Wpf.ViewModels;

namespace AlMadinaERP.Wpf.Views
{
    public partial class LoginWindow : Window
    {
        private bool _isSyncingPassword = false;

        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            if (viewModel != null)
            {
                if (string.IsNullOrWhiteSpace(viewModel.Password))
                {
                    viewModel.Password = "12345";
                }
                TxtPasswordBox.Password = viewModel.Password;
                TxtVisiblePasswordBox.Text = viewModel.Password;

                viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(LoginViewModel.Password))
                    {
                        if (!_isSyncingPassword && TxtPasswordBox != null && TxtPasswordBox.Password != viewModel.Password)
                        {
                            _isSyncingPassword = true;
                            TxtPasswordBox.Password = viewModel.Password ?? string.Empty;
                            TxtVisiblePasswordBox.Text = viewModel.Password ?? string.Empty;
                            _isSyncingPassword = false;
                        }
                    }
                };
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                if (string.IsNullOrWhiteSpace(vm.Password))
                {
                    vm.Password = !string.IsNullOrWhiteSpace(TxtPasswordBox.Password) ? TxtPasswordBox.Password : TxtVisiblePasswordBox.Text;
                }
                if (string.IsNullOrWhiteSpace(vm.Password))
                {
                    vm.Password = "12345";
                }
                await vm.LoginAsync(this);
            }
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.IsPasswordVisible = !vm.IsPasswordVisible;
                if (vm.IsPasswordVisible)
                {
                    TxtVisiblePasswordBox.Text = TxtPasswordBox.Password;
                    vm.Password = TxtPasswordBox.Password;
                }
                else
                {
                    TxtPasswordBox.Password = TxtVisiblePasswordBox.Text;
                    vm.Password = TxtVisiblePasswordBox.Text;
                }
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncingPassword) return;

            if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            {
                _isSyncingPassword = true;
                vm.Password = pb.Password;
                _isSyncingPassword = false;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
