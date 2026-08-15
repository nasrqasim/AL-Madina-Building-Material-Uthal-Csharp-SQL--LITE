using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;

namespace AlMadinaERP.Wpf.ViewModels
{
    public class DailyActivityJournalDto
    {
        public DateTime Date { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string AccountPartyName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Amount { get; set; }
    }

    public enum SalarySubViewMode
    {
        StaffList,
        StaffLedger,
        JournalList,
        AdvanceList,
        AdvanceForm
    }

    public enum ReportsSubViewMode
    {
        FinancialReports,
        JournalReport,
        PurchaseSummary,
        VendorBalancesList,
        VendorLedgerDetail,
        PosSalesReport,
        ItemWiseProfitLossReport,
        CustomerBalancesList,
        CustomerLedgerDetail,
        InventoryLedgerReport,
        InventoryBalancesReport,
        LowStockAlertReport,
        BalanceSheet
    }

    public enum ReceiptsActiveSubView
    {
        CashReceiptList,
        CashReceiptForm,
        BankReceiptList,
        BankReceiptForm,
        OtherIncomeList,
        ExpenseList,
        CashPaymentList,
        CashPaymentForm,
        BankPaymentList,
        BankPaymentForm
    }

    public partial class ReceiptsPaymentsViewModel : ObservableObject
    {
        private readonly IReceiptPaymentService _service;
        private readonly ICustomerService _customerService;
        private readonly IVendorService _vendorService;

        [ObservableProperty]
        private string _activeSubView = "CashReceiptList";

        [ObservableProperty]
        private ObservableCollection<Receipt> _cashReceipts = new();

        [ObservableProperty]
        private ObservableCollection<Receipt> _bankReceipts = new();

        [ObservableProperty]
        private ObservableCollection<Receipt> _otherIncomes = new();

        [ObservableProperty]
        private ObservableCollection<Receipt> _receipts = new();

        [ObservableProperty]
        private ObservableCollection<Payment> _cashPayments = new();

        [ObservableProperty]
        private ObservableCollection<Payment> _bankPayments = new();

        [ObservableProperty]
        private ObservableCollection<Payment> _payments = new();

        [ObservableProperty]
        private ObservableCollection<Expense> _expenses = new();

        [ObservableProperty]
        private ObservableCollection<Bank> _banks = new();

        [ObservableProperty]
        private ObservableCollection<Customer> _customers = new();

        [ObservableProperty]
        private ObservableCollection<Vendor> _vendors = new();

        [ObservableProperty]
        private Receipt _newReceipt = new();

        [ObservableProperty]
        private Receipt _newOtherIncome = new();

        [ObservableProperty]
        private Payment _newPayment = new();

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private Vendor? _selectedVendor;

        [ObservableProperty]
        private Bank? _selectedBank;

        [ObservableProperty]
        private Expense _newExpense = new()
        {
            VoucherNumber = "EXP-" + DateTime.Now.ToString("fffSSmm"),
            Date = DateTime.Now,
            Category = "Utility (Electricity, Water)",
            ExpenseType = "Operating",
            PaidFrom = "Cash",
            Status = "Paid"
        };

        [ObservableProperty]
        private Bank _newBank = new()
        {
            Code = "BANK-001",
            AccountType = "Current Account",
            CurrentBalance = 0m,
            IsActive = true
        };

        [ObservableProperty]
        private decimal _totalEnteredExpenses = 0m;

        [ObservableProperty]
        private decimal _paidExpenses = 0m;

        [ObservableProperty]
        private decimal _pendingExpenses = 0.00m;

        [ObservableProperty]
        private decimal _totalOtherIncome = 0m;

        [ObservableProperty]
        private decimal _monthlyIncome = 0m;

        [ObservableProperty]
        private decimal _yearlyIncome = 0m;

        [ObservableProperty]
        private bool _isAddExpenseModalOpen;

        [ObservableProperty]
        private bool _isAddBankModalOpen;

        [ObservableProperty]
        private bool _isAddOtherIncomeModalOpen;

        [ObservableProperty]
        private string _otherIncomeModalTitle = "Add Other Income";

        [ObservableProperty]
        private string _expenseSearchQuery = string.Empty;

        partial void OnExpenseSearchQueryChanged(string value)
        {
            _ = LoadDataAsync();
        }

        [ObservableProperty]
        private Expense _selectedExpenseForView = new();

        [ObservableProperty]
        private bool _isViewExpenseModalOpen;

        [ObservableProperty]
        private decimal _grandTotalExpenses = 0m;

        [ObservableProperty]
        private string _bankSearchQuery = string.Empty;

        [ObservableProperty]
        private string _receiptSearchQuery = string.Empty;

        partial void OnReceiptSearchQueryChanged(string value)
        {
            _ = LoadDataAsync();
        }

        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        [ObservableProperty]
        private string _paymentSearchQuery = string.Empty;

        partial void OnPaymentSearchQueryChanged(string value)
        {
            _ = LoadDataAsync();
        }

        [ObservableProperty]
        private bool _isViewReceiptModalOpen;

        [ObservableProperty]
        private bool _isViewPaymentModalOpen;

        [ObservableProperty]
        private Receipt? _selectedViewReceipt;

        [ObservableProperty]
        private Payment? _selectedViewPayment;

        [ObservableProperty]
        private bool _isVendorPayment = true;

        public bool IsCustomerPayment => !_isVendorPayment;

        [RelayCommand]
        public void SwitchPaymentPartyType(string partyType)
        {
            IsVendorPayment = string.Equals(partyType, "Vendor", StringComparison.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(IsCustomerPayment));
            if (NewPayment != null)
            {
                NewPayment.PayToCategory = IsVendorPayment ? "Vendor" : "Customer";
            }
        }

        public ReceiptsPaymentsViewModel(IReceiptPaymentService service, ICustomerService customerService, IVendorService vendorService, IPrintService printService, IRepository<CompanySetting> companyRepo)
        {
            _service = service;
            _customerService = customerService;
            _vendorService = vendorService;
            _printService = printService;
            _companyRepo = companyRepo;
            ResetNewReceipt();
            ResetNewOtherIncome();
            ResetNewPayment();
        }

        private void ResetNewReceipt()
        {
            var isBank = ActiveSubView == "BankReceiptForm" || ActiveSubView == "BankReceiptList";
            NewReceipt = new Receipt
            {
                ReceiptNumber = (isBank ? "BR-" : "CR-") + DateTime.Now.ToString("fffSSm"),
                Date = DateTime.Now,
                ReceiptType = isBank ? ReceiptType.BankReceipt : ReceiptType.CashReceipt,
                PaymentMethod = isBank ? PaymentMethod.Bank : PaymentMethod.Cash,
                ReceivedBy = isBank ? "Bank Account" : "Cashier / Counter",
                Status = "Posted"
            };
        }

        private void ResetNewOtherIncome()
        {
            NewOtherIncome = new Receipt
            {
                ReceiptNumber = "INC-" + DateTime.Now.ToString("fffSSm"),
                Date = DateTime.Now,
                ReceiptType = ReceiptType.OtherIncome,
                PaymentMethod = PaymentMethod.Cash,
                IncomeType = "One Time",
                IncomeTitle = "",
                Amount = 0m
            };
        }

        private void ResetNewPayment()
        {
            var isBank = ActiveSubView == "BankPaymentForm" || ActiveSubView == "BankPaymentList";
            NewPayment = new Payment
            {
                PaymentNumber = (isBank ? "BP-" : "CP-") + DateTime.Now.ToString("fffSSm"),
                Date = DateTime.Now,
                PaymentType = isBank ? PaymentType.BankPayment : PaymentType.CashPayment,
                PaymentMethod = isBank ? PaymentMethod.Bank : PaymentMethod.Cash,
                PaidFrom = isBank ? "Bank Account" : "Cashier / Counter",
                PaymentCategory = "Party Payment",
                PayToCategory = "Vendor",
                Status = "Posted"
            };
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            var rList = await _service.SearchReceiptsAsync(ReceiptSearchQuery);
            Receipts = new ObservableCollection<Receipt>(rList);

            var cashList = rList.Where(r => r.ReceiptType == ReceiptType.CashReceipt).ToList();
            var bankList = rList.Where(r => r.ReceiptType == ReceiptType.BankReceipt).ToList();
            var otherList = rList.Where(r => r.ReceiptType == ReceiptType.OtherIncome).ToList();

            CashReceipts = new ObservableCollection<Receipt>(cashList);
            BankReceipts = new ObservableCollection<Receipt>(bankList);
            OtherIncomes = new ObservableCollection<Receipt>(otherList);

            if (otherList.Count > 0)
            {
                TotalOtherIncome = otherList.Sum(o => o.Amount);
                MonthlyIncome = TotalOtherIncome;
                YearlyIncome = TotalOtherIncome;
            }

            var pList = await _service.SearchPaymentsAsync(PaymentSearchQuery);
            Payments = new ObservableCollection<Payment>(pList);

            var cashPList = pList.Where(p => p.PaymentType == PaymentType.CashPayment).ToList();
            var bankPList = pList.Where(p => p.PaymentType == PaymentType.BankPayment).ToList();

            CashPayments = new ObservableCollection<Payment>(cashPList);
            BankPayments = new ObservableCollection<Payment>(bankPList);

            var exList = await _service.SearchExpensesAsync(ExpenseSearchQuery ?? "");
            Expenses.Clear();
            foreach (var exp in exList)
            {
                Expenses.Add(exp);
            }

            TotalEnteredExpenses = exList.Sum(e => e.Amount);
            PaidExpenses = exList.Where(e => e.Status == "Paid").Sum(e => e.Amount);
            PendingExpenses = TotalEnteredExpenses - PaidExpenses;
            GrandTotalExpenses = TotalEnteredExpenses;

            var bList = await _service.GetBanksAsync();
            Banks = new ObservableCollection<Bank>(bList);

            var cList = await _customerService.SearchCustomersAsync("");
            Customers = new ObservableCollection<Customer>(cList);

            var vList = await _vendorService.SearchVendorsAsync("");
            Vendors = new ObservableCollection<Vendor>(vList);
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                NewReceipt.CustomerId = value.Id;
                NewReceipt.CustomerName = value.Name;
                NewPayment.CustomerId = value.Id;
                NewPayment.CustomerName = value.Name;
            }
        }

        partial void OnSelectedVendorChanged(Vendor? value)
        {
            if (value != null)
            {
                NewPayment.VendorId = value.Id;
                NewPayment.VendorName = value.Name;
            }
        }

        partial void OnSelectedBankChanged(Bank? value)
        {
            if (value != null)
            {
                NewReceipt.BankId = value.Id;
                NewReceipt.BankName = value.BankName;
                NewReceipt.BankAccountNo = value.AccountNumber;

                NewPayment.BankId = value.Id;
                NewPayment.BankName = value.BankName;
                NewPayment.BankAccountNo = value.AccountNumber;
            }
        }

        [RelayCommand]
        public void OpenNewCashReceiptForm()
        {
            ActiveSubView = "CashReceiptForm";
            ResetNewReceipt();
        }

        [RelayCommand]
        public void OpenNewBankReceiptForm()
        {
            ActiveSubView = "BankReceiptForm";
            ResetNewReceipt();
        }

        [RelayCommand]
        public void OpenNewCashPaymentForm()
        {
            ActiveSubView = "CashPaymentForm";
            ResetNewPayment();
        }

        [RelayCommand]
        public void OpenNewBankPaymentForm()
        {
            ActiveSubView = "BankPaymentForm";
            ResetNewPayment();
        }

        [RelayCommand]
        public void CloseSubView()
        {
            if (ActiveSubView == "CashReceiptForm")
                ActiveSubView = "CashReceiptList";
            else if (ActiveSubView == "BankReceiptForm")
                ActiveSubView = "BankReceiptList";
            else if (ActiveSubView == "CashPaymentForm")
                ActiveSubView = "CashPaymentList";
            else if (ActiveSubView == "BankPaymentForm")
                ActiveSubView = "BankPaymentList";
            else if (ActiveSubView == "ExpenseList")
                ActiveSubView = "ExpenseList";
            else
                ActiveSubView = "CashReceiptList";
        }

        [RelayCommand]
        public void SwitchReceiptMode(string mode)
        {
            if (mode == "Bank")
            {
                ActiveSubView = "BankReceiptForm";
                NewReceipt.ReceiptType = ReceiptType.BankReceipt;
                NewReceipt.PaymentMethod = PaymentMethod.Bank;
            }
            else
            {
                ActiveSubView = "CashReceiptForm";
                NewReceipt.ReceiptType = ReceiptType.CashReceipt;
                NewReceipt.PaymentMethod = PaymentMethod.Cash;
            }
        }

        [RelayCommand]
        public void SwitchPaymentMode(string mode)
        {
            if (mode == "Bank")
            {
                ActiveSubView = "BankPaymentForm";
                NewPayment.PaymentType = PaymentType.BankPayment;
                NewPayment.PaymentMethod = PaymentMethod.Bank;
            }
            else
            {
                ActiveSubView = "CashPaymentForm";
                NewPayment.PaymentType = PaymentType.CashPayment;
                NewPayment.PaymentMethod = PaymentMethod.Cash;
            }
        }

        [RelayCommand]
        public async Task SaveReceiptDraftAsync()
        {
            NewReceipt.Status = "Draft";
            await SaveReceiptInternalAsync();
        }

        [RelayCommand]
        public async Task SaveReceiptPostedAsync()
        {
            NewReceipt.Status = "Posted";
            await SaveReceiptInternalAsync();
        }

        private async Task SaveReceiptInternalAsync()
        {
            if (string.IsNullOrWhiteSpace(NewReceipt.ReceiptNumber))
            {
                NewReceipt.ReceiptNumber = "RCT-" + DateTime.Now.ToString("fffSSm");
            }

            if (SelectedCustomer != null)
            {
                NewReceipt.CustomerId = SelectedCustomer.Id;
                NewReceipt.CustomerName = SelectedCustomer.Name;
            }
            else if (string.IsNullOrWhiteSpace(NewReceipt.CustomerName))
            {
                NewReceipt.CustomerName = "Cash Customer";
            }

            if (SelectedBank != null)
            {
                NewReceipt.BankId = SelectedBank.Id;
                NewReceipt.BankName = SelectedBank.BankName;
                NewReceipt.BankAccountNo = SelectedBank.AccountNumber;
            }

            await _service.ProcessReceiptAsync(NewReceipt);
            CloseSubView();
            await LoadDataAsync();
        }

        [RelayCommand]
        public async Task SavePaymentDraftAsync()
        {
            NewPayment.Status = "Draft";
            await SavePaymentInternalAsync();
        }

        [RelayCommand]
        public async Task SavePaymentPostedAsync()
        {
            NewPayment.Status = "Posted";
            await SavePaymentInternalAsync();
        }

        private async Task SavePaymentInternalAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPayment.PaymentNumber))
            {
                NewPayment.PaymentNumber = "PAY-" + DateTime.Now.ToString("fffSSm");
            }

            if (SelectedVendor != null)
            {
                NewPayment.VendorId = SelectedVendor.Id;
                NewPayment.VendorName = SelectedVendor.Name;
            }
            else if (SelectedCustomer != null)
            {
                NewPayment.CustomerId = SelectedCustomer.Id;
                NewPayment.CustomerName = SelectedCustomer.Name;
            }

            if (SelectedBank != null)
            {
                NewPayment.BankId = SelectedBank.Id;
                NewPayment.BankName = SelectedBank.BankName;
                NewPayment.BankAccountNo = SelectedBank.AccountNumber;
            }

            NewPayment.WhtAmount = (NewPayment.Amount * NewPayment.WhtRatePercent) / 100m;
            NewPayment.NetAmountToPay = NewPayment.Amount - NewPayment.WhtAmount;

            await _service.ProcessPaymentAsync(NewPayment);
            CloseSubView();
            await LoadDataAsync();
        }

        [RelayCommand]
        public void OpenAddOtherIncomeModal()
        {
            ResetNewOtherIncome();
            OtherIncomeModalTitle = "Add Other Income";
            IsAddOtherIncomeModalOpen = true;
        }

        [RelayCommand]
        public void EditOtherIncome(Receipt income)
        {
            if (income == null) return;
            OtherIncomeModalTitle = "Edit Other Income";
            NewOtherIncome = new Receipt
            {
                Id = income.Id,
                ReceiptNumber = income.ReceiptNumber,
                Date = income.Date,
                ReceiptType = ReceiptType.OtherIncome,
                IncomeTitle = !string.IsNullOrWhiteSpace(income.IncomeTitle) ? income.IncomeTitle : income.CustomerName,
                IncomeType = !string.IsNullOrWhiteSpace(income.IncomeType) ? income.IncomeType : "One Time",
                Amount = income.Amount,
                PaymentMethod = income.PaymentMethod,
                BankId = income.BankId,
                BankName = income.BankName,
                ChequeNo = income.ChequeNo,
                Remarks = income.Remarks,
                Description = income.Remarks
            };
            IsAddOtherIncomeModalOpen = true;
        }

        [RelayCommand]
        public void CloseAddOtherIncomeModal()
        {
            IsAddOtherIncomeModalOpen = false;
        }

        [RelayCommand]
        public async Task SaveOtherIncomeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewOtherIncome.IncomeTitle))
            {
                NewOtherIncome.IncomeTitle = "Other Income " + DateTime.Now.ToString("fffSSm");
            }

            NewOtherIncome.ReceiptType = ReceiptType.OtherIncome;
            NewOtherIncome.CustomerName = NewOtherIncome.IncomeTitle;
            NewOtherIncome.Remarks = !string.IsNullOrWhiteSpace(NewOtherIncome.Description) ? NewOtherIncome.Description : NewOtherIncome.Remarks;

            await _service.ProcessReceiptAsync(NewOtherIncome);
            IsAddOtherIncomeModalOpen = false;
            await LoadDataAsync();
        }

        [RelayCommand]
        public async Task DeleteReceiptAsync(Receipt receipt)
        {
            if (receipt != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete receipt voucher '{receipt.ReceiptNumber}' for PKR {receipt.Amount:N0}?",
                    "Confirm Delete Receipt",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _service.DeleteReceiptAsync(receipt.Id);
                        await LoadDataAsync();
                        System.Windows.MessageBox.Show($"Receipt '{receipt.ReceiptNumber}' deleted successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete receipt: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public async Task DeletePaymentAsync(Payment payment)
        {
            if (payment != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete payment voucher '{payment.PaymentNumber}' for PKR {payment.Amount:N0}?",
                    "Confirm Delete Payment",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _service.DeletePaymentAsync(payment.Id);
                        await LoadDataAsync();
                        System.Windows.MessageBox.Show($"Payment '{payment.PaymentNumber}' deleted successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete payment: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public void ViewReceipt(Receipt receipt)
        {
            if (receipt == null) return;
            SelectedViewReceipt = receipt;
            IsViewReceiptModalOpen = true;
        }

        [RelayCommand]
        public void CloseViewReceiptModal()
        {
            IsViewReceiptModalOpen = false;
        }

        [RelayCommand]
        public void ViewPayment(Payment payment)
        {
            if (payment == null) return;
            SelectedViewPayment = payment;
            IsViewPaymentModalOpen = true;
        }

        [RelayCommand]
        public void CloseViewPaymentModal()
        {
            IsViewPaymentModalOpen = false;
        }

        [RelayCommand]
        public void EditReceipt(Receipt receipt)
        {
            if (receipt == null) return;
            NewReceipt = receipt;
            if (receipt.ReceiptType == ReceiptType.BankReceipt)
            {
                ActiveSubView = "BankReceiptForm";
            }
            else
            {
                ActiveSubView = "CashReceiptForm";
            }
        }

        [RelayCommand]
        public void EditPayment(Payment payment)
        {
            if (payment == null) return;
            NewPayment = payment;
            if (payment.PaymentType == PaymentType.BankPayment)
            {
                ActiveSubView = "BankPaymentForm";
            }
            else
            {
                ActiveSubView = "CashPaymentForm";
            }
        }

        [RelayCommand]
        public async Task PrintReceiptVoucherAsync(Receipt receipt)
        {
            if (receipt == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintReceiptVoucher(receipt, company);
        }

        [RelayCommand]
        public async Task PrintPaymentVoucherAsync(Payment payment)
        {
            if (payment == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintPaymentVoucher(payment, company);
        }

        [RelayCommand]
        public async Task PrintCashReceiptListAsync()
        {
            await PrintReceiptListInternalAsync("CASH RECEIPTS LIST", CashReceipts);
        }

        [RelayCommand]
        public async Task PrintBankReceiptListAsync()
        {
            await PrintReceiptListInternalAsync("BANK RECEIPTS LIST", BankReceipts);
        }

        private async Task PrintReceiptListInternalAsync(string title, System.Collections.Generic.IEnumerable<Receipt> list)
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var doc = new System.Windows.Documents.FlowDocument
            {
                PageWidth = 794,
                PageHeight = 1123,
                PagePadding = new System.Windows.Thickness(30),
                FontFamily = new System.Windows.Media.FontFamily("Times New Roman"),
                FontSize = 10
            };

            var compName = string.IsNullOrEmpty(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName;
            var header = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{compName}\n{title}\nDate: {DateTime.Now:dd/MM/yyyy HH:mm} | Total Records: {list.Count()}"))
            {
                TextAlignment = System.Windows.TextAlignment.Center,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 0, 15)
            };
            doc.Blocks.Add(header);

            var table = new System.Windows.Documents.Table();
            table.CellSpacing = 0;
            table.BorderThickness = new System.Windows.Thickness(1);
            table.BorderBrush = System.Windows.Media.Brushes.LightGray;

            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(200) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(130) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(84) });

            var rowGroup = new System.Windows.Documents.TableRowGroup();
            var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
            string[] headers = { "VOUCHER #", "DATE", "CUSTOMER / PAYER", "RECEIVED BY", "AMOUNT (PKR)", "STATUS" };
            foreach (var h in headers)
            {
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                {
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 9,
                    Margin = new System.Windows.Thickness(4)
                }));
            }
            rowGroup.Rows.Add(headerRow);

            int rowIdx = 0;
            decimal totalAmt = 0m;
            foreach (var rec in list)
            {
                totalAmt += rec.Amount;
                var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                var r = new System.Windows.Documents.TableRow { Background = bg };

                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(rec.ReceiptNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(rec.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(rec.CustomerName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(rec.ReceivedBy ?? rec.BankName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{rec.Amount:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(rec.Status ?? "Posted")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));

                rowGroup.Rows.Add(r);
                rowIdx++;
            }

            var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontWeight = System.Windows.FontWeights.Bold, FontSize = 9, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{list.Count()} Records")) { FontWeight = System.Windows.FontWeights.Bold, FontSize = 9, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{totalAmt:N2}")) { FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Green, FontSize = 10, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            rowGroup.Rows.Add(totalRow);

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);

            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, title);
            }
        }

        [RelayCommand]
        public async Task PrintCashPaymentListAsync()
        {
            await PrintPaymentListInternalAsync("CASH PAYMENTS LIST", CashPayments);
        }

        [RelayCommand]
        public async Task PrintBankPaymentListAsync()
        {
            await PrintPaymentListInternalAsync("BANK PAYMENTS LIST", BankPayments);
        }

        [RelayCommand]
        public async Task PrintExpenseListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var doc = new System.Windows.Documents.FlowDocument
            {
                PageWidth = 794,
                PageHeight = 1123,
                PagePadding = new System.Windows.Thickness(30),
                FontFamily = new System.Windows.Media.FontFamily("Times New Roman"),
                FontSize = 10
            };

            var compName = string.IsNullOrEmpty(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName;
            var header = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{compName}\nBUSINESS EXPENSES REGISTER\nDate: {DateTime.Now:dd/MM/yyyy HH:mm} | Total Records: {Expenses.Count}"))
            {
                TextAlignment = System.Windows.TextAlignment.Center,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 0, 15)
            };
            doc.Blocks.Add(header);

            var table = new System.Windows.Documents.Table();
            table.CellSpacing = 0;
            table.BorderThickness = new System.Windows.Thickness(1);
            table.BorderBrush = System.Windows.Media.Brushes.LightGray;

            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(180) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(130) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(100) });

            var rowGroup = new System.Windows.Documents.TableRowGroup();
            var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
            string[] headers = { "VOUCHER #", "DATE", "TITLE / EXPENSE", "CATEGORY", "AMOUNT (PKR)", "STATUS" };
            foreach (var h in headers)
            {
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                {
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 9,
                    Margin = new System.Windows.Thickness(4)
                }));
            }
            rowGroup.Rows.Add(headerRow);

            int rowIdx = 0;
            decimal totalAmt = 0m;
            foreach (var exp in Expenses)
            {
                totalAmt += exp.Amount;
                var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                var r = new System.Windows.Documents.TableRow { Background = bg };

                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(exp.VoucherNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(exp.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(exp.Title ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(exp.Category ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{exp.Amount:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(exp.Status ?? "Paid")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));

                rowGroup.Rows.Add(r);
                rowIdx++;
            }

            var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontWeight = System.Windows.FontWeights.Bold, FontSize = 9, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{Expenses.Count} Records")) { FontWeight = System.Windows.FontWeights.Bold, FontSize = 9, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{totalAmt:N2}")) { FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, FontSize = 10, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            rowGroup.Rows.Add(totalRow);

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);

            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, "Business Expenses Register");
            }
        }

        private async Task PrintPaymentListInternalAsync(string title, System.Collections.Generic.IEnumerable<Payment> list)
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var doc = new System.Windows.Documents.FlowDocument
            {
                PageWidth = 794,
                PageHeight = 1123,
                PagePadding = new System.Windows.Thickness(30),
                FontFamily = new System.Windows.Media.FontFamily("Times New Roman"),
                FontSize = 10
            };

            var compName = string.IsNullOrEmpty(company.CompanyName) ? "AL MADINA BUILDING MATERIAL" : company.CompanyName;
            var header = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{compName}\n{title}\nDate: {DateTime.Now:dd/MM/yyyy HH:mm} | Total Records: {list.Count()}"))
            {
                TextAlignment = System.Windows.TextAlignment.Center,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 0, 15)
            };
            doc.Blocks.Add(header);

            var table = new System.Windows.Documents.Table();
            table.CellSpacing = 0;
            table.BorderThickness = new System.Windows.Thickness(1);
            table.BorderBrush = System.Windows.Media.Brushes.LightGray;

            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(90) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(200) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(130) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(110) });
            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new System.Windows.GridLength(84) });

            var rowGroup = new System.Windows.Documents.TableRowGroup();
            var headerRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.Maroon };
            string[] headers = { "VOUCHER #", "DATE", "PARTY / PAYEE", "PAID FROM", "AMOUNT (PKR)", "STATUS" };
            foreach (var h in headers)
            {
                headerRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h))
                {
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 9,
                    Margin = new System.Windows.Thickness(4)
                }));
            }
            rowGroup.Rows.Add(headerRow);

            int rowIdx = 0;
            decimal totalAmt = 0m;
            foreach (var pay in list)
            {
                totalAmt += pay.Amount;
                var bg = (rowIdx % 2 == 1) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.White;
                var r = new System.Windows.Documents.TableRow { Background = bg };

                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pay.PaymentNumber ?? "")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pay.Date.ToString("dd/MM/yyyy"))) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pay.PartyName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pay.PaidFrom ?? pay.BankName ?? "")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{pay.Amount:N2}")) { FontSize = 9, FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, Margin = new System.Windows.Thickness(4) }));
                r.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(pay.Status ?? "Posted")) { FontSize = 9, Margin = new System.Windows.Thickness(4) }));

                rowGroup.Rows.Add(r);
                rowIdx++;
            }

            var totalRow = new System.Windows.Documents.TableRow { Background = System.Windows.Media.Brushes.LightYellow };
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("TOTAL")) { FontWeight = System.Windows.FontWeights.Bold, FontSize = 9, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{list.Count()} Records")) { FontWeight = System.Windows.FontWeights.Bold, FontSize = 9, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"{totalAmt:N2}")) { FontWeight = System.Windows.FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Red, FontSize = 10, Margin = new System.Windows.Thickness(4) }));
            totalRow.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))));
            rowGroup.Rows.Add(totalRow);

            table.RowGroups.Add(rowGroup);
            doc.Blocks.Add(table);

            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, title);
            }
        }

        [RelayCommand]
        private void OpenAddExpenseModal()
        {
            NewExpense = new Expense
            {
                VoucherNumber = "EXP-" + DateTime.Now.ToString("fffSSmm"),
                Date = DateTime.Now,
                Category = "Utility (Electricity, Water)",
                ExpenseType = "Operating",
                PaidFrom = "Cash",
                Status = "Paid"
            };
            IsAddExpenseModalOpen = true;
        }

        [RelayCommand]
        private void CloseAddExpenseModal()
        {
            IsAddExpenseModalOpen = false;
        }

        [RelayCommand]
        public void ViewExpense(Expense expense)
        {
            if (expense == null) return;
            SelectedExpenseForView = expense;
            IsViewExpenseModalOpen = true;
        }

        [RelayCommand]
        public void CloseViewExpenseModal()
        {
            IsViewExpenseModalOpen = false;
        }

        [RelayCommand]
        public void EditExpense(Expense expense)
        {
            if (expense == null) return;
            NewExpense = expense;
            IsAddExpenseModalOpen = true;
        }

        [RelayCommand]
        public async Task DeleteExpenseAsync(Expense expense)
        {
            if (expense != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete expense '{expense.Title}' ({expense.VoucherNumber})?",
                    "Confirm Expense Deletion",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await _service.DeleteExpenseAsync(expense.Id);
                    await LoadDataAsync();
                }
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
        public async Task SaveBankAsync()
        {
            if (string.IsNullOrWhiteSpace(NewBank.BankName))
            {
                NewBank.BankName = "Bank " + DateTime.Now.ToString("fffSSm");
            }
            if (string.IsNullOrWhiteSpace(NewBank.AccountNumber))
            {
                NewBank.AccountNumber = "ACC-" + DateTime.Now.ToString("fffSSm");
            }

            await _service.SaveBankAsync(NewBank);
            IsAddBankModalOpen = false;
            await LoadDataAsync();
        }

        [RelayCommand]
        public async Task DeleteBankAsync(Bank bank)
        {
            if (bank != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete bank account '{bank.BankName}' ({bank.AccountNumber})?",
                    "Confirm Delete Bank Account",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _service.DeleteBankAsync(bank.Id);
                        await LoadDataAsync();
                        System.Windows.MessageBox.Show($"Bank account '{bank.BankName}' deleted successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete bank account: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public async Task SaveExpenseAsync()
        {
            if (string.IsNullOrWhiteSpace(NewExpense.Title))
            {
                NewExpense.Title = "Expense " + DateTime.Now.ToString("fffSSm");
            }
            if (string.IsNullOrWhiteSpace(NewExpense.VoucherNumber))
            {
                NewExpense.VoucherNumber = "EXP-" + DateTime.Now.ToString("fffSSmm");
            }
            if (string.IsNullOrWhiteSpace(NewExpense.Category))
            {
                NewExpense.Category = "General Expense";
            }
            if (string.IsNullOrWhiteSpace(NewExpense.PaidFrom))
            {
                NewExpense.PaidFrom = "Cash";
            }
            if (string.IsNullOrWhiteSpace(NewExpense.Status))
            {
                NewExpense.Status = "Paid";
            }

            await _service.ProcessExpenseAsync(NewExpense);
            IsAddExpenseModalOpen = false;
            await LoadDataAsync();
        }
    }

    public partial class SalaryViewModel : ObservableObject
    {
        private readonly ISalaryService _service;

        [ObservableProperty]
        private SalarySubViewMode _subViewMode = SalarySubViewMode.StaffList;

        [ObservableProperty]
        private ObservableCollection<Staff> _staffs = new();

        [ObservableProperty]
        private ObservableCollection<Salary> _salaries = new();

        [ObservableProperty]
        private ObservableCollection<SalaryAdvance> _salaryAdvances = new();

        [ObservableProperty]
        private ObservableCollection<JournalEntry> _journalEntries = new();

        [ObservableProperty]
        private Staff? _selectedStaff;

        [ObservableProperty]
        private Staff _newStaff = new()
        {
            StaffCode = "STF-" + DateTime.Now.ToString("fffSSmm"),
            JoiningDate = DateTime.Now,
            EmploymentStatus = "Permanent",
            IsActive = true
        };

        [ObservableProperty]
        private Salary _newSalary = new()
        {
            Date = DateTime.Now,
            SalaryMonth = DateTime.Now.ToString("MMMM yyyy"),
            PaymentMode = PaymentMethod.Cash
        };

        [ObservableProperty]
        private SalaryAdvance _newSalaryAdvance = new()
        {
            VoucherNumber = "ADV-" + DateTime.Now.ToString("fffSSmm"),
            Date = DateTime.Now,
            RecoveryMonth = DateTime.Now.ToString("MMMM yyyy"),
            Status = "Approved"
        };

        [ObservableProperty]
        private JournalEntry _newJournalEntry = new()
        {
            VoucherNumber = "JV-" + DateTime.Now.ToString("fffSSmm"),
            Date = DateTime.Now,
            Status = "Posted"
        };

        [ObservableProperty]
        private bool _isPaySalaryModalOpen;

        [ObservableProperty]
        private bool _isAddStaffModalOpen;

        [ObservableProperty]
        private bool _isAddJournalModalOpen;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private decimal _totalBasicSalaries;

        [ObservableProperty]
        private decimal _totalDeductions;

        [ObservableProperty]
        private decimal _totalNetPaid;

        [ObservableProperty]
        private int _totalJournalTransactions;

        [ObservableProperty]
        private decimal _totalJournalDebits;

        [ObservableProperty]
        private decimal _totalJournalCredits;

        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        public SalaryViewModel(ISalaryService service, IPrintService printService, IRepository<CompanySetting> companyRepo)
        {
            _service = service;
            _printService = printService;
            _companyRepo = companyRepo;
        }

        [RelayCommand]
        public async Task LoadSalariesAsync()
        {
            var stList = await _service.GetStaffsAsync(SearchQuery);
            Staffs = new ObservableCollection<Staff>(stList);

            var list = await _service.GetSalariesAsync();
            Salaries = new ObservableCollection<Salary>(list);

            var advList = await _service.GetSalaryAdvancesAsync(SearchQuery);
            SalaryAdvances = new ObservableCollection<SalaryAdvance>(advList);

            TotalBasicSalaries = stList.Sum(s => s.BasicSalary);
            TotalDeductions = list.Sum(s => s.AdvanceDeduction + s.LoanDeduction);
            TotalNetPaid = list.Sum(s => s.NetPaid);

            var jList = await _service.GetJournalEntriesAsync(SearchQuery);
            JournalEntries = new ObservableCollection<JournalEntry>(jList);

            TotalJournalTransactions = jList.Count;
            TotalJournalDebits = jList.Sum(j => j.Debit);
            TotalJournalCredits = jList.Sum(j => j.Credit);
        }

        partial void OnSelectedStaffChanged(Staff? value)
        {
            if (value != null)
            {
                NewSalaryAdvance.StaffId = value.Id;
                NewSalaryAdvance.StaffName = value.FullName;
                NewSalaryAdvance.Department = value.Department;
            }
        }

        [RelayCommand]
        public void OpenNewAdvanceForm()
        {
            NewSalaryAdvance = new SalaryAdvance
            {
                VoucherNumber = "ADV-" + DateTime.Now.ToString("fffSSmm"),
                Date = DateTime.Now,
                RecoveryMonth = DateTime.Now.ToString("MMMM yyyy"),
                Status = "Approved"
            };
            SubViewMode = SalarySubViewMode.AdvanceForm;
        }

        [RelayCommand]
        public void CloseAdvanceForm()
        {
            SubViewMode = SalarySubViewMode.AdvanceList;
        }

        [RelayCommand]
        public async Task SaveSalaryAdvanceAsync()
        {
            if (SelectedStaff != null)
            {
                NewSalaryAdvance.StaffId = SelectedStaff.Id;
                NewSalaryAdvance.StaffName = SelectedStaff.FullName;
                NewSalaryAdvance.Department = SelectedStaff.Department;
            }
            else if (string.IsNullOrWhiteSpace(NewSalaryAdvance.StaffName))
            {
                NewSalaryAdvance.StaffName = "Staff Member";
            }

            await _service.SaveSalaryAdvanceAsync(NewSalaryAdvance);
            SubViewMode = SalarySubViewMode.AdvanceList;
            await LoadSalariesAsync();
        }

        [RelayCommand]
        public async Task DeleteSalaryAdvanceAsync(SalaryAdvance advance)
        {
            if (advance != null)
            {
                await _service.DeleteSalaryAdvanceAsync(advance.Id);
                await LoadSalariesAsync();
            }
        }

        [ObservableProperty]
        private string _addOrEditStaffTitle = "+ Add New Salary Staff";

        [RelayCommand]
        public void OpenAddStaffModal()
        {
            NewStaff = new Staff
            {
                StaffCode = "STF-" + DateTime.Now.ToString("fffSSmm"),
                JoiningDate = DateTime.Now,
                EmploymentStatus = "Permanent",
                IsActive = true
            };
            AddOrEditStaffTitle = "+ Add New Salary Staff";
            IsAddStaffModalOpen = true;
        }

        [RelayCommand]
        public void OpenEditStaffModal(Staff staff)
        {
            if (staff == null) return;
            NewStaff = new Staff
            {
                Id = staff.Id,
                StaffCode = staff.StaffCode,
                FullName = staff.FullName,
                CNIC = staff.CNIC,
                Phone = staff.Phone,
                Email = staff.Email,
                City = staff.City,
                Address = staff.Address,
                Designation = staff.Designation,
                Department = staff.Department,
                Grade = staff.Grade,
                JoiningDate = staff.JoiningDate,
                EmploymentStatus = staff.EmploymentStatus,
                LinkedOperationalEmployee = staff.LinkedOperationalEmployee,
                IsActive = staff.IsActive,
                BankName = staff.BankName,
                AccountNumber = staff.AccountNumber,
                IBAN = staff.IBAN,
                NTN = staff.NTN,
                EOBINumber = staff.EOBINumber,
                SESSINumber = staff.SESSINumber,
                ProvidentFundNumber = staff.ProvidentFundNumber,
                BasicSalary = staff.BasicSalary,
                AllowancesText = staff.AllowancesText,
                DeductionsText = staff.DeductionsText,
                TotalSalaryPaid = staff.TotalSalaryPaid,
                TotalAdvances = staff.TotalAdvances,
                TotalLoans = staff.TotalLoans,
                LoanOutstanding = staff.LoanOutstanding
            };
            AddOrEditStaffTitle = "✏️ Edit Salary Staff Details";
            IsAddStaffModalOpen = true;
        }

        [RelayCommand]
        public void CloseAddStaffModal()
        {
            IsAddStaffModalOpen = false;
        }

        [RelayCommand]
        public async Task SaveStaffAsync()
        {
            if (string.IsNullOrWhiteSpace(NewStaff.FullName))
            {
                NewStaff.FullName = "Staff Member " + DateTime.Now.ToString("fffSSm");
            }
            await _service.SaveStaffAsync(NewStaff);
            IsAddStaffModalOpen = false;
            await LoadSalariesAsync();
        }

        [RelayCommand]
        public void OpenPaySalaryModal(Staff? staff)
        {
            SelectedStaff = staff;
            NewSalary = new Salary
            {
                StaffId = staff?.Id,
                StaffName = staff?.FullName ?? "",
                BasicSalary = staff?.BasicSalary ?? 0m,
                Date = DateTime.Now,
                SalaryMonth = DateTime.Now.ToString("MMMM yyyy"),
                PaymentMode = PaymentMethod.Cash,
                Remarks = $"{DateTime.Now:MMMM yyyy} Salary"
            };
            IsPaySalaryModalOpen = true;
        }

        [RelayCommand]
        public void ClosePaySalaryModal()
        {
            IsPaySalaryModalOpen = false;
        }

        [RelayCommand]
        public async Task SaveSalaryAsync()
        {
            if (SelectedStaff != null)
            {
                NewSalary.StaffId = SelectedStaff.Id;
                NewSalary.StaffName = SelectedStaff.FullName;
            }
            else if (string.IsNullOrWhiteSpace(NewSalary.StaffName))
            {
                NewSalary.StaffName = "Employee " + DateTime.Now.ToString("fffSSm");
            }

            NewSalary.NetPaid = NewSalary.BasicSalary - NewSalary.AdvanceDeduction - NewSalary.LoanDeduction + NewSalary.Bonus;
            await _service.ProcessSalaryAsync(NewSalary);

            IsPaySalaryModalOpen = false;
            await LoadSalariesAsync();
        }

        [RelayCommand]
        public void ViewStaffLedger(Staff staff)
        {
            SelectedStaff = staff;
            SubViewMode = SalarySubViewMode.StaffLedger;
            RebuildStaffLedgerRows();
        }

        [ObservableProperty]
        private ObservableCollection<SalaryLedgerRowDto> _staffLedgerRows = new();

        [ObservableProperty]
        private int _selectedLedgerYear = DateTime.Now.Year;

        [ObservableProperty]
        private string _selectedLedgerMonth = "All";

        partial void OnSelectedLedgerYearChanged(int value) => RebuildStaffLedgerRows();
        partial void OnSelectedLedgerMonthChanged(string value) => RebuildStaffLedgerRows();

        [RelayCommand]
        public void PrevLedgerYear() => SelectedLedgerYear--;

        [RelayCommand]
        public void NextLedgerYear() => SelectedLedgerYear++;

        public void RebuildStaffLedgerRows()
        {
            if (SelectedStaff == null) return;

            var rows = new List<SalaryLedgerRowDto>();

            // 1. Add Salary Payments
            var staffSalaries = Salaries.Where(s => s.StaffId == SelectedStaff.Id || (s.StaffName != null && s.StaffName.Equals(SelectedStaff.FullName, StringComparison.OrdinalIgnoreCase)));
            foreach (var s in staffSalaries)
            {
                rows.Add(new SalaryLedgerRowDto
                {
                    Date = s.Date,
                    Type = "Salary Payment",
                    Description = string.IsNullOrWhiteSpace(s.Remarks) ? $"{s.SalaryMonth} Salary" : s.Remarks,
                    PaidOut = s.NetPaid,
                    AdvanceReceived = s.AdvanceDeduction
                });
            }

            // 2. Add Salary Advances taken by employee
            var staffAdvances = SalaryAdvances.Where(a => a.StaffId == SelectedStaff.Id || (a.StaffName != null && a.StaffName.Equals(SelectedStaff.FullName, StringComparison.OrdinalIgnoreCase)));
            foreach (var a in staffAdvances)
            {
                rows.Add(new SalaryLedgerRowDto
                {
                    Date = a.Date,
                    Type = "Salary Advance",
                    Description = $"Advance Taken (Recovery: {a.RecoveryMonth}) - {a.Remarks}",
                    PaidOut = a.Amount,
                    AdvanceReceived = 0m
                });
            }

            var filtered = rows.AsEnumerable();
            if (SelectedLedgerYear > 0)
                filtered = filtered.Where(r => r.Date.Year == SelectedLedgerYear);

            if (!string.Equals(SelectedLedgerMonth, "All", StringComparison.OrdinalIgnoreCase) && DateTime.TryParseExact(SelectedLedgerMonth, "MMMM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var mDt))
            {
                filtered = filtered.Where(r => r.Date.Month == mDt.Month);
            }

            StaffLedgerRows = new ObservableCollection<SalaryLedgerRowDto>(filtered.OrderByDescending(r => r.Date));
        }

        [RelayCommand]
        public async Task PrintStaffLedgerAsync()
        {
            if (SelectedStaff == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintStaffLedger(SelectedStaff, StaffLedgerRows, company);
        }

        [RelayCommand]
        public async Task PrintSalaryStaffListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintSalaryStaffRegister(Staffs, company);
        }

        [RelayCommand]
        public void ExportSalaryStaffList()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Salary_Staff_List_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Staff Code,Full Name,CNIC,Phone,Designation,Department,Joining Date,Basic Salary");
                    foreach (var s in Staffs)
                    {
                        sb.AppendLine($"\"{s.StaffCode}\",\"{s.FullName}\",\"{s.CNIC}\",\"{s.Phone}\",\"{s.Designation}\",\"{s.Department}\",\"{s.JoiningDate:dd/MM/yyyy}\",{s.BasicSalary}");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    System.Windows.MessageBox.Show($"Salary Staff List successfully exported to:\n{sfd.FileName}", "Export Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintSalaryAdvancesAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Voucher #", "Staff Name", "Department", "Date", "Recovery Month", "Amount (PKR)", "Status" };
            var rows = SalaryAdvances.Select(a => new[] { a.VoucherNumber, a.StaffName, a.Department, a.Date.ToString("dd/MM/yyyy"), a.RecoveryMonth, $"Rs. {a.Amount:N0}", a.Status });
            var totalAdv = SalaryAdvances.Sum(a => a.Amount);
            var totals = new[] { "TOTAL ADVANCES", $"{SalaryAdvances.Count} Vouchers", "", "", "", $"Rs. {totalAdv:N0}", "" };
            _printService.PrintReportTable("Salary Advances Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public void ExportSalaryAdvances()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"Salary_Advances_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Voucher Number,Staff Name,Department,Date,Recovery Month,Amount,Status,Remarks");
                    foreach (var a in SalaryAdvances)
                    {
                        sb.AppendLine($"\"{a.VoucherNumber}\",\"{a.StaffName}\",\"{a.Department}\",\"{a.Date:dd/MM/yyyy}\",\"{a.RecoveryMonth}\",{a.Amount},\"{a.Status}\",\"{a.Remarks}\"");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    System.Windows.MessageBox.Show($"Salary Advances successfully exported to:\n{sfd.FileName}", "Export Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintSalaryJournalAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Voucher #", "Date", "Account Name", "Remarks", "Debit (PKR)", "Credit (PKR)", "Status" };
            var rows = JournalEntries.Select(j => new[] { j.VoucherNumber, j.Date.ToString("dd/MM/yyyy"), j.AccountName, j.Remarks, $"Rs. {j.Debit:N0}", $"Rs. {j.Credit:N0}", j.Status });
            var totalDebit = JournalEntries.Sum(j => j.Debit);
            var totalCredit = JournalEntries.Sum(j => j.Credit);
            var totals = new[] { "JOURNAL TOTAL", $"{JournalEntries.Count} Entries", "", "", $"Rs. {totalDebit:N0}", $"Rs. {totalCredit:N0}", "" };
            _printService.PrintReportTable("General Journal Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public void ExportSalaryJournal()
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV File (*.csv)|*.csv",
                    FileName = $"General_Journal_{DateTime.Now:yyyyMMdd}.csv"
                };
                if (sfd.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Voucher Number,Date,Account Name,Remarks,Debit,Credit,Status");
                    foreach (var j in JournalEntries)
                    {
                        sb.AppendLine($"\"{j.VoucherNumber}\",\"{j.Date:dd/MM/yyyy}\",\"{j.AccountName}\",\"{j.Remarks}\",{j.Debit},{j.Credit},\"{j.Status}\"");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                    System.Windows.MessageBox.Show($"General Journal successfully exported to:\n{sfd.FileName}", "Export Successful", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public void CloseStaffLedger()
        {
            SubViewMode = SalarySubViewMode.StaffList;
        }

        [RelayCommand]
        public async Task DeleteStaffAsync(Staff staff)
        {
            if (staff != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete staff member '{staff.FullName}'?",
                    "Confirm Delete Staff",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        await _service.DeleteStaffAsync(staff.Id);
                        await LoadSalariesAsync();
                        System.Windows.MessageBox.Show($"Staff member '{staff.FullName}' deleted successfully.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to delete staff member: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }

        [RelayCommand]
        public void OpenAddJournalModal()
        {
            NewJournalEntry = new JournalEntry
            {
                VoucherNumber = "JV-" + DateTime.Now.ToString("fffSSmm"),
                Date = DateTime.Now,
                Status = "Posted"
            };
            IsAddJournalModalOpen = true;
        }

        [RelayCommand]
        public void CloseAddJournalModal()
        {
            IsAddJournalModalOpen = false;
        }

        [RelayCommand]
        public async Task SaveJournalEntryAsync()
        {
            if (string.IsNullOrWhiteSpace(NewJournalEntry.AccountName))
            {
                NewJournalEntry.AccountName = "General Account";
            }
            await _service.SaveJournalEntryAsync(NewJournalEntry);
            IsAddJournalModalOpen = false;
            await LoadSalariesAsync();
        }

        [RelayCommand]
        public async Task DeleteJournalEntryAsync(JournalEntry entry)
        {
            if (entry != null)
            {
                await _service.DeleteJournalEntryAsync(entry.Id);
                await LoadSalariesAsync();
            }
        }
    }

    public partial class ReportsViewModel : ObservableObject
    {
        private readonly IReportService _reportService;
        private readonly IPurchaseService _purchaseService;
        private readonly ISaleService _saleService;
        private readonly ISalaryService _salaryService;
        private readonly IVendorService _vendorService;
        private readonly ICustomerService _customerService;
        private readonly IInventoryService _inventoryService;

        [ObservableProperty]
        private ReportsSubViewMode _activeSubViewMode = ReportsSubViewMode.FinancialReports;

        [ObservableProperty]
        private int _selectedTabIndex;

        partial void OnActiveSubViewModeChanged(ReportsSubViewMode value)
        {
            SelectedTabIndex = value switch
            {
                ReportsSubViewMode.InventoryLedgerReport => 0,
                ReportsSubViewMode.InventoryBalancesReport => 1,
                ReportsSubViewMode.LowStockAlertReport => 2,
                ReportsSubViewMode.FinancialReports or ReportsSubViewMode.JournalReport or ReportsSubViewMode.PurchaseSummary or ReportsSubViewMode.PosSalesReport => 3,
                ReportsSubViewMode.BalanceSheet => 4,
                ReportsSubViewMode.ItemWiseProfitLossReport => 5,
                ReportsSubViewMode.CustomerBalancesList or ReportsSubViewMode.CustomerLedgerDetail => 6,
                ReportsSubViewMode.VendorBalancesList or ReportsSubViewMode.VendorLedgerDetail => 7,
                _ => 0
            };
        }

        [ObservableProperty]
        private BalanceSheetReportDto _balanceSheet = new();

        [ObservableProperty]
        private ProfitLossReportDto _profitAndLoss = new();

        [ObservableProperty]
        private ObservableCollection<JournalEntry> _journalEntries = new();

        [ObservableProperty]
        private ObservableCollection<PurchaseInvoice> _purchaseInvoices = new();

        [ObservableProperty]
        private ObservableCollection<SaleInvoice> _saleInvoices = new();

        [ObservableProperty]
        private ObservableCollection<ItemProfitLossDto> _itemWiseProfitLoss = new();

        [ObservableProperty]
        private ObservableCollection<Item> _inventoryItems = new();

        [ObservableProperty]
        private ObservableCollection<LowStockItemDto> _lowStockAlerts = new();

        [ObservableProperty]
        private ObservableCollection<VendorBalanceDto> _vendorBalances = new();

        [ObservableProperty]
        private ObservableCollection<CustomerBalanceDto> _customerBalances = new();

        [ObservableProperty]
        private ObservableCollection<VendorLedger> _vendorLedgerEntries = new();

        [ObservableProperty]
        private ObservableCollection<CustomerLedger> _customerLedgerEntries = new();

        [ObservableProperty]
        private ObservableCollection<InventoryLedger> _inventoryLedgerEntries = new();

        [ObservableProperty]
        private DateTime _journalFromDate = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime _journalToDate = DateTime.Today;

        [ObservableProperty]
        private ObservableCollection<DailyActivityJournalDto> _journalActivities = new();

        partial void OnJournalFromDateChanged(DateTime value) => _ = LoadJournalActivitiesAsync();
        partial void OnJournalToDateChanged(DateTime value) => _ = LoadJournalActivitiesAsync();

        [ObservableProperty]
        private Item? _selectedInventoryItem;

        [ObservableProperty]
        private VendorBalanceDto? _selectedVendorBalance;

        [ObservableProperty]
        private CustomerBalanceDto? _selectedCustomerBalance;

        [ObservableProperty]
        private string _vendorSearchQuery = string.Empty;

        private System.Threading.CancellationTokenSource? _vendorReportSearchCts;
        private System.Threading.CancellationTokenSource? _customerReportSearchCts;

        partial void OnVendorSearchQueryChanged(string value)
        {
            _vendorReportSearchCts?.Cancel();
            _vendorReportSearchCts = new System.Threading.CancellationTokenSource();
            var token = _vendorReportSearchCts.Token;

            Task.Delay(250, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
                    {
                        await LoadVendorBalancesAsync();
                    });
                }
            }, TaskScheduler.Default);
        }

        [ObservableProperty]
        private string _customerSearchQuery = string.Empty;

        partial void OnCustomerSearchQueryChanged(string value)
        {
            _customerReportSearchCts?.Cancel();
            _customerReportSearchCts = new System.Threading.CancellationTokenSource();
            var token = _customerReportSearchCts.Token;

            Task.Delay(250, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
                    {
                        await LoadCustomerBalancesAsync();
                    });
                }
            }, TaskScheduler.Default);
        }

        public async Task LoadCustomerBalancesAsync()
        {
            var cBalances = await _customerService.GetCustomerBalancesAsync(CustomerSearchQuery);
            CustomerBalances = new ObservableCollection<CustomerBalanceDto>(cBalances);
            CustomersWithBalanceCount = cBalances.Count(c => c.CustomerOwes > 0 || c.AdvanceAvailable > 0);
            TotalCustomerReceivable = cBalances.Sum(c => c.CustomerOwes);
            TotalCustomerAdvance = cBalances.Sum(c => c.AdvanceAvailable);
            NetCustomerBalance = TotalCustomerReceivable - TotalCustomerAdvance;
        }

        public async Task LoadVendorBalancesAsync()
        {
            var vBalances = await _vendorService.GetVendorBalancesAsync(VendorSearchQuery);
            VendorBalances = new ObservableCollection<VendorBalanceDto>(vBalances);
            TotalVendorsCount = vBalances.Count;
            TotalVendorPayableWeOwe = vBalances.Sum(v => v.VendorOwes);
            TotalVendorAdvanceVendorOwes = vBalances.Sum(v => v.AdvanceAvailable);
        }

        [ObservableProperty]
        private string _inventorySearchQuery = string.Empty;

        [ObservableProperty]
        private DateTime? _inventoryFromDate = new DateTime(2026, 1, 1);

        [ObservableProperty]
        private DateTime? _inventoryToDate = DateTime.Today;

        [RelayCommand]
        public async Task FilterInventoryLedgerAsync()
        {
            var items = await _inventoryService.SearchItemsAsync(InventorySearchQuery);
            InventoryItems = new ObservableCollection<Item>(items);
            TotalInventoryItemsCount = items.Count;
            TotalInventoryStockValue = items.Sum(i => i.CurrentStock * i.SalePrice);
            TotalInventoryPurchaseValue = items.Sum(i => i.CurrentStock * i.PurchasePrice);

            var invLedgerList = await _inventoryService.GetAllInventoryLedgerAsync(InventoryFromDate, InventoryToDate);
            InventoryLedgerEntries = new ObservableCollection<InventoryLedger>(invLedgerList);
        }

        [ObservableProperty]
        private int _totalJournalCount;

        [ObservableProperty]
        private decimal _totalJournalDebits = 0m;

        [ObservableProperty]
        private decimal _totalJournalCredits = 0m;

        [ObservableProperty]
        private int _totalPurchaseInvoicesCount;

        [ObservableProperty]
        private decimal _totalPurchaseGrossAmount;

        [ObservableProperty]
        private decimal _totalPurchaseDiscount;

        [ObservableProperty]
        private decimal _totalPurchaseNetAmount;

        [ObservableProperty]
        private int _totalVendorsCount;

        [ObservableProperty]
        private decimal _totalVendorPurchasesAmount;

        [ObservableProperty]
        private decimal _totalVendorPayableWeOwe = 0m;

        [ObservableProperty]
        private decimal _totalVendorAdvanceVendorOwes = 0m;

        [ObservableProperty]
        private int _customersWithBalanceCount;

        [ObservableProperty]
        private decimal _totalCustomerReceivable = 0m;

        [ObservableProperty]
        private decimal _totalCustomerAdvance = 0m;

        [ObservableProperty]
        private decimal _netCustomerBalance = 0m;

        [ObservableProperty]
        private decimal _totalPosSales = 0m;

        [ObservableProperty]
        private decimal _totalPosCashSales = 0m;

        [ObservableProperty]
        private decimal _totalPosCardSales = 0m;

        // Inventory Balances Stat Cards (Screenshot 2)
        [ObservableProperty]
        private int _totalInventoryItemsCount;

        [ObservableProperty]
        private decimal _totalInventoryStockValue = 0m;

        [ObservableProperty]
        private decimal _totalInventoryPurchaseValue = 0m;

        [ObservableProperty]
        private int _itemsLowStockCount = 0;

        private readonly IPrintService _printService;
        private readonly IRepository<CompanySetting> _companyRepo;

        public ReportsViewModel(
            IReportService reportService,
            IPurchaseService purchaseService,
            ISaleService saleService,
            ISalaryService salaryService,
            IVendorService vendorService,
            ICustomerService customerService,
            IInventoryService inventoryService,
            IPrintService printService,
            IRepository<CompanySetting> companyRepo)
        {
            _reportService = reportService;
            _purchaseService = purchaseService;
            _saleService = saleService;
            _salaryService = salaryService;
            _vendorService = vendorService;
            _customerService = customerService;
            _inventoryService = inventoryService;
            _printService = printService;
            _companyRepo = companyRepo;
        }

        [RelayCommand]
        public async Task PrintCustomerBalancesListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Code", "Customer Name", "Phone", "Area", "Advance Available", "Outstanding Owed", "Status" };
            var rows = CustomerBalances.Select(c => new[] { c.Code, c.Name, c.Phone, c.Area, $"Rs. {c.AdvanceAvailable:N0}", $"Rs. {c.CustomerOwes:N0}", c.StatusText });
            var totals = new[] { "TOTAL", $"{CustomerBalances.Count} Customers", "", "", $"Rs. {TotalCustomerAdvance:N0}", $"Rs. {TotalCustomerReceivable:N0}", $"Net: Rs. {NetCustomerBalance:N0}" };
            _printService.PrintReportTable("Customer Balances Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintVendorBalancesListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Code", "Vendor Name", "Phone", "Area", "Advance Paid", "Outstanding Payable", "Status" };
            var rows = VendorBalances.Select(v => new[] { v.Code, v.Name, v.Phone, v.Area, $"Rs. {v.AdvanceAvailable:N0}", $"Rs. {v.VendorOwes:N0}", v.StatusText });
            var totals = new[] { "TOTAL", $"{VendorBalances.Count} Vendors", "", "", $"Rs. {TotalVendorAdvanceVendorOwes:N0}", $"Rs. {TotalVendorPayableWeOwe:N0}", $"Net: Rs. {(TotalVendorPayableWeOwe - TotalVendorAdvanceVendorOwes):N0}" };
            _printService.PrintReportTable("Vendor Balances Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintCustomerLedgerAsync()
        {
            if (SelectedCustomerBalance == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintCustomerLedger(SelectedCustomerBalance, CustomerLedgerEntries, company);
        }

        [RelayCommand]
        public async Task PrintVendorLedgerAsync()
        {
            if (SelectedVendorBalance == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintVendorLedger(SelectedVendorBalance, VendorLedgerEntries, company);
        }

        [RelayCommand]
        public async Task PrintInventoryBalancesListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Code", "Item Name", "Category", "Unit", "Stock Qty", "Purchase Rate", "Sale Rate" };
            var rows = InventoryItems.Select(i => new[] { i.Code, i.Name, i.CategoryName, i.SellingUnit, $"{i.CurrentStock:N0}", $"Rs. {i.PurchasePrice:N0}", $"Rs. {i.SalePrice:N0}" });
            var totals = new[] { "TOTAL", $"{InventoryItems.Count} Items", "", "", "", $"Purchase Val: Rs. {TotalInventoryPurchaseValue:N0}", $"Stock Val: Rs. {TotalInventoryStockValue:N0}" };
            _printService.PrintReportTable("Inventory Balances Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintInventoryLedgerListAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Code", "Item Name", "Purchase Rate", "Retail Rate", "Unit", "Current Stock" };
            var rows = InventoryItems.Select(i => new[] { i.Code, i.Name, $"Rs. {i.PurchasePrice:N0}", $"Rs. {i.SalePrice:N0}", i.SellingUnit, $"{i.CurrentStock:N0} Pcs" });
            var totals = new[] { "TOTAL", $"{InventoryItems.Count} Items Recorded", "", "", "", $"Stock Value: Rs. {TotalInventoryStockValue:N0}" };
            _printService.PrintReportTable("Inventory Ledger Summary Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintBalanceSheetAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Category", "Financial Item", "Amount (PKR)" };
            var rows = new List<string[]>
            {
                new[] { "ASSETS", "Cash & Bank Balances", $"Rs. {BalanceSheet.CashAndBankBalance:N0}" },
                new[] { "ASSETS", "Accounts Receivable (Customer Owes)", $"Rs. {BalanceSheet.AccountsReceivable:N0}" },
                new[] { "ASSETS", "Inventory Asset Valuation", $"Rs. {BalanceSheet.InventoryAssetValue:N0}" },
                new[] { "TOTAL ASSETS", "Total Assets", $"Rs. {BalanceSheet.TotalCurrentAssets:N0}" },
                new[] { "LIABILITIES", "Accounts Payable (Vendor We Owe)", $"Rs. {BalanceSheet.AccountsPayable:N0}" },
                new[] { "EQUITY", "Retained Earnings & Owner Equity", $"Rs. {BalanceSheet.EquityAndRetainedEarnings:N0}" }
            };
            var totals = new[] { "BALANCE SHEET STATUS", "Balanced", $"Total Liabilities & Equity: Rs. {BalanceSheet.TotalCurrentAssets:N0}" };
            _printService.PrintReportTable("Balance Sheet Statement", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintItemWiseProfitLossAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Item Code", "Item Name", "Qty Sold", "Sales Revenue", "COGS (Cost)", "Profit / Loss", "Margin %" };
            var rows = ItemWiseProfitLoss.Select(i => new[] { i.Code, i.Name, $"{i.QuantitySold:N0} {i.Unit}", $"Rs. {i.TotalSaleAmount:N0}", $"Rs. {i.TotalCostAmount:N0}", $"Rs. {i.ProfitOrLoss:N0}", $"{i.ProfitMarginPercent:F1}%" });
            var totalSales = ItemWiseProfitLoss.Sum(i => i.TotalSaleAmount);
            var totalCost = ItemWiseProfitLoss.Sum(i => i.TotalCostAmount);
            var totalProfit = ItemWiseProfitLoss.Sum(i => i.ProfitOrLoss);
            var totals = new[] { "TOTAL", $"{ItemWiseProfitLoss.Count} Items", "", $"Rs. {totalSales:N0}", $"Rs. {totalCost:N0}", $"Net Profit: Rs. {totalProfit:N0}", "" };
            _printService.PrintReportTable("Item-Wise Profit & Loss Report", headers, rows, totals, company);
        }

        [RelayCommand]
        public async Task PrintInventoryLedgerAsync()
        {
            if (SelectedInventoryItem == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintInventoryLedger(SelectedInventoryItem, InventoryLedgerEntries, company);
        }

        [RelayCommand]
        partial void OnSelectedTabIndexChanged(int value)
        {
            _ = LoadActiveTabReportAsync(value);
        }

        public async Task GenerateReportsAsync()
        {
            await LoadActiveTabReportAsync(SelectedTabIndex);
        }

        private async Task LoadActiveTabReportAsync(int tabIndex)
        {
            var from = new DateTime(2026, 1, 1);
            var to = DateTime.Now;

            try
            {
                switch (tabIndex)
                {
                    case 0: // Inventory Ledger
                        {
                            var items = await _inventoryService.SearchItemsAsync(InventorySearchQuery);
                            InventoryItems = new ObservableCollection<Item>(items);
                            TotalInventoryItemsCount = items.Count;
                            TotalInventoryStockValue = items.Sum(i => i.CurrentStock * i.SalePrice);
                            TotalInventoryPurchaseValue = items.Sum(i => i.CurrentStock * i.PurchasePrice);

                            var invLedgerList = await _inventoryService.GetAllInventoryLedgerAsync(from, to);
                            InventoryLedgerEntries = new ObservableCollection<InventoryLedger>(invLedgerList);
                            break;
                        }
                    case 1: // Inventory Balances
                        {
                            var items = await _inventoryService.SearchItemsAsync(InventorySearchQuery);
                            InventoryItems = new ObservableCollection<Item>(items);
                            TotalInventoryItemsCount = items.Count;
                            TotalInventoryStockValue = items.Sum(i => i.CurrentStock * i.SalePrice);
                            TotalInventoryPurchaseValue = items.Sum(i => i.CurrentStock * i.PurchasePrice);
                            break;
                        }
                    case 2: // Low Stock Alert
                        {
                            var alerts = await _inventoryService.GetLowStockAlertsAsync();
                            LowStockAlerts = new ObservableCollection<LowStockItemDto>(alerts);
                            ItemsLowStockCount = alerts.Count;
                            break;
                        }
                    case 3: // Customer Balances
                        {
                            var cBalances = await _customerService.GetCustomerBalancesAsync(CustomerSearchQuery);
                            CustomerBalances = new ObservableCollection<CustomerBalanceDto>(cBalances);
                            CustomersWithBalanceCount = cBalances.Count(c => c.CustomerOwes > 0 || c.AdvanceAvailable > 0);
                            TotalCustomerReceivable = cBalances.Sum(c => c.CustomerOwes);
                            TotalCustomerAdvance = cBalances.Sum(c => c.AdvanceAvailable);
                            NetCustomerBalance = TotalCustomerReceivable - TotalCustomerAdvance;
                            break;
                        }
                    case 4: // Vendor Balances
                        {
                            var vBalances = await _vendorService.GetVendorBalancesAsync(VendorSearchQuery);
                            VendorBalances = new ObservableCollection<VendorBalanceDto>(vBalances);
                            TotalVendorsCount = vBalances.Count;
                            TotalVendorPayableWeOwe = vBalances.Sum(v => v.VendorOwes);
                            TotalVendorAdvanceVendorOwes = vBalances.Sum(v => v.AdvanceAvailable);
                            break;
                        }
                    case 5: // Journal / Activity
                        {
                            await LoadJournalActivitiesAsync();
                            break;
                        }
                    case 6: // Salary Staff Report
                        {
                            await LoadStaffReportsAsync();
                            break;
                        }
                    default:
                        {
                            BalanceSheet = await _reportService.GetBalanceSheetReportAsync(to);
                            ProfitAndLoss = await _reportService.GetProfitLossReportAsync(from, to);
                            var itemPL = await _reportService.GetItemWiseProfitLossAsync(from, to);
                            ItemWiseProfitLoss = new ObservableCollection<ItemProfitLossDto>(itemPL);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Report Load Error (Tab {tabIndex}): {ex.Message}");
            }
        }

        [ObservableProperty]
        private ObservableCollection<Staff> _staffReports = new();

        [ObservableProperty]
        private Staff? _selectedStaffReport;

        [ObservableProperty]
        private ObservableCollection<SalaryLedgerRowDto> _staffLedgerReportEntries = new();

        [ObservableProperty]
        private bool _isStaffLedgerModalOpen;

        [ObservableProperty]
        private string _staffReportSearchQuery = string.Empty;

        [ObservableProperty]
        private decimal _totalStaffBasicSalary = 0m;

        [ObservableProperty]
        private int _totalStaffCount = 0;

        private System.Threading.CancellationTokenSource? _staffReportSearchCts;

        partial void OnStaffReportSearchQueryChanged(string value)
        {
            _staffReportSearchCts?.Cancel();
            _staffReportSearchCts = new System.Threading.CancellationTokenSource();
            var token = _staffReportSearchCts.Token;

            Task.Delay(250, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
                    {
                        await LoadStaffReportsAsync();
                    });
                }
            }, TaskScheduler.Default);
        }

        public async Task LoadStaffReportsAsync()
        {
            var list = await _salaryService.GetStaffsAsync(StaffReportSearchQuery);
            StaffReports = new ObservableCollection<Staff>(list);
            TotalStaffCount = list.Count;
            TotalStaffBasicSalary = list.Sum(s => s.BasicSalary);
        }

        [RelayCommand]
        public async Task ViewStaffReportLedgerAsync(Staff staff)
        {
            if (staff == null) return;
            SelectedStaffReport = staff;

            var rows = new List<SalaryLedgerRowDto>();
            var salaries = await _salaryService.GetSalariesAsync();
            var staffSalaries = salaries.Where(s => s.StaffId == staff.Id || (s.StaffName != null && s.StaffName.Equals(staff.FullName, StringComparison.OrdinalIgnoreCase)));
            foreach (var s in staffSalaries)
            {
                rows.Add(new SalaryLedgerRowDto
                {
                    Date = s.Date,
                    Type = "Salary Payment",
                    Description = string.IsNullOrWhiteSpace(s.Remarks) ? $"{s.SalaryMonth} Salary" : s.Remarks,
                    PaidOut = s.NetPaid,
                    AdvanceReceived = s.AdvanceDeduction
                });
            }

            var advances = await _salaryService.GetSalaryAdvancesAsync();
            var staffAdvances = advances.Where(a => a.StaffId == staff.Id || (a.StaffName != null && a.StaffName.Equals(staff.FullName, StringComparison.OrdinalIgnoreCase)));
            foreach (var a in staffAdvances)
            {
                rows.Add(new SalaryLedgerRowDto
                {
                    Date = a.Date,
                    Type = "Salary Advance",
                    Description = $"Advance Taken (Recovery: {a.RecoveryMonth}) - {a.Remarks}",
                    PaidOut = a.Amount,
                    AdvanceReceived = 0m
                });
            }

            StaffLedgerReportEntries = new ObservableCollection<SalaryLedgerRowDto>(rows.OrderByDescending(r => r.Date));
            IsStaffLedgerModalOpen = true;
        }

        [RelayCommand]
        public void CloseStaffLedgerReport()
        {
            IsStaffLedgerModalOpen = false;
        }

        [RelayCommand]
        public async Task PrintStaffReportLedgerAsync()
        {
            if (SelectedStaffReport == null) return;
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintStaffLedger(SelectedStaffReport, StaffLedgerReportEntries, company);
        }

        [RelayCommand]
        public async Task PrintSalaryStaffRegisterCommandAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            _printService.PrintSalaryStaffRegister(StaffReports, company);
        }

        [RelayCommand]
        public async Task LoadJournalActivitiesAsync()
        {
            var activities = new List<DailyActivityJournalDto>();
            var f = JournalFromDate.Date;
            var t = JournalToDate.Date.AddDays(1).AddTicks(-1);

            // 1. Sales
            var sales = await _saleService.SearchInvoicesAsync("");
            foreach (var s in sales.Where(x => x.Date >= f && x.Date <= t))
            {
                var isReturn = s.Type == Core.Enums.InvoiceType.SaleReturn;
                activities.Add(new DailyActivityJournalDto
                {
                    Date = s.Date,
                    TransactionType = isReturn ? "Sale Return" : "Sale Invoice",
                    ReferenceNumber = s.InvoiceNumber,
                    AccountPartyName = string.IsNullOrWhiteSpace(s.CustomerName) ? "Walk-in Cash Customer" : s.CustomerName,
                    Description = isReturn ? "Items Returned" : "Goods Sold",
                    Debit = isReturn ? 0m : s.NetAmount,
                    Credit = isReturn ? s.NetAmount : 0m,
                    Amount = s.NetAmount
                });
            }

            // 2. Purchases
            var purchases = await _purchaseService.SearchPurchasesAsync("");
            foreach (var p in purchases.Where(x => x.Date >= f && x.Date <= t))
            {
                var isReturn = p.Type == Core.Enums.PurchaseType.PurchaseReturn;
                activities.Add(new DailyActivityJournalDto
                {
                    Date = p.Date,
                    TransactionType = isReturn ? "Purchase Return" : "Purchase Invoice",
                    ReferenceNumber = p.PurchaseNumber,
                    AccountPartyName = string.IsNullOrWhiteSpace(p.VendorName) ? "Supplier" : p.VendorName,
                    Description = isReturn ? "Items Returned to Supplier" : "Inventory Purchased",
                    Debit = isReturn ? p.NetAmount : 0m,
                    Credit = isReturn ? 0m : p.NetAmount,
                    Amount = p.NetAmount
                });
            }

            // 3. Journal Entries
            var jEntries = await _salaryService.GetJournalEntriesAsync("");
            foreach (var j in jEntries.Where(x => x.Date >= f && x.Date <= t))
            {
                activities.Add(new DailyActivityJournalDto
                {
                    Date = j.Date,
                    TransactionType = "Journal Entry",
                    ReferenceNumber = j.VoucherNumber,
                    AccountPartyName = j.AccountName,
                    Description = j.Remarks,
                    Debit = j.Debit,
                    Credit = j.Credit,
                    Amount = Math.Max(j.Debit, j.Credit)
                });
            }

            JournalActivities = new ObservableCollection<DailyActivityJournalDto>(activities.OrderByDescending(a => a.Date));
            TotalJournalCount = JournalActivities.Count;
            TotalJournalDebits = JournalActivities.Sum(a => a.Debit);
            TotalJournalCredits = JournalActivities.Sum(a => a.Credit);
        }

        [RelayCommand]
        public async Task PrintJournalReportAsync()
        {
            var company = (await _companyRepo.GetAllAsync()).FirstOrDefault() ?? new CompanySetting();
            var headers = new[] { "Date", "Type", "Ref #", "Account / Party", "Description", "Debit (PKR)", "Credit (PKR)" };
            var rows = JournalActivities.Select(a => new[] { a.Date.ToString("yyyy-MM-dd HH:mm"), a.TransactionType, a.ReferenceNumber, a.AccountPartyName, a.Description, $"Rs. {a.Debit:N0}", $"Rs. {a.Credit:N0}" });
            var totals = new[] { "JOURNAL TOTAL", $"{JournalActivities.Count} Activities", "", "", $"Period: {JournalFromDate:dd/MM/yyyy} to {JournalToDate:dd/MM/yyyy}", $"Rs. {TotalJournalDebits:N0}", $"Rs. {TotalJournalCredits:N0}" };
            _printService.PrintReportTable("General Activity Journal Report", headers, rows, totals, company);
        }

        [ObservableProperty]
        private bool _isCustomerLedgerModalOpen;

        [ObservableProperty]
        private bool _isVendorLedgerModalOpen;

        [ObservableProperty]
        private bool _isInventoryLedgerModalOpen;

        [RelayCommand]
        public async Task ViewVendorLedger(VendorBalanceDto vendorBalance)
        {
            SelectedVendorBalance = vendorBalance;
            IsVendorLedgerModalOpen = true;

            var ledgerList = await _vendorService.GetVendorLedgerAsync(vendorBalance.Id);
            VendorLedgerEntries = new ObservableCollection<VendorLedger>(ledgerList);
        }

        [RelayCommand]
        public void CloseVendorLedger()
        {
            IsVendorLedgerModalOpen = false;
        }

        [RelayCommand]
        public async Task ViewCustomerLedger(CustomerBalanceDto customerBalance)
        {
            SelectedCustomerBalance = customerBalance;
            IsCustomerLedgerModalOpen = true;

            var ledgerList = await _customerService.GetCustomerLedgerAsync(customerBalance.Id);
            CustomerLedgerEntries = new ObservableCollection<CustomerLedger>(ledgerList);
        }

        [RelayCommand]
        public void CloseCustomerLedger()
        {
            IsCustomerLedgerModalOpen = false;
        }

        [RelayCommand]
        public async Task ViewInventoryItemLedger(Item item)
        {
            SelectedInventoryItem = item;
            IsInventoryLedgerModalOpen = true;

            var ledgerList = await _inventoryService.GetInventoryLedgerAsync(item.Id);
            InventoryLedgerEntries = new ObservableCollection<InventoryLedger>(ledgerList);
        }

        [RelayCommand]
        public void CloseInventoryLedger()
        {
            IsInventoryLedgerModalOpen = false;
        }

        [RelayCommand]
        public void SwitchSubView(string mode)
        {
            if (mode == "JournalReport")
                ActiveSubViewMode = ReportsSubViewMode.JournalReport;
            else if (mode == "PurchaseSummary")
                ActiveSubViewMode = ReportsSubViewMode.PurchaseSummary;
            else if (mode == "VendorBalances")
                ActiveSubViewMode = ReportsSubViewMode.VendorBalancesList;
            else if (mode == "PosSales")
                ActiveSubViewMode = ReportsSubViewMode.PosSalesReport;
            else if (mode == "ItemWiseProfitLoss")
                ActiveSubViewMode = ReportsSubViewMode.ItemWiseProfitLossReport;
            else if (mode == "CustomerBalances")
                ActiveSubViewMode = ReportsSubViewMode.CustomerBalancesList;
            else if (mode == "InventoryLedger")
                ActiveSubViewMode = ReportsSubViewMode.InventoryLedgerReport;
            else if (mode == "InventoryBalances")
                ActiveSubViewMode = ReportsSubViewMode.InventoryBalancesReport;
            else if (mode == "LowStockAlert")
                ActiveSubViewMode = ReportsSubViewMode.LowStockAlertReport;
            else
                ActiveSubViewMode = ReportsSubViewMode.FinancialReports;
        }
    }

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IRepository<CompanySetting> _settingRepo;
        private readonly IDatabaseSeederAndVerifierService _seederService;
        private readonly IAuthService _authService;
        private readonly IBackupService _backupService;
        private readonly ICustomerService _customerService;
        private readonly IVendorService _vendorService;
        private readonly IInventoryService _inventoryService;
        private readonly ISaleService _saleService;
        private readonly IPurchaseService _purchaseService;
        private readonly IReceiptPaymentService _receiptPaymentService;

        [ObservableProperty]
        private CompanySetting _setting = new();

        [ObservableProperty]
        private string _verificationReportResult = string.Empty;

        [ObservableProperty]
        private string _backupStatusMessage = string.Empty;

        [ObservableProperty]
        private string _currentPassword = string.Empty;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        [ObservableProperty]
        private string _confirmNewPassword = string.Empty;

        [ObservableProperty]
        private string _passwordStatusMessage = string.Empty;

        [ObservableProperty]
        private string _passwordStatusColor = "#16A34A";

        [ObservableProperty]
        private bool _isPasswordVisible;

        public SettingsViewModel(
            IRepository<CompanySetting> settingRepo,
            IDatabaseSeederAndVerifierService seederService,
            IAuthService authService,
            IBackupService backupService,
            ICustomerService customerService,
            IVendorService vendorService,
            IInventoryService inventoryService,
            ISaleService saleService,
            IPurchaseService purchaseService,
            IReceiptPaymentService receiptPaymentService)
        {
            _settingRepo = settingRepo;
            _seederService = seederService;
            _authService = authService;
            _backupService = backupService;
            _customerService = customerService;
            _vendorService = vendorService;
            _inventoryService = inventoryService;
            _saleService = saleService;
            _purchaseService = purchaseService;
            _receiptPaymentService = receiptPaymentService;
        }

        [RelayCommand]
        public async Task LoadSettingsAsync()
        {
            var list = await _settingRepo.GetAllAsync();
            Setting = list.FirstOrDefault() ?? new CompanySetting();
        }

        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            var list = await _settingRepo.GetAllAsync();
            var existing = list.FirstOrDefault();
            if (existing != null)
            {
                Setting.Id = existing.Id;
                await _settingRepo.UpdateAsync(Setting);
            }
            else
            {
                await _settingRepo.AddAsync(Setting);
            }
        }

        [RelayCommand]
        public async Task CreateBackupAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save Full Software Backup",
                    Filter = "Zip Archive (*.zip)|*.zip|Database File (*.db)|*.db|All Files (*.*)|*.*",
                    FileName = $"AlMadinaERP_FullBackup_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip"
                };

                if (dialog.ShowDialog() == true)
                {
                    var targetPath = dialog.FileName;
                    var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP");
                    var tempBackupDir = Path.Combine(Path.GetTempPath(), "AlMadinaBackupTemp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempBackupDir);

                    // Perform WAL checkpoint & create copy of DB
                    var tempDbPath = await _backupService.CreateBackupAsync(tempBackupDir);

                    // Add a ReadMe text file inside the backup folder explaining .db usage
                    var readMePath = Path.Combine(tempBackupDir, "ReadMe_HowToRestore.txt");
                    var readMeContent = $@"===================================================================
AL MADINA BUILDING MATERIAL ERP - SOFTWARE BACKUP FILE
===================================================================
Backup Date: {DateTime.Now:dd-MMM-yyyy HH:mm:ss}

IMPORTANT INFORMATION:
This backup package contains your complete software database.
SQLite database (.db) files are system files used by the ERP software.
You do NOT need to double-click the .db file in Windows.

TO RESTORE THIS BACKUP:
1. Keep this backup zip or .db file safe on your PC or USB drive.
2. In case of computer replacement or disaster recovery, place the database file in:
   %AppData%\AlMadinaERP\Company.db
===================================================================";
                    File.WriteAllText(readMePath, readMeContent, System.Text.Encoding.UTF8);

                    if (targetPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(targetPath)) File.Delete(targetPath);
                        System.IO.Compression.ZipFile.CreateFromDirectory(tempBackupDir, targetPath);
                    }
                    else
                    {
                        File.Copy(tempDbPath, targetPath, overwrite: true);
                    }

                    try { Directory.Delete(tempBackupDir, recursive: true); } catch { }

                    var fileNameOnly = Path.GetFileName(targetPath);
                    BackupStatusMessage = $"✓ Full System Backup created!\nFile Name: {fileNameOnly}\nFull Path: {targetPath}";

                    try { Clipboard.SetText(fileNameOnly); } catch { }

                    MessageBox.Show(
                        $"✓ Complete System Backup created successfully!\n\nFILE NAME:\n{fileNameOnly}\n\nSAVED LOCATION:\n{targetPath}\n\n(The file name '{fileNameOnly}' has been copied to your clipboard!)\n\nNote: Backup zip files contain software database records (.db). You do NOT need to double-click the .db file inside Windows.",
                        "Al Madina ERP - Backup Created",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                BackupStatusMessage = $"Error creating backup: {ex.Message}";
                MessageBox.Show($"Backup Error: {ex.Message}", "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task ExportDataAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Export Software Data for Microsoft Excel",
                    Filter = "Excel CSV (*.csv)|*.csv|All Files (*.*)|*.*",
                    FileName = $"AlMadinaERP_DataExport_{DateTime.Now:yyyy-MM-dd}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new System.Text.StringBuilder();

                    // 1. CUSTOMERS
                    sb.AppendLine("=== CUSTOMERS ===");
                    sb.AppendLine("Customer Code,Customer Name,Phone,Address,Receivable Owes Amount (PKR),Advance Available (PKR)");
                    var customers = await _customerService.SearchCustomersAsync("");
                    var customerBalances = await _customerService.GetCustomerBalancesAsync("");
                    foreach (var c in customers)
                    {
                        var bal = customerBalances.FirstOrDefault(b => b.Id == c.Id);
                        sb.AppendLine($"\"{c.Code}\",\"{c.Name}\",\"{c.Phone}\",\"{c.Address}\",{bal?.CustomerOwes ?? 0:0.00},{bal?.AdvanceAvailable ?? 0:0.00}");
                    }
                    sb.AppendLine();

                    // 2. VENDORS
                    sb.AppendLine("=== VENDORS ===");
                    sb.AppendLine("Vendor Code,Vendor Name,Phone,Address,Payable Owes Amount (PKR),Advance Available (PKR)");
                    var vendors = await _vendorService.SearchVendorsAsync("");
                    var vendorBalances = await _vendorService.GetVendorBalancesAsync("");
                    foreach (var v in vendors)
                    {
                        var bal = vendorBalances.FirstOrDefault(b => b.Id == v.Id);
                        sb.AppendLine($"\"{v.Code}\",\"{v.Name}\",\"{v.Phone}\",\"{v.Address}\",{bal?.VendorOwes ?? 0:0.00},{bal?.AdvanceAvailable ?? 0:0.00}");
                    }
                    sb.AppendLine();

                    // 3. ITEMS / INVENTORY
                    sb.AppendLine("=== ITEMS / INVENTORY ===");
                    sb.AppendLine("Item Code,Item Name,Category,Sale Price (PKR),Purchase Price (PKR),Current Stock,Unit");
                    var items = await _inventoryService.SearchItemsAsync("");
                    foreach (var item in items)
                    {
                        sb.AppendLine($"\"{item.Code}\",\"{item.Name}\",\"{item.CategoryName}\",{item.SalePrice:0.00},{item.PurchasePrice:0.00},{item.CurrentStock:0.##},\"{item.SellingUnit}\"");
                    }
                    sb.AppendLine();

                    // 4. SALE INVOICES
                    sb.AppendLine("=== SALE INVOICES ===");
                    sb.AppendLine("Invoice #,Date,Customer Name,Payment Method,Total Amount (PKR),Paid Amount (PKR),Status");
                    var sales = await _saleService.SearchInvoicesAsync("");
                    foreach (var s in sales)
                    {
                        sb.AppendLine($"\"{s.InvoiceNumber}\",\"{s.Date:yyyy-MM-dd HH:mm}\",\"{s.CustomerName}\",\"{s.PaymentMethod}\",{s.TotalAmount:0.00},{s.PaidAmount:0.00},\"{s.Status}\"");
                    }
                    sb.AppendLine();

                    // 5. PURCHASE INVOICES
                    sb.AppendLine("=== PURCHASE INVOICES ===");
                    sb.AppendLine("Purchase #,Date,Vendor Name,Payment Method,Total Amount (PKR),Status");
                    var purchases = await _purchaseService.SearchPurchasesAsync("");
                    foreach (var p in purchases)
                    {
                        sb.AppendLine($"\"{p.PurchaseNumber}\",\"{p.Date:yyyy-MM-dd HH:mm}\",\"{p.VendorName}\",\"{p.PaymentMethod}\",{p.TotalAmount:0.00},\"{p.Status}\"");
                    }
                    sb.AppendLine();

                    // 6. CASH / BANK RECEIPTS
                    sb.AppendLine("=== CASH & BANK RECEIPTS ===");
                    sb.AppendLine("Receipt #,Date,Customer Name,Payment Method,Received By / Bank,Amount (PKR),Status");
                    var receipts = await _receiptPaymentService.SearchReceiptsAsync("");
                    foreach (var r in receipts)
                    {
                        sb.AppendLine($"\"{r.ReceiptNumber}\",\"{r.Date:yyyy-MM-dd HH:mm}\",\"{r.CustomerName}\",\"{r.PaymentMethod}\",\"{r.ReceivedBy}\",{r.Amount:0.00},\"{r.Status}\"");
                    }
                    sb.AppendLine();

                    // 7. CASH & BANK PAYMENTS
                    sb.AppendLine("=== CASH & BANK PAYMENTS ===");
                    sb.AppendLine("Payment #,Date,Party / Vendor,Payment Method,Paid From / Bank,Amount (PKR),Status");
                    var payments = await _receiptPaymentService.SearchPaymentsAsync("");
                    foreach (var p in payments)
                    {
                        sb.AppendLine($"\"{p.PaymentNumber}\",\"{p.Date:yyyy-MM-dd HH:mm}\",\"{p.PartyName}\",\"{p.PaymentMethod}\",\"{p.PaidFrom}\",{p.Amount:0.00},\"{p.Status}\"");
                    }
                    sb.AppendLine();

                    // 8. EXPENSES
                    sb.AppendLine("=== EXPENSES ===");
                    sb.AppendLine("Voucher #,Date,Category,Title / Details,Amount (PKR),Status");
                    var expenses = await _receiptPaymentService.GetExpensesAsync();
                    foreach (var e in expenses)
                    {
                        sb.AppendLine($"\"{e.VoucherNumber}\",\"{e.Date:yyyy-MM-dd HH:mm}\",\"{e.Category}\",\"{e.Title}\",{e.Amount:0.00},\"{e.Status}\"");
                    }

                    // Use UTF-8 with BOM for 100% native Microsoft Excel compatibility
                    File.WriteAllText(dialog.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));

                    var fileNameOnly = Path.GetFileName(dialog.FileName);
                    BackupStatusMessage = $"✓ Data exported successfully!\nFile Name: {fileNameOnly}\nFull Path: {dialog.FileName}";

                    try { Clipboard.SetText(fileNameOnly); } catch { }

                    MessageBox.Show(
                        $"✓ Data exported successfully into Excel CSV format!\n\nFILE NAME:\n{fileNameOnly}\n\nSAVED LOCATION:\n{dialog.FileName}\n\n(Double-click this file to open it directly in Microsoft Excel! The file name '{fileNameOnly}' has been copied to your clipboard!)",
                        "Al Madina ERP - Data Exported",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                BackupStatusMessage = $"Error exporting data: {ex.Message}";
                MessageBox.Show($"Export Error: {ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task UpdatePasswordAsync()
        {
            PasswordStatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                PasswordStatusMessage = "Current Password is required.";
                PasswordStatusColor = "#DC2626";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                PasswordStatusMessage = "New Password cannot be empty.";
                PasswordStatusColor = "#DC2626";
                return;
            }

            if (NewPassword != ConfirmNewPassword)
            {
                PasswordStatusMessage = "New Password and Confirm New Password do not match.";
                PasswordStatusColor = "#DC2626";
                return;
            }

            var userId = _authService.CurrentUser?.Id ?? 1;
            var success = await _authService.ChangePasswordAsync(userId, CurrentPassword.Trim(), NewPassword.Trim());

            if (success)
            {
                PasswordStatusMessage = "✓ Administrator Password updated successfully!";
                PasswordStatusColor = "#16A34A";
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmNewPassword = string.Empty;
            }
            else
            {
                PasswordStatusMessage = "Current Password is incorrect. Password update failed.";
                PasswordStatusColor = "#DC2626";
            }
        }

        [RelayCommand]
        public void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        [RelayCommand]
        public async Task SeedDemoDataAndVerifyAllAsync()
        {
            VerificationReportResult = "Running Automated 23-Step Database Seeding & Verification...";
            VerificationReportResult = await _seederService.SeedDemoDataAndVerifyAllAsync();
        }
    }
}
