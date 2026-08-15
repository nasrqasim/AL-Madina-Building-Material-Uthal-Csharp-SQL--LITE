using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

namespace AlMadinaERP.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly AppDbContext _context;

        public CustomerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetNextCustomerCodeAsync()
        {
            var lastCode = await _context.Customers
                .AsNoTracking()
                .Where(c => c.Code.StartsWith("CUST-"))
                .OrderByDescending(c => c.Code)
                .Select(c => c.Code)
                .FirstOrDefaultAsync();

            int maxNum = 0;
            if (!string.IsNullOrEmpty(lastCode) && int.TryParse(lastCode.Substring(5), out int num))
            {
                maxNum = num;
            }
            if (maxNum == 0) maxNum = await _context.Customers.AsNoTracking().CountAsync();
            return $"CUST-{(maxNum + 1):D5}";
        }

        private async Task EnsureCustomerCodesAsync()
        {
            var unassigned = await _context.Customers
                .Where(c => string.IsNullOrEmpty(c.Code))
                .OrderBy(c => c.Id)
                .ToListAsync();

            if (unassigned.Count > 0)
            {
                var codes = await _context.Customers
                    .Where(c => !string.IsNullOrEmpty(c.Code))
                    .Select(c => c.Code)
                    .ToListAsync();

                int maxNum = 0;
                foreach (var c in codes)
                {
                    if (c.StartsWith("CUST-") && int.TryParse(c.Substring(5), out int num))
                    {
                        if (num > maxNum) maxNum = num;
                    }
                }

                foreach (var cust in unassigned)
                {
                    maxNum++;
                    cust.Code = $"CUST-{maxNum:D5}";
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Customer>> SearchCustomersAsync(string query)
        {
            await EnsureCustomerCodesAsync();

            if (string.IsNullOrWhiteSpace(query))
            {
                return await _context.Customers
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Take(100)
                    .AsNoTracking()
                    .ToListAsync();
            }

            var q = query.Trim().ToLower();
            return await _context.Customers
                .Where(c => c.IsActive && (c.Name.ToLower().Contains(q) || c.Code.ToLower().Contains(q) || c.Phone.Contains(q) || (c.Area != null && c.Area.ToLower().Contains(q))))
                .OrderBy(c => c.Name)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Customer?> GetCustomerByIdAsync(int id)
        {
            return await _context.Customers.FindAsync(id);
        }

        public async Task<Customer> SaveCustomerAsync(Customer customer)
        {
            if (customer.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(customer.Code))
                {
                    customer.Code = await GetNextCustomerCodeAsync();
                }
                customer.CreatedAt = DateTime.Now;
                await _context.Customers.AddAsync(customer);
            }
            else
            {
                _context.Customers.Update(customer);
            }

            await _context.SaveChangesAsync();
            return customer;
        }

        public async Task DeleteCustomerAsync(int id)
        {
            var cust = await _context.Customers.FindAsync(id);
            if (cust != null)
            {
                cust.IsActive = false; // Soft delete
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<CustomerBalanceDto>> GetCustomerBalancesAsync(string query = "")
        {
            await EnsureCustomerCodesAsync();

            var q = query.Trim().ToLower();
            var customers = _context.Customers.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
            {
                customers = customers.Where(c => c.Name.ToLower().Contains(q) || c.Code.ToLower().Contains(q) || c.Phone.Contains(q) || (c.Area != null && c.Area.ToLower().Contains(q)));
            }

            return await customers
                .OrderBy(c => c.Name)
                .Select(c => new CustomerBalanceDto
                {
                    Id = c.Id,
                    Code = c.Code,
                    Name = c.Name,
                    Phone = c.Phone,
                    Area = c.Area,
                    CustomerOwes = c.OwesAmount,
                    AdvanceAvailable = c.AdvanceAvailable
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<CustomerLedger>> GetCustomerLedgerAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.CustomerLedgers
                .Include(cl => cl.SaleInvoice!)
                .ThenInclude(s => s.Items)
                .Where(cl => cl.CustomerId == customerId);

            if (fromDate.HasValue)
                query = query.Where(cl => cl.Date >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(cl => cl.Date <= toDate.Value);

            return await query
                .OrderBy(cl => cl.Date)
                .ThenBy(cl => cl.Id)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<CustomerPurchasedItemDto>> GetCustomerPurchasedItemsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.SaleInvoices.Where(s => s.CustomerId == customerId);

            if (fromDate.HasValue) query = query.Where(s => s.Date >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(s => s.Date <= toDate.Value);

            var invoices = await query
                .Include(s => s.Items)
                .OrderByDescending(s => s.Date)
                .AsNoTracking()
                .ToListAsync();

            var result = new List<CustomerPurchasedItemDto>();
            foreach (var inv in invoices)
            {
                foreach (var item in inv.Items)
                {
                    result.Add(new CustomerPurchasedItemDto
                    {
                        Date = inv.Date,
                        InvoiceNumber = inv.InvoiceNumber,
                        VoucherNumber = inv.VoucherNumber,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        Quantity = item.Quantity,
                        Unit = item.UnitName,
                        UnitPrice = item.Rate,
                        TotalAmount = item.TotalPrice,
                        PaidAmount = inv.PaidAmount,
                        AdvanceUsed = inv.AdvanceUsed,
                        OutstandingBalance = inv.BalanceDue
                    });
                }
            }
            return result;
        }

        public async Task<List<PaymentHistoryDto>> GetCustomerReceiptsAndPaymentsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var receipts = await _context.Receipts
                .Where(r => r.CustomerId == customerId && r.Status == "Posted")
                .OrderByDescending(r => r.Date)
                .AsNoTracking()
                .ToListAsync();

            if (fromDate.HasValue) receipts = receipts.Where(r => r.Date >= fromDate.Value).ToList();
            if (toDate.HasValue) receipts = receipts.Where(r => r.Date <= toDate.Value).ToList();

            return receipts.Select(r => new PaymentHistoryDto
            {
                Date = r.Date,
                VoucherNumber = r.ReceiptNumber,
                TransactionType = r.ReceiptType.ToString(),
                Mode = r.PaymentMethod.ToString(),
                Amount = r.Amount,
                ReferenceInvoice = r.ReceiptNumber,
                Remarks = r.Remarks
            }).ToList();
        }

        public async Task<List<OutstandingInvoiceDto>> GetCustomerOutstandingInvoicesAsync(int customerId)
        {
            var invoices = await _context.SaleInvoices
                .Where(s => s.CustomerId == customerId && (s.TotalAmount - s.PaidAmount - s.AdvanceUsed) > 0)
                .OrderByDescending(s => s.Date)
                .AsNoTracking()
                .ToListAsync();

            return invoices.Select(inv => new OutstandingInvoiceDto
            {
                InvoiceDate = inv.Date,
                InvoiceNumber = inv.InvoiceNumber,
                VoucherNumber = inv.VoucherNumber,
                PaymentTerms = inv.PaymentTerms,
                TotalAmount = inv.TotalAmount,
                PaidAmount = inv.PaidAmount,
                AdvanceUsed = inv.AdvanceUsed,
                BalanceDue = inv.BalanceDue,
                Status = inv.BalanceDue > 0 ? "Outstanding" : "Settled"
            }).ToList();
        }
    }
}
