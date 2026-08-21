using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

using Microsoft.Extensions.DependencyInjection;

namespace AlMadinaERP.Services
{
    public class ReceiptPaymentService : IReceiptPaymentService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ReceiptPaymentService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task<Receipt> ProcessReceiptAsync(Receipt receipt)
        {
            using var _context = CreateContext();
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

                // If editing (Id > 0), revert original receipt values first to ensure double-entry integrity
                if (receipt.Id > 0)
                {
                    var original = await _context.Receipts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == receipt.Id);
                    if (original != null)
                    {
                        // 1. Revert original Bank balance
                        if (original.PaymentMethod == PaymentMethod.Bank && original.BankId.HasValue)
                        {
                            var bank = await _context.Banks.FindAsync(original.BankId.Value);
                            if (bank != null)
                            {
                                bank.CurrentBalance -= original.Amount;
                                _context.Banks.Update(bank);
                            }
                        }

                        // 2. Revert original Customer balance
                        if (original.CustomerId.HasValue && original.CustomerId.Value > 0)
                        {
                            var customer = await _context.Customers.FindAsync(original.CustomerId.Value);
                            if (customer != null)
                            {
                                if (original.IsAdvance)
                                {
                                    if (customer.AdvanceAvailable >= original.Amount)
                                    {
                                        customer.AdvanceAvailable -= original.Amount;
                                    }
                                    else
                                    {
                                        var rem = original.Amount - customer.AdvanceAvailable;
                                        customer.AdvanceAvailable = 0;
                                        customer.OwesAmount += rem;
                                    }
                                }
                                else
                                {
                                    customer.OwesAmount += original.Amount;
                                }
                                _context.Customers.Update(customer);
                            }

                            // Remove old customer ledger entry for this receipt to prevent duplicates
                            var ledgers = await _context.CustomerLedgers
                                .Where(cl => cl.CustomerId == original.CustomerId.Value && 
                                            (cl.VoucherNumber == original.ReceiptNumber || 
                                             cl.VoucherNumber == receipt.ReceiptNumber))
                                .ToListAsync();
                            if (ledgers.Any())
                            {
                                _context.CustomerLedgers.RemoveRange(ledgers);
                            }
                        }
                    }
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

                receipt.Customer = null;
                receipt.Vendor = null;
                receipt.Bank = null;
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
                _context.ChangeTracker.Clear();
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
            using var _context = CreateContext();
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

                // If editing (Id > 0), revert original payment values first to ensure double-entry integrity
                if (payment.Id > 0)
                {
                    var original = await _context.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == payment.Id);
                    if (original != null)
                    {
                        // 1. Revert original Bank balance
                        if (original.PaymentMethod == PaymentMethod.Bank && original.BankId.HasValue)
                        {
                            var bank = await _context.Banks.FindAsync(original.BankId.Value);
                            if (bank != null)
                            {
                                bank.CurrentBalance += original.Amount;
                                _context.Banks.Update(bank);
                            }
                        }

                        // 2. Revert original Vendor balance
                        if (original.VendorId.HasValue && original.VendorId.Value > 0)
                        {
                            var vendor = await _context.Vendors.FindAsync(original.VendorId.Value);
                            if (vendor != null)
                            {
                                if (original.IsAdvance)
                                {
                                    if (vendor.AdvanceAvailable >= original.Amount)
                                    {
                                        vendor.AdvanceAvailable -= original.Amount;
                                    }
                                    else
                                    {
                                        var rem = original.Amount - vendor.AdvanceAvailable;
                                        vendor.AdvanceAvailable = 0;
                                        vendor.OwesAmount += rem;
                                    }
                                }
                                else
                                {
                                    vendor.OwesAmount += original.Amount;
                                }
                                _context.Vendors.Update(vendor);
                            }

                            // Remove old vendor ledger entry for this payment to prevent duplicates
                            var ledgers = await _context.VendorLedgers
                                .Where(vl => vl.VendorId == original.VendorId.Value && 
                                            (vl.VoucherNumber == original.PaymentNumber || 
                                             vl.VoucherNumber == payment.PaymentNumber))
                                .ToListAsync();
                            if (ledgers.Any())
                            {
                                _context.VendorLedgers.RemoveRange(ledgers);
                            }
                        }

                        // 3. Revert original Customer balance (if customer payment)
                        if (original.CustomerId.HasValue && original.CustomerId.Value > 0)
                        {
                            var customer = await _context.Customers.FindAsync(original.CustomerId.Value);
                            if (customer != null)
                            {
                                if (customer.OwesAmount >= original.Amount)
                                {
                                    customer.OwesAmount -= original.Amount;
                                }
                                else
                                {
                                    var rem = original.Amount - customer.OwesAmount;
                                    customer.OwesAmount = 0;
                                    customer.AdvanceAvailable += rem;
                                }
                                _context.Customers.Update(customer);
                            }

                            // Remove old customer ledger entry for this payment to prevent duplicates
                            var ledgers = await _context.CustomerLedgers
                                .Where(cl => cl.CustomerId == original.CustomerId.Value && 
                                            (cl.VoucherNumber == original.PaymentNumber || 
                                             cl.VoucherNumber == payment.PaymentNumber))
                                .ToListAsync();
                            if (ledgers.Any())
                            {
                                _context.CustomerLedgers.RemoveRange(ledgers);
                            }
                        }
                    }
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

                payment.Customer = null;
                payment.Vendor = null;
                payment.Bank = null;
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
                _context.ChangeTracker.Clear();
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
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            var q = _context.Receipts.Include(r => r.Customer).Include(r => r.Vendor).AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim().ToLower();
                q = q.Where(r => (r.ReceiptNumber != null && r.ReceiptNumber.ToLower().Contains(query)) ||
                                 (r.CustomerName != null && r.CustomerName.ToLower().Contains(query)) ||
                                 (r.VendorName != null && r.VendorName.ToLower().Contains(query)) ||
                                 (r.IncomeTitle != null && r.IncomeTitle.ToLower().Contains(query)) ||
                                 (r.ReceivedBy != null && r.ReceivedBy.ToLower().Contains(query)) ||
                                 (r.BankName != null && r.BankName.ToLower().Contains(query)) ||
                                 (r.Remarks != null && r.Remarks.ToLower().Contains(query)));
            }

            if (fromDate.HasValue)
                q = q.Where(r => r.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(r => r.Date <= toDate.Value);

            var receipts = await q.OrderByDescending(r => r.Date).ToListAsync();
            foreach (var r in receipts)
            {
                if (r.CustomerId.HasValue && r.CustomerId.Value > 0)
                {
                    if (r.Customer != null)
                    {
                        r.CustomerName = r.Customer.Name;
                    }
                    else
                    {
                        var cust = await _context.Customers.FindAsync(r.CustomerId.Value);
                        if (cust != null) r.CustomerName = cust.Name;
                    }
                }
                if (r.VendorId.HasValue && r.VendorId.Value > 0)
                {
                    if (r.Vendor != null)
                    {
                        r.VendorName = r.Vendor.Name;
                    }
                    else
                    {
                        var vend = await _context.Vendors.FindAsync(r.VendorId.Value);
                        if (vend != null) r.VendorName = vend.Name;
                    }
                }
            }
            return receipts;
        }

        public async Task<List<Payment>> SearchPaymentsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            var q = _context.Payments.Include(p => p.Vendor).Include(p => p.Customer).AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.Trim().ToLower();
                q = q.Where(p => (p.PaymentNumber != null && p.PaymentNumber.ToLower().Contains(query)) ||
                                 (p.VendorName != null && p.VendorName.ToLower().Contains(query)) ||
                                 (p.CustomerName != null && p.CustomerName.ToLower().Contains(query)) ||
                                 (p.PaidFrom != null && p.PaidFrom.ToLower().Contains(query)) ||
                                 (p.BankName != null && p.BankName.ToLower().Contains(query)) ||
                                 (p.Remarks != null && p.Remarks.ToLower().Contains(query)));
            }

            if (fromDate.HasValue)
                q = q.Where(p => p.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(p => p.Date <= toDate.Value);

            var payments = await q.OrderByDescending(p => p.Date).ToListAsync();
            foreach (var p in payments)
            {
                if (p.VendorId.HasValue && p.VendorId.Value > 0)
                {
                    if (p.Vendor != null)
                    {
                        p.VendorName = p.Vendor.Name;
                    }
                    else
                    {
                        var vend = await _context.Vendors.FindAsync(p.VendorId.Value);
                        if (vend != null) p.VendorName = vend.Name;
                    }
                }
                if (p.CustomerId.HasValue && p.CustomerId.Value > 0)
                {
                    if (p.Customer != null)
                    {
                        p.CustomerName = p.Customer.Name;
                    }
                    else
                    {
                        var cust = await _context.Customers.FindAsync(p.CustomerId.Value);
                        if (cust != null) p.CustomerName = cust.Name;
                    }
                }
            }
            return payments;
        }

        public async Task DeleteReceiptAsync(int id)
        {
            using var _context = CreateContext();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var item = await _context.Receipts.FindAsync(id);
                if (item != null)
                {
                    // Revert customer balance & ledger
                    if (item.CustomerId.HasValue && item.CustomerId.Value > 0)
                    {
                        var customer = await _context.Customers.FindAsync(item.CustomerId.Value);
                        if (customer != null)
                        {
                            if (item.IsAdvance)
                            {
                                if (customer.AdvanceAvailable >= item.Amount)
                                    customer.AdvanceAvailable -= item.Amount;
                                else
                                {
                                    var rem = item.Amount - customer.AdvanceAvailable;
                                    customer.AdvanceAvailable = 0;
                                    customer.OwesAmount += rem;
                                }
                            }
                            else
                            {
                                customer.OwesAmount += item.Amount;
                            }
                            _context.Customers.Update(customer);
                        }

                        var ledgers = await _context.CustomerLedgers
                            .Where(cl => cl.CustomerId == item.CustomerId.Value && (cl.VoucherNumber == item.ReceiptNumber || (cl.Remarks != null && cl.Remarks.Contains(item.ReceiptNumber))))
                            .ToListAsync();
                        if (ledgers.Any())
                            _context.CustomerLedgers.RemoveRange(ledgers);
                    }

                    // Revert bank balance
                    if (item.BankId.HasValue && item.BankId.Value > 0)
                    {
                        var bank = await _context.Banks.FindAsync(item.BankId.Value);
                        if (bank != null)
                        {
                            bank.CurrentBalance -= item.Amount;
                            _context.Banks.Update(bank);
                        }
                    }

                    _context.Receipts.Remove(item);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeletePaymentAsync(int id)
        {
            using var _context = CreateContext();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var item = await _context.Payments.FindAsync(id);
                if (item != null)
                {
                    // Revert vendor balance & ledger
                    if (item.VendorId.HasValue && item.VendorId.Value > 0)
                    {
                        var vendor = await _context.Vendors.FindAsync(item.VendorId.Value);
                        if (vendor != null)
                        {
                            if (item.IsAdvance)
                            {
                                if (vendor.AdvanceAvailable >= item.Amount)
                                    vendor.AdvanceAvailable -= item.Amount;
                                else
                                {
                                    var rem = item.Amount - vendor.AdvanceAvailable;
                                    vendor.AdvanceAvailable = 0;
                                    vendor.OwesAmount += rem;
                                }
                            }
                            else
                            {
                                vendor.OwesAmount += item.Amount;
                            }
                            _context.Vendors.Update(vendor);
                        }

                        var ledgers = await _context.VendorLedgers
                            .Where(vl => vl.VendorId == item.VendorId.Value && (vl.VoucherNumber == item.PaymentNumber || (vl.Remarks != null && vl.Remarks.Contains(item.PaymentNumber))))
                            .ToListAsync();
                        if (ledgers.Any())
                            _context.VendorLedgers.RemoveRange(ledgers);
                    }

                    // Revert bank balance
                    if (item.BankId.HasValue && item.BankId.Value > 0)
                    {
                        var bank = await _context.Banks.FindAsync(item.BankId.Value);
                        if (bank != null)
                        {
                            bank.CurrentBalance += item.Amount;
                            _context.Banks.Update(bank);
                        }
                    }

                    _context.Payments.Remove(item);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Bank>> GetBanksAsync()
        {
            using var _context = CreateContext();
            return await _context.Banks.Where(b => b.IsActive).ToListAsync();
        }

        public async Task<Bank> SaveBankAsync(Bank bank)
        {
            using var _context = CreateContext();
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

        public async Task DeleteBankAsync(int id)
        {
            using var _context = CreateContext();
            var bank = await _context.Banks.FindAsync(id);
            if (bank != null)
            {
                bank.IsActive = false; // Soft delete
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Expense>> SearchExpensesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
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
            using var _context = CreateContext();
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

            expense.Bank = null;
            expense.AccountCategory = null;
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
            using var _context = CreateContext();
            var exp = await _context.Expenses.FindAsync(id);
            if (exp != null)
            {
                bool isPaid = string.Equals(exp.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(exp.Status, "Posted", StringComparison.OrdinalIgnoreCase);

                if (isPaid && exp.PaymentMethod == PaymentMethod.Bank && exp.BankId.HasValue && exp.BankId.Value > 0)
                {
                    var bank = await _context.Banks.FindAsync(exp.BankId.Value);
                    if (bank != null)
                    {
                        bank.CurrentBalance += exp.Amount;
                        _context.Banks.Update(bank);
                    }
                }

                _context.Expenses.Remove(exp);
                await _context.SaveChangesAsync();
            }
        }
    }
}
