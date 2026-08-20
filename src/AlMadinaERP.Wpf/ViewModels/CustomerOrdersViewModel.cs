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
        private readonly ICustomerService _customerService;
        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        [ObservableProperty]
        private ObservableCollection<CustomerOrder> _orders = new();

        [ObservableProperty]
        private ObservableCollection<CustomerOrder> _filteredOrders = new();

        [ObservableProperty]
        private ObservableCollection<Item> _masterItems = new();

        [ObservableProperty]
        private ObservableCollection<Customer> _masterCustomers = new();

        [ObservableProperty]
        private CustomerOrder? _selectedOrder;

        [ObservableProperty]
        private CustomerOrder _currentOrder = new();

        [ObservableProperty]
        private ObservableCollection<CustomerOrderItem> _currentOrderItems = new();

        // Search and Filters
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedStatusFilter = "All"; // All, Pending, Completed

        // Summary Cards
        [ObservableProperty]
        private int _totalOrdersCount = 0;

        [ObservableProperty]
        private int _pendingOrdersCount = 0;

        [ObservableProperty]
        private int _completedOrdersCount = 0;

        [ObservableProperty]
        private decimal _totalOrdersAmount = 0m;

        [ObservableProperty]
        private decimal _totalPaidAmount = 0m;

        [ObservableProperty]
        private decimal _totalRemainingAmount = 0m;

        // Modal States
        [ObservableProperty]
        private bool _isOrderModalOpen = false;

        [ObservableProperty]
        private bool _isViewModalOpen = false;

        [ObservableProperty]
        private string _modalTitle = "New Customer Order";

        // Form Fields
        [ObservableProperty]
        private string _formCustomerName = string.Empty;

        [ObservableProperty]
        private Customer? _formSelectedCustomer;

        partial void OnFormSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                FormCustomerName = value.Name;
                FormAddress = value.Address ?? string.Empty;
                FormContactNumber = value.Phone ?? string.Empty;
            }
        }

        [ObservableProperty]
        private string _formAddress = string.Empty;

        [ObservableProperty]
        private string _formContactNumber = string.Empty;

        [ObservableProperty]
        private DateTime _formOrderDate = DateTime.Now;

        [ObservableProperty]
        private DateTime _formReceivingDate = DateTime.Now.AddDays(1);

        [ObservableProperty]
        private string _formStatus = "Pending";

        [ObservableProperty]
        private decimal _formTotalAmount = 0m;

        [ObservableProperty]
        private decimal _formPaidAmount = 0m;

        partial void OnFormPaidAmountChanged(decimal value)
        {
            OnPropertyChanged(nameof(FormRemainingAmount));
        }

        public decimal FormRemainingAmount => Math.Max(0m, FormTotalAmount - FormPaidAmount);

        // Line Item Entry Fields
        [ObservableProperty]
        private Item? _selectedMasterItem;

        partial void OnSelectedMasterItemChanged(Item? value)
        {
            if (value != null)
            {
                NewItemName = value.Name;
                NewItemCode = value.Code;
                NewItemUnit = !string.IsNullOrWhiteSpace(value.SaleUnitName) ? value.SaleUnitName : (value.SellingUnit ?? "Pcs");
                NewItemRate = value.SalePrice > 0 ? value.SalePrice : value.PurchasePrice;
                
                var nameLower = value.Name.ToLower();
                var unitLower = NewItemUnit.ToLower();
                IsLengthInputVisible = nameLower.Contains("tear") || nameLower.Contains("girder") ||
                                       unitLower.Contains("feet") || unitLower.Contains("foot");
                if (IsLengthInputVisible && NewItemLengthFeet <= 0)
                {
                    NewItemLengthFeet = nameLower.Contains("tear") ? 20.0 : (nameLower.Contains("girder") ? 15.0 : 10.0);
                }
                RecalculateLineInputTotal();
            }
        }

        [ObservableProperty]
        private string _newItemName = string.Empty;

        [ObservableProperty]
        private string _newItemCode = string.Empty;

        [ObservableProperty]
        private string _newItemUnit = "Pcs";

        [ObservableProperty]
        private decimal _newItemQuantity = 1m;

        partial void OnNewItemQuantityChanged(decimal value) => RecalculateLineInputTotal();

        [ObservableProperty]
        private double _newItemLengthFeet = 0.0;

        partial void OnNewItemLengthFeetChanged(double value) => RecalculateLineInputTotal();

        [ObservableProperty]
        private decimal _newItemRate = 0m;

        partial void OnNewItemRateChanged(decimal value) => RecalculateLineInputTotal();

        [ObservableProperty]
        private decimal _newItemLineTotal = 0m;

        [ObservableProperty]
        private bool _isLengthInputVisible = false;

        private void RecalculateLineInputTotal()
        {
            if (IsLengthInputVisible && NewItemLengthFeet > 0)
            {
                NewItemLineTotal = NewItemQuantity * (decimal)NewItemLengthFeet * NewItemRate;
            }
            else
            {
                NewItemLineTotal = NewItemQuantity * NewItemRate;
            }
        }

        public CustomerOrdersViewModel(
            ICustomerOrderService orderService,
            IInventoryService inventoryService,
            ICustomerService customerService,
            IPrintService printService,
            IRepository<CompanySetting> companyRepo)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _customerService = customerService;
            _printService = printService;
            _companyRepo = companyRepo;
        }

        [RelayCommand]
        public async Task LoadOrdersAsync()
        {
            var list = await _orderService.GetCustomerOrdersAsync(SearchQuery, SelectedStatusFilter);
            Orders = new ObservableCollection<CustomerOrder>(list);
            FilteredOrders = Orders;

            TotalOrdersCount = Orders.Count;
            PendingOrdersCount = Orders.Count(o => o.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase));
            CompletedOrdersCount = Orders.Count(o => o.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase));
            TotalOrdersAmount = Orders.Sum(o => o.TotalAmount);
            TotalPaidAmount = Orders.Sum(o => o.PaidAmount);
            TotalRemainingAmount = Orders.Sum(o => o.RemainingAmount);

            var items = await _inventoryService.SearchItemsAsync("");
            MasterItems = new ObservableCollection<Item>(items);

            var custs = await _customerService.SearchCustomersAsync("");
            MasterCustomers = new ObservableCollection<Customer>(custs);
        }

        partial void OnSearchQueryChanged(string value)
        {
            _ = LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task FilterByStatusAsync(string status)
        {
            SelectedStatusFilter = status;
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task OpenNewOrderModalAsync()
        {
            var items = await _inventoryService.SearchItemsAsync("");
            MasterItems = new ObservableCollection<Item>(items);

            var custs = await _customerService.SearchCustomersAsync("");
            MasterCustomers = new ObservableCollection<Customer>(custs);

            ModalTitle = "New Customer Order";
            var nextNum = await _orderService.GenerateNextOrderNumberAsync();

            CurrentOrder = new CustomerOrder
            {
                OrderNumber = nextNum,
                OrderDate = DateTime.Now,
                ReceivingDate = DateTime.Now.AddDays(1),
                Status = "Pending"
            };

            FormSelectedCustomer = null;
            FormCustomerName = string.Empty;
            FormAddress = string.Empty;
            FormContactNumber = string.Empty;
            FormOrderDate = DateTime.Now;
            FormReceivingDate = DateTime.Now.AddDays(1);
            FormStatus = "Pending";
            FormPaidAmount = 0m;

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
            if (SelectedMasterItem == null && !string.IsNullOrWhiteSpace(NewItemName))
            {
                var match = MasterItems.FirstOrDefault(i => i.Name.Trim().Equals(NewItemName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                           i.Code.Trim().Equals(NewItemName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectedMasterItem = match;
                }
            }

            if (string.IsNullOrWhiteSpace(NewItemName) && SelectedMasterItem == null)
            {
                MessageBox.Show("Please select an item or enter an item name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nameToUse = SelectedMasterItem != null ? SelectedMasterItem.Name : NewItemName.Trim();

            if (NewItemQuantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity greater than 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var line = new CustomerOrderItem
            {
                ItemId = SelectedMasterItem?.Id,
                ItemNameSnapshot = nameToUse,
                ItemCode = SelectedMasterItem?.Code ?? NewItemCode ?? string.Empty,
                Unit = string.IsNullOrWhiteSpace(NewItemUnit) ? (SelectedMasterItem?.SellingUnit ?? "Pcs") : NewItemUnit.Trim(),
                Quantity = NewItemQuantity,
                LengthFeet = IsLengthInputVisible ? NewItemLengthFeet : 0.0,
                Rate = NewItemRate > 0 ? NewItemRate : (SelectedMasterItem?.SalePrice ?? 0m)
            };
            line.Recalculate();

            line.PropertyChanged += (s, e) => RecalculateFormTotal();

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
            NewItemLengthFeet = 0.0;
            NewItemRate = 0m;
            NewItemLineTotal = 0m;
            IsLengthInputVisible = false;
        }

        public void RecalculateFormTotal()
        {
            FormTotalAmount = CurrentOrderItems.Sum(i => i.LineTotal);
            OnPropertyChanged(nameof(FormRemainingAmount));
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
            CurrentOrder.PaidAmount = FormPaidAmount;

            CurrentOrder.Items = new ObservableCollection<CustomerOrderItem>(CurrentOrderItems);

            await _orderService.SaveCustomerOrderAsync(CurrentOrder);

            IsOrderModalOpen = false;
            await LoadOrdersAsync();
        }

        [RelayCommand]
        public async Task EditOrderAsync(CustomerOrder order)
        {
            if (order == null) return;

            var items = await _inventoryService.SearchItemsAsync("");
            MasterItems = new ObservableCollection<Item>(items);

            var custs = await _customerService.SearchCustomersAsync("");
            MasterCustomers = new ObservableCollection<Customer>(custs);

            ModalTitle = $"Edit Customer Order ({order.OrderNumber})";
            CurrentOrder = order;

            FormCustomerName = order.CustomerName ?? string.Empty;
            FormSelectedCustomer = MasterCustomers.FirstOrDefault(c => c.Name.Equals(FormCustomerName, StringComparison.OrdinalIgnoreCase));
            FormAddress = order.Address ?? string.Empty;
            FormContactNumber = order.ContactNumber ?? string.Empty;
            FormOrderDate = order.OrderDate;
            FormReceivingDate = order.ReceivingDate ?? DateTime.Now.AddDays(1);
            FormStatus = order.Status ?? "Pending";
            FormPaidAmount = order.PaidAmount;

            CurrentOrderItems = new ObservableCollection<CustomerOrderItem>();
            foreach (var i in order.Items)
            {
                var line = new CustomerOrderItem
                {
                    Id = i.Id,
                    CustomerOrderId = i.CustomerOrderId,
                    ItemId = i.ItemId,
                    ItemNameSnapshot = i.ItemNameSnapshot,
                    ItemCode = i.ItemCode,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    LengthFeet = i.LengthFeet,
                    Rate = i.Rate,
                    LineTotal = i.LineTotal
                };
                line.Recalculate();
                line.PropertyChanged += (s, e) => RecalculateFormTotal();
                CurrentOrderItems.Add(line);
            }

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
            var headers = new[] { "Sr #", "Order No.", "Customer Name", "Contact", "Order Date", "Receiving Date", "Total (PKR)", "Paid (PKR)", "Balance (PKR)", "Status" };

            int sr = 1;
            var rows = Orders.Select(o => new[]
            {
                sr++.ToString(),
                o.OrderNumber ?? "",
                o.CustomerName ?? "-",
                o.ContactNumber ?? "-",
                o.OrderDate.ToString("dd/MM/yyyy"),
                o.ReceivingDate.HasValue ? o.ReceivingDate.Value.ToString("dd/MM/yyyy") : "-",
                $"Rs. {o.TotalAmount:N2}",
                $"Rs. {o.PaidAmount:N2}",
                $"Rs. {o.RemainingAmount:N2}",
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
                $"Total: Rs. {Orders.Sum(o => o.TotalAmount):N2}",
                $"Paid: Rs. {Orders.Sum(o => o.PaidAmount):N2}",
                $"Bal: Rs. {Orders.Sum(o => o.RemainingAmount):N2}",
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
