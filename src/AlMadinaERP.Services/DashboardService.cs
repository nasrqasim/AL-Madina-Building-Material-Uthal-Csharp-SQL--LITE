using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Data;

using Microsoft.Extensions.DependencyInjection;

namespace AlMadinaERP.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public DashboardService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            using var _context = CreateContext();
            try
            {
                var today = DateTime.Today;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                // Pure SQL Database Aggregations for 100,000+ Scalability (Zero RAM allocation)
                var customerReceivables = (decimal)(await _context.Customers
                    .Where(c => c.IsActive)
                    .AsNoTracking()
                    .SumAsync(c => (double?)c.OwesAmount) ?? 0);

                var vendorPayables = (decimal)(await _context.Vendors
                    .Where(v => v.IsActive)
                    .AsNoTracking()
                    .SumAsync(v => (double?)v.OwesAmount) ?? 0);

                var inventoryValue = (decimal)(await _context.Items
                    .Where(i => i.IsActive)
                    .AsNoTracking()
                    .SumAsync(i => (double?)(double)(i.CurrentStock * i.PurchasePrice)) ?? 0);

                var cashSales = (decimal)(await _context.SaleInvoices
                    .Where(s => s.Type != InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(s => (double?)s.PaidAmount) ?? 0);

                var saleReturns = (decimal)(await _context.SaleInvoices
                    .Where(s => s.Type == InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(s => (double?)s.AmountRefunded) ?? 0);

                var cashPurchases = (decimal)(await _context.PurchaseInvoices
                    .Where(p => p.Type != PurchaseType.PurchaseReturn)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.AmountPaid) ?? 0);

                var cashReceipts = (decimal)(await _context.Receipts
                    .Where(r => r.PaymentMethod == PaymentMethod.Cash)
                    .AsNoTracking()
                    .SumAsync(r => (double?)r.Amount) ?? 0);

                var bankBalances = (decimal)(await _context.Banks
                    .Where(b => b.IsActive)
                    .AsNoTracking()
                    .SumAsync(b => (double?)b.CurrentBalance) ?? 0);

                var cashPayments = (decimal)(await _context.Payments
                    .Where(p => p.PaymentMethod == PaymentMethod.Cash)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.Amount) ?? 0);

                var expenses = (decimal)(await _context.Expenses
                    .Where(e => e.PaymentMethod == PaymentMethod.Cash)
                    .AsNoTracking()
                    .SumAsync(e => (double?)e.Amount) ?? 0);

                var cashAndBanks = (cashSales + cashReceipts + bankBalances) - (cashPurchases + cashPayments + saleReturns + expenses);

                // Sales Today & Purchases Today (Live Outstanding portion)
                var outstandingSalesToday = (decimal)(await _context.SaleInvoices
                    .Where(s => s.Date >= today && s.Type != InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(s => (double?)s.OutstandingAmount) ?? 0);

                var outstandingPurchasesToday = (decimal)(await _context.PurchaseInvoices
                    .Where(p => p.Date >= today && p.Type != PurchaseType.PurchaseReturn)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.OutstandingAmount) ?? 0);

                // Collections Today (Card 1: CashReceivedToday)
                var receiptsReceivedToday = (decimal)(await _context.Receipts
                    .Where(r => r.Date >= today)
                    .AsNoTracking()
                    .SumAsync(r => (double?)r.Amount) ?? 0);

                var salesPaidToday = (decimal)(await _context.SaleInvoices
                    .Where(s => s.Date >= today && s.Type != InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(s => (double?)s.PaidAmount) ?? 0);

                var cashReceivedToday = receiptsReceivedToday + salesPaidToday;

                // Disbursements Today (Card 1: CashPaidToday)
                var paymentsPaidToday = (decimal)(await _context.Payments
                    .Where(p => p.Date >= today)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.Amount) ?? 0);

                var purchasesPaidToday = (decimal)(await _context.PurchaseInvoices
                    .Where(p => p.Date >= today && p.Type != PurchaseType.PurchaseReturn)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.AmountPaid) ?? 0);

                var expensesToday = (decimal)(await _context.Expenses
                    .Where(e => e.Date >= today)
                    .AsNoTracking()
                    .SumAsync(e => (double?)e.Amount) ?? 0);

                var returnsRefundedToday = (decimal)(await _context.SaleInvoices
                    .Where(s => s.Date >= today && s.Type == InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(s => (double?)s.AmountRefunded) ?? 0);

                var cashPaidToday = paymentsPaidToday + purchasesPaidToday + expensesToday + returnsRefundedToday;

                // Customer Collections Today (Card 2: ReceivedToday)
                var customerReceiptsToday = (decimal)(await _context.Receipts
                    .Where(r => r.Date >= today && r.CustomerId.HasValue && r.CustomerId.Value > 0)
                    .AsNoTracking()
                    .SumAsync(r => (double?)r.Amount) ?? 0);

                // Vendor Payments Today (Card 3: PaidToday)
                var vendorPaymentsToday = (decimal)(await _context.Payments
                    .Where(p => p.Date >= today && p.VendorId.HasValue && p.VendorId.Value > 0)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.Amount) ?? 0);

                // Monthly Sales & Purchases
                var monthlySales = (decimal)(await _context.SaleInvoices
                    .Where(s => s.Date >= startOfMonth && s.Type != InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(s => (double?)s.TotalAmount) ?? 0);

                var monthlyPurchases = (decimal)(await _context.PurchaseInvoices
                    .Where(p => p.Date >= startOfMonth && p.Type != PurchaseType.PurchaseReturn)
                    .AsNoTracking()
                    .SumAsync(p => (double?)p.TotalAmount) ?? 0);

                // Monthly Net Profit
                var monthlyCOGS = (decimal)(await _context.SaleInvoiceItems
                    .Where(si => si.SaleInvoice != null && si.SaleInvoice.Date >= startOfMonth && si.SaleInvoice.Type != InvoiceType.SaleReturn)
                    .AsNoTracking()
                    .SumAsync(si => (double?)(double)(si.Quantity * si.Rate)) ?? 0);

                var monthlyExpenses = (decimal)(await _context.Expenses
                    .Where(e => e.Date >= startOfMonth)
                    .AsNoTracking()
                    .SumAsync(e => (double?)e.Amount) ?? 0);

                var netProfit = monthlySales - monthlyCOGS - monthlyExpenses;

                return new DashboardSummaryDto
                {
                    CashAndBanks = cashAndBanks,
                    CustomerReceivables = customerReceivables,
                    VendorPayables = vendorPayables,
                    InventoryValue = inventoryValue,
                    SalesToday = outstandingSalesToday,
                    PurchasesToday = outstandingPurchasesToday,
                    MonthlySales = monthlySales,
                    MonthlyPurchases = monthlyPurchases,
                    NetProfit = netProfit,

                    OpeningCashBank = 0m,
                    CashReceivedToday = cashReceivedToday,
                    CashPaidToday = cashPaidToday,
                    CurrentCashBankBalance = cashAndBanks,
                    SalesTodayCash = salesPaidToday,
                    ReceiptsReceivedToday = receiptsReceivedToday,

                    OpeningReceivables = 0m,
                    ReceivedToday = customerReceiptsToday,
                    TotalCustomerReceivables = customerReceivables,

                    OpeningPayables = 0m,
                    PaidToday = vendorPaymentsToday,
                    TotalVendorPayables = vendorPayables,

                    BusinessHealthScore = (customerReceivables >= vendorPayables) ? 85 : 65,
                    HealthStatus = (customerReceivables >= vendorPayables) ? "Good" : "Fair",
                    FinancialYear = $"{DateTime.Today.Year}-01-01 to {DateTime.Today.Year}-12-31"
                };
            }
            catch
            {
                return new DashboardSummaryDto();
            }
        }
    }
}
