using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;

namespace AlMadinaERP.Wpf.ViewModels
{
    public partial class CustomersViewModel : ObservableObject
    {
        private readonly ICustomerService _customerService;

        [ObservableProperty]
        private ObservableCollection<CustomerBalanceDto> _customers = new();

        [ObservableProperty]
        private ObservableCollection<CustomerLedger> _customerLedgerEntries = new();

        [ObservableProperty]
        private Customer _selectedCustomer = new();

        [ObservableProperty]
        private CustomerBalanceDto? _selectedCustomerDto;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        // Top Summary Metric Cards
        [ObservableProperty]
        private int _totalCustomersCount = 0;

        [ObservableProperty]
        private decimal _totalCustomerReceivable = 0m;

        [ObservableProperty]
        private decimal _totalCustomerAdvance = 0m;

        [ObservableProperty]
        private decimal _netCustomerBalance = 0m;

        [ObservableProperty]
        private string _selectedFilter = "All";

        [ObservableProperty]
        private bool _isAddCustomerModalOpen;

        [ObservableProperty]
        private bool _isLedgerModalOpen;

        [ObservableProperty]
        private ObservableCollection<CustomerPurchasedItemDto> _purchasedItems = new();

        [ObservableProperty]
        private ObservableCollection<PaymentHistoryDto> _paymentHistory = new();

        [ObservableProperty]
        private ObservableCollection<OutstandingInvoiceDto> _outstandingInvoices = new();

        [ObservableProperty]
        private int _selectedDetailTabIndex = 0;

        // Stat Cards
        [ObservableProperty]
        private decimal _totalSalesValue;

        [ObservableProperty]
        private decimal _totalPaymentsReceived;

        [ObservableProperty]
        private string _lastSaleInfo = "No sales recorded";

        [ObservableProperty]
        private string _lastPaymentInfo = "No payments recorded";

        // Tab Totals
        [ObservableProperty]
        private decimal _totalPurchasedQty;

        [ObservableProperty]
        private decimal _totalPurchasedAmount;

        [ObservableProperty]
        private decimal _totalPurchasedPaid;

        [ObservableProperty]
        private decimal _totalPurchasedOutstanding;

        [ObservableProperty]
        private decimal _totalPaymentHistoryAmount;

        [ObservableProperty]
        private decimal _totalOutstandingInvoiceAmount;

        [ObservableProperty]
        private decimal _totalOutstandingPaidAmount;

        [ObservableProperty]
        private decimal _totalOutstandingBalanceDue;

        [ObservableProperty]
        private decimal _ledgerTotalDebit;

        [ObservableProperty]
        private decimal _ledgerTotalCredit;

        public CustomersViewModel(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [RelayCommand]
        public async Task LoadCustomersAsync()
        {
            var list = await _customerService.GetCustomerBalancesAsync(SearchQuery);

            if (SelectedFilter == "Customer Owes")
                list = list.Where(c => c.CustomerOwes > 0).ToList();
            else if (SelectedFilter == "Advance Available")
                list = list.Where(c => c.AdvanceAvailable > 0).ToList();
            else if (SelectedFilter == "Settled")
                list = list.Where(c => c.CustomerOwes == 0 && c.AdvanceAvailable == 0).ToList();

            Customers = new ObservableCollection<CustomerBalanceDto>(list);

            TotalCustomersCount = list.Count;
            TotalCustomerReceivable = list.Sum(c => c.CustomerOwes);
            TotalCustomerAdvance = list.Sum(c => c.AdvanceAvailable);
            NetCustomerBalance = TotalCustomerReceivable - TotalCustomerAdvance;
        }

        [RelayCommand]
        private async Task FilterStatusAsync(string filter)
        {
            SelectedFilter = filter;
            await LoadCustomersAsync();
        }

        partial void OnSearchQueryChanged(string value)
        {
            _ = LoadCustomersAsync();
        }

        [RelayCommand]
        private async Task OpenAddCustomerModal()
        {
            var nextCode = await _customerService.GetNextCustomerCodeAsync();
            SelectedCustomer = new Customer { Code = nextCode };
            IsAddCustomerModalOpen = true;
        }

        [RelayCommand]
        private void CloseAddCustomerModal()
        {
            IsAddCustomerModalOpen = false;
        }

        [RelayCommand]
        public async Task EditCustomerAsync(CustomerBalanceDto? dto)
        {
            if (dto == null) return;
            var cust = await _customerService.GetCustomerByIdAsync(dto.Id);
            if (cust != null)
            {
                SelectedCustomer = cust;
                IsAddCustomerModalOpen = true;
            }
        }

        [RelayCommand]
        public async Task DeleteCustomerAsync(int customerId)
        {
            await _customerService.DeleteCustomerAsync(customerId);
            await LoadCustomersAsync();
        }

        [RelayCommand]
        public async Task SaveCustomerAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedCustomer.Name)) return;

            await _customerService.SaveCustomerAsync(SelectedCustomer);
            SelectedCustomer = new Customer();
            IsAddCustomerModalOpen = false;
            await LoadCustomersAsync();
        }

        [RelayCommand]
        public void CloseLedgerModal()
        {
            IsLedgerModalOpen = false;
        }

        [RelayCommand]
        public async Task LoadCustomerLedgerAsync(int customerId)
        {
            SelectedCustomerDto = Customers.FirstOrDefault(c => c.Id == customerId);
            var entries = await _customerService.GetCustomerLedgerAsync(customerId);
            CustomerLedgerEntries = new ObservableCollection<CustomerLedger>(entries);

            var items = await _customerService.GetCustomerPurchasedItemsAsync(customerId);
            PurchasedItems = new ObservableCollection<CustomerPurchasedItemDto>(items);

            var payments = await _customerService.GetCustomerReceiptsAndPaymentsAsync(customerId);
            PaymentHistory = new ObservableCollection<PaymentHistoryDto>(payments);

            var outstanding = await _customerService.GetCustomerOutstandingInvoicesAsync(customerId);
            OutstandingInvoices = new ObservableCollection<OutstandingInvoiceDto>(outstanding);

            // Calculate Stat Cards & Grand Totals
            TotalSalesValue = items.Sum(i => i.TotalAmount);
            TotalPaymentsReceived = payments.Sum(p => p.Amount);

            var lastSale = items.FirstOrDefault();
            LastSaleInfo = lastSale != null ? $"{lastSale.Date:yyyy-MM-dd} ({lastSale.InvoiceNumber})" : "No sales recorded";

            var lastPayment = payments.FirstOrDefault();
            LastPaymentInfo = lastPayment != null ? $"{lastPayment.Date:yyyy-MM-dd} (Rs. {lastPayment.Amount:N0})" : "No payments recorded";

            TotalPurchasedQty = items.Sum(i => i.Quantity);
            TotalPurchasedAmount = items.Sum(i => i.TotalAmount);
            TotalPurchasedPaid = items.Sum(i => i.PaidAmount);
            TotalPurchasedOutstanding = items.Sum(i => i.OutstandingBalance);

            TotalPaymentHistoryAmount = payments.Sum(p => p.Amount);

            TotalOutstandingInvoiceAmount = outstanding.Sum(o => o.TotalAmount);
            TotalOutstandingPaidAmount = outstanding.Sum(o => o.PaidAmount);
            TotalOutstandingBalanceDue = outstanding.Sum(o => o.BalanceDue);

            LedgerTotalDebit = entries.Sum(e => e.Debit);
            LedgerTotalCredit = entries.Sum(e => e.Credit);

            IsLedgerModalOpen = true;
        }

        [RelayCommand]
        public void PrintCustomerLedger()
        {
            if (SelectedCustomerDto == null && CustomerLedgerEntries.Count == 0)
            {
                System.Windows.MessageBox.Show("No customer ledger opened to print.", "Print Ledger", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var custName = SelectedCustomerDto?.Name ?? "Customer";
                    var custCode = SelectedCustomerDto?.Code ?? "";
                    var custPhone = SelectedCustomerDto?.Phone ?? "N/A";
                    var custNetBal = SelectedCustomerDto?.NetBalance ?? 0m;

                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PageWidth = 794;
                    doc.PageHeight = 1123;
                    doc.PagePadding = new System.Windows.Thickness(40);
                    doc.ColumnWidth = 714;
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

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"CUSTOMER LEDGER STATEMENT - {custName.ToUpper()} ({custCode})"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pMeta = new System.Windows.Documents.Paragraph();
                    pMeta.Inlines.Add(new System.Windows.Documents.Run($"Phone: {custPhone}   |   Current Net Balance: PKR {custNetBal:N2}\n") { FontWeight = System.Windows.FontWeights.Bold });
                    pMeta.Inlines.Add(new System.Windows.Documents.Run($"Statement Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Entries: {CustomerLedgerEntries.Count}"));
                    pMeta.FontSize = 10;
                    pMeta.Margin = new System.Windows.Thickness(0, 0, 0, 14);
                    doc.Blocks.Add(pMeta);

                    var table = new System.Windows.Documents.Table { CellSpacing = 0, BorderThickness = new System.Windows.Thickness(0.5), BorderBrush = System.Windows.Media.Brushes.Gray };
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(80) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(180) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(85) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(85) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(94) });

                    var headerRowGroup = new System.Windows.Documents.TableRowGroup();
                    var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.DarkRed };
                    string[] headers = { "DATE", "VOUCHER #", "TYPE", "DESCRIPTION", "DEBIT (PKR)", "CREDIT (PKR)", "BALANCE (PKR)" };
                    foreach (var h in headers)
                    {
                        var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontSize = 9,
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            TextAlignment = h.Contains("PKR") ? System.Windows.TextAlignment.Right : System.Windows.TextAlignment.Left
                        })
                        { Padding = new System.Windows.Thickness(4) };
                        headerRow.Cells.Add(cell);
                    }
                    headerRowGroup.Rows.Add(headerRow);
                    table.RowGroups.Add(headerRowGroup);

                    var dataRowGroup = new System.Windows.Documents.TableRowGroup();
                    bool alt = false;
                    foreach (var entry in CustomerLedgerEntries)
                    {
                        var row = new System.Windows.Documents.TableRow
                        {
                            Background = alt ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White
                        };
                        alt = !alt;

                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Date.ToString("dd/MM/yyyy"))) { FontSize = 9 }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.VoucherNumber ?? "-")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.TransactionType ?? "-")) { FontSize = 9 }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Remarks ?? "-")) { FontSize = 9 }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Debit > 0 ? $"{entry.Debit:N2}" : "-")) { FontSize = 9, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Credit > 0 ? $"{entry.Credit:N2}" : "-")) { FontSize = 9, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{entry.RunningBalance:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });

                        dataRowGroup.Rows.Add(row);
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightGray };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold }) { ColumnSpan = 4, Padding = new System.Windows.Thickness(4) });
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{LedgerTotalDebit:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{LedgerTotalCredit:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{custNetBal:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                    dataRowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(dataRowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Customer Ledger - {custName}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void PrintCustomersList()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PageWidth = 794;  // Standard A4 Width (210mm @ 96 DPI)
                    doc.PageHeight = 1123; // Standard A4 Height (297mm @ 96 DPI)
                    doc.PagePadding = new System.Windows.Thickness(40);
                    doc.ColumnWidth = 714;
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

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("CUSTOMER MASTER & BALANCES REPORT"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pDate = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Customers: {Customers.Count}"))
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
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(180) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(140) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(140) });

                    var rowGroup = new System.Windows.Documents.TableRowGroup();

                    var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
                    string[] headers = { "CODE", "CUSTOMER NAME", "PHONE", "RECEIVABLE (OWES)", "ADVANCE BALANCE" };
                    foreach (var h in headers)
                    {
                        var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 10,
                            Margin = new System.Windows.Thickness(6, 4, 6, 4)
                        });
                        headerRow.Cells.Add(cell);
                    }
                    rowGroup.Rows.Add(headerRow);

                    int rowIdx = 0;
                    foreach (var c in Customers)
                    {
                        var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                        var r = new System.Windows.Documents.TableRow { Background = bg };

                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(c.Code ?? "")) { FontSize = 10, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(c.Name ?? "")) { FontSize = 10, FontWeight = System.Windows.FontWeights.SemiBold, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(c.Phone ?? "")) { FontSize = 10, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {c.CustomerOwes:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {c.AdvanceAvailable:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));

                        rowGroup.Rows.Add(r);
                        rowIdx++;
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{TotalCustomersCount} Customers")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 10 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {TotalCustomerReceivable:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {TotalCustomerAdvance:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Customer Balances List");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public partial class VendorsViewModel : ObservableObject
    {
        private readonly IVendorService _vendorService;

        [ObservableProperty]
        private ObservableCollection<VendorBalanceDto> _vendors = new();

        [ObservableProperty]
        private ObservableCollection<VendorLedger> _vendorLedgerEntries = new();

        [ObservableProperty]
        private Vendor _selectedVendor = new();

        [ObservableProperty]
        private VendorBalanceDto? _selectedVendorDto;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private int _totalVendorsCount = 0;

        [ObservableProperty]
        private decimal _totalVendorPayables = 0m;

        [ObservableProperty]
        private decimal _totalVendorAdvances = 0m;

        [ObservableProperty]
        private decimal _netVendorBalance = 0m;

        [ObservableProperty]
        private bool _isAddVendorModalOpen;

        [ObservableProperty]
        private bool _isLedgerModalOpen;

        [ObservableProperty]
        private ObservableCollection<VendorPurchasedItemDto> _purchasedItems = new();

        [ObservableProperty]
        private ObservableCollection<PaymentHistoryDto> _paymentHistory = new();

        [ObservableProperty]
        private ObservableCollection<OutstandingInvoiceDto> _outstandingInvoices = new();

        [ObservableProperty]
        private int _selectedDetailTabIndex = 0;

        // Stat Cards
        [ObservableProperty]
        private decimal _totalPurchasesValue;

        [ObservableProperty]
        private decimal _totalPaymentsMade;

        [ObservableProperty]
        private string _lastPurchaseInfo = "No purchases recorded";

        [ObservableProperty]
        private string _lastPaymentInfo = "No payments recorded";

        // Tab Totals
        [ObservableProperty]
        private decimal _totalPurchasedQty;

        [ObservableProperty]
        private decimal _totalPurchasedAmount;

        [ObservableProperty]
        private decimal _totalPurchasedPaid;

        [ObservableProperty]
        private decimal _totalPurchasedOutstanding;

        [ObservableProperty]
        private decimal _totalPaymentHistoryAmount;

        [ObservableProperty]
        private decimal _totalOutstandingInvoiceAmount;

        [ObservableProperty]
        private decimal _totalOutstandingPaidAmount;

        [ObservableProperty]
        private decimal _totalOutstandingBalanceDue;

        [ObservableProperty]
        private decimal _ledgerTotalDebit;

        [ObservableProperty]
        private decimal _ledgerTotalCredit;

        public VendorsViewModel(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        [RelayCommand]
        public async Task LoadVendorsAsync()
        {
            var list = await _vendorService.GetVendorBalancesAsync(SearchQuery);
            Vendors = new ObservableCollection<VendorBalanceDto>(list);

            TotalVendorsCount = list.Count;
            TotalVendorPayables = list.Sum(v => v.VendorOwes);
            TotalVendorAdvances = list.Sum(v => v.AdvanceAvailable);
            NetVendorBalance = TotalVendorPayables - TotalVendorAdvances;
        }

        partial void OnSearchQueryChanged(string value)
        {
            _ = LoadVendorsAsync();
        }

        [RelayCommand]
        private async Task OpenAddVendorModal()
        {
            var nextCode = await _vendorService.GetNextVendorCodeAsync();
            SelectedVendor = new Vendor { Code = nextCode };
            IsAddVendorModalOpen = true;
        }

        [RelayCommand]
        private void CloseAddVendorModal()
        {
            IsAddVendorModalOpen = false;
        }

        [RelayCommand]
        public async Task EditVendorAsync(VendorBalanceDto? dto)
        {
            if (dto == null) return;
            var vendor = await _vendorService.GetVendorByIdAsync(dto.Id);
            if (vendor != null)
            {
                SelectedVendor = vendor;
                IsAddVendorModalOpen = true;
            }
        }

        [RelayCommand]
        public async Task DeleteVendorAsync(int vendorId)
        {
            await _vendorService.DeleteVendorAsync(vendorId);
            await LoadVendorsAsync();
        }

        [RelayCommand]
        public async Task SaveVendorAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedVendor.Name)) return;

            await _vendorService.SaveVendorAsync(SelectedVendor);
            SelectedVendor = new Vendor();
            IsAddVendorModalOpen = false;
            await LoadVendorsAsync();
        }

        [RelayCommand]
        public void CloseLedgerModal()
        {
            IsLedgerModalOpen = false;
        }

        [RelayCommand]
        public async Task LoadVendorLedgerAsync(int vendorId)
        {
            SelectedVendorDto = Vendors.FirstOrDefault(v => v.Id == vendorId);
            var entries = await _vendorService.GetVendorLedgerAsync(vendorId);
            VendorLedgerEntries = new ObservableCollection<VendorLedger>(entries);

            var items = await _vendorService.GetVendorPurchasedItemsAsync(vendorId);
            PurchasedItems = new ObservableCollection<VendorPurchasedItemDto>(items);

            var payments = await _vendorService.GetVendorReceiptsAndPaymentsAsync(vendorId);
            PaymentHistory = new ObservableCollection<PaymentHistoryDto>(payments);

            var outstanding = await _vendorService.GetVendorOutstandingInvoicesAsync(vendorId);
            OutstandingInvoices = new ObservableCollection<OutstandingInvoiceDto>(outstanding);

            // Calculate Stat Cards & Grand Totals
            TotalPurchasesValue = items.Sum(i => i.TotalAmount);
            TotalPaymentsMade = payments.Sum(p => p.Amount);

            var lastPurchase = items.FirstOrDefault();
            LastPurchaseInfo = lastPurchase != null ? $"{lastPurchase.Date:yyyy-MM-dd} ({lastPurchase.PurchaseNumber})" : "No purchases recorded";

            var lastPayment = payments.FirstOrDefault();
            LastPaymentInfo = lastPayment != null ? $"{lastPayment.Date:yyyy-MM-dd} (Rs. {lastPayment.Amount:N0})" : "No payments recorded";

            TotalPurchasedQty = items.Sum(i => i.Quantity);
            TotalPurchasedAmount = items.Sum(i => i.TotalAmount);
            TotalPurchasedPaid = items.Sum(i => i.PaidAmount);
            TotalPurchasedOutstanding = items.Sum(i => i.OutstandingBalance);

            TotalPaymentHistoryAmount = payments.Sum(p => p.Amount);

            TotalOutstandingInvoiceAmount = outstanding.Sum(o => o.TotalAmount);
            TotalOutstandingPaidAmount = outstanding.Sum(o => o.PaidAmount);
            TotalOutstandingBalanceDue = outstanding.Sum(o => o.BalanceDue);

            LedgerTotalDebit = entries.Sum(e => e.Debit);
            LedgerTotalCredit = entries.Sum(e => e.Credit);

            IsLedgerModalOpen = true;
        }

        [RelayCommand]
        public void PrintVendorLedger()
        {
            if (SelectedVendorDto == null && VendorLedgerEntries.Count == 0)
            {
                System.Windows.MessageBox.Show("No vendor ledger opened to print.", "Print Ledger", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var vendName = SelectedVendorDto?.Name ?? "Vendor";
                    var vendCode = SelectedVendorDto?.Code ?? "";
                    var vendPhone = SelectedVendorDto?.Phone ?? "N/A";
                    var vendNetBal = SelectedVendorDto?.NetBalance ?? 0m;

                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PageWidth = 794;
                    doc.PageHeight = 1123;
                    doc.PagePadding = new System.Windows.Thickness(40);
                    doc.ColumnWidth = 714;
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

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"VENDOR LEDGER STATEMENT - {vendName.ToUpper()} ({vendCode})"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pMeta = new System.Windows.Documents.Paragraph();
                    pMeta.Inlines.Add(new System.Windows.Documents.Run($"Phone: {vendPhone}   |   Current Net Payable: PKR {vendNetBal:N2}\n") { FontWeight = System.Windows.FontWeights.Bold });
                    pMeta.Inlines.Add(new System.Windows.Documents.Run($"Statement Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Entries: {VendorLedgerEntries.Count}"));
                    pMeta.FontSize = 10;
                    pMeta.Margin = new System.Windows.Thickness(0, 0, 0, 14);
                    doc.Blocks.Add(pMeta);

                    var table = new System.Windows.Documents.Table { CellSpacing = 0, BorderThickness = new System.Windows.Thickness(0.5), BorderBrush = System.Windows.Media.Brushes.Gray };
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(80) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(180) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(85) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(85) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(94) });

                    var headerRowGroup = new System.Windows.Documents.TableRowGroup();
                    var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.DarkGreen };
                    string[] headers = { "DATE", "VOUCHER #", "TYPE", "DESCRIPTION", "DEBIT (PKR)", "CREDIT (PKR)", "BALANCE (PKR)" };
                    foreach (var h in headers)
                    {
                        var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontSize = 9,
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            TextAlignment = h.Contains("PKR") ? System.Windows.TextAlignment.Right : System.Windows.TextAlignment.Left
                        })
                        { Padding = new System.Windows.Thickness(4) };
                        headerRow.Cells.Add(cell);
                    }
                    headerRowGroup.Rows.Add(headerRow);
                    table.RowGroups.Add(headerRowGroup);

                    var dataRowGroup = new System.Windows.Documents.TableRowGroup();
                    bool alt = false;
                    foreach (var entry in VendorLedgerEntries)
                    {
                        var row = new System.Windows.Documents.TableRow
                        {
                            Background = alt ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White
                        };
                        alt = !alt;

                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Date.ToString("dd/MM/yyyy"))) { FontSize = 9 }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.VoucherNumber ?? "-")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.TransactionType ?? "-")) { FontSize = 9 }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Remarks ?? "-")) { FontSize = 9 }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Debit > 0 ? $"{entry.Debit:N2}" : "-")) { FontSize = 9, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(entry.Credit > 0 ? $"{entry.Credit:N2}" : "-")) { FontSize = 9, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                        row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{entry.RunningBalance:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });

                        dataRowGroup.Rows.Add(row);
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightGray };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold }) { ColumnSpan = 4, Padding = new System.Windows.Thickness(4) });
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{LedgerTotalDebit:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{LedgerTotalCredit:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{vendNetBal:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, TextAlignment = System.Windows.TextAlignment.Right }) { Padding = new System.Windows.Thickness(4) });
                    dataRowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(dataRowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"Vendor Ledger - {vendName}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void PrintVendorsList()
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var doc = new System.Windows.Documents.FlowDocument();
                    doc.PageWidth = 794;  // Standard A4 Width (210mm @ 96 DPI)
                    doc.PageHeight = 1123; // Standard A4 Height (297mm @ 96 DPI)
                    doc.PagePadding = new System.Windows.Thickness(40);
                    doc.ColumnWidth = 714;
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

                    var pSub = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("VENDOR MASTER & PAYABLES REPORT"))
                    {
                        FontSize = 13,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Foreground = System.Windows.Media.Brushes.DarkSlateGray,
                        TextAlignment = System.Windows.TextAlignment.Center,
                        Margin = new System.Windows.Thickness(0, 0, 0, 10)
                    };
                    doc.Blocks.Add(pSub);

                    var pDate = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Total Vendors: {Vendors.Count}"))
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
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(180) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(140) });
                    table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(140) });

                    var rowGroup = new System.Windows.Documents.TableRowGroup();

                    var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
                    string[] headers = { "CODE", "VENDOR NAME", "PHONE", "PAYABLE (WE OWE)", "ADVANCE PAID" };
                    foreach (var h in headers)
                    {
                        var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                        {
                            FontWeight = System.Windows.FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            FontSize = 10,
                            Margin = new System.Windows.Thickness(6, 4, 6, 4)
                        });
                        headerRow.Cells.Add(cell);
                    }
                    rowGroup.Rows.Add(headerRow);

                    int rowIdx = 0;
                    foreach (var v in Vendors)
                    {
                        var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                        var r = new System.Windows.Documents.TableRow { Background = bg };

                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(v.Code ?? "")) { FontSize = 10, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(v.Name ?? "")) { FontSize = 10, FontWeight = System.Windows.FontWeights.SemiBold, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(v.Phone ?? "")) { FontSize = 10, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {v.VendorOwes:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));
                        r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {v.AdvanceAvailable:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(6, 4, 6, 4) }));

                        rowGroup.Rows.Add(r);
                        rowIdx++;
                    }

                    var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{TotalVendorsCount} Vendors")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")) { FontSize = 10 }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {TotalVendorPayables:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"PKR {TotalVendorAdvances:N2}")) { FontSize = 10, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(6, 6, 6, 6) }));
                    rowGroup.Rows.Add(totalRow);

                    table.RowGroups.Add(rowGroup);
                    doc.Blocks.Add(table);

                    var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                    printDialog.PrintDocument(paginator, "Vendor Payables List");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Printing error: " + ex.Message, "Print Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
