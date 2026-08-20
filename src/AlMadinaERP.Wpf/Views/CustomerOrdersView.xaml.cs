using System.Windows.Controls;
using AlMadinaERP.Wpf.ViewModels;

namespace AlMadinaERP.Wpf.Views
{
    public partial class CustomerOrdersView : UserControl
    {
        public CustomerOrdersView()
        {
            InitializeComponent();
        }

        public CustomerOrdersView(CustomerOrdersViewModel viewModel) : this()
        {
            DataContext = viewModel;
            Loaded += async (s, e) =>
            {
                if (DataContext is CustomerOrdersViewModel vm)
                {
                    await vm.LoadOrdersAsync();
                }
            };
        }
    }
}
