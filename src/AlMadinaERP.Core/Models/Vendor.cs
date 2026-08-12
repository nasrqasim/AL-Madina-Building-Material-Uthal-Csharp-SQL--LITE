using System;

namespace AlMadinaERP.Core.Models
{
    public class Vendor
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string VendorType { get; set; } = "Supplier";
        public string Phone { get; set; } = string.Empty;
        public string NTN { get; set; } = string.Empty;
        public string GSTNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Region { get; set; } = "Select Region";
        public string Area { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "Pakistan";
        
        public decimal CreditLimit { get; set; } = 0m;
        public int CreditDays { get; set; } = 30;

        // Vendor Owes: balance business owes to vendor for credit purchases
        public decimal OwesAmount { get; set; } = 0m;
        
        // Advance Available: advance paid to vendor
        public decimal AdvanceAvailable { get; set; } = 0m;

        public string BankName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankBranch { get; set; } = string.Empty;
        public bool DeductWithholdingTax { get; set; } = false;

        public string Notes { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
