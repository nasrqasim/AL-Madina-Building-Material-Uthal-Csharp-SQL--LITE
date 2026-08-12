using System;
using System.ComponentModel.DataAnnotations.Schema;
using AlMadinaERP.Core.Enums;

namespace AlMadinaERP.Core.Models
{
    public class Receipt
    {
        public int Id { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        
        [NotMapped]
        public string VoucherNumber { get => ReceiptNumber; set => ReceiptNumber = value; }
        public DateTime Date { get; set; } = DateTime.Now;
        public ReceiptType ReceiptType { get; set; } = ReceiptType.CashReceipt; // CashReceipt, BankReceipt, OtherIncome
        
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        
        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }
        public string VendorName { get; set; } = string.Empty;
        
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        
        public int? BankId { get; set; }
        public Bank? Bank { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNo { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
        public string ReceivedBy { get; set; } = "Cash";
        
        public bool IsAdvance { get; set; } = false;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = "Posted"; // Posted, Draft
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InternalNotes { get; set; } = string.Empty;
        public string IncomeTitle { get; set; } = string.Empty;
        public string IncomeType { get; set; } = "One Time"; // One Time, Recurring
    }

    public class Payment
    {
        public int Id { get; set; }
        public string PaymentNumber { get; set; } = string.Empty;
        
        [NotMapped]
        public string VoucherNumber { get => PaymentNumber; set => PaymentNumber = value; }
        public DateTime Date { get; set; } = DateTime.Now;
        public PaymentType PaymentType { get; set; } = PaymentType.CashPayment; // CashPayment, BankPayment
        public string PaymentCategory { get; set; } = "Party Payment"; // Party Payment, Petty Payment
        
        public string PayToCategory { get; set; } = "Vendor"; // Customer, Vendor
        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }
        public string VendorName { get; set; } = string.Empty;
        
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string CustomerName { get; set; } = string.Empty;

        [NotMapped]
        public string PartyName => PayToCategory == "Customer" ? CustomerName : VendorName;
        
        public decimal Amount { get; set; }
        public decimal WhtRatePercent { get; set; }
        public decimal WhtAmount { get; set; }
        public decimal NetAmountToPay { get; set; }
        
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        
        public int? BankId { get; set; }
        public Bank? Bank { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNo { get; set; } = string.Empty;
        public string ChequeNo { get; set; } = string.Empty;
        public DateTime? ChequeDate { get; set; }
        public string PaidFrom { get; set; } = "Cashier / Counter";
        
        public bool IsAdvance { get; set; } = false;
        public string Narration { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Posted"; // Posted, Draft
        public string InternalNotes { get; set; } = string.Empty;
    }

    public class Bank
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public string AccountType { get; set; } = "Current Account";
        public string IBAN { get; set; } = string.Empty;
        public string SwiftCode { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; } = 0m;
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
    }

    public class AccountCategory
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public int? ParentId { get; set; }
    }

    public class Expense
    {
        public int Id { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string Category { get; set; } = "Utility (Electricity, Water)";
        public string ExpenseType { get; set; } = "Operating";
        public string Title { get; set; } = string.Empty;
        public int? AccountCategoryId { get; set; }
        public AccountCategory? AccountCategory { get; set; }
        public string AccountCategoryName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public string PaidFrom { get; set; } = "Cash";
        public int? BankId { get; set; }
        public Bank? Bank { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Paid";
        public string Notes { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class Staff
    {
        public int Id { get; set; }
        public string StaffCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        public string Designation { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public DateTime JoiningDate { get; set; } = DateTime.Now;
        public string EmploymentStatus { get; set; } = "Permanent";
        public string LinkedOperationalEmployee { get; set; } = "None";
        public bool IsActive { get; set; } = true;

        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IBAN { get; set; } = string.Empty;

        public string NTN { get; set; } = string.Empty;
        public string EOBINumber { get; set; } = string.Empty;
        public string SESSINumber { get; set; } = string.Empty;
        public string ProvidentFundNumber { get; set; } = string.Empty;

        public decimal BasicSalary { get; set; }
        public string AllowancesText { get; set; } = string.Empty;
        public string DeductionsText { get; set; } = string.Empty;

        public decimal TotalSalaryPaid { get; set; } = 0m;
        public decimal TotalAdvances { get; set; } = 0m;
        public decimal TotalLoans { get; set; } = 0m;
        public decimal LoanOutstanding { get; set; } = 0m;
    }

    public class Salary
    {
        public int Id { get; set; }
        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string SalaryMonth { get; set; } = string.Empty;
        public decimal BasicSalary { get; set; }
        public decimal AdvanceDeduction { get; set; }
        public decimal LoanDeduction { get; set; }
        public decimal Bonus { get; set; }
        public decimal NetPaid { get; set; }
        public PaymentMethod PaymentMode { get; set; } = PaymentMethod.Cash;
        public string Remarks { get; set; } = string.Empty;
    }

    public class SalaryAdvance
    {
        public int Id { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string RecoveryMonth { get; set; } = DateTime.Now.ToString("MMMM yyyy");
        public string Status { get; set; } = "Approved"; // Approved, Pending, Rejected
        public string Remarks { get; set; } = string.Empty;
    }

    public class JournalEntry
    {
        public int Id { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string AccountName { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Status { get; set; } = "Posted";
    }

    public class CompanySetting
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "AL Madina Building Material Uthal";
        public string Tagline { get; set; } = "Quality Building Materials & Supplies";
        public string Phone { get; set; } = "0300-1234567";
        public string Address { get; set; } = "Main Bazaar, Uthal, Balochistan";
        public string InvoicePrefix { get; set; } = "INV";
        public string PurchasePrefix { get; set; } = "PUR";
        public string ReceiptPrefix { get; set; } = "RCT";
        public string PaymentPrefix { get; set; } = "PAY";
        public string VoucherPrefix { get; set; } = "VCH";
        public DateTime FinancialYearStart { get; set; } = new DateTime(2026, 1, 1);
        public DateTime FinancialYearEnd { get; set; } = new DateTime(2026, 12, 31);
        public string HeaderNotes { get; set; } = "Welcome to AL Madina Building Material Uthal";
        public string FooterNotes { get; set; } = "Thank you for doing business with us!";
        public string BackupPath { get; set; } = string.Empty;
        public bool AutoBackupDaily { get; set; } = true;
    }

    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Cashier;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
