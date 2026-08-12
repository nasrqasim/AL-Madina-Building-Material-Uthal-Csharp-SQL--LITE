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

        partial void OnSearchQueryChanged(string value) => _ = LoadPurchasesAsync();
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

        private void ResetNewPurchase()
        {
            SelectedVendor = null;
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
        public async Task LoadPurchasesAsync()
        {
            var toDateEnd = ToDate.HasValue ? ToDate.Value.Date.AddDays(1).AddSeconds(-1) : (DateTime?)null;
            var list = await _purchaseService.SearchPurchasesAsync(SearchQuery, FromDate, toDateEnd);

            if (!string.IsNullOrEmpty(StatusFilter) && StatusFilter != "All")
                list = list.Where(p => p.Status == StatusFilter).ToList();

            var invoices = list.Where(p => p.Type == PurchaseType.PurchaseInvoice).ToList();
            var returns = list.Where(p => p.Type == PurchaseType.PurchaseReturn).ToList();

            Purchases.Clear();
            foreach (var inv in invoices) Purchases.Add(inv);
            PurchaseReturns.Clear();
            foreach (var ret in returns) PurchaseReturns.Add(ret);

            // Metrics for Invoices
            TotalInvoicesCount = invoices.Count;
            OutstandingAmount = invoices.Sum(i => i.OutstandingAmount > 0 ? i.OutstandingAmount : i.TotalAmount);
            PostedInvoicesCount = invoices.Count(i => i.Status == "Posted");
            PaidInvoicesCount = invoices.Count(i => i.Status == "Paid");

            // Metrics for Returns
            TotalReturnsCount = returns.Count;
            PostedReturnsCount = returns.Count(r => r.Status == "Posted");
            DraftReturnsCount = returns.Count(r => r.Status == "Draft");

            var vList = await _vendorService.SearchVendorsAsync("");
            Vendors.Clear();
            foreach (var v in vList) Vendors.Add(v);

            var iList = await _inventoryService.SearchItemsAsync("");
            AvailableItems.Clear();
            foreach (var item in iList) AvailableItems.Add(item);
        }

        partial void OnSelectedVendorChanged(Vendor? value)
        {
            if (value != null)
            {
                NewPurchase.VendorId = value.Id;
                NewPurchase.VendorName = value.Name;
            }
            else
            {
                NewPurchase.VendorId = null;
                NewPurchase.VendorName = "Direct / Walk-in Purchase (No Vendor)";
            }
        }

        private async Task EnsureItemsLoadedAsync()
        {
            if (AvailableItems.Count == 0)
            {
                var items = await _inventoryService.SearchItemsAsync("");
                foreach (var item in items) AvailableItems.Add(item);
            }
            if (Vendors.Count == 0)
            {
                var vList = await _vendorService.SearchVendorsAsync("");
                foreach (var v in vList) Vendors.Add(v);
            }
        }

        [RelayCommand]
        public void OpenNewInvoiceForm()
        {
            IsReturnMode = false;
            ResetNewPurchase();
            IsFormVisible = true;
            _ = EnsureItemsLoadedAsync();
        }

        [RelayCommand]
        public void OpenNewReturnForm()
        {
            IsReturnMode = true;
            ResetNewPurchase();
            IsFormVisible = true;
            _ = EnsureItemsLoadedAsync();
        }

        [RelayCommand]
        public void CloseForm()
        {
            IsFormVisible = false;
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

            newItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PurchaseInvoiceItem.Quantity) ||
                    e.PropertyName == nameof(PurchaseInvoiceItem.Rate) ||
                    e.PropertyName == nameof(PurchaseInvoiceItem.DiscountPercent) ||
                    e.PropertyName == nameof(PurchaseInvoiceItem.TaxPercent) ||
                    e.PropertyName == nameof(PurchaseInvoiceItem.Item))
                {
                    RecalculateTotals();
                }
            };

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
                NewPurchase.Items.Remove(item);
                RecalculateTotals();
            }
            else if (NewPurchase.Items.Count > 0)
            {
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
                    var lineSubtotal = item.Quantity * item.Rate;
                    var disc = (lineSubtotal * item.DiscountPercent) / 100m;
                    var tax = ((lineSubtotal - disc) * item.TaxPercent) / 100m;
                    var total = lineSubtotal - disc + tax;

                    if (item.DiscountAmount != disc) item.DiscountAmount = disc;
                    if (item.TaxAmount != tax) item.TaxAmount = tax;
                    if (item.TotalPrice != total) item.TotalPrice = total;
                }

                NewPurchase.Subtotal = NewPurchase.Items.Sum(i => i.Quantity * i.Rate);
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
            NewPurchase.Status = "Draft";
            await SaveInternalAsync();
        }

        [RelayCommand]
        public async Task SavePurchasePostedAsync()
        {
            NewPurchase.Status = "Posted";
            await SaveInternalAsync();
        }

        private async Task SaveInternalAsync()
        {
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
                System.Windows.MessageBox.Show(
                    "Please select at least one item from the list before saving.",
                    "No Items Selected",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
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

            await _purchaseService.SavePurchaseInvoiceAsync(NewPurchase);
            IsFormVisible = false;
            await LoadPurchasesAsync();
        }

        [RelayCommand]
        public async Task DeletePurchaseAsync(PurchaseInvoice purchase)
        {
            if (purchase != null)
            {
                await _purchaseService.DeletePurchaseInvoiceAsync(purchase.Id);
                await LoadPurchasesAsync();
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
            if (purchase == null) return;
            var fullPurchase = await _purchaseService.GetPurchaseInvoiceByIdAsync(purchase.Id);
            if (fullPurchase == null) return;

            NewPurchase = fullPurchase;
            SelectedVendor = Vendors.FirstOrDefault(v => v.Id == fullPurchase.VendorId);
            IsReturnMode = fullPurchase.Type == PurchaseType.PurchaseReturn;
            IsFormVisible = true;

            foreach (var item in NewPurchase.Items)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PurchaseInvoiceItem.Quantity) ||
                        e.PropertyName == nameof(PurchaseInvoiceItem.Rate) ||
                        e.PropertyName == nameof(PurchaseInvoiceItem.DiscountPercent) ||
                        e.PropertyName == nameof(PurchaseInvoiceItem.Item))
                    {
                        RecalculateTotals();
                    }
                };
            }
            RecalculateTotals();
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

        partial void OnSearchQueryChanged(string value)
        {
            _ = LoadInventoryAsync();
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
        public async Task LoadInventoryAsync()
        {
            var list = await _inventoryService.SearchItemsAsync(SearchQuery ?? "");

            if (SelectedCategory != null && !string.IsNullOrWhiteSpace(SelectedCategory.Name) && !SelectedCategory.Name.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
            {
                var catName = SelectedCategory.Name.Trim().ToLower();
                list = list.Where(i => (i.CategoryName ?? "").Trim().ToLower().Contains(catName)).ToList();
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

            if (Categories.Count == 0)
            {
                var catList = await _inventoryService.GetCategoriesAsync();
                Categories.Clear();
                foreach (var c in catList) Categories.Add(c);
            }

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
                await _inventoryService.DeleteItemAsync(item.Id);
                await LoadInventoryAsync();
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
