using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Data;

namespace AlMadinaERP.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ProfitLossReportDto> GetProfitLossReportAsync(DateTime startDate, DateTime endDate)
        {
            var salesQuery = _context.SaleInvoices.Where(s => s.Date >= startDate && s.Date <= endDate);

            var grossSales = (decimal)(await salesQuery
                .Where(s => s.Type != InvoiceType.SaleReturn)
                .SumAsync(s => (double?)s.Subtotal) ?? 0);

            var discounts = (decimal)(await salesQuery
                .SumAsync(s => (double?)s.DiscountAmount) ?? 0);

            var returns = (decimal)(await salesQuery
                .Where(s => s.Type == InvoiceType.SaleReturn)
                .SumAsync(s => (double?)s.TotalAmount) ?? 0);

            var netSales = grossSales - discounts - returns;

            var cogs = (decimal)(await _context.SaleInvoiceItems
                .Where(si => si.SaleInvoice != null && si.SaleInvoice.Date >= startDate && si.SaleInvoice.Date <= endDate && si.SaleInvoice.Type != InvoiceType.SaleReturn)
                .SumAsync(si => (double?)(double)(si.Quantity * (si.Item != null ? si.Item.PurchasePrice : 0))) ?? 0);

            var grossProfit = netSales - cogs;

            var expenses = (decimal)(await _context.Expenses
                .Where(e => e.Date >= startDate && e.Date <= endDate)
                .SumAsync(e => (double?)e.Amount) ?? 0);

            var netProfit = grossProfit - expenses;

            return new ProfitLossReportDto
            {
                StartDate = startDate,
                EndDate = endDate,
                GrossSales = grossSales,
                SalesDiscounts = discounts,
                NetSales = netSales,
                CostOfGoodsSold = cogs,
                GrossProfit = grossProfit,
                TotalExpenses = expenses,
                NetProfit = netProfit
            };
        }

        public async Task<BalanceSheetReportDto> GetBalanceSheetReportAsync(DateTime asOfDate)
        {
            var bankBalances = (decimal)(await _context.Banks
                .Where(b => b.IsActive)
                .SumAsync(b => (double?)b.CurrentBalance) ?? 0);

            var receivables = (decimal)(await _context.Customers
                .Where(c => c.IsActive)
                .SumAsync(c => (double?)c.OwesAmount) ?? 0);

            var inventoryVal = (decimal)(await _context.Items
                .Where(i => i.IsActive)
                .SumAsync(i => (double?)(double)(i.CurrentStock * i.PurchasePrice)) ?? 0);

            var totalAssets = bankBalances + receivables + inventoryVal;

            var payables = (decimal)(await _context.Vendors
                .Where(v => v.IsActive)
                .SumAsync(v => (double?)v.OwesAmount) ?? 0);

            var equity = totalAssets - payables;

            return new BalanceSheetReportDto
            {
                AsOfDate = asOfDate,
                CashAndBankBalance = bankBalances,
                AccountsReceivable = receivables,
                InventoryAssetValue = inventoryVal,
                TotalCurrentAssets = totalAssets,
                AccountsPayable = payables,
                TotalLiabilities = payables,
                EquityAndRetainedEarnings = equity
            };
        }

        public async Task<List<ItemProfitLossDto>> GetItemWiseProfitLossAsync(DateTime startDate, DateTime endDate)
        {
            var items = await _context.SaleInvoiceItems
                .Include(si => si.SaleInvoice)
                .Include(si => si.Item)
                .Where(si => si.SaleInvoice != null && si.SaleInvoice.Date >= startDate && si.SaleInvoice.Date <= endDate)
                .ToListAsync();

            var grouped = items.GroupBy(si => si.ItemId).Select(g =>
            {
                var first = g.First();
                var itemObj = first.Item;
                var totalQty = g.Sum(x => x.SaleInvoice?.Type == InvoiceType.SaleReturn ? -x.Quantity : x.Quantity);
                var totalSale = g.Sum(x => x.SaleInvoice?.Type == InvoiceType.SaleReturn ? -x.TotalPrice : x.TotalPrice);
                var costPrice = itemObj != null ? itemObj.PurchasePrice : 0m;
                var totalCost = totalQty * costPrice;

                return new ItemProfitLossDto
                {
                    ItemId = g.Key,
                    Code = itemObj?.Code ?? "N/A",
                    Name = itemObj?.Name ?? first.ItemName,
                    QuantitySold = totalQty,
                    Unit = first.UnitName,
                    TotalSaleAmount = totalSale,
                    TotalCostAmount = totalCost,
                    ProfitOrLoss = totalSale - totalCost
                };
            }).ToList();

            return grouped;
        }
    }
}
