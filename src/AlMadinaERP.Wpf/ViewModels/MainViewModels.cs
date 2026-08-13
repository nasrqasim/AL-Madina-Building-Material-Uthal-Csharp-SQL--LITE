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
    public partial class MainViewModel : ObservableObject
    {
        private readonly IAuthService _authService;

        [ObservableProperty]
        private object _currentView;

        [ObservableProperty]
        private string _statusMessage = "Ready - AL Madina Building Material Uthal ERP";

        [ObservableProperty]
        private string _currentUser = "Superadmin";

        [ObservableProperty]
        private string _currentTime = System.DateTime.Now.ToString("HH:mm");

        [ObservableProperty]
        private string _currentDate = System.DateTime.Now.ToString("dd/MM/yyyy");

        public DashboardViewModel DashboardVM { get; }
        public SalesViewModel SalesVM { get; }
        public PosViewModel PosVM { get; }
        public PurchasesViewModel PurchasesVM { get; }
        public CustomersViewModel CustomersVM { get; }
        public VendorsViewModel VendorsVM { get; }
        public InventoryViewModel InventoryVM { get; }
        public ChartOfInventoryViewModel ChartOfInventoryVM { get; }
        public ReceiptsPaymentsViewModel ReceiptsPaymentsVM { get; }
        public BanksViewModel BanksVM { get; }
        public SalaryViewModel SalaryVM { get; }
        public ReportsViewModel ReportsVM { get; }
        public SettingsViewModel SettingsVM { get; }

        public MainViewModel(
            DashboardViewModel dashboardVM,
            SalesViewModel salesVM,
            PosViewModel posVM,
            PurchasesViewModel purchasesVM,
            CustomersViewModel customersVM,
            VendorsViewModel vendorsVM,
            InventoryViewModel inventoryVM,
            ChartOfInventoryViewModel chartOfInventoryVM,
            ReceiptsPaymentsViewModel receiptsPaymentsVM,
            BanksViewModel banksVM,
            SalaryViewModel salaryVM,
            ReportsViewModel reportsVM,
            SettingsViewModel settingsVM,
            IAuthService authService)
        {
            _authService = authService;
            DashboardVM = dashboardVM;
            SalesVM = salesVM;
            PosVM = posVM;
            PurchasesVM = purchasesVM;
            CustomersVM = customersVM;
            VendorsVM = vendorsVM;
            InventoryVM = inventoryVM;
            ChartOfInventoryVM = chartOfInventoryVM;
            ReceiptsPaymentsVM = receiptsPaymentsVM;
            BanksVM = banksVM;
            SalaryVM = salaryVM;
            ReportsVM = reportsVM;
            SettingsVM = settingsVM;

            _currentUser = _authService.CurrentUser?.FullName ?? "Superadmin";
            _currentView = dashboardVM;
        }

        [RelayCommand]
        public void Logout(System.Windows.Window? mainWindow)
        {
            _authService.Logout();
            var loginWindow = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AlMadinaERP.Wpf.Views.LoginWindow>(App.ServiceProvider);
            loginWindow.Show();
            mainWindow?.Close();
        }

        [RelayCommand]
        private async Task NavigateTabAsync(string tabName)
        {
            if (string.IsNullOrEmpty(tabName)) return;

            StatusMessage = $"Navigating to {tabName}...";
            Func<Task>? loadTask = null;

            if (tabName.Equals("Dashboard"))
            {
                CurrentView = DashboardVM;
                loadTask = () => DashboardVM.LoadDashboardAsync();
            }
            else if (tabName.StartsWith("Sales") || tabName.StartsWith("Sale") || tabName.Contains("POS"))
            {
                CurrentView = SalesVM;
                if (tabName.Contains("Return"))
                {
                    SalesVM.ActiveSubView = SalesActiveSubView.SaleReturnList;
                }
                else if (tabName.Contains("POS") || tabName.Contains("Counter"))
                {
                    SalesVM.ActiveSubView = SalesActiveSubView.PosList;
                }
                else
                {
                    SalesVM.ActiveSubView = SalesActiveSubView.SaleInvoiceList;
                }
                loadTask = () => SalesVM.LoadInvoicesAsync();
            }
            else if (tabName.StartsWith("Purchases") || tabName.StartsWith("Purchase"))
            {
                CurrentView = PurchasesVM;
                if (tabName.Contains("Return"))
                {
                    PurchasesVM.IsReturnMode = true;
                    PurchasesVM.IsFormVisible = false;
                }
                else
                {
                    PurchasesVM.IsReturnMode = false;
                    PurchasesVM.IsFormVisible = false;
                }
                loadTask = () => PurchasesVM.LoadPurchasesAsync();
            }
            else if (tabName.Equals("CustomerBalances") || tabName.Equals("Customer Balances"))
            {
                CurrentView = ReportsVM;
                ReportsVM.SelectedTabIndex = 3;
                loadTask = () => ReportsVM.GenerateReportsAsync();
            }
            else if (tabName.Equals("VendorBalances") || tabName.Equals("Vendor Balances"))
            {
                CurrentView = ReportsVM;
                ReportsVM.SelectedTabIndex = 4;
                loadTask = () => ReportsVM.GenerateReportsAsync();
            }
            else if (tabName.Equals("Customers") || tabName.Equals("Customer") || tabName.Equals("Customer Master"))
            {
                CurrentView = CustomersVM;
                loadTask = () => CustomersVM.LoadCustomersAsync();
            }
            else if (tabName.Equals("Vendors") || tabName.Equals("Vendor") || tabName.Equals("Vendor Master"))
            {
                CurrentView = VendorsVM;
                loadTask = () => VendorsVM.LoadVendorsAsync();
            }
            else if (tabName.Equals("ChartOfInventory") || tabName.Equals("Chart of Inventory"))
            {
                CurrentView = ChartOfInventoryVM;
                loadTask = () => ChartOfInventoryVM.LoadChartAsync();
            }
            else if (tabName.Equals("InventoryBalances") || tabName.Equals("Inventory Balances") ||
                     tabName.Equals("InventoryLedger") || tabName.Equals("Inventory Ledger") ||
                     tabName.Equals("BalanceSheet") || tabName.Equals("Balance Sheet") ||
                     tabName.Contains("ItemWiseProfit") || tabName.Contains("Item-wise Profit"))
            {
                CurrentView = ReportsVM;
                if (tabName.Contains("Balances") || tabName.Equals("InventoryBalances"))
                    ReportsVM.ActiveSubViewMode = ReportsSubViewMode.InventoryBalancesReport;
                else if (tabName.Contains("Ledger") || tabName.Equals("InventoryLedger"))
                    ReportsVM.ActiveSubViewMode = ReportsSubViewMode.InventoryLedgerReport;
                else if (tabName.Contains("BalanceSheet") || tabName.Contains("Balance Sheet"))
                    ReportsVM.ActiveSubViewMode = ReportsSubViewMode.BalanceSheet;
                else if (tabName.Contains("Profit"))
                    ReportsVM.ActiveSubViewMode = ReportsSubViewMode.ItemWiseProfitLossReport;

                loadTask = () => ReportsVM.GenerateReportsAsync();
            }
            else if (tabName.Equals("Inventory") || tabName.Equals("Inventory Master") || tabName.Equals("Items / Products") || tabName.Equals("Stock") || tabName.Equals("Items"))
            {
                CurrentView = InventoryVM;
                loadTask = () => InventoryVM.LoadInventoryAsync();
            }
            else if (tabName.Equals("Banks") || tabName.Equals("Bank"))
            {
                CurrentView = BanksVM;
                loadTask = () => BanksVM.LoadBanksAsync();
            }
            else if (tabName.StartsWith("Receipts") || tabName.StartsWith("Payments") || tabName.Contains("Receipt") || tabName.Contains("Payment") || tabName.Contains("Income") || tabName.Contains("Expense"))
            {
                CurrentView = ReceiptsPaymentsVM;
                if (tabName.Contains("Bank Receipt") || tabName.Equals("Bank Receipt"))
                {
                    ReceiptsPaymentsVM.ActiveSubView = "BankReceiptList";
                }
                else if (tabName.Contains("Other Income") || tabName.Equals("Other Income"))
                {
                    ReceiptsPaymentsVM.ActiveSubView = "OtherIncomeList";
                }
                else if (tabName.Contains("Expense") || tabName.Equals("Expenses"))
                {
                    ReceiptsPaymentsVM.ActiveSubView = "ExpenseList";
                }
                else if (tabName.Contains("Bank Payment") || tabName.Equals("Bank Payment"))
                {
                    ReceiptsPaymentsVM.ActiveSubView = "BankPaymentList";
                }
                else if (tabName.Contains("Cash Payment") || tabName.Equals("Cash Payment") || tabName.StartsWith("Payment"))
                {
                    ReceiptsPaymentsVM.ActiveSubView = "CashPaymentList";
                }
                else
                {
                    ReceiptsPaymentsVM.ActiveSubView = "CashReceiptList";
                }
                loadTask = () => ReceiptsPaymentsVM.LoadDataAsync();
            }
            else if (tabName.Equals("Journal") || tabName.Equals("Journal Entry"))
            {
                CurrentView = SalaryVM;
                SalaryVM.SubViewMode = SalarySubViewMode.JournalList;
                loadTask = () => SalaryVM.LoadSalariesAsync();
            }
            else if (tabName.Contains("Advance") || tabName.Equals("Salary Advance"))
            {
                CurrentView = SalaryVM;
                SalaryVM.SubViewMode = SalarySubViewMode.AdvanceList;
                loadTask = () => SalaryVM.LoadSalariesAsync();
            }
            else if (tabName.Equals("Salary Staff Report"))
            {
                CurrentView = ReportsVM;
                ReportsVM.SelectedTabIndex = 6;
                loadTask = () => ReportsVM.GenerateReportsAsync();
            }
            else if (tabName.StartsWith("Salary"))
            {
                CurrentView = SalaryVM;
                SalaryVM.SubViewMode = SalarySubViewMode.StaffList;
                loadTask = () => SalaryVM.LoadSalariesAsync();
            }
            else if (tabName.StartsWith("Reports") || tabName.Contains("Report") || tabName.Contains("Register") || tabName.Contains("Summary") || tabName.Contains("Vendor") || tabName.Contains("Customer") || tabName.Contains("Low Stock") || tabName.Contains("Inventory"))
            {
                CurrentView = ReportsVM;
                if (tabName.Contains("Journal Report"))
                {
                    ReportsVM.SelectedTabIndex = 5;
                }
                else if (tabName.Contains("Vendor Balances") || tabName.Contains("Vendor Balance"))
                {
                    ReportsVM.SelectedTabIndex = 4;
                }
                else if (tabName.Contains("Customer Balances") || tabName.Contains("Customer Balance"))
                {
                    ReportsVM.SelectedTabIndex = 3;
                }
                else if (tabName.Contains("Low Stock Alert") || tabName.Contains("Low Stock"))
                {
                    ReportsVM.SelectedTabIndex = 2;
                }
                else if (tabName.Contains("Inventory Balances") || tabName.Contains("Inventory Balance"))
                {
                    ReportsVM.SelectedTabIndex = 1;
                }
                else if (tabName.Contains("Inventory Ledger"))
                {
                    ReportsVM.SelectedTabIndex = 0;
                }
                else
                {
                    ReportsVM.SelectedTabIndex = 5;
                }
                loadTask = () => ReportsVM.GenerateReportsAsync();
            }
            else if (tabName.StartsWith("Settings") || tabName.Contains("Company") || tabName.Contains("Year") || tabName.Contains("Backup"))
            {
                CurrentView = SettingsVM;
                loadTask = () => SettingsVM.LoadSettingsAsync();
            }

            if (loadTask != null)
            {
                try
                {
                    await loadTask();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation data load error for {tabName}: {ex.Message}");
                }
            }

            StatusMessage = $"Ready - {tabName}";
        }
    }

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly IDashboardService _dashboardService;
        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        [ObservableProperty]
        private DashboardSummaryDto _summary = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _currentTime = System.DateTime.Now.ToString("HH:mm");

        [ObservableProperty]
        private string _currentDayDate = System.DateTime.Now.ToString("dddd, MMMM d, yyyy").ToUpper();

        [ObservableProperty]
        private string _selectedDate = System.DateTime.Now.ToString("dd/MM/yyyy");

        public DashboardViewModel(
            IDashboardService dashboardService,
            IPrintService printService,
            IRepository<CompanySetting> companyRepo)
        {
            _dashboardService = dashboardService;
            _printService = printService;
            _companyRepo = companyRepo;
        }

        [RelayCommand]
        public async Task LoadDashboardAsync()
        {
            IsLoading = true;
            CurrentTime = System.DateTime.Now.ToString("HH:mm");
            CurrentDayDate = System.DateTime.Now.ToString("dddd, MMMM d, yyyy").ToUpper();
            Summary = await _dashboardService.GetDashboardSummaryAsync();
            IsLoading = false;
        }

        [RelayCommand]
        public async Task PrintDashboardSummaryAsync()
        {
            if (Summary == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            
            var headers = new[] { "CATEGORY / METRIC", "DESCRIPTION & STATUS", "AMOUNT / COUNT (PKR)" };
            
            var rows = new List<string[]>
            {
                new[] { "CASH & BANKS BALANCE", "Current Cash Counter & Bank Funds Position", $"Rs. {Summary.CurrentCashBankBalance:N0}" },
                new[] { "CASH RECEIVED TODAY", "Total Receipts Recorded Today", $"Rs. {Summary.CashReceivedToday:N0}" },
                new[] { "CASH PAID TODAY", "Total Payments Made Today", $"Rs. {Summary.CashPaidToday:N0}" },
                new[] { "TOTAL CUSTOMER RECEIVABLES", "Outstanding Amount Owed by Customers", $"Rs. {Summary.TotalCustomerReceivables:N0}" },
                new[] { "CUSTOMER RECEIPTS TODAY", "Collections Recorded Today from Customers", $"Rs. {Summary.ReceivedToday:N0}" },
                new[] { "TOTAL VENDOR PAYABLES", "Outstanding Amount Owed to Suppliers / Vendors", $"Rs. {Summary.TotalVendorPayables:N0}" },
                new[] { "VENDOR PAYMENTS TODAY", "Supplier Payments Recorded Today", $"Rs. {Summary.PaidToday:N0}" },
                new[] { "SALES TODAY", "Gross Sales Invoiced Today", $"Rs. {Summary.SalesToday:N0}" },
                new[] { "PURCHASES TODAY", "Inventory Purchases Invoiced Today", $"Rs. {Summary.PurchasesToday:N0}" },
                new[] { "MONTHLY SALES", "Cumulative Sales for Current Month", $"Rs. {Summary.MonthlySales:N0}" },
                new[] { "MONTHLY PURCHASES", "Cumulative Purchases for Current Month", $"Rs. {Summary.MonthlyPurchases:N0}" },
                new[] { "TOTAL INVENTORY VALUE", "Current Valuation of Stock In Hand", $"Rs. {Summary.InventoryValue:N0}" },
                new[] { "NET PROFIT POSITION", "Calculated Revenue Less Cost & Expenses", $"Rs. {Summary.NetProfit:N0}" }
            };

            var totals = new[] { "EXECUTIVE SUMMARY TOTAL", "Net Liquidity & Receivables less Payables", $"Rs. {(Summary.CurrentCashBankBalance + Summary.TotalCustomerReceivables - Summary.TotalVendorPayables):N0}" };

            _printService.PrintReportTable("Executive Dashboard & Cash / Bank / Balances Summary", headers, rows, totals, company);
        }
    }

    public partial class BanksViewModel : ObservableObject
    {
        private readonly IReceiptPaymentService _service;
        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;
        private readonly IRepository<Bank> _bankRepo;

        [ObservableProperty]
        private ObservableCollection<Bank> _banks = new();

        [ObservableProperty]
        private Bank _newBank = new()
        {
            Code = "BANK-001",
            AccountType = "Current Account",
            CurrentBalance = 0m,
            IsActive = true
        };

        [ObservableProperty]
        private bool _isAddBankModalOpen;

        [ObservableProperty]
        private string _bankSearchQuery = string.Empty;

        partial void OnBankSearchQueryChanged(string value)
        {
            _ = LoadBanksAsync();
        }

        public BanksViewModel(IReceiptPaymentService service, IPrintService printService, IRepository<CompanySetting> companyRepo, IRepository<Bank> bankRepo)
        {
            _service = service;
            _printService = printService;
            _companyRepo = companyRepo;
            _bankRepo = bankRepo;
        }

        [ObservableProperty]
        private Bank _selectedBankForView = new();

        [ObservableProperty]
        private bool _isViewBankModalOpen;

        [RelayCommand]
        public async Task LoadBanksAsync()
        {
            var list = await _service.GetBanksAsync();
            if (!string.IsNullOrWhiteSpace(BankSearchQuery))
            {
                var q = BankSearchQuery.Trim().ToLower();
                list = list.Where(b => (b.BankName ?? "").ToLower().Contains(q) || (b.AccountNumber ?? "").ToLower().Contains(q) || (b.AccountName ?? "").ToLower().Contains(q) || (b.Code ?? "").ToLower().Contains(q)).ToList();
            }
            Banks.Clear();
            foreach (var b in list)
            {
                Banks.Add(b);
            }
        }

        [RelayCommand]
        private void OpenAddBankModal()
        {
            NewBank = new Bank
            {
                Code = "BANK-" + (Banks.Count + 1).ToString("D3"),
                AccountType = "Current Account",
                CurrentBalance = 0m,
                IsActive = true
            };
            IsAddBankModalOpen = true;
        }

        [RelayCommand]
        private void CloseAddBankModal()
        {
            IsAddBankModalOpen = false;
        }

        [RelayCommand]
        public void ViewBank(Bank bank)
        {
            if (bank == null) return;
            SelectedBankForView = bank;
            IsViewBankModalOpen = true;
        }

        [RelayCommand]
        public void CloseViewBankModal()
        {
            IsViewBankModalOpen = false;
        }

        [RelayCommand]
        public async Task SaveBankAsync()
        {
            if (string.IsNullOrWhiteSpace(NewBank.BankName))
            {
                NewBank.BankName = "Bank " + System.DateTime.Now.ToString("fffSSm");
            }
            if (string.IsNullOrWhiteSpace(NewBank.AccountNumber))
            {
                NewBank.AccountNumber = "ACC-" + System.DateTime.Now.ToString("fffSSm");
            }

            await _service.SaveBankAsync(NewBank);
            IsAddBankModalOpen = false;
            await LoadBanksAsync();
        }

        [RelayCommand]
        public void EditBank(Bank bank)
        {
            if (bank == null) return;
            NewBank = bank;
            IsAddBankModalOpen = true;
        }

        [RelayCommand]
        public async Task DeleteBankAsync(Bank bank)
        {
            if (bank != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete bank account '{bank.BankName}' ({bank.AccountNumber})?",
                    "Confirm Bank Deletion",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await _bankRepo.DeleteAsync(bank);
                    await LoadBanksAsync();
                }
            }
        }

        [RelayCommand]
        public async Task PrintBankListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Code", "Bank Name", "Account Number", "Account Title", "Account Type", "Current Balance (PKR)" };
            var rows = Banks.Select(b => new[] { b.Code ?? "", b.BankName ?? "", b.AccountNumber ?? "", b.AccountName ?? "", b.AccountType ?? "Current", $"Rs. {b.CurrentBalance:N2}" });
            var totals = new[] { "TOTAL", $"{Banks.Count} Bank Accounts", "", "", "", $"Total: Rs. {Banks.Sum(b => b.CurrentBalance):N2}" };
            _printService.PrintReportTable("Bank Accounts Directory", headers, rows, totals, company);
        }
    }
}
