using System;

namespace AlMadinaERP.Core.Models
{
    public class CustomerLedger
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
        
        public DateTime Date { get; set; } = DateTime.Now;
        public string TransactionType { get; set; } = string.Empty; // SaleInvoice, SaleReturn, CashReceipt, BankReceipt, AdvanceUsed, etc.
        public string VoucherNumber { get; set; } = string.Empty;
        
        public decimal Debit { get; set; }  // Increases balance (e.g. Sale Invoice)
        public decimal Credit { get; set; } // Decreases balance (e.g. Receipt, Return, Advance Used)
        public decimal RunningBalance { get; set; }
        
        public string Remarks { get; set; } = string.Empty;
        public int? SaleInvoiceId { get; set; }
        public int? ReceiptId { get; set; }
    }

    public class VendorLedger
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public Vendor? Vendor { get; set; }
        
        public DateTime Date { get; set; } = DateTime.Now;
        public string TransactionType { get; set; } = string.Empty; // PurchaseInvoice, PurchaseReturn, CashPayment, BankPayment, AdvanceUsed, etc.
        public string VoucherNumber { get; set; } = string.Empty;
        
        public decimal Debit { get; set; }  // Decreases payable (e.g. Payment, Return)
        public decimal Credit { get; set; } // Increases payable (e.g. Purchase Invoice)
        public decimal RunningBalance { get; set; }
        
        public string Remarks { get; set; } = string.Empty;
        public int? PurchaseInvoiceId { get; set; }
        public int? PaymentId { get; set; }
    }

    public class InventoryLedger
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public Item? Item { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        
        public DateTime Date { get; set; } = DateTime.Now;
        public string VoucherNumber { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty; // PurchaseInvoice, PurchaseReturn, SaleInvoice, SaleReturn, Adjustment
        
        public string Unit { get; set; } = string.Empty;
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public decimal RunningBalance { get; set; }
        
        public string Warehouse { get; set; } = "Godown A";
        public string User { get; set; } = "Admin";
        public string Reference { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;

        public int? PurchaseInvoiceId { get; set; }
        public int? SaleInvoiceId { get; set; }
    }
}
