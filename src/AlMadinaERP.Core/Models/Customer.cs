using System;

namespace AlMadinaERP.Core.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Category { get; set; } = "Cash Customer";
        public string Phone { get; set; } = string.Empty;
        public string NTN { get; set; } = string.Empty;
        public string STRN { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Region { get; set; } = "Select Region";
        public string Area { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = "Pakistan";
        
        public decimal CreditLimit { get; set; } = 0m;
        public int CreditDays { get; set; } = 30;

        // Customer Owes: unpaid credit balance customer needs to pay
        public decimal OwesAmount { get; set; } = 0m;
        
        // Advance Available: advance credit customer paid in advance
        public decimal AdvanceAvailable { get; set; } = 0m;

        public string Notes { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
