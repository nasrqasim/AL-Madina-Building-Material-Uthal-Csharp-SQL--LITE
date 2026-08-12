using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using AlMadinaERP.Wpf.ViewModels;

namespace AlMadinaERP.Wpf.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            if (App.ServiceProvider != null)
            {
                var viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
                DataContext = viewModel;
                _ = viewModel.DashboardVM.LoadDashboardAsync();
            }
        }

        private void HeaderBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
