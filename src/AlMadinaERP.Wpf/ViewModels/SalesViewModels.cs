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

        private System.Threading.CancellationTokenSource? _salesSearchCts;

        partial void OnSearchQueryChanged(string value)
        {
            _salesSearchCts?.Cancel();
            _salesSearchCts = new System.Threading.CancellationTokenSource();
            var token = _salesSearchCts.Token;

            Task.Delay(250, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
                    {
                        await LoadInvoicesAsync();
                    });
                }
            }, TaskScheduler.Default);
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

        private void ResetNewInvoice()
        {
            var prefix = IsReturnMode ? "SR-" :
                         ActiveSubView == SalesActiveSubView.PosTerminal ? "POS-" : "SI-";

            SelectedCustomer = null;
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

            if (Customers.Count == 0)
            {
                var custs = await _customerService.SearchCustomersAsync("");
                Customers = new ObservableCollection<Customer>(custs);
            }

            if (AvailableItems.Count == 0)
            {
                var items = await _inventoryService.SearchItemsAsync("");
                AvailableItems = new ObservableCollection<Item>(items);
            }
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (NewInvoice != null)
            {
                if (value != null)
                {
                    NewInvoice.IsCashSale = false;
                    NewInvoice.CustomerId = value.Id;
                    NewInvoice.CustomerName = value.Name;
                }
                else
                {
                    NewInvoice.IsCashSale = true;
                    NewInvoice.CustomerId = null;
                    NewInvoice.CustomerName = "WALK-IN CUSTOMER";
                }
            }
        }

        private async Task EnsureItemsLoadedAsync()
        {
            if (AvailableItems.Count == 0)
            {
                var items = await _inventoryService.SearchItemsAsync("");
                foreach (var item in items) AvailableItems.Add(item);
            }
            if (Customers.Count == 0)
            {
                var custs = await _customerService.SearchCustomersAsync("");
                foreach (var c in custs) Customers.Add(c);
            }
        }

        [RelayCommand]
        public void OpenCreateInvoice()
        {
            IsReturnMode = false;
            ActiveSubView = SalesActiveSubView.SaleInvoiceForm;
            ResetNewInvoice();
            _ = EnsureItemsLoadedAsync();
        }

        [RelayCommand]
        public void OpenNewReturnForm()
        {
            IsReturnMode = true;
            ActiveSubView = SalesActiveSubView.SaleReturnForm;
            ResetNewInvoice();
            _ = EnsureItemsLoadedAsync();
        }

        [RelayCommand]
        public void OpenNewPosTerminal()
        {
            IsReturnMode = false;
            ActiveSubView = SalesActiveSubView.PosTerminal;
            ResetNewInvoice();
            _ = EnsureItemsLoadedAsync();
        }

        [RelayCommand]
        public void CloseSubView()
        {
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

            newItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SaleInvoiceItem.Quantity) ||
                    e.PropertyName == nameof(SaleInvoiceItem.Rate) ||
                    e.PropertyName == nameof(SaleInvoiceItem.DiscountPercent) ||
                    e.PropertyName == nameof(SaleInvoiceItem.Item))
                {
                    RecalculateTotals();
                }
            };

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
                NewInvoice.Items.Remove(item);
                RecalculateTotals();
            }
            else if (NewInvoice.Items.Count > 0)
            {
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
                    var lineSubtotal = item.Quantity * item.Rate;
                    var disc = (lineSubtotal * item.DiscountPercent) / 100m;
                    var total = lineSubtotal - disc;

                    if (item.DiscountAmount != disc) item.DiscountAmount = disc;
                    if (item.TotalPrice != total) item.TotalPrice = total;
                }

                NewInvoice.Subtotal = NewInvoice.Items.Sum(i => i.Quantity * i.Rate);
                NewInvoice.DiscountAmount = NewInvoice.Items.Sum(i => i.DiscountAmount);
                NewInvoice.TotalAmount = Math.Max(0m, (NewInvoice.Subtotal - NewInvoice.DiscountAmount) + NewInvoice.ExtraCharges - NewInvoice.AdditionalDiscount);
                
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
            NewInvoice.Status = "Draft";
            await SaveInternalAsync();
        }

        [RelayCommand]
        public async Task SaveInvoicePostedAsync()
        {
            NewInvoice.Status = "Posted";
            await SaveInternalAsync();
        }

        private async Task SaveInternalAsync()
        {
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
                System.Windows.MessageBox.Show(
                    "Please select at least one item from the list before saving.",
                    "No Items Selected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            NewInvoice.Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>(validItems);

            if (SelectedCustomer != null)
            {
                NewInvoice.CustomerId = SelectedCustomer.Id;
                NewInvoice.CustomerName = SelectedCustomer.Name;
            }

            var savedInvoice = await _saleService.SaveSaleInvoiceAsync(NewInvoice);

            var confirmPrint = System.Windows.MessageBox.Show(
                "Sale Invoice saved successfully! Do you want to print receipt?",
                "Print Receipt",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirmPrint == System.Windows.MessageBoxResult.Yes)
            {
                var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
                _printService.PrintThermalReceipt(savedInvoice, company);
            }

            CloseSubView();
            await LoadInvoicesAsync();
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
                lineItem.PropertyChanged += (s, e) => RecalculateTotals();
                NewInvoice.Items.Add(lineItem);
            }
            RecalculateTotals();
        }

        [RelayCommand]
        public async Task EditInvoiceAsync(SaleInvoice invoice)
        {
            if (invoice == null) return;
            var fullInvoice = await _saleService.GetSaleInvoiceByIdAsync(invoice.Id);
            if (fullInvoice == null) return;

            NewInvoice = fullInvoice;
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == fullInvoice.CustomerId);

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
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SaleInvoiceItem.Quantity) ||
                        e.PropertyName == nameof(SaleInvoiceItem.Rate) ||
                        e.PropertyName == nameof(SaleInvoiceItem.DiscountPercent) ||
                        e.PropertyName == nameof(SaleInvoiceItem.Item))
                    {
                        RecalculateTotals();
                    }
                };
            }
            RecalculateTotals();
        }

        [RelayCommand]
        public void PrintSalesList()
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

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("SALE INVOICES REGISTER"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pDate = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Invoices: {Invoices.Count}"))
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
                    string[] headers = { "INVOICE #", "DATE", "CUSTOMER", "TOTAL (PKR)", "PAID (PKR)", "BALANCE (PKR)", "STATUS" };
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
                    foreach (var inv in Invoices)
                    {
                        var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                        var r = new System.Windows.Documents.TableRow { Background = bg };

                        decimal bal = inv.TotalAmount - inv.PaidAmount;

                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(inv.InvoiceNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(inv.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(inv.CustomerName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{inv.TotalAmount:N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{inv.PaidAmount:N0}")) { FontSize = 9, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{bal:N0}")) { FontSize = 9, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(inv.Status ?? "Posted")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));

                        rowGroup.Rows.Add(r);
                        rowIdx++;
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Invoices.Count} Invoices")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{GrandTotalSales:N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Invoices.Sum(i => i.PaidAmount):N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Invoices.Sum(i => i.TotalAmount - i.PaidAmount):N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Sale Invoices Register");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void PrintSalesReturnList()
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

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("SALE RETURNS REGISTER"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pDate = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Returns: {SaleReturns.Count}"))
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
                    string[] headers = { "RETURN #", "DATE", "CUSTOMER", "AGAINST INV #", "RETURN AMOUNT", "STATUS" };
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
                    foreach (var ret in SaleReturns)
                    {
                        var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                        var r = new System.Windows.Documents.TableRow { Background = bg };

                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.InvoiceNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.CustomerName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.AgainstInvoiceNo ?? "-")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{ret.TotalAmount:N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(ret.Status ?? "Posted")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));

                        rowGroup.Rows.Add(r);
                        rowIdx++;
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{SaleReturns.Count} Returns")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{GrandTotalReturns:N0}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 9 }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Sale Returns Register");
                }
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
