using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;

namespace AlMadinaERP.Wpf.ViewModels
{
    public enum SalesActiveSubView
    {
        SaleInvoiceList,
        SaleInvoiceForm,
        SaleReturnList,
        SaleReturnForm,
        PosList,
        PosTerminal
    }

    public partial class SalesViewModel : ObservableObject
    {
        private readonly ISaleService _saleService;
        private readonly ICustomerService _customerService;
        private readonly IInventoryService _inventoryService;
        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        // View Mode State
        [ObservableProperty]
        private SalesActiveSubView _activeSubView = SalesActiveSubView.SaleInvoiceList;

        [ObservableProperty]
        private bool _isReturnMode = false; // false = Sale Invoice, true = Sale Return

        // Collections
        [ObservableProperty]
        private ObservableCollection<SaleInvoice> _invoices = new();

        [ObservableProperty]
        private ObservableCollection<SaleInvoice> _saleReturns = new();

        [ObservableProperty]
        private ObservableCollection<SaleInvoice> _posSales = new();

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private ObservableCollection<Item> _availableItems = new();

        // Models
        [ObservableProperty]
        private SaleInvoice _newInvoice = new();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private bool _isViewInvoiceModalOpen;

        [ObservableProperty]
        private SaleInvoice? _selectedViewInvoice;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private DateTime? _fromDate = null;

        [ObservableProperty]
        private DateTime? _toDate = null;

        [ObservableProperty]
        private string _statusFilter = "All";

        private List<Item> _allMasterItems = new();
        private System.Threading.CancellationTokenSource? _salesSearchCts;
        private bool _isBusy = false;
        private System.Threading.CancellationTokenSource? _loadCts;

        private void CancelLoading()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        }

        public void CancelPendingSearch()
        {
            _salesSearchCts?.Cancel();
            _salesSearchCts?.Dispose();
            _salesSearchCts = null;
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterPosItems(value);

            CancelPendingSearch();
            _salesSearchCts = new System.Threading.CancellationTokenSource();
            var token = _salesSearchCts.Token;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250, token);
                    if (!token.IsCancellationRequested && dispatcher != null)
                    {
                        await dispatcher.InvokeAsync(async () =>
                        {
                            if (!token.IsCancellationRequested)
                                await LoadInvoicesAsync();
                        });
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception) { }
            }, token);
        }
        partial void OnFromDateChanged(DateTime? value) => _ = LoadInvoicesAsync();
        partial void OnToDateChanged(DateTime? value) => _ = LoadInvoicesAsync();
        partial void OnStatusFilterChanged(string value) => _ = LoadInvoicesAsync();

        // Stat Card Metrics - Invoices
        [ObservableProperty]
        private int _totalInvoicesCount = 0;

        [ObservableProperty]
        private int _fullyPaidCount = 0;

        [ObservableProperty]
        private int _postedCount = 0;

        [ObservableProperty]
        private int _draftsCount = 0;

        // Stat Card Metrics - Returns (Screenshot 1)
        [ObservableProperty]
        private int _totalReturnsCount = 0;

        [ObservableProperty]
        private int _postedReturnsCount = 0;

        [ObservableProperty]
        private int _draftReturnsCount = 0;

        // Stat Card Metrics - POS (Screenshot 3)
        [ObservableProperty]
        private int _todayPosSalesCount = 0;

        [ObservableProperty]
        private decimal _totalPosCash = 0m;

        [ObservableProperty]
        private int _completedPosCount = 0;

        [ObservableProperty]
        private int _draftPosCount = 0;

        // Grand totals for footer bars
        [ObservableProperty]
        private decimal _grandTotalSales = 0m;

        [ObservableProperty]
        private decimal _grandTotalReturns = 0m;

        [ObservableProperty]
        private decimal _grandTotalPosSales = 0m;

        public SalesViewModel(
            ISaleService saleService,
            ICustomerService customerService,
            IInventoryService inventoryService,
            IPrintService printService,
            IRepository<CompanySetting> companyRepo)
        {
            _saleService = saleService;
            _customerService = customerService;
            _inventoryService = inventoryService;
            _printService = printService;
            _companyRepo = companyRepo;
            ResetNewInvoice();
        }

        private bool _isRefreshingCustomers = false;

        private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaleInvoiceItem.Quantity) ||
                e.PropertyName == nameof(SaleInvoiceItem.Rate) ||
                e.PropertyName == nameof(SaleInvoiceItem.LengthFeet) ||
                e.PropertyName == nameof(SaleInvoiceItem.RatePerFoot) ||
                e.PropertyName == nameof(SaleInvoiceItem.DiscountPercent) ||
                e.PropertyName == nameof(SaleInvoiceItem.Item) ||
                e.PropertyName == nameof(SaleInvoiceItem.TotalPrice))
            {
                RecalculateTotals();
            }
        }

        private void SubscribeItemEvents(SaleInvoiceItem? item)
        {
            if (item == null) return;
            item.PropertyChanged -= OnItemPropertyChanged;
            item.PropertyChanged += OnItemPropertyChanged;
        }

        private void UnsubscribeItemEvents(SaleInvoiceItem? item)
        {
            if (item == null) return;
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void UnsubscribeAllItemEvents(SaleInvoice? invoice)
        {
            if (invoice?.Items != null)
            {
                foreach (var item in invoice.Items)
                {
                    UnsubscribeItemEvents(item);
                }
            }
        }

        private void ResetNewInvoice()
        {
            UnsubscribeAllItemEvents(NewInvoice);

            var prefix = IsReturnMode ? "SR-" :
                         ActiveSubView == SalesActiveSubView.PosTerminal ? "POS-" : "SI-";

            NewInvoice = new SaleInvoice
            {
                InvoiceNumber = prefix + DateTime.Now.ToString("fffSSm"),
                Date = DateTime.Now,
                IsCashSale = true,
                CustomerName = "WALK-IN CUSTOMER",
                DeliveryStatus = DeliveryStatus.Received,
                Status = "Posted",
                SaleCategory = "Casual",
                Salesman = "Admin",
                Location = "Main Warehouse",
                Employee = "System Admin"
            };
            SelectedCustomer = null;
            AddEmptyLineItem();
        }

        [RelayCommand]
        public void ClearDateFilter()
        {
            FromDate = null;
            ToDate = null;
        }

        [RelayCommand]
        public void SetTodayFilter()
        {
            FromDate = DateTime.Today;
            ToDate = DateTime.Today.AddDays(1).AddSeconds(-1);
        }

        [RelayCommand]
        public void SetThisMonthFilter()
        {
            var now = DateTime.Now;
            FromDate = new DateTime(now.Year, now.Month, 1);
            ToDate = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59);
        }

        [RelayCommand]
        public async Task LoadInvoicesAsync()
        {
            try
            {
                var toDateEnd = ToDate.HasValue ? ToDate.Value.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;
                var list = await _saleService.SearchInvoicesAsync(SearchQuery, FromDate, toDateEnd);

                // Apply status filter in-memory
                if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "All")
                    list = list.Where(i => i.Status == StatusFilter).ToList();

                var invoices = list.Where(i => i.Type == InvoiceType.SaleInvoice).ToList();
                var returns = list.Where(i => i.Type == InvoiceType.SaleReturn).ToList();
                var posList = list.Where(i => i.Type == InvoiceType.POSCounterSale).ToList();

                Invoices = new ObservableCollection<SaleInvoice>(invoices);
                SaleReturns = new ObservableCollection<SaleInvoice>(returns);
                PosSales = new ObservableCollection<SaleInvoice>(posList);

                TotalInvoicesCount = invoices.Count;
                PostedCount = invoices.Count(i => i.Status == "Posted");
                FullyPaidCount = invoices.Count(i => i.PaidAmount >= i.TotalAmount && i.TotalAmount > 0);
                DraftsCount = invoices.Count(i => i.Status == "Draft");

                TotalReturnsCount = returns.Count;
                PostedReturnsCount = returns.Count(r => r.Status == "Posted");
                DraftReturnsCount = returns.Count(r => r.Status == "Draft");

                TodayPosSalesCount = posList.Count;
                TotalPosCash = posList.Sum(p => p.TotalAmount);
                CompletedPosCount = posList.Count(p => p.Status == "Completed" || p.Status == "Posted");
                DraftPosCount = posList.Count(p => p.Status == "Draft");

                // Grand totals for footer bars
                GrandTotalSales = invoices.Sum(i => i.TotalAmount);
                GrandTotalReturns = returns.Sum(r => r.TotalAmount);
                GrandTotalPosSales = posList.Sum(p => p.TotalAmount);

                _isRefreshingCustomers = true;
                try
                {
                    var custs = await _customerService.SearchCustomersAsync("");
                    var selId = SelectedCustomer?.Id ?? NewInvoice?.CustomerId;
                    Customers = new ObservableCollection<Customer>(custs);
                    if (selId.HasValue && selId.Value > 0)
                        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == selId.Value);
                }
                finally
                {
                    _isRefreshingCustomers = false;
                }

                if (_allMasterItems == null || _allMasterItems.Count == 0)
                {
                    _allMasterItems = await _inventoryService.SearchItemsAsync("");
                    FilterPosItems(SearchQuery);
                }

                EditInvoiceCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SalesViewModel] LoadInvoicesAsync error: {ex.Message}");
            }
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (_isRefreshingCustomers) return;
            if (NewInvoice != null)
            {
                if (value != null)
                {
                    NewInvoice.IsCashSale = false;
                    NewInvoice.CustomerId = value.Id;
                    NewInvoice.CustomerName = value.Name;
                }
                else if (NewInvoice.Id == 0)
                {
                    NewInvoice.IsCashSale = true;
                    NewInvoice.CustomerId = null;
                    NewInvoice.CustomerName = "WALK-IN CUSTOMER";
                }
            }
        }

        private void FilterPosItems(string query)
        {
            if (_allMasterItems == null || _allMasterItems.Count == 0) return;

            List<Item> filtered;
            if (string.IsNullOrWhiteSpace(query))
            {
                filtered = _allMasterItems.ToList();
            }
            else
            {
                var term = query.Trim().ToLower();
                filtered = _allMasterItems.Where(i =>
                    (i.Name != null && i.Name.ToLower().Contains(term)) ||
                    (i.Code != null && i.Code.ToLower().Contains(term)) ||
                    (i.CategoryName != null && i.CategoryName.ToLower().Contains(term))
                ).ToList();
            }

            AvailableItems = new ObservableCollection<Item>(filtered);
        }

        private async Task EnsureItemsLoadedAsync(System.Threading.CancellationToken token = default)
        {
            var items = await _inventoryService.SearchItemsAsync("");
            if (token.IsCancellationRequested) return;

            _allMasterItems = items;
            FilterPosItems(SearchQuery);

            _isRefreshingCustomers = true;
            try
            {
                var custs = await _customerService.SearchCustomersAsync("");
                if (token.IsCancellationRequested) return;

                var selId = SelectedCustomer?.Id ?? NewInvoice?.CustomerId;
                Customers = new ObservableCollection<Customer>(custs);
                if (selId.HasValue && selId.Value > 0)
                    SelectedCustomer = Customers.FirstOrDefault(c => c.Id == selId.Value);
            }
            finally
            {
                _isRefreshingCustomers = false;
            }
        }

        [RelayCommand]
        public async Task OpenCreateInvoice()
        {
            if (_isBusy) return;
            _isBusy = true;
            
            CancelLoading();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsReturnMode = false;
                ActiveSubView = SalesActiveSubView.SaleInvoiceForm;
                ResetNewInvoice();
                await EnsureItemsLoadedAsync(token);
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public async Task OpenNewReturnForm()
        {
            if (_isBusy) return;
            _isBusy = true;

            CancelLoading();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsReturnMode = true;
                ActiveSubView = SalesActiveSubView.SaleReturnForm;
                ResetNewInvoice();
                await EnsureItemsLoadedAsync(token);
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public async Task OpenNewPosTerminal()
        {
            if (_isBusy) return;
            _isBusy = true;

            CancelLoading();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsReturnMode = false;
                ActiveSubView = SalesActiveSubView.PosTerminal;
                ResetNewInvoice();
                SearchQuery = string.Empty;
                await EnsureItemsLoadedAsync(token);
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public void CloseSubView()
        {
            CancelLoading();
            UnsubscribeAllItemEvents(NewInvoice);
            ResetNewInvoice();
            SelectedCustomer = null;

            if (ActiveSubView == SalesActiveSubView.SaleReturnForm)
                ActiveSubView = SalesActiveSubView.SaleReturnList;
            else if (ActiveSubView == SalesActiveSubView.PosTerminal)
                ActiveSubView = SalesActiveSubView.PosList;
            else
                ActiveSubView = SalesActiveSubView.SaleInvoiceList;
        }

        [RelayCommand]
        public void AddLineItem()
        {
            AddEmptyLineItem();
        }

        private bool _isRecalculating = false;

        private void AddEmptyLineItem()
        {
            var newItem = new SaleInvoiceItem
            {
                Quantity = 1,
                Rate = 0,
                UnitName = "Pcs",
                TotalPrice = 0
            };

            SubscribeItemEvents(newItem);

            var app = System.Windows.Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(() =>
                {
                    NewInvoice.Items.Add(newItem);
                    RecalculateTotals();
                });
            }
            else
            {
                NewInvoice.Items.Add(newItem);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        public void RemoveLineItem(object? parameter)
        {
            if (parameter is SaleInvoiceItem item && NewInvoice.Items.Contains(item))
            {
                UnsubscribeItemEvents(item);
                NewInvoice.Items.Remove(item);
                RecalculateTotals();
            }
            else if (NewInvoice.Items.Count > 0)
            {
                var lastItem = NewInvoice.Items[NewInvoice.Items.Count - 1];
                UnsubscribeItemEvents(lastItem);
                NewInvoice.Items.RemoveAt(NewInvoice.Items.Count - 1);
                RecalculateTotals();
            }
        }

        public void RecalculateTotals()
        {
            if (_isRecalculating) return;
            _isRecalculating = true;
            try
            {
                foreach (var item in NewInvoice.Items)
                {
                    item.Recalculate();
                }

                NewInvoice.Subtotal = NewInvoice.Items.Sum(i => i.TotalPrice + i.DiscountAmount);
                NewInvoice.DiscountAmount = NewInvoice.Items.Sum(i => i.DiscountAmount);
                NewInvoice.TotalAmount = Math.Max(0m, (NewInvoice.Subtotal - NewInvoice.DiscountAmount) + NewInvoice.ExtraCharges + NewInvoice.VehicleCharges - NewInvoice.AdditionalDiscount);
                
                // Refund calculation
                NewInvoice.GrossRefund = NewInvoice.TotalAmount;
                NewInvoice.NetRefund = Math.Max(0m, NewInvoice.GrossRefund - NewInvoice.AdditionalDiscount - NewInvoice.CarServiceCharge + NewInvoice.CarWashDiscount);

                OnPropertyChanged(nameof(NewInvoice));
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        [RelayCommand]
        public async Task SaveInvoiceDraftAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                NewInvoice.Status = "Draft";
                await SaveInternalAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public async Task SaveInvoicePostedAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                NewInvoice.Status = "Posted";
                await SaveInternalAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task SaveInternalAsync()
        {
            var app = System.Windows.Application.Current;
            if (app != null && app.Dispatcher != null)
            {
                if (!app.Dispatcher.CheckAccess())
                    app.Dispatcher.Invoke(() => System.Windows.Input.Keyboard.ClearFocus());
                else
                    System.Windows.Input.Keyboard.ClearFocus();
            }

            if (ActiveSubView == SalesActiveSubView.SaleReturnForm)
                NewInvoice.Type = InvoiceType.SaleReturn;
            else if (ActiveSubView == SalesActiveSubView.PosTerminal)
                NewInvoice.Type = InvoiceType.POSCounterSale;
            else
                NewInvoice.Type = InvoiceType.SaleInvoice;

            foreach (var item in NewInvoice.Items)
            {
                if (item.Item != null && item.ItemId <= 0)
                {
                    item.ItemId = item.Item.Id;
                }
                else if (item.ItemId <= 0 && !string.IsNullOrWhiteSpace(item.ItemName))
                {
                    var match = AvailableItems.FirstOrDefault(i => i.Name.Equals(item.ItemName, StringComparison.OrdinalIgnoreCase) || i.Code.Equals(item.ItemCode, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        item.ItemId = match.Id;
                        item.ItemCode = match.Code;
                        item.ItemName = match.Name;
                    }
                }

                if (item.Quantity <= 0)
                {
                    item.Quantity = 1;
                }
            }

            var validItems = NewInvoice.Items.Where(i => i.ItemId > 0 && i.Quantity > 0).ToList();
            if (validItems.Count == 0)
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                if (owner != null)
                {
                    System.Windows.MessageBox.Show(
                        owner,
                        "Please select at least one item from the list before saving.",
                        "No Items Selected",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Please select at least one item from the list before saving.",
                        "No Items Selected",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
                return;
            }

            NewInvoice.Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>(validItems);

            if (SelectedCustomer != null)
            {
                NewInvoice.CustomerId = SelectedCustomer.Id;
                NewInvoice.CustomerName = SelectedCustomer.Name;
            }

            SaleInvoice savedInvoice;
            try
            {
                savedInvoice = await _saleService.SaveSaleInvoiceAsync(NewInvoice);
            }
            catch (Exception ex)
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                if (owner != null)
                {
                    System.Windows.MessageBox.Show(
                        owner,
                        $"Failed to save sale invoice: {ex.Message}",
                        "Save Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to save sale invoice: {ex.Message}",
                        "Save Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                return;
            }

            CloseSubView();

            var printOwner = System.Windows.Application.Current?.MainWindow;
            System.Windows.MessageBoxResult confirmPrint;
            if (printOwner != null)
            {
                confirmPrint = System.Windows.MessageBox.Show(
                    printOwner,
                    "Sale Invoice saved successfully! Do you want to print receipt?",
                    "Print Receipt",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
            }
            else
            {
                confirmPrint = System.Windows.MessageBox.Show(
                    "Sale Invoice saved successfully! Do you want to print receipt?",
                    "Print Receipt",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
            }

            if (confirmPrint == System.Windows.MessageBoxResult.Yes)
            {
                var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
                _printService.PrintThermalReceipt(savedInvoice, company);
            }

            await LoadInvoicesAsync();
            ResetNewInvoice();
        }

        [RelayCommand]
        public async Task DeleteInvoiceAsync(SaleInvoice invoice)
        {
            if (invoice != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete invoice #{invoice.InvoiceNumber} for PKR {invoice.NetAmount:N0}?\n\nThis will automatically reverse stock movements and customer balances.",
                    "Confirm Delete Invoice",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _saleService.DeleteSaleInvoiceAsync(invoice.Id);
                        await LoadInvoicesAsync();
                        System.Windows.MessageBox.Show($"Invoice #{invoice.InvoiceNumber} deleted successfully and accounting/stock entries reversed.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete invoice: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public async Task PrintThermalAsync(SaleInvoice? invoice)
        {
            invoice ??= SelectedViewInvoice;
            if (invoice == null) return;
            var fullInvoice = await _saleService.GetSaleInvoiceByIdAsync(invoice.Id) ?? invoice;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintThermalReceipt(fullInvoice, company);
        }

        [RelayCommand]
        public async Task PrintA4Async(SaleInvoice? invoice)
        {
            invoice ??= SelectedViewInvoice;
            if (invoice == null) return;
            var fullInvoice = await _saleService.GetSaleInvoiceByIdAsync(invoice.Id) ?? invoice;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintA4SaleInvoice(fullInvoice, company);
        }

        [RelayCommand]
        public async Task ViewInvoiceAsync(SaleInvoice invoice)
        {
            if (invoice == null) return;
            var fullInvoice = await _saleService.GetSaleInvoiceByIdAsync(invoice.Id);
            SelectedViewInvoice = fullInvoice ?? invoice;
            IsViewInvoiceModalOpen = true;
        }

        [RelayCommand]
        public void CloseViewInvoiceModal()
        {
            IsViewInvoiceModalOpen = false;
        }

        [RelayCommand]
        public void AddPosItem(Item item)
        {
            if (item == null) return;

            NewInvoice.Items ??= new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>();

            var existing = NewInvoice.Items.FirstOrDefault(i => i.ItemId == item.Id || (i.ItemName != null && i.ItemName.Equals(item.Name, StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                existing.Quantity += 1;
                existing.TotalPrice = existing.Quantity * existing.Rate;
            }
            else
            {
                var lineItem = new SaleInvoiceItem
                {
                    ItemId = item.Id,
                    ItemCode = item.Code,
                    ItemName = item.Name,
                    UnitName = item.SellingUnit ?? "Pcs",
                    Rate = item.SalePrice,
                    Quantity = 1,
                    TotalPrice = item.SalePrice,
                    IsReceived = true
                };
                SubscribeItemEvents(lineItem);
                NewInvoice.Items.Add(lineItem);
            }
            RecalculateTotals();
        }

        [RelayCommand]
        public async Task EditInvoiceAsync(SaleInvoice invoice)
        {
            if (invoice == null || _isBusy) return;
            _isBusy = true;

            CancelLoading();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                await EnsureItemsLoadedAsync(token);
                if (token.IsCancellationRequested) return;

                var fullInvoice = await _saleService.GetSaleInvoiceByIdAsync(invoice.Id);
                if (token.IsCancellationRequested || fullInvoice == null) return;

                UnsubscribeAllItemEvents(NewInvoice);
                fullInvoice.Items = fullInvoice.Items != null
                    ? new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>(fullInvoice.Items)
                    : new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>();
                NewInvoice = fullInvoice;

                var targetCust = Customers.FirstOrDefault(c =>
                    (fullInvoice.CustomerId.HasValue && fullInvoice.CustomerId.Value > 0 && c.Id == fullInvoice.CustomerId.Value) ||
                    (!string.IsNullOrWhiteSpace(fullInvoice.CustomerName) && c.Name.Equals(fullInvoice.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase)));

                SelectedCustomer = targetCust;
                NewInvoice.CustomerId = fullInvoice.CustomerId;
                NewInvoice.CustomerName = !string.IsNullOrWhiteSpace(fullInvoice.CustomerName) ? fullInvoice.CustomerName : (targetCust?.Name ?? "WALK-IN CUSTOMER");

                if (fullInvoice.Type == InvoiceType.SaleReturn)
                {
                    IsReturnMode = true;
                    ActiveSubView = SalesActiveSubView.SaleReturnForm;
                }
                else
                {
                    IsReturnMode = false;
                    ActiveSubView = SalesActiveSubView.SaleInvoiceForm;
                }

                foreach (var item in NewInvoice.Items)
                {
                    if (token.IsCancellationRequested) return;
                    var match = AvailableItems.FirstOrDefault(i =>
                        (item.ItemId > 0 && i.Id == item.ItemId) ||
                        (!string.IsNullOrWhiteSpace(item.ItemCode) && i.Code.Equals(item.ItemCode.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(item.ItemName) && i.Name.Equals(item.ItemName.Trim(), StringComparison.OrdinalIgnoreCase)));

                    if (match != null)
                    {
                        item.Item = match;
                        item.ItemCode = match.Code;
                        item.ItemName = match.Name;
                    }
                    else if (!string.IsNullOrWhiteSpace(item.ItemName))
                    {
                        var fallback = new Item { Id = item.ItemId, Code = item.ItemCode ?? "ITM-001", Name = item.ItemName, SalePrice = item.Rate };
                        AvailableItems.Add(fallback);
                        item.Item = fallback;
                    }

                    SubscribeItemEvents(item);
                }
                if (token.IsCancellationRequested) return;
                RecalculateTotals();
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public async Task PrintSalesListAsync()
        {
            try
            {
                var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
                var headers = new[] { "Invoice #", "Date", "Customer", "Total Amount (PKR)", "Paid (PKR)", "Balance (PKR)", "Status" };
                var rows = Invoices.Select(inv => new[] {
                    inv.InvoiceNumber ?? "",
                    inv.Date.ToString("dd/MM/yyyy"),
                    inv.CustomerName ?? "",
                    $"{inv.TotalAmount:N0}",
                    $"{inv.PaidAmount:N0}",
                    $"{(inv.TotalAmount - inv.PaidAmount):N0}",
                    inv.Status ?? "Posted"
                });
                var totals = new[] { "TOTAL SALE INVOICES", $"{Invoices.Count} Invoices", "", $"{GrandTotalSales:N0}", $"{Invoices.Sum(i => i.PaidAmount):N0}", $"{Invoices.Sum(i => i.TotalAmount - i.PaidAmount):N0}", "" };
                _printService.PrintReportTable("Sale Invoices Register", headers, rows, totals, company);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintSalesReturnListAsync()
        {
            try
            {
                var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
                var headers = new[] { "Return #", "Date", "Customer", "Total Amount (PKR)", "Status" };
                var rows = SaleReturns.Select(ret => new[] {
                    ret.InvoiceNumber ?? "",
                    ret.Date.ToString("dd/MM/yyyy"),
                    ret.CustomerName ?? "",
                    $"{ret.TotalAmount:N0}",
                    ret.Status ?? "Posted"
                });
                var totals = new[] { "TOTAL SALE RETURNS", $"{SaleReturns.Count} Returns", "", $"{GrandTotalReturns:N0}", "" };
                _printService.PrintReportTable("Sale Returns Register", headers, rows, totals, company);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintPosListAsync()
        {
            try
            {
                var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
                var headers = new[] { "Receipt #", "Date & Time", "Customer", "Payment Mode", "Total Amount (PKR)", "Status" };
                var rows = PosSales.Select(p => new[] {
                    p.InvoiceNumber ?? "",
                    p.Date.ToString("yyyy-MM-dd HH:mm"),
                    p.CustomerName ?? "WALK-IN CUSTOMER",
                    p.PaymentTerms ?? "Cash",
                    $"{p.TotalAmount:N0}",
                    p.Status ?? "Posted"
                });
                var totals = new[] { "TOTAL POS COUNTER SALES", $"{PosSales.Count} Receipts", "", "", $"{GrandTotalPosSales:N0}", "" };
                _printService.PrintReportTable("POS Counter Sales Register", headers, rows, totals, company);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public partial class PosViewModel : ObservableObject
    {
        private readonly ISaleService _saleService;
        private readonly IInventoryService _inventoryService;

        [ObservableProperty]
        private ObservableCollection<SaleInvoiceItem> _posItems = new();

        [ObservableProperty]
        private decimal _grandTotal;

        public PosViewModel(ISaleService saleService, IInventoryService inventoryService)
        {
            _saleService = saleService;
            _inventoryService = inventoryService;
        }
    }
}
