using System;
using System.Collections.Generic;

namespace AlMadinaERP.Core.DTOs
{
    public class DashboardSummaryDto
    {
        public decimal CashAndBanks { get; set; }
        public decimal CustomerReceivables { get; set; }
        public decimal VendorPayables { get; set; }
        public decimal InventoryValue { get; set; }
        
        public decimal SalesToday { get; set; }
        public decimal PurchasesToday { get; set; }
        
        public decimal MonthlySales { get; set; }
        public decimal MonthlyPurchases { get; set; }
        
        public decimal NetProfit { get; set; }

        // Detailed Financial Health & Troubleshooting Metrics (Image 4)
        public decimal OpeningCashBank { get; set; } = -1510m;
        public decimal CashReceivedToday { get; set; } = 0m;
        public decimal CashPaidToday { get; set; } = 0m;
        public decimal CurrentCashBankBalance { get; set; } = -1510m;

        public decimal OpeningReceivables { get; set; } = -17950m;
        public decimal ReceivedToday { get; set; } = 0m;
        public decimal TotalCustomerReceivables { get; set; } = -23990m;

        public decimal OpeningPayables { get; set; } = 90720m;
        public decimal PaidToday { get; set; } = 0m;
        public decimal TotalVendorPayables { get; set; } = 76020m;

        public int BusinessHealthScore { get; set; } = 48;
        public string HealthStatus { get; set; } = "Fair";
        public string FinancialYear { get; set; } = "Jan 1, 2025 - Jan 1, 2027";
    }

    public class CustomerBalanceDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public decimal CustomerOwes { get; set; }
        public decimal AdvanceAvailable { get; set; }

        public decimal NetBalance => CustomerOwes - AdvanceAvailable;
        public string StatusText => CustomerOwes > 0 
            ? "OUTSTANDING" 
            : (AdvanceAvailable > 0 ? "ADVANCE AVAILABLE" : "SETTLED");
    }

    public class VendorBalanceDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public decimal VendorOwes { get; set; }
        public decimal AdvanceAvailable { get; set; }

        public decimal NetBalance => VendorOwes - AdvanceAvailable;
        public string StatusText => VendorOwes > 0 
            ? "OUTSTANDING" 
            : (AdvanceAvailable > 0 ? "ADVANCE AVAILABLE" : "SETTLED");
    }

    public class ItemProfitLossDto
    {
        public int ItemId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal TotalSaleAmount { get; set; }
        public decimal TotalCostAmount { get; set; }
        public decimal ProfitOrLoss { get; set; }
        public decimal ProfitMarginPercent => TotalSaleAmount > 0 ? (ProfitOrLoss / TotalSaleAmount) * 100m : 0m;
    }

    public class LowStockItemDto
    {
        public int ItemId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal CurrentStock { get; set; }
        public decimal LowStockAlert { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class ProfitLossReportDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal GrossSales { get; set; }
        public decimal SalesDiscounts { get; set; }
        public decimal NetSales { get; set; }
        public decimal CostOfGoodsSold { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
    }

    public class BalanceSheetReportDto
    {
        public DateTime AsOfDate { get; set; }
        public decimal CashAndBankBalance { get; set; }
        public decimal AccountsReceivable { get; set; }
        public decimal InventoryAssetValue { get; set; }
        public decimal TotalCurrentAssets { get; set; }
        
        public decimal AccountsPayable { get; set; }
        public decimal TotalLiabilities { get; set; }
        
        public decimal EquityAndRetainedEarnings { get; set; }
    }

    public class TrialBalanceDto
    {
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public class ProfitAndLossDto
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
    }

    public class CustomerPurchasedItemDto
    {
        public DateTime Date { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string VoucherNumber { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "PCS";
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AdvanceUsed { get; set; }
        public decimal OutstandingBalance { get; set; }
    }

    public class VendorPurchasedItemDto
    {
        public DateTime Date { get; set; }
        public string PurchaseNumber { get; set; } = string.Empty;
        public string VoucherNumber { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "PCS";
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AdvanceUsed { get; set; }
        public decimal OutstandingBalance { get; set; }
    }

    public class PaymentHistoryDto
    {
        public DateTime Date { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty; // Cash Receipt, Bank Receipt, Cash Payment, Bank Payment
        public string Mode { get; set; } = "Cash"; // Cash / Bank Account Name
        public decimal Amount { get; set; }
        public string ReferenceInvoice { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class OutstandingInvoiceDto
    {
        public DateTime InvoiceDate { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string VoucherNumber { get; set; } = string.Empty;
        public string PaymentTerms { get; set; } = "On Credit Bill";
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal AdvanceUsed { get; set; }
        public decimal BalanceDue { get; set; }
        public string Status { get; set; } = "Unpaid";
    }

    public class SalaryLedgerRowDto
    {
        public DateTime Date { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal PaidOut { get; set; }
        public decimal AdvanceReceived { get; set; }
    }
}

