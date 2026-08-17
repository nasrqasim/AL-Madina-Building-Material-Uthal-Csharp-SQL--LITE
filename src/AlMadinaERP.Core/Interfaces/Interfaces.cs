using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Models;

namespace AlMadinaERP.Core.Interfaces
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<List<T>> GetAllAsync();
        Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    }

    public interface ICustomerService
    {
        Task<List<Customer>> SearchCustomersAsync(string query);
        Task<Customer?> GetCustomerByIdAsync(int id);
        Task<Customer> SaveCustomerAsync(Customer customer);
        Task DeleteCustomerAsync(int id);
        Task<List<CustomerBalanceDto>> GetCustomerBalancesAsync(string query = "");
        Task<List<CustomerLedger>> GetCustomerLedgerAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<string> GetNextCustomerCodeAsync();
        Task<List<CustomerPurchasedItemDto>> GetCustomerPurchasedItemsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<PaymentHistoryDto>> GetCustomerReceiptsAndPaymentsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<OutstandingInvoiceDto>> GetCustomerOutstandingInvoicesAsync(int customerId);
    }

    public interface IVendorService
    {
        Task<List<Vendor>> SearchVendorsAsync(string query);
        Task<Vendor?> GetVendorByIdAsync(int id);
        Task<Vendor> SaveVendorAsync(Vendor vendor);
        Task DeleteVendorAsync(int id);
        Task<List<VendorBalanceDto>> GetVendorBalancesAsync(string query = "");
        Task<List<VendorLedger>> GetVendorLedgerAsync(int vendorId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<string> GetNextVendorCodeAsync();
        Task<List<VendorPurchasedItemDto>> GetVendorPurchasedItemsAsync(int vendorId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<PaymentHistoryDto>> GetVendorReceiptsAndPaymentsAsync(int vendorId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<OutstandingInvoiceDto>> GetVendorOutstandingInvoicesAsync(int vendorId);
    }

    public interface IInventoryService
    {
        Task<List<Item>> SearchItemsAsync(string query, int? categoryId = null);
        Task<Item?> GetItemByIdAsync(int id);
        Task<Item> SaveItemAsync(Item item);
        Task DeleteItemAsync(int id);
        Task<List<Category>> GetCategoriesAsync();
        Task<Category> SaveCategoryAsync(Category category);
        Task<List<Subcategory>> GetSubcategoriesAsync(int? categoryId = null);
        Task<Subcategory> SaveSubcategoryAsync(Subcategory subcategory);
        Task<List<Unit>> GetUnitsAsync();
        Task<Unit> SaveUnitAsync(Unit unit);
        Task<List<LowStockItemDto>> GetLowStockAlertsAsync();
        Task<List<InventoryLedger>> GetInventoryLedgerAsync(int itemId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<InventoryLedger>> GetAllInventoryLedgerAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }

    public interface ISaleService
    {
        Task<SaleInvoice> CreateSaleInvoiceAsync(SaleInvoice invoice);
        Task<SaleInvoice> SaveSaleInvoiceAsync(SaleInvoice invoice);
        Task<SaleInvoice?> GetSaleInvoiceByIdAsync(int id);
        Task<List<SaleInvoice>> SearchInvoicesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);
        Task DeleteSaleInvoiceAsync(int id);
        Task<string> GenerateNextInvoiceNumberAsync();
    }

    public interface IPurchaseService
    {
        Task<PurchaseInvoice> CreatePurchaseInvoiceAsync(PurchaseInvoice invoice);
        Task<PurchaseInvoice> SavePurchaseInvoiceAsync(PurchaseInvoice invoice);
        Task<PurchaseInvoice?> GetPurchaseInvoiceByIdAsync(int id);
        Task<List<PurchaseInvoice>> SearchPurchasesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);
        Task DeletePurchaseInvoiceAsync(int id);
        Task<string> GenerateNextPurchaseNumberAsync();
    }

    public interface IReceiptPaymentService
    {
        Task<Receipt> ProcessReceiptAsync(Receipt receipt);
        Task<Payment> ProcessPaymentAsync(Payment payment);
        Task<List<Receipt>> SearchReceiptsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<Payment>> SearchPaymentsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);
        Task DeleteReceiptAsync(int id);
        Task DeletePaymentAsync(int id);
        Task<List<Bank>> GetBanksAsync();
        Task<Bank> SaveBankAsync(Bank bank);
        Task DeleteBankAsync(int id);
        Task<List<Expense>> SearchExpensesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<Expense>> GetExpensesAsync();
        Task<Expense> SaveExpenseAsync(Expense expense);
        Task<Expense> ProcessExpenseAsync(Expense expense);
        Task DeleteExpenseAsync(int id);
    }

    public interface ISalaryService
    {
        Task<Salary> ProcessSalaryAsync(Salary salary);
        Task<List<Salary>> GetSalariesAsync(string staffName = "", string salaryMonth = "");
        Task<List<Staff>> GetStaffsAsync(string query = "");
        Task<Staff> SaveStaffAsync(Staff staff);
        Task DeleteStaffAsync(int id);
        Task<List<SalaryAdvance>> GetSalaryAdvancesAsync(string query = "");
        Task<SalaryAdvance> SaveSalaryAdvanceAsync(SalaryAdvance advance);
        Task DeleteSalaryAdvanceAsync(int id);
        Task<List<JournalEntry>> GetJournalEntriesAsync(string query = "");
        Task<JournalEntry> SaveJournalEntryAsync(JournalEntry entry);
        Task DeleteJournalEntryAsync(int id);
    }

    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetDashboardSummaryAsync();
    }

    public interface IReportService
    {
        Task<ProfitLossReportDto> GetProfitLossReportAsync(DateTime startDate, DateTime endDate);
        Task<BalanceSheetReportDto> GetBalanceSheetReportAsync(DateTime asOfDate);
        Task<List<ItemProfitLossDto>> GetItemWiseProfitLossAsync(DateTime startDate, DateTime endDate);
    }

    public interface IPrintService
    {
        void PrintThermalReceipt(SaleInvoice invoice, CompanySetting company);
        void PrintA4SaleInvoice(SaleInvoice invoice, CompanySetting company);
        void PrintA4PurchaseInvoice(PurchaseInvoice invoice, CompanySetting company);
        void PrintReceiptVoucher(Receipt receipt, CompanySetting company);
        void PrintPaymentVoucher(Payment payment, CompanySetting company);

        void PrintCustomerLedger(CustomerBalanceDto customer, IEnumerable<CustomerLedger> entries, CompanySetting company);
        void PrintVendorLedger(VendorBalanceDto vendor, IEnumerable<VendorLedger> entries, CompanySetting company);
        void PrintInventoryLedger(Item item, IEnumerable<InventoryLedger> entries, CompanySetting company);
        void PrintStaffLedger(Staff staff, IEnumerable<SalaryLedgerRowDto> entries, CompanySetting company);
        void PrintSalaryStaffRegister(IEnumerable<Staff> staffs, CompanySetting company);
        void PrintReportTable(string title, IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows, IEnumerable<string>? totalsRow, CompanySetting company);
        void PrintCustomerOrder(CustomerOrder order, CompanySetting company);
    }

    public interface ICustomerOrderService
    {
        Task<List<CustomerOrder>> GetCustomerOrdersAsync(string searchQuery = "", string statusFilter = "All");
        Task<CustomerOrder?> GetCustomerOrderByIdAsync(int id);
        Task<CustomerOrder> SaveCustomerOrderAsync(CustomerOrder order);
        Task DeleteCustomerOrderAsync(int id);
        Task<string> GenerateNextOrderNumberAsync();
        Task<CustomerOrder?> ToggleOrderStatusAsync(int id);
    }

    public interface IBackupService
    {
        Task<string> CreateBackupAsync(string targetFolderPath);
        Task RestoreBackupAsync(string backupFilePath);
        Task PerformAutoBackupIfEnabledAsync(CompanySetting setting);
    }

    public interface IAuthService
    {
        User? CurrentUser { get; }
        Task<User?> AuthenticateAsync(string username, string password);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task EnsureSuperadminExistsAsync();
        void Logout();
    }

    public interface IDatabaseSeederAndVerifierService
    {
        Task<string> SeedDemoDataAndVerifyAllAsync();
    }
}
