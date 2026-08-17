using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;

namespace AlMadinaERP.Wpf.ViewModels
{
    public partial class CustomerOrdersViewModel : ObservableObject
    {
        private readonly ICustomerOrderService _orderService;
        private readonly IInventoryService _inventoryService;
        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        [ObservableProperty]
        private ObservableCollection<CustomerOrder> _orders = new();

        [ObservableProperty]
        private ObservableCollection<Item> _masterItems = new();

        [ObservableProperty]
        private Item? _selectedMasterItem;

        partial void OnSelectedMasterItemChanged(Item? value)
        {
            if (value != null)
            {
                NewItemName = value.Name ?? string.Empty;
                NewItemCode = value.Code ?? string.Empty;
                NewItemUnit = !string.IsNullOrWhiteSpace(value.SaleUnitName) ? value.SaleUnitName : (!string.IsNullOrWhiteSpace(value.SellingUnit) ? value.SellingUnit : "Pcs");
                NewItemRate = value.SellingPrice > 0 ? value.SellingPrice : (value.WholesalePrice > 0 ? value.WholesalePrice : 0m);
                if (NewItemQuantity <= 0) NewItemQuantity = 1m;
                CalculateLineTotal();
            }
        }

        [ObservableProperty]
        private CustomerOrder? _selectedOrder;

        [ObservableProperty]
        private CustomerOrder _currentOrder = new();

        [ObservableProperty]
        private ObservableCollection<CustomerOrderItem> _currentOrderItems = new();

        // Form Fields
        [ObservableProperty]
        private string _formCustomerName = string.Empty;

        [ObservableProperty]
        private string _formAddress = string.Empty;

        [ObservableProperty]
        private string _formContactNumber = string.Empty;

        [ObservableProperty]
        private DateTime _formOrderDate = DateTime.Now;

        [ObservableProperty]
        private DateTime? _formReceivingDate = DateTime.Now.AddDays(1);

        [ObservableProperty]
        private string _formStatus = "Pending";

        // Order Item Line Fields
        [ObservableProperty]
        private string _newItemName = string.Empty;

        [ObservableProperty]
        private string _newItemCode = string.Empty;

        [ObservableProperty]
        private string _newItemUnit = "Pcs";

        [ObservableProperty]
        private decimal _newItemQuantity = 1m;

        partial void OnNewItemQuantityChanged(decimal value) => CalculateLineTotal();

        [ObservableProperty]
        private decimal _newItemRate = 0m;

        partial void OnNewItemRateChanged(decimal value) => CalculateLineTotal();

        [ObservableProperty]
        private decimal _newItemLineTotal = 0m;

        private void CalculateLineTotal()
        {
            NewItemLineTotal = Math.Max(0m, NewItemQuantity) * Math.Max(0m, NewItemRate);
        }

        [ObservableProperty]
        private decimal _formTotalAmount = 0m;

        // Modal visibility
        [ObservableProperty]
        private bool _isOrderModalOpen;

        [ObservableProperty]
        private bool _isViewModalOpen;

        [ObservableProperty]
        private string _modalTitle = "New Customer Order";

        // Summary Calculations
        [ObservableProperty]
        private int _totalOrdersCount;

        [ObservableProperty]
        private int _pendingOrdersCount;

        [ObservableProperty]
        private int _completedOrdersCount;

        [ObservableProperty]
        private decimal _totalOrdersAmount;

        // Filter and Search
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        partial void OnSearchQueryChanged(string value)
        {
            _ = LoadOrdersAsync();
        }

        [ObservableProperty]
        private string _selectedStatusFilter = "All";

        public CustomerOrdersViewModel(
            ICustomerOrderService orderService,
            IInventoryService inventoryService,
            IPrintService printService,
            IRepository<CompanySetting> companyRepo)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _printService = printService;
            _companyRepo = companyRepo;
        }

        [RelayCommand]
        public async Task LoadOrdersAsync()
        {
            var list = await _orderService.GetCustomerOrdersAsync(SearchQuery, SelectedStatusFilter);

            Orders.Clear();
            foreach (var order in list)
            {
                Orders.Add(order);
            }

            // Recalculate summary metrics from ALL customer orders
            var allOrders = await _orderService.GetCustomerOrdersAsync("", "All");
            TotalOrdersCount = allOrders.Count;
            PendingOrdersCount = allOrders.Count(o => o.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));
            CompletedOrdersCount = allOrders.Count(o => o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase));
            TotalOrdersAmount = allOrders.Sum(o => o.TotalAmount);

            // Load master items if not loaded
            if (MasterItems.Count == 0)
            {
                var items = await _inventoryService.SearchItemsAsync("");
                MasterItems.Clear();
                foreach (var item in items)
                {
                    MasterItems.Add(item);
                }
            }
        }

        [RelayCommand]
        public async Task FilterStatusAsync(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) status = "All";
            SelectedStatusFilter = status;
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task OpenNewOrderModalAsync()
        {
            ModalTitle = "New Customer Order";
            var nextNum = await _orderService.GenerateNextOrderNumberAsync();

            CurrentOrder = new CustomerOrder
            {
                OrderNumber = nextNum,
                OrderDate = DateTime.Now,
                ReceivingDate = DateTime.Now.AddDays(1),
                Status = "Pending"
            };

            FormCustomerName = string.Empty;
            FormAddress = string.Empty;
            FormContactNumber = string.Empty;
            FormOrderDate = DateTime.Now;
            FormReceivingDate = DateTime.Now.AddDays(1);
            FormStatus = "Pending";

            CurrentOrderItems.Clear();
            FormTotalAmount = 0m;
            ResetItemLineInputs();

            IsOrderModalOpen = true;
        }

        [RelayCommand]
        public void CloseOrderModal()
        {
            IsOrderModalOpen = false;
        }

        [RelayCommand]
        public void CloseViewModal()
        {
            IsViewModalOpen = false;
        }

        [RelayCommand]
        public void AddItemLine()
        {
            if (string.IsNullOrWhiteSpace(NewItemName))
            {
                MessageBox.Show("Please select an item or enter an item name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewItemQuantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var line = new CustomerOrderItem
            {
                ItemId = SelectedMasterItem?.Id,
                ItemNameSnapshot = NewItemName.Trim(),
                ItemCode = SelectedMasterItem?.Code ?? NewItemCode ?? string.Empty,
                Unit = string.IsNullOrWhiteSpace(NewItemUnit) ? "Pcs" : NewItemUnit.Trim(),
                Quantity = NewItemQuantity,
                Rate = Math.Max(0m, NewItemRate),
                LineTotal = NewItemQuantity * Math.Max(0m, NewItemRate)
            };

            CurrentOrderItems.Add(line);
            RecalculateFormTotal();
            ResetItemLineInputs();
        }

        [RelayCommand]
        public void RemoveItemLine(CustomerOrderItem line)
        {
            if (line != null && CurrentOrderItems.Contains(line))
            {
                CurrentOrderItems.Remove(line);
                RecalculateFormTotal();
            }
        }

        private void ResetItemLineInputs()
        {
            SelectedMasterItem = null;
            NewItemName = string.Empty;
            NewItemCode = string.Empty;
            NewItemUnit = "Pcs";
            NewItemQuantity = 1m;
            NewItemRate = 0m;
            NewItemLineTotal = 0m;
        }

        public void RecalculateFormTotal()
        {
            FormTotalAmount = CurrentOrderItems.Sum(i => i.LineTotal);
        }

        [RelayCommand]
        public async Task SaveOrderAsync()
        {
            CurrentOrder.CustomerName = FormCustomerName?.Trim() ?? string.Empty;
            CurrentOrder.Address = FormAddress?.Trim() ?? string.Empty;
            CurrentOrder.ContactNumber = FormContactNumber?.Trim() ?? string.Empty;
            CurrentOrder.OrderDate = FormOrderDate;
            CurrentOrder.ReceivingDate = FormReceivingDate;
            CurrentOrder.Status = string.IsNullOrWhiteSpace(FormStatus) ? "Pending" : FormStatus;

            CurrentOrder.Items = new ObservableCollection<CustomerOrderItem>(CurrentOrderItems);

            await _orderService.SaveCustomerOrderAsync(CurrentOrder);

            IsOrderModalOpen = false;
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public void EditOrder(CustomerOrder order)
        {
            if (order == null) return;

            ModalTitle = $"Edit Customer Order ({order.OrderNumber})";
            CurrentOrder = order;

            FormCustomerName = order.CustomerName ?? string.Empty;
            FormAddress = order.Address ?? string.Empty;
            FormContactNumber = order.ContactNumber ?? string.Empty;
            FormOrderDate = order.OrderDate;
            FormReceivingDate = order.ReceivingDate;
            FormStatus = order.Status ?? "Pending";

            CurrentOrderItems = new ObservableCollection<CustomerOrderItem>(
                order.Items.Select(i => new CustomerOrderItem
                {
                    Id = i.Id,
                    CustomerOrderId = i.CustomerOrderId,
                    ItemId = i.ItemId,
                    ItemNameSnapshot = i.ItemNameSnapshot,
                    ItemCode = i.ItemCode,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    Rate = i.Rate,
                    LineTotal = i.LineTotal
                })
            );

            RecalculateFormTotal();
            ResetItemLineInputs();

            IsOrderModalOpen = true;
        }

        [RelayCommand]
        public void ViewOrder(CustomerOrder order)
        {
            if (order == null) return;
            SelectedOrder = order;
            IsViewModalOpen = true;
        }

        [RelayCommand]
        public async Task DeleteOrderAsync(CustomerOrder order)
        {
            if (order == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete Customer Order '{order.OrderNumber}' (Customer: {(string.IsNullOrWhiteSpace(order.CustomerName) ? "Unspecified" : order.CustomerName)})?\n\nThis action cannot be undone.",
                "Confirm Delete Customer Order",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                await _orderService.DeleteCustomerOrderAsync(order.Id);
                await LoadOrdersAsync();
            }
        }

        [RelayCommand]
        public async Task ToggleOrderStatusAsync(CustomerOrder order)
        {
            if (order == null) return;
            await _orderService.ToggleOrderStatusAsync(order.Id);
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task PrintOrdersListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Sr #", "Order No.", "Customer Name", "Contact", "Address", "Order Date", "Receiving Date", "Total (PKR)", "Status" };

            int sr = 1;
            var rows = Orders.Select(o => new[]
            {
                sr++.ToString(),
                o.OrderNumber ?? "",
                o.CustomerName ?? "-",
                o.ContactNumber ?? "-",
                o.Address ?? "-",
                o.OrderDate.ToString("dd/MM/yyyy"),
                o.ReceivingDate.HasValue ? o.ReceivingDate.Value.ToString("dd/MM/yyyy") : "-",
                $"Rs. {o.TotalAmount:N2}",
                o.Status ?? "Pending"
            });

            var totals = new[]
            {
                "TOTAL",
                $"{Orders.Count} Orders",
                "",
                "",
                "",
                "",
                "",
                $"Total: Rs. {Orders.Sum(o => o.TotalAmount):N2}",
                $"{Orders.Count(o => o.Status == "Pending")} Pending / {Orders.Count(o => o.Status == "Completed")} Done"
            };

            _printService.PrintReportTable("Customer Orders Summary Directory", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintCustomerOrderAsync(CustomerOrder? order)
        {
            order ??= SelectedOrder;
            if (order == null) return;

            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintCustomerOrder(order, company);
        }
    }
}
