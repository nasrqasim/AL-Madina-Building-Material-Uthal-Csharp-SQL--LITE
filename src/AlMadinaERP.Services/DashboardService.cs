using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Data;

namespace AlMadinaERP.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            try
            {
                var today = DateTime.Today;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                // Customer Receivables
                var custs = await _context.Customers.Where(c => c.IsActive).Select(c => c.OwesAmount).ToListAsync();
                var customerReceivables = custs.Sum();

                // Vendor Payables
                var vends = await _context.Vendors.Where(v => v.IsActive).Select(v => v.OwesAmount).ToListAsync();
                var vendorPayables = vends.Sum();

                // Inventory Value
                var itemValList = await _context.Items.Where(i => i.IsActive).Select(i => i.CurrentStock * i.PurchasePrice).ToListAsync();
                var inventoryValue = itemValList.Sum();

                // Cash & Bank Calculation
                var salesList = await _context.SaleInvoices.Select(s => new { s.IsCashSale, s.Type, s.TotalAmount, s.Date }).ToListAsync();
                var purchasesList = await _context.PurchaseInvoices.Select(p => new { p.IsCashPurchase, p.Type, p.TotalAmount, p.Date }).ToListAsync();
                var receiptsList = await _context.Receipts.Select(r => new { r.PaymentMethod, r.Amount, r.Date }).ToListAsync();
                var paymentsList = await _context.Payments.Select(p => new { p.Amount, p.Date }).ToListAsync();
                var banksList = await _context.Banks.Where(b => b.IsActive).Select(b => b.CurrentBalance).ToListAsync();
                var expensesList = await _context.Expenses.Select(e => new { e.Amount, e.Date }).ToListAsync();

                var cashSales = salesList.Where(s => s.IsCashSale && s.Type != InvoiceType.SaleReturn).Sum(s => s.TotalAmount);
                var cashReceipts = receiptsList.Where(r => r.PaymentMethod == PaymentMethod.Cash || r.PaymentMethod == PaymentMethod.Bank).Sum(r => r.Amount);
                var bankBalances = banksList.Sum();

                var cashPurchases = purchasesList.Where(p => p.IsCashPurchase && p.Type != PurchaseType.PurchaseReturn).Sum(p => p.TotalAmount);
                var cashPayments = paymentsList.Sum(p => p.Amount);
                var saleReturns = salesList.Where(s => s.Type == InvoiceType.SaleReturn).Sum(s => s.TotalAmount);
                var expenses = expensesList.Sum(e => e.Amount);

                var cashAndBanks = (cashSales + cashReceipts + bankBalances) - (cashPurchases + cashPayments + saleReturns + expenses);

                // Sales Today & Today's Cash Movements
                var salesToday = salesList.Where(s => s.Date >= today && s.Type != InvoiceType.SaleReturn).Sum(s => s.TotalAmount);
                var cashReceivedToday = receiptsList.Where(r => r.Date >= today).Sum(r => r.Amount);

                var purchasesToday = purchasesList.Where(p => p.Date >= today && p.Type != PurchaseType.PurchaseReturn).Sum(p => p.TotalAmount);
                var cashPaidToday = paymentsList.Where(p => p.Date >= today).Sum(p => p.Amount);

                // Monthly Sales & Purchases
                var monthlySales = salesList.Where(s => s.Date >= startOfMonth && s.Type != InvoiceType.SaleReturn).Sum(s => s.TotalAmount);
                var monthlyPurchases = purchasesList.Where(p => p.Date >= startOfMonth && p.Type != PurchaseType.PurchaseReturn).Sum(p => p.TotalAmount);

                // Monthly Net Profit
                var monthlyCOGSList = await _context.SaleInvoiceItems
                    .Where(si => si.SaleInvoice != null && si.SaleInvoice.Date >= startOfMonth && si.SaleInvoice.Type != InvoiceType.SaleReturn)
                    .Select(si => si.Quantity * si.Rate)
                    .ToListAsync();
                var monthlyCOGS = monthlyCOGSList.Sum();
                var monthlyExpenses = expensesList.Where(e => e.Date >= startOfMonth).Sum(e => e.Amount);

                var netProfit = monthlySales - monthlyCOGS - monthlyExpenses;

                return new DashboardSummaryDto
                {
                    CashAndBanks = cashAndBanks,
                    CustomerReceivables = customerReceivables,
                    VendorPayables = vendorPayables,
                    InventoryValue = inventoryValue,
                    SalesToday = salesToday,
                    PurchasesToday = purchasesToday,
                    MonthlySales = monthlySales,
                    MonthlyPurchases = monthlyPurchases,
                    NetProfit = netProfit,

                    OpeningCashBank = 0m,
                    CashReceivedToday = cashReceivedToday,
                    CashPaidToday = cashPaidToday,
                    CurrentCashBankBalance = cashAndBanks,

                    OpeningReceivables = 0m,
                    ReceivedToday = cashReceivedToday,
                    TotalCustomerReceivables = customerReceivables,

                    OpeningPayables = 0m,
                    PaidToday = cashPaidToday,
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
