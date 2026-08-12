using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

namespace AlMadinaERP.Services
{
    public class ReceiptPaymentService : IReceiptPaymentService
    {
        private readonly AppDbContext _context;

        public ReceiptPaymentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Receipt> ProcessReceiptAsync(Receipt receipt)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(receipt.ReceiptNumber))
                {
                    var company = await _context.CompanySettings.FirstOrDefaultAsync();
                    var prefix = company?.ReceiptPrefix ?? "RCT";
                    var count = await _context.Receipts.CountAsync();
                    receipt.ReceiptNumber = $"{prefix}-{(count + 1):D5}";
                }
                
                if (receipt.Date == default)
                {
                    receipt.Date = DateTime.Now;
                }

                // Handle Bank Balance
                if (receipt.PaymentMethod == PaymentMethod.Bank && receipt.BankId.HasValue)
                {
                    var bank = await _context.Banks.FindAsync(receipt.BankId.Value);
                    if (bank != null)
                    {
                        bank.CurrentBalance += receipt.Amount;
                        _context.Banks.Update(bank);
                    }
                }

                // Resolve Customer by Name if ID missing
                if ((!receipt.CustomerId.HasValue || receipt.CustomerId == 0) && !string.IsNullOrWhiteSpace(receipt.CustomerName))
                {
                    var custByName = await _context.Customers.FirstOrDefaultAsync(c => c.Name.ToLower() == receipt.CustomerName.Trim().ToLower());
                    if (custByName != null)
                    {
                        receipt.CustomerId = custByName.Id;
                    }
                }

                // Handle Customer Receipt
                if (receipt.CustomerId.HasValue && receipt.CustomerId.Value > 0)
                {
                    var customer = await _context.Customers.FindAsync(receipt.CustomerId.Value);
                    if (customer != null)
                    {
                        receipt.CustomerName = customer.Name;
                        if (receipt.IsAdvance)
                        {
                            customer.AdvanceAvailable += receipt.Amount;
                        }
                        else
                        {
                            if (customer.OwesAmount >= receipt.Amount)
                            {
                                customer.OwesAmount -= receipt.Amount;
                            }
                            else
                            {
                                var remaining = receipt.Amount - customer.OwesAmount;
                                customer.OwesAmount = 0;
                                customer.AdvanceAvailable += remaining;
                            }
                        }
                        _context.Customers.Update(customer);

                        // Add Ledger entry
                        var ledger = new CustomerLedger
                        {
                            CustomerId = customer.Id,
                            Date = receipt.Date,
                            TransactionType = "Receipt",
                            VoucherNumber = receipt.ReceiptNumber,
                            Credit = receipt.Amount, // Receipt reduces customer balance
                            Debit = 0,
                            RunningBalance = customer.OwesAmount - customer.AdvanceAvailable,
                            Remarks = string.IsNullOrWhiteSpace(receipt.Remarks) ? "Cash Receipt Received" : receipt.Remarks
                        };
                        await _context.CustomerLedgers.AddAsync(ledger);
                    }
                }

                if (receipt.Id == 0)
                {
                    await _context.Receipts.AddAsync(receipt);
                }
                else
                {
                    _context.Receipts.Update(receipt);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return receipt;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Payment> ProcessPaymentAsync(Payment payment)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(payment.PaymentNumber))
                {
                    var company = await _context.CompanySettings.FirstOrDefaultAsync();
                    var prefix = company?.PaymentPrefix ?? "PAY";
                    var count = await _context.Payments.CountAsync();
                    payment.PaymentNumber = $"{prefix}-{(count + 1):D5}";
                }

                if (payment.Date == default)
                {
                    payment.Date = DateTime.Now;
                }
                if (string.IsNullOrWhiteSpace(payment.Remarks))
                {
                    payment.Remarks = payment.Narration ?? "";
                }

                // Handle Bank Balance
                if (payment.PaymentMethod == PaymentMethod.Bank && payment.BankId.HasValue)
                {
                    var bank = await _context.Banks.FindAsync(payment.BankId.Value);
                    if (bank != null)
                    {
                        bank.CurrentBalance -= payment.Amount;
                        _context.Banks.Update(bank);
                    }
                }

                // Resolve Vendor or Customer by Name if ID missing
                if ((!payment.VendorId.HasValue || payment.VendorId == 0) && !string.IsNullOrWhiteSpace(payment.VendorName))
                {
                    var vendByName = await _context.Vendors.FirstOrDefaultAsync(v => v.Name.ToLower() == payment.VendorName.Trim().ToLower());
                    if (vendByName != null)
                    {
                        payment.VendorId = vendByName.Id;
                    }
                }
                if ((!payment.CustomerId.HasValue || payment.CustomerId == 0) && !string.IsNullOrWhiteSpace(payment.CustomerName))
                {
                    var custByName = await _context.Customers.FirstOrDefaultAsync(c => c.Name.ToLower() == payment.CustomerName.Trim().ToLower());
                    if (custByName != null)
                    {
                        payment.CustomerId = custByName.Id;
                    }
                }

                // Handle Vendor Payment
                if (payment.VendorId.HasValue && payment.VendorId.Value > 0)
                {
                    var vendor = await _context.Vendors.FindAsync(payment.VendorId.Value);
                    if (vendor != null)
                    {
                        payment.VendorName = vendor.Name;
                        if (payment.IsAdvance)
                        {
                            vendor.AdvanceAvailable += payment.Amount;
                        }
                        else
                        {
                            if (vendor.OwesAmount >= payment.Amount)
                            {
                                vendor.OwesAmount -= payment.Amount;
                            }
                            else
                            {
                                var remaining = payment.Amount - vendor.OwesAmount;
                                vendor.OwesAmount = 0;
                                vendor.AdvanceAvailable += remaining;
                            }
                        }
                        _context.Vendors.Update(vendor);

                        // Add Ledger entry
                        var ledger = new VendorLedger
                        {
                            VendorId = vendor.Id,
                            Date = payment.Date,
                            TransactionType = "Payment",
                            VoucherNumber = payment.PaymentNumber,
                            Debit = payment.Amount, // Payment reduces vendor balance
                            Credit = 0,
                            RunningBalance = vendor.OwesAmount - vendor.AdvanceAvailable,
                            Remarks = string.IsNullOrWhiteSpace(payment.Remarks) ? "Cash Payment Paid" : payment.Remarks
                        };
                        await _context.VendorLedgers.AddAsync(ledger);
                    }
                }

                // Handle Customer Payment (Payment to Customer)
                if (payment.CustomerId.HasValue && payment.CustomerId.Value > 0)
                {
                    var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                    if (customer != null)
                    {
                        payment.CustomerName = customer.Name;
                        if (customer.AdvanceAvailable >= payment.Amount)
                        {
                            customer.AdvanceAvailable -= payment.Amount;
                        }
                        else
                        {
                            var remaining = payment.Amount - customer.AdvanceAvailable;
                            customer.AdvanceAvailable = 0;
                            customer.OwesAmount += remaining;
                        }
                        _context.Customers.Update(customer);

                        var ledger = new CustomerLedger
                        {
                            CustomerId = customer.Id,
                            Date = payment.Date,
                            TransactionType = "Payment Out",
                            VoucherNumber = payment.PaymentNumber,
                            Debit = payment.Amount,
                            Credit = 0,
                            RunningBalance = customer.OwesAmount - customer.AdvanceAvailable,
                            Remarks = string.IsNullOrWhiteSpace(payment.Remarks) ? "Payment to Customer" : payment.Remarks
                        };
                        await _context.CustomerLedgers.AddAsync(ledger);
                    }
                }

                if (payment.Id == 0)
                {
                    await _context.Payments.AddAsync(payment);
                }
                else
                {
                    _context.Payments.Update(payment);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return payment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Receipt>> SearchReceiptsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var q = _context.Receipts.Include(r => r.Customer).AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();
                q = q.Where(r => r.ReceiptNumber.ToLower().Contains(query) ||
                                 r.CustomerName.ToLower().Contains(query) ||
                                 r.IncomeTitle.ToLower().Contains(query) ||
                                 r.Remarks.ToLower().Contains(query));
            }

            if (fromDate.HasValue)
                q = q.Where(r => r.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(r => r.Date <= toDate.Value);

            return await q.OrderByDescending(r => r.Date).ToListAsync();
        }

        public async Task<List<Payment>> SearchPaymentsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var q = _context.Payments.Include(p => p.Vendor).AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();
                q = q.Where(p => p.PaymentNumber.ToLower().Contains(query) ||
                                 p.VendorName.ToLower().Contains(query) ||
                                 p.Remarks.ToLower().Contains(query));
            }

            if (fromDate.HasValue)
                q = q.Where(p => p.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(p => p.Date <= toDate.Value);

            return await q.OrderByDescending(p => p.Date).ToListAsync();
        }

        public async Task DeleteReceiptAsync(int id)
        {
            var item = await _context.Receipts.FindAsync(id);
            if (item != null)
            {
                _context.Receipts.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeletePaymentAsync(int id)
        {
            var item = await _context.Payments.FindAsync(id);
            if (item != null)
            {
                _context.Payments.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Bank>> GetBanksAsync()
        {
            return await _context.Banks.Where(b => b.IsActive).ToListAsync();
        }

        public async Task<Bank> SaveBankAsync(Bank bank)
        {
            if (bank.Id == 0)
            {
                await _context.Banks.AddAsync(bank);
            }
            else
            {
                _context.Banks.Update(bank);
            }
            await _context.SaveChangesAsync();
            return bank;
        }

        public async Task<List<Expense>> SearchExpensesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var q = _context.Expenses.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();
                q = q.Where(e => e.VoucherNumber.ToLower().Contains(query) ||
                                 e.Title.ToLower().Contains(query) ||
                                 e.Category.ToLower().Contains(query));
            }

            if (fromDate.HasValue)
                q = q.Where(e => e.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(e => e.Date <= toDate.Value);

            return await q.OrderByDescending(e => e.Date).ToListAsync();
        }

        public async Task<List<Expense>> GetExpensesAsync()
        {
            return await SearchExpensesAsync("");
        }

        public async Task<Expense> SaveExpenseAsync(Expense expense)
        {
            // Ensure default AccountCategory to prevent SQLite NOT NULL constraint failure
            var defaultAccCat = await _context.AccountCategories.FirstOrDefaultAsync();
            if (defaultAccCat == null)
            {
                defaultAccCat = new AccountCategory { Name = "Operating Expense", Code = "EXP-01", Type = AccountType.Expense };
                await _context.AccountCategories.AddAsync(defaultAccCat);
                await _context.SaveChangesAsync();
            }

            if (!expense.AccountCategoryId.HasValue || expense.AccountCategoryId.Value <= 0)
            {
                expense.AccountCategoryId = defaultAccCat.Id;
            }

            if (expense.BankId.HasValue && expense.BankId.Value <= 0)
            {
                expense.BankId = null;
                expense.Bank = null;
            }

            // Status Check: Only decrease cash/bank balance if Paid/Posted
            bool isPaid = string.Equals(expense.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(expense.Status, "Posted", StringComparison.OrdinalIgnoreCase);

            if (expense.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(expense.VoucherNumber))
                {
                    expense.VoucherNumber = "EXP-" + DateTime.Now.ToString("fffSSmm");
                }
                expense.Date = DateTime.Now;
                await _context.Expenses.AddAsync(expense);

                if (isPaid && expense.Amount > 0)
                {
                    if (expense.PaymentMethod == PaymentMethod.Bank && expense.BankId.HasValue)
                    {
                        var bank = await _context.Banks.FindAsync(expense.BankId.Value);
                        if (bank != null)
                        {
                            bank.CurrentBalance -= expense.Amount;
                            _context.Banks.Update(bank);
                        }
                    }
                    else
                    {
                        // Cash Payment: Deduct from primary Cash Bank Account
                        var cashAccount = await _context.Banks.FirstOrDefaultAsync(b => b.AccountName.Contains("Cash") || b.BankName.Contains("Cash"));
                        if (cashAccount != null)
                        {
                            cashAccount.CurrentBalance -= expense.Amount;
                            _context.Banks.Update(cashAccount);
                        }
                    }
                }
            }
            else
            {
                _context.Expenses.Update(expense);
            }

            await _context.SaveChangesAsync();
            return expense;
        }

        public async Task<Expense> ProcessExpenseAsync(Expense expense)
        {
            return await SaveExpenseAsync(expense);
        }

        public async Task DeleteExpenseAsync(int id)
        {
            var exp = await _context.Expenses.FindAsync(id);
            if (exp != null)
            {
                _context.Expenses.Remove(exp);
                await _context.SaveChangesAsync();
            }
        }
    }
}
