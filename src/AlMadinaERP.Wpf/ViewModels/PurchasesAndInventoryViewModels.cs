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
    public partial class PurchasesViewModel : ObservableObject
    {
        private readonly IPurchaseService _purchaseService;
        private readonly IVendorService _vendorService;
        private readonly IInventoryService _inventoryService;
        private readonly IPrintService _printService;

        // View Mode Flags
        [ObservableProperty]
        private bool _isFormVisible;

        [ObservableProperty]
        private bool _isReturnMode; // false = Purchase Invoice, true = Purchase Return

        // Collections
        [ObservableProperty]
        private ObservableCollection<PurchaseInvoice> _purchases = new();

        [ObservableProperty]
        private ObservableCollection<PurchaseInvoice> _purchaseReturns = new();

        [ObservableProperty]
        private ObservableCollection<Vendor> _vendors = new();

        [ObservableProperty]
        private ObservableCollection<Item> _availableItems = new();

        // Active Models
        [ObservableProperty]
        private PurchaseInvoice _newPurchase = new();

        [ObservableProperty]
        private Vendor? _selectedVendor;

        [ObservableProperty]
        private bool _isViewInvoiceModalOpen;

        [ObservableProperty]
        private PurchaseInvoice? _selectedViewInvoice;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private DateTime? _fromDate = null;

        [ObservableProperty]
        private DateTime? _toDate = null;

        [ObservableProperty]
        private string _statusFilter = "All";

        private System.Threading.CancellationTokenSource? _searchCts;

        public void CancelPendingSearch()
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;
        }

        partial void OnSearchQueryChanged(string value)
        {
            CancelPendingSearch();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

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
                                await LoadPurchasesAsync();
                        });
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception) { }
            }, token);
        }
        partial void OnFromDateChanged(DateTime? value) => _ = LoadPurchasesAsync();
        partial void OnToDateChanged(DateTime? value) => _ = LoadPurchasesAsync();
        partial void OnStatusFilterChanged(string value) => _ = LoadPurchasesAsync();

        // Stat Card Metrics (Screenshots 1 & 3)
        [ObservableProperty]
        private int _totalInvoicesCount;

        [ObservableProperty]
        private decimal _outstandingAmount;

        [ObservableProperty]
        private int _postedInvoicesCount;

        [ObservableProperty]
        private int _paidInvoicesCount;

        [ObservableProperty]
        private int _totalReturnsCount;

        [ObservableProperty]
        private int _postedReturnsCount;

        [ObservableProperty]
        private int _draftReturnsCount;

        [ObservableProperty]
        private decimal _totalReturnsAmount;

        public PurchasesViewModel(
            IPurchaseService purchaseService,
            IVendorService vendorService,
            IInventoryService inventoryService,
            IPrintService printService)
        {
            _purchaseService = purchaseService;
            _vendorService = vendorService;
            _inventoryService = inventoryService;
            _printService = printService;
            ResetNewPurchase();
        }

        private bool _isRefreshingVendors = false;
        private bool _isBusy = false;
        private System.Threading.CancellationTokenSource? _loadCts;

        private void CancelLoading()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        }

        private void OnPurchaseItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PurchaseInvoiceItem.Quantity) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.Rate) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.LengthFeet) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.RatePerFoot) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.DiscountPercent) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.TaxPercent) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.Item) ||
                e.PropertyName == nameof(PurchaseInvoiceItem.TotalPrice))
            {
                RecalculateTotals();
            }
        }

        private void SubscribePurchaseItemEvents(PurchaseInvoiceItem? item)
        {
            if (item == null) return;
            item.PropertyChanged -= OnPurchaseItemPropertyChanged;
            item.PropertyChanged += OnPurchaseItemPropertyChanged;
        }

        private void UnsubscribePurchaseItemEvents(PurchaseInvoiceItem? item)
        {
            if (item == null) return;
            item.PropertyChanged -= OnPurchaseItemPropertyChanged;
        }

        private void UnsubscribeAllPurchaseItemEvents(PurchaseInvoice? invoice)
        {
            if (invoice?.Items != null)
            {
                foreach (var item in invoice.Items)
                {
                    UnsubscribePurchaseItemEvents(item);
                }
            }
        }

        private void ResetNewPurchase()
        {
            _isRefreshingVendors = true;
            try
            {
                UnsubscribeAllPurchaseItemEvents(NewPurchase);

                NewPurchase = new PurchaseInvoice
                {
                    PurchaseNumber = (IsReturnMode ? "PR-" : "PI-") + DateTime.Now.ToString("fffSSm"),
                    Date = DateTime.Now,
                    VendorInvoiceDate = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(30),
                    Status = "Draft",
                    Currency = "PKR",
                    PaymentMethod = "Cash"
                };
                SelectedVendor = null;
                AddEmptyLineItem();
            }
            finally
            {
                _isRefreshingVendors = false;
            }
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
        public async Task LoadPurchasesAsync()
        {
            try
            {
                var toDateEnd = ToDate.HasValue ? ToDate.Value.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;
                var targetType = IsReturnMode ? PurchaseType.PurchaseReturn : PurchaseType.PurchaseInvoice;
                var list = await _purchaseService.SearchPurchasesAsync(SearchQuery, FromDate, toDateEnd, targetType);

                if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "All")
                    list = list.Where(p => p.Status == StatusFilter).ToList();

                var invoices = list.Where(p => p.Type == PurchaseType.PurchaseInvoice).ToList();
                var returns = list.Where(p => p.Type == PurchaseType.PurchaseReturn).ToList();

                Purchases = new ObservableCollection<PurchaseInvoice>(invoices);
                PurchaseReturns = new ObservableCollection<PurchaseInvoice>(returns);

                // Metrics for Invoices
                TotalInvoicesCount = invoices.Count;
                OutstandingAmount = invoices.Sum(i => i.OutstandingAmount > 0 ? i.OutstandingAmount : i.TotalAmount);
                PostedInvoicesCount = invoices.Count(i => i.Status == "Posted");
                PaidInvoicesCount = invoices.Count(i => i.Status == "Paid");

                // Metrics for Returns
                TotalReturnsCount = returns.Count;
                PostedReturnsCount = returns.Count(r => r.Status == "Posted");
                DraftReturnsCount = returns.Count(r => r.Status == "Draft");
                TotalReturnsAmount = returns.Sum(r => r.TotalAmount);

                _isRefreshingVendors = true;
                try
                {
                    var vList = await _vendorService.SearchVendorsAsync("");
                    var selVendId = SelectedVendor?.Id ?? NewPurchase?.VendorId;
                    Vendors = new ObservableCollection<Vendor>(vList);
                    if (selVendId.HasValue && selVendId.Value > 0)
                        SelectedVendor = Vendors.FirstOrDefault(v => v.Id == selVendId.Value);
                }
                finally
                {
                    _isRefreshingVendors = false;
                }

                var iList = await _inventoryService.SearchItemsAsync("");
                AvailableItems = new ObservableCollection<Item>(iList);

                EditInvoiceCommand.NotifyCanExecuteChanged();
                DeletePurchaseCommand.NotifyCanExecuteChanged();
                ViewInvoiceCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PurchasesViewModel] LoadPurchasesAsync error: {ex.Message}");
            }
        }

        partial void OnSelectedVendorChanged(Vendor? value)
        {
            if (_isRefreshingVendors) return;
            if (NewPurchase != null)
            {
                if (value != null)
                {
                    NewPurchase.VendorId = value.Id;
                    NewPurchase.VendorName = value.Name;
                }
                else
                {
                    NewPurchase.VendorId = null;
                    if (string.IsNullOrWhiteSpace(NewPurchase.VendorName) || 
                        NewPurchase.VendorName == "Direct / Walk-in Purchase (No Vendor)")
                    {
                        NewPurchase.VendorName = "Direct / Walk-in Purchase (No Vendor)";
                    }
                }
            }
        }

        private async Task EnsureItemsLoadedAsync(System.Threading.CancellationToken token = default)
        {
            var items = await _inventoryService.SearchItemsAsync("");
            if (token.IsCancellationRequested) return;

            AvailableItems = new ObservableCollection<Item>(items);

            _isRefreshingVendors = true;
            try
            {
                var vList = await _vendorService.SearchVendorsAsync("");
                if (token.IsCancellationRequested) return;

                var selVendId = SelectedVendor?.Id ?? NewPurchase?.VendorId;
                Vendors = new ObservableCollection<Vendor>(vList);
                if (selVendId.HasValue && selVendId.Value > 0)
                    SelectedVendor = Vendors.FirstOrDefault(v => v.Id == selVendId.Value);
            }
            finally
            {
                _isRefreshingVendors = false;
            }
        }

        [RelayCommand]
        public async Task OpenNewInvoiceForm()
        {
            if (_isBusy) return;
            _isBusy = true;

            CancelLoading();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsReturnMode = false;
                ResetNewPurchase();
                IsFormVisible = true;
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
                ResetNewPurchase();
                IsFormVisible = true;
                await EnsureItemsLoadedAsync(token);
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public void CloseForm()
        {
            CancelLoading();
            IsFormVisible = false;
            ResetNewPurchase();
        }

        [RelayCommand]
        public void AddLineItem()
        {
            AddEmptyLineItem();
        }

        private bool _isRecalculating = false;

        private void AddEmptyLineItem()
        {
            var newItem = new PurchaseInvoiceItem
            {
                Quantity = 1,
                Rate = 0,
                UnitName = "Pcs",
                TotalPrice = 0
            };

            SubscribePurchaseItemEvents(newItem);

            var app = System.Windows.Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(() =>
                {
                    NewPurchase.Items.Add(newItem);
                    RecalculateTotals();
                });
            }
            else
            {
                NewPurchase.Items.Add(newItem);
                RecalculateTotals();
            }
        }

        [RelayCommand]
        public void RemoveLineItem(object? parameter)
        {
            if (parameter is PurchaseInvoiceItem item && NewPurchase.Items.Contains(item))
            {
                UnsubscribePurchaseItemEvents(item);
                NewPurchase.Items.Remove(item);
                RecalculateTotals();
            }
            else if (NewPurchase.Items.Count > 0)
            {
                var lastItem = NewPurchase.Items[NewPurchase.Items.Count - 1];
                UnsubscribePurchaseItemEvents(lastItem);
                NewPurchase.Items.RemoveAt(NewPurchase.Items.Count - 1);
                RecalculateTotals();
            }
        }

        public void RecalculateTotals()
        {
            if (_isRecalculating) return;
            _isRecalculating = true;
            try
            {
                foreach (var item in NewPurchase.Items)
                {
                    item.Recalculate();
                }

                NewPurchase.Subtotal = NewPurchase.Items.Sum(i => i.TotalPrice + i.DiscountAmount - i.TaxAmount);
                NewPurchase.DiscountAmount = NewPurchase.Items.Sum(i => i.DiscountAmount);
                NewPurchase.TaxAmount = NewPurchase.Items.Sum(i => i.TaxAmount);
                NewPurchase.TotalAmount = (NewPurchase.Subtotal - NewPurchase.DiscountAmount) + NewPurchase.TaxAmount + NewPurchase.ExtraExpenses + NewPurchase.VehicleCharges;
                NewPurchase.BalanceDue = NewPurchase.TotalAmount - NewPurchase.AmountPaid;

                OnPropertyChanged(nameof(NewPurchase));
            }
            finally
            {
                _isRecalculating = false;
            }
        }

        [RelayCommand]
        public async Task SavePurchaseDraftAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                NewPurchase.Status = "Draft";
                await SaveInternalAsync();
            }
            finally
            {
                _isBusy = false;
            }
        }

        [RelayCommand]
        public async Task SavePurchasePostedAsync()
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                NewPurchase.Status = "Posted";
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

            NewPurchase.Type = IsReturnMode ? PurchaseType.PurchaseReturn : PurchaseType.PurchaseInvoice;

            foreach (var item in NewPurchase.Items)
            {
                if (item.Item != null && item.ItemId <= 0)
                {
                    item.ItemId = item.Item.Id;
                }
            }

            var validItems = NewPurchase.Items.Where(i => i.ItemId > 0 && i.Quantity > 0).ToList();
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

            NewPurchase.Items = new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>(validItems);

            if (SelectedVendor != null)
            {
                NewPurchase.VendorId = SelectedVendor.Id;
                NewPurchase.VendorName = SelectedVendor.Name;
                NewPurchase.IsCashPurchase = false;
            }
            else if (!string.IsNullOrWhiteSpace(NewPurchase.VendorName) &&
                     !NewPurchase.VendorName.Equals("Direct / Walk-in Purchase (No Vendor)", StringComparison.OrdinalIgnoreCase))
            {
                var matchedVendor = Vendors.FirstOrDefault(v => v.Name.Equals(NewPurchase.VendorName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedVendor != null)
                {
                    NewPurchase.VendorId = matchedVendor.Id;
                    NewPurchase.VendorName = matchedVendor.Name;
                }
                else
                {
                    NewPurchase.VendorId = null;
                }
                NewPurchase.IsCashPurchase = false;
            }
            else if (NewPurchase.VendorId.HasValue && NewPurchase.VendorId.Value > 0)
            {
                var matchedVendor = Vendors.FirstOrDefault(v => v.Id == NewPurchase.VendorId.Value);
                if (matchedVendor != null)
                {
                    NewPurchase.VendorName = matchedVendor.Name;
                }
                NewPurchase.IsCashPurchase = false;
            }
            else
            {
                NewPurchase.VendorId = null;
                NewPurchase.VendorName = "Direct / Walk-in Purchase (No Vendor)";
                NewPurchase.IsCashPurchase = true;
            }

            PurchaseInvoice savedInvoice;
            try
            {
                savedInvoice = await _purchaseService.SavePurchaseInvoiceAsync(NewPurchase);
            }
            catch (Exception ex)
            {
                var owner = System.Windows.Application.Current?.MainWindow;
                if (owner != null)
                {
                    System.Windows.MessageBox.Show(
                        owner,
                        $"Failed to save purchase invoice: {ex.Message}",
                        "Save Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to save purchase invoice: {ex.Message}",
                        "Save Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                return;
            }

            CloseForm();

            var printOwner = System.Windows.Application.Current?.MainWindow;
            System.Windows.MessageBoxResult confirmPrint;
            if (printOwner != null)
            {
                confirmPrint = System.Windows.MessageBox.Show(
                    printOwner,
                    "Purchase Invoice saved successfully! Do you want to print A4 invoice?",
                    "Success",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
            }
            else
            {
                confirmPrint = System.Windows.MessageBox.Show(
                    "Purchase Invoice saved successfully! Do you want to print A4 invoice?",
                    "Success",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
            }

            if (confirmPrint == System.Windows.MessageBoxResult.Yes)
            {
                _printService.PrintA4PurchaseInvoice(savedInvoice, new CompanySetting());
            }

            await Task.Delay(50); // Allow UI to settle
            await LoadPurchasesAsync();
        }

        [RelayCommand]
        public async Task DeletePurchaseAsync(PurchaseInvoice purchase)
        {
            if (purchase != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete purchase invoice #{purchase.PurchaseNumber} for PKR {purchase.NetAmount:N0}?\n\nThis will automatically reverse stock movements and vendor balances.",
                    "Confirm Delete Purchase Invoice",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _purchaseService.DeletePurchaseInvoiceAsync(purchase.Id);
                        await LoadPurchasesAsync();
                        System.Windows.MessageBox.Show($"Purchase invoice #{purchase.PurchaseNumber} deleted successfully and accounting/stock entries reversed.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete purchase invoice: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public async Task ViewInvoiceAsync(PurchaseInvoice purchase)
        {
            if (purchase == null) return;
            var fullPurchase = await _purchaseService.GetPurchaseInvoiceByIdAsync(purchase.Id);
            SelectedViewInvoice = fullPurchase ?? purchase;
            IsViewInvoiceModalOpen = true;
        }

        [RelayCommand]
        public void CloseViewInvoiceModal()
        {
            IsViewInvoiceModalOpen = false;
        }

        [RelayCommand]
        public async Task PrintA4InvoiceAsync(PurchaseInvoice? purchase)
        {
            var target = purchase ?? SelectedViewInvoice ?? NewPurchase;
            if (target == null) return;

            if (target.Id > 0 && (target.Items == null || target.Items.Count == 0))
            {
                var full = await _purchaseService.GetPurchaseInvoiceByIdAsync(target.Id);
                if (full != null) target = full;
            }

            _printService.PrintA4PurchaseInvoice(target, new CompanySetting());
        }

        [RelayCommand]
        public async Task EditInvoiceAsync(PurchaseInvoice purchase)
        {
            if (purchase == null || _isBusy) return;
            _isBusy = true;

            CancelLoading();
            _loadCts = new System.Threading.CancellationTokenSource();
            var token = _loadCts.Token;

            _isRefreshingVendors = true;
            try
            {
                await EnsureItemsLoadedAsync(token);
                if (token.IsCancellationRequested) return;

                var fullPurchase = await _purchaseService.GetPurchaseInvoiceByIdAsync(purchase.Id);
                if (token.IsCancellationRequested || fullPurchase == null) return;

                UnsubscribeAllPurchaseItemEvents(NewPurchase);
                fullPurchase.Items = fullPurchase.Items != null
                    ? new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>(fullPurchase.Items)
                    : new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>();
                NewPurchase = fullPurchase;

                var targetVendor = Vendors.FirstOrDefault(v =>
                    (fullPurchase.VendorId.HasValue && fullPurchase.VendorId.Value > 0 && v.Id == fullPurchase.VendorId.Value) ||
                    (!string.IsNullOrWhiteSpace(fullPurchase.VendorName) && v.Name.Equals(fullPurchase.VendorName.Trim(), StringComparison.OrdinalIgnoreCase)));

                SelectedVendor = targetVendor;
                NewPurchase.VendorId = fullPurchase.VendorId;
                NewPurchase.VendorName = !string.IsNullOrWhiteSpace(fullPurchase.VendorName) ? fullPurchase.VendorName : (targetVendor?.Name ?? "Direct / Walk-in Purchase (No Vendor)");

                IsReturnMode = fullPurchase.Type == PurchaseType.PurchaseReturn;
                IsFormVisible = true;

                foreach (var item in NewPurchase.Items)
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
                        var fallback = new Item { Id = item.ItemId, Code = item.ItemCode ?? "ITM-001", Name = item.ItemName, PurchasePrice = item.Rate, SalePrice = item.Rate };
                        AvailableItems.Add(fallback);
                        item.Item = fallback;
                    }

                    SubscribePurchaseItemEvents(item);
                }
                if (token.IsCancellationRequested) return;
                RecalculateTotals();
            }
            finally
            {
                _isRefreshingVendors = false;
                _isBusy = false;
            }
        }

        [RelayCommand]
        public void PrintPurchasesList()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PageWidth = 794;  // Standard A4 Width
                    doc.PageHeight = 1123; // Standard A4 Height
                    doc.PagePadding = new System.Windows.Thickness(35);
                    doc.ColumnWidth = 724;
                    doc.FontFamily = new System.Windows.Media.FontFamily("Times New Roman");

                    var pHeader = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("AL MADINA BUILDING MATERIAL ERP"))
                    {
                        FontSize = 18,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.Maroon,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 4)
                    };
                    doc.Blocks.Add(pHeader);

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("PURCHASE INVOICES REGISTER"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pDate = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Invoices: {Purchases.Count}"))
                    {
                        FontSize = 10,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        TextAlignment = System.Windows.TextAlignment.Right,
                        Margin = new System.Windows.Thickness(0, 0, 0, 14)
                    };
                    doc.Blocks.Add(pDate);

                    var table = new System.Windows.Documents.Table();
                    table.CellSpacing = 0;
                    table.BorderThickness = new System.Windows.Thickness(1);
                    table.BorderBrush = System.Windows.Media.Brushes.LightGray;

                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(160) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(54) });

                    var rowGroup = new System.Windows.Documents.TableRowGroup();

                    var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
                    string[] headers = { "INVOICE #", "DATE", "VENDOR", "TOTAL (PKR)", "PAID (PKR)", "BALANCE (PKR)", "STATUS" };
                    foreach (var h in headers)
                    {
                        var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 9,
                            Margin = new System.Windows.Thickness(4)
                        });
                        headerRow.Cells.Add(cell);
                    }
                    rowGroup.Rows.Add(headerRow);

                    int rowIdx = 0;
                    foreach (var pur in Purchases)
                    {
                        var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                        var r = new System.Windows.Documents.TableRow { Background = bg };

                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pur.PurchaseNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pur.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pur.VendorName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{pur.TotalAmount:N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{pur.AmountPaid:N0}")) { FontSize = 9, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{pur.BalanceDue:N0}")) { FontSize = 9, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pur.Status ?? "Posted")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));

                        rowGroup.Rows.Add(r);
                        rowIdx++;
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Purchases.Count} Invoices")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Purchases.Sum(p => p.TotalAmount):N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Purchases.Sum(p => p.AmountPaid):N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Purchases.Sum(p => p.BalanceDue):N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Purchase Invoices Register");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void PrintPurchaseReturnList()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PageWidth = 794;  // Standard A4 Width
                    doc.PageHeight = 1123; // Standard A4 Height
                    doc.PagePadding = new System.Windows.Thickness(35);
                    doc.ColumnWidth = 724;

                    var pHeader = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("AL MADINA BUILDING MATERIAL ERP"))
                    {
                        FontSize = 18,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.Maroon,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 4)
                    };
                    doc.Blocks.Add(pHeader);

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("PURCHASE RETURNS REGISTER"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pDate = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Returns: {PurchaseReturns.Count}"))
                    {
                        FontSize = 10,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        TextAlignment = System.Windows.TextAlignment.Right,
                        Margin = new System.Windows.Thickness(0, 0, 0, 14)
                    };
                    doc.Blocks.Add(pDate);

                    var table = new System.Windows.Documents.Table();
                    table.CellSpacing = 0;
                    table.BorderThickness = new System.Windows.Thickness(1);
                    table.BorderBrush = System.Windows.Media.Brushes.LightGray;

                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(190) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(120) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(130) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(84) });

                    var rowGroup = new System.Windows.Documents.TableRowGroup();

                    var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
                    string[] headers = { "RETURN #", "DATE", "VENDOR", "AGAINST INV #", "RETURN AMOUNT", "STATUS" };
                    foreach (var h in headers)
                    {
                        var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 9,
                            Margin = new System.Windows.Thickness(4)
                        });
                        headerRow.Cells.Add(cell);
                    }
                    rowGroup.Rows.Add(headerRow);

                    int rowIdx = 0;
                    foreach (var ret in PurchaseReturns)
                    {
                        var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                        var r = new System.Windows.Documents.TableRow { Background = bg };

                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.PurchaseNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.VendorName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.LinkedRef ?? "-")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{ret.TotalAmount:N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.Status ?? "Posted")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));

                        rowGroup.Rows.Add(r);
                        rowIdx++;
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{PurchaseReturns.Count} Returns")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{PurchaseReturns.Sum(p => p.TotalAmount):N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Purchase Returns Register");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public partial class InventoryViewModel : ObservableObject
    {
        private readonly IInventoryService _inventoryService;

        [ObservableProperty]
        private ObservableCollection<Item> _items = new();

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private ObservableCollection<Unit> _units = new();

        [ObservableProperty]
        private Item _selectedItem = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        private System.Threading.CancellationTokenSource? _inventorySearchCts;

        partial void OnSearchQueryChanged(string value)
        {
            _inventorySearchCts?.Cancel();
            _inventorySearchCts = new System.Threading.CancellationTokenSource();
            var token = _inventorySearchCts.Token;

            Task.Delay(250, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
                    {
                        await LoadInventoryAsync();
                    });
                }
            }, TaskScheduler.Default);
        }

        [ObservableProperty]
        private Category? _selectedCategory;

        partial void OnSelectedCategoryChanged(Category? value)
        {
            _ = LoadInventoryAsync();
        }

        [ObservableProperty]
        private bool _hasNoItemsFound;

        // Stat Cards Metrics (Screenshot 10)
        [ObservableProperty]
        private int _totalItemsCount = 0;

        [ObservableProperty]
        private int _lowStockCount = 0;

        [ObservableProperty]
        private decimal _totalValue = 0m;

        [ObservableProperty]
        private decimal _avgPurchaseRate = 0m;

        [ObservableProperty]
        private bool _isAddItemModalOpen;

        // Category Modal State
        [ObservableProperty]
        private bool _isAddCategoryModalOpen;

        [ObservableProperty]
        private string _newCategoryName = string.Empty;

        public InventoryViewModel(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [RelayCommand]
        public void SelectCategory(Category? category)
        {
            SelectedCategory = category;
            _ = LoadInventoryAsync();
        }

        [RelayCommand]
        public async Task DeleteCategoryAsync(Category? category)
        {
            if (category == null || category.Id <= 0) return;

            try
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete category '{category.Name}'?",
                    "Confirm Delete Category",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    await _inventoryService.DeleteCategoryAsync(category.Id);
                    if (SelectedCategory?.Id == category.Id)
                    {
                        SelectedCategory = null;
                    }
                    var catList = await _inventoryService.GetCategoriesAsync();
                    Categories.Clear();
                    foreach (var c in catList) Categories.Add(c);
                    await LoadInventoryAsync();
                    System.Windows.MessageBox.Show($"Category '{category.Name}' deleted successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Cannot Delete Category", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        public async Task LoadInventoryAsync()
        {
            int? catId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : null;
            var list = await _inventoryService.SearchItemsAsync(SearchQuery ?? "", catId);

            if (catId == null && SelectedCategory != null && !string.IsNullOrWhiteSpace(SelectedCategory.Name) && !SelectedCategory.Name.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
            {
                var catName = SelectedCategory.Name.Trim().ToLower();
                list = list.Where(i => (i.CategoryName ?? "").Trim().ToLower().Equals(catName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            Items.Clear();
            foreach (var item in list)
            {
                Items.Add(item);
            }

            HasNoItemsFound = Items.Count == 0;

            TotalItemsCount = list.Count;
            LowStockCount = list.Count(i => i.CurrentStock <= (i.LowStockAlert > 0 ? i.LowStockAlert : 5));
            TotalValue = list.Sum(i => i.CurrentStock * i.PurchasePrice);
            AvgPurchaseRate = list.Count > 0 ? list.Average(i => i.PurchasePrice) : 0m;

            var catList = await _inventoryService.GetCategoriesAsync();
            Categories.Clear();
            foreach (var c in catList) Categories.Add(c);

            if (Units.Count == 0)
            {
                var uList = await _inventoryService.GetUnitsAsync();
                Units.Clear();
                foreach (var u in uList) Units.Add(u);
            }
        }

        [RelayCommand]
        private void OpenAddItemModal()
        {
            SelectedItem = new Item
            {
                Code = "ITEM-" + DateTime.Now.ToString("fffSSm"),
                Name = string.Empty,
                CategoryName = Categories.FirstOrDefault()?.Name ?? "Cement & Aggregates",
                SellingUnit = "Per Piece",
                BaseUnit = "Per Piece",
                PurchaseUnitName = "Per Piece",
                SaleUnitName = "Per Piece",
                ConversionFactor = 1.0,
                Quality = "Premium",
                Status = "Active",
                Warehouse = "Godown A"
            };
            IsAddItemModalOpen = true;
        }

        [RelayCommand]
        private void CloseAddItemModal()
        {
            IsAddItemModalOpen = false;
        }

        [RelayCommand]
        private void EditItem(Item item)
        {
            if (item != null)
            {
                SelectedItem = item;
                IsAddItemModalOpen = true;
            }
        }

        [RelayCommand]
        public async Task SaveItemAsync()
        {
            // All fields are 100% optional; auto-default if empty
            if (SelectedItem != null)
            {
                if (string.IsNullOrWhiteSpace(SelectedItem.Name))
                {
                    SelectedItem.Name = "Item " + DateTime.Now.ToString("fffSSm");
                }
                if (string.IsNullOrWhiteSpace(SelectedItem.Code))
                {
                    SelectedItem.Code = "ITM-" + DateTime.Now.ToString("fffSSm");
                }
                if (string.IsNullOrWhiteSpace(SelectedItem.SellingUnit))
                {
                    SelectedItem.SellingUnit = "Per Piece";
                }

                // Sync base, purchase, and sale units automatically
                SelectedItem.BaseUnit = SelectedItem.SellingUnit;
                SelectedItem.PurchaseUnitName = SelectedItem.SellingUnit;
                SelectedItem.SaleUnitName = SelectedItem.SellingUnit;

                // Auto-save typed CategoryName to Categories table if new
                if (!string.IsNullOrWhiteSpace(SelectedItem.CategoryName))
                {
                    var existingCat = Categories.FirstOrDefault(c => c.Name.Equals(SelectedItem.CategoryName, StringComparison.OrdinalIgnoreCase));
                    if (existingCat == null)
                    {
                        var newCat = await _inventoryService.SaveCategoryAsync(new Category { Name = SelectedItem.CategoryName.Trim() });
                        SelectedItem.CategoryId = newCat.Id;
                    }
                    else
                    {
                        SelectedItem.CategoryId = existingCat.Id;
                    }
                }

                // Detach navigation properties to avoid EF tracking conflicts
                SelectedItem.Category = null;
                SelectedItem.Subcategory = null;
                SelectedItem.PurchaseUnit = null;
                SelectedItem.SaleUnit = null;

                await _inventoryService.SaveItemAsync(SelectedItem);
                SelectedItem = new Item();
            }
            IsAddItemModalOpen = false;
            await LoadInventoryAsync();
        }

        [RelayCommand]
        public async Task DeleteItemAsync(Item item)
        {
            if (item != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete item '{item.Name}' ({item.Code})?",
                    "Confirm Delete Item",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _inventoryService.DeleteItemAsync(item.Id);
                        await LoadInventoryAsync();
                        System.Windows.MessageBox.Show($"Item '{item.Name}' deleted successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete item: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public void PrintItemList()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var doc = new System.Windows.Documents.FlowDocument
                    {
                        PagePadding = new System.Windows.Thickness(30),
                        FontFamily = new System.Windows.Media.FontFamily("Times New Roman"),
                        FontSize = 11,
                        ColumnWidth = 999999
                    };

                    var titleBlock = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("AL MADINA BUILDING MATERIAL ERP — CHART OF INVENTORY"))
                    {
                        FontSize = 16,
                        FontWeight = System.Windows.FontWeights.Bold,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(titleBlock);

                    var table = new System.Windows.Documents.Table();
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(200) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(120) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });

                    var headerRow = new System.Windows.Documents.TableRow();
                    headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("CODE")) { FontWeight = System.Windows.FontWeights.Bold }));
                    headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("ITEM NAME")) { FontWeight = System.Windows.FontWeights.Bold }));
                    headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("CATEGORY")) { FontWeight = System.Windows.FontWeights.Bold }));
                    headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("STOCK")) { FontWeight = System.Windows.FontWeights.Bold }));
                    headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("UNIT")) { FontWeight = System.Windows.FontWeights.Bold }));
                    headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("SALE PRICE")) { FontWeight = System.Windows.FontWeights.Bold }));

                    var rowGroup = new System.Windows.Documents.TableRowGroup();
                    rowGroup.Rows.Add(headerRow);

                    foreach (var item in Items)
                    {
                        var row = new System.Windows.Documents.TableRow();
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(item.Code ?? ""))));
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(item.Name ?? ""))));
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(item.CategoryName ?? ""))));
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(item.CurrentStock.ToString("N0")))));
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(item.SellingUnit ?? "PCS"))));
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {item.SalePrice:N0}"))));
                        rowGroup.Rows.Add(row);
                    }

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Chart of Inventory");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        // Category Modal Handlers
        [RelayCommand]
        private void OpenAddCategoryModal()
        {
            NewCategoryName = string.Empty;
            IsAddCategoryModalOpen = true;
        }

        [RelayCommand]
        private void CloseAddCategoryModal()
        {
            IsAddCategoryModalOpen = false;
        }

        [RelayCommand]
        public async Task SaveCategoryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                NewCategoryName = "Category " + DateTime.Now.ToString("fffSSm");
            }

            var newCat = new Category { Name = NewCategoryName.Trim() };
            await _inventoryService.SaveCategoryAsync(newCat);
            NewCategoryName = string.Empty;
            IsAddCategoryModalOpen = false;
            await LoadInventoryAsync();
        }
    }

    public partial class ChartOfInventoryViewModel : ObservableObject
    {
        private readonly IInventoryService _inventoryService;

        [ObservableProperty]
        private ObservableCollection<Category> _categories = new();

        [ObservableProperty]
        private ObservableCollection<Item> _items = new();

        [ObservableProperty]
        private Category? _selectedCategory;

        [ObservableProperty]
        private Item? _selectedItem;

        public ChartOfInventoryViewModel(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [RelayCommand]
        public async Task LoadChartAsync()
        {
            var catList = await _inventoryService.GetCategoriesAsync();
            Categories = new ObservableCollection<Category>(catList);
            SelectedCategory = Categories.FirstOrDefault();

            var subList = await _inventoryService.GetSubcategoriesAsync(SelectedCategory?.Id);
            Subcategories = new ObservableCollection<Subcategory>(subList);

            var itemList = await _inventoryService.SearchItemsAsync("");
            Items = new ObservableCollection<Item>(itemList);
            if (SelectedItem == null || !Items.Any(i => i.Id == SelectedItem.Id))
            {
                SelectedItem = Items.FirstOrDefault();
            }
        }

        [ObservableProperty]
        private ObservableCollection<Subcategory> _subcategories = new();

        [RelayCommand]
        public void AddNewItem()
        {
            SelectedItem = new Item
            {
                Code = "ITM-" + DateTime.Now.ToString("fffSSm"),
                Name = "New Item " + DateTime.Now.ToString("fffSSm"),
                CategoryName = SelectedCategory?.Name ?? "General",
                SellingUnit = "Pcs",
                BaseUnit = "Pcs",
                Status = "Active"
            };
        }

        [RelayCommand]
        public async Task AddNewCategoryAsync()
        {
            var catName = "Cat " + DateTime.Now.ToString("fffSSm");
            var newCat = await _inventoryService.SaveCategoryAsync(new Category { Name = catName });
            await LoadChartAsync();
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == newCat.Id);
        }

        [RelayCommand]
        public async Task AddNewSubcategoryAsync()
        {
            if (SelectedCategory == null)
            {
                await AddNewCategoryAsync();
            }

            var subName = "SubCat " + DateTime.Now.ToString("fffSSm");
            await _inventoryService.SaveSubcategoryAsync(new Subcategory { Name = subName, CategoryId = SelectedCategory!.Id });
            await LoadChartAsync();
        }

        [RelayCommand]
        public async Task SaveSelectedItemAsync()
        {
            if (SelectedItem != null)
            {
                if (string.IsNullOrWhiteSpace(SelectedItem.Name))
                {
                    SelectedItem.Name = "Item " + DateTime.Now.ToString("fffSSm");
                }
                if (string.IsNullOrWhiteSpace(SelectedItem.Code))
                {
                    SelectedItem.Code = "ITM-" + DateTime.Now.ToString("fffSSm");
                }
                if (string.IsNullOrWhiteSpace(SelectedItem.SellingUnit))
                {
                    SelectedItem.SellingUnit = "Pcs";
                }
                if (SelectedCategory != null && (SelectedItem.CategoryId == null || SelectedItem.CategoryId <= 0))
                {
                    SelectedItem.CategoryId = SelectedCategory.Id;
                    SelectedItem.CategoryName = SelectedCategory.Name;
                }

                SelectedItem.Category = null;
                SelectedItem.Subcategory = null;
                SelectedItem.PurchaseUnit = null;
                SelectedItem.SaleUnit = null;

                await _inventoryService.SaveItemAsync(SelectedItem);
                await LoadChartAsync();
            }
        }

        [RelayCommand]
        public async Task DeleteSelectedItemAsync()
        {
            if (SelectedItem != null && SelectedItem.Id > 0)
            {
                await _inventoryService.DeleteItemAsync(SelectedItem.Id);
                SelectedItem = null;
                await LoadChartAsync();
            }
        }
    }
}
