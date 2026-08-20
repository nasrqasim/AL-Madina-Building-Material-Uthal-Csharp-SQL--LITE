using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.DTOs;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

using Microsoft.Extensions.DependencyInjection;

namespace AlMadinaERP.Services
{
    public class VendorService : IVendorService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public VendorService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task<string> GetNextVendorCodeAsync()
        {
            using var _context = CreateContext();
            var lastCode = await _context.Vendors
                .AsNoTracking()
                .Where(v => v.Code.StartsWith("VND-"))
                .OrderByDescending(v => v.Code)
                .Select(v => v.Code)
                .FirstOrDefaultAsync();

            int maxNum = 0;
            if (!string.IsNullOrEmpty(lastCode) && int.TryParse(lastCode.Substring(4), out int num))
            {
                maxNum = num;
            }
            if (maxNum == 0) maxNum = await _context.Vendors.AsNoTracking().CountAsync();
            return $"VND-{(maxNum + 1):D5}";
        }

        private async Task EnsureVendorCodesAsync()
        {
            using var _context = CreateContext();
            var unassigned = await _context.Vendors
                .Where(v => string.IsNullOrEmpty(v.Code))
                .OrderBy(v => v.Id)
                .ToListAsync();

            if (unassigned.Count > 0)
            {
                var codes = await _context.Vendors
                    .Where(v => !string.IsNullOrEmpty(v.Code))
                    .Select(v => v.Code)
                    .ToListAsync();

                int maxNum = 0;
                foreach (var c in codes)
                {
                    if (c.StartsWith("VND-") && int.TryParse(c.Substring(4), out int num))
                    {
                        if (num > maxNum) maxNum = num;
                    }
                }

                foreach (var vend in unassigned)
                {
                    maxNum++;
                    vend.Code = $"VND-{maxNum:D5}";
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Vendor>> SearchVendorsAsync(string query)
        {
            await EnsureVendorCodesAsync();
            using var _context = CreateContext();

            if (string.IsNullOrWhiteSpace(query))
            {
                return await _context.Vendors
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.Name)
                    .Take(100)
                    .AsNoTracking()
                    .ToListAsync();
            }

            var q = query.Trim().ToLower();
            return await _context.Vendors
                .Where(v => v.IsActive && (v.Name.ToLower().Contains(q) || v.Code.ToLower().Contains(q) || v.Phone.Contains(q) || (v.Area != null && v.Area.ToLower().Contains(q))))
                .OrderBy(v => v.Name)
                .Take(100)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Vendor?> GetVendorByIdAsync(int id)
        {
            using var _context = CreateContext();
            return await _context.Vendors.FindAsync(id);
        }

        public async Task<Vendor> SaveVendorAsync(Vendor vendor)
        {
            using var _context = CreateContext();
            if (vendor.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(vendor.Code))
                {
                    vendor.Code = await GetNextVendorCodeAsync();
                }
                vendor.CreatedAt = DateTime.Now;
                await _context.Vendors.AddAsync(vendor);
            }
            else
            {
                _context.Vendors.Update(vendor);
            }

            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task DeleteVendorAsync(int id)
        {
            using var _context = CreateContext();
            var vend = await _context.Vendors.FindAsync(id);
            if (vend != null)
            {
                vend.IsActive = false; // Soft delete
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<VendorBalanceDto>> GetVendorBalancesAsync(string query = "")
        {
            await EnsureVendorCodesAsync();
            using var _context = CreateContext();

            var q = query.Trim().ToLower();
            var vendors = _context.Vendors.Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
            {
                vendors = vendors.Where(v => v.Name.ToLower().Contains(q) || v.Code.ToLower().Contains(q) || v.Phone.Contains(q) || (v.Area != null && v.Area.ToLower().Contains(q)));
            }

            return await vendors
                .OrderBy(v => v.Name)
                .Select(v => new VendorBalanceDto
                {
                    Id = v.Id,
                    Code = v.Code,
                    Name = v.Name,
                    Phone = v.Phone,
                    Area = v.Area,
                    VendorOwes = v.OwesAmount,
                    AdvanceAvailable = v.AdvanceAvailable
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<VendorLedger>> GetVendorLedgerAsync(int vendorId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
            var query = _context.VendorLedgers
                .Include(vl => vl.PurchaseInvoice!)
                .ThenInclude(p => p.Items)
                .Where(vl => vl.VendorId == vendorId);

            if (fromDate.HasValue)
                query = query.Where(vl => vl.Date >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(vl => vl.Date <= toDate.Value);

            return await query
                .OrderBy(vl => vl.Date)
                .ThenBy(vl => vl.Id)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<VendorPurchasedItemDto>> GetVendorPurchasedItemsAsync(int vendorId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
            var query = _context.PurchaseInvoices.Where(p => p.VendorId == vendorId);

            if (fromDate.HasValue) query = query.Where(p => p.Date >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(p => p.Date <= toDate.Value);

            var purchases = await query
                .Include(p => p.Items)
                .OrderByDescending(p => p.Date)
                .AsNoTracking()
                .ToListAsync();

            var result = new List<VendorPurchasedItemDto>();
            foreach (var pur in purchases)
            {
                foreach (var item in pur.Items)
                {
                    result.Add(new VendorPurchasedItemDto
                    {
                        Date = pur.Date,
                        PurchaseNumber = pur.PurchaseNumber,
                        VoucherNumber = pur.VoucherNumber,
                        ItemCode = item.ItemCode,
                        ItemName = item.ItemName,
                        Quantity = item.Quantity,
                        Unit = item.UnitName,
                        UnitPrice = item.Rate,
                        TotalAmount = item.TotalPrice,
                        PaidAmount = pur.AmountPaid,
                        AdvanceUsed = pur.AdvanceUsed,
                        OutstandingBalance = pur.BalanceDue
                    });
                }
            }
            return result;
        }

        public async Task<List<PaymentHistoryDto>> GetVendorReceiptsAndPaymentsAsync(int vendorId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
            var payments = await _context.Payments
                .Where(p => p.VendorId == vendorId && p.Status == "Posted")
                .OrderByDescending(p => p.Date)
                .AsNoTracking()
                .ToListAsync();

            if (fromDate.HasValue) payments = payments.Where(p => p.Date >= fromDate.Value).ToList();
            if (toDate.HasValue) payments = payments.Where(p => p.Date <= toDate.Value).ToList();

            return payments.Select(p => new PaymentHistoryDto
            {
                Date = p.Date,
                VoucherNumber = p.PaymentNumber,
                TransactionType = p.PaymentCategory,
                Mode = p.PaymentMethod.ToString(),
                Amount = p.Amount,
                ReferenceInvoice = p.PaymentNumber,
                Remarks = p.Remarks
            }).ToList();
        }

        public async Task<List<OutstandingInvoiceDto>> GetVendorOutstandingInvoicesAsync(int vendorId)
        {
            using var _context = CreateContext();
            var invoices = await _context.PurchaseInvoices
                .Where(p => p.VendorId == vendorId && (p.TotalAmount - p.AmountPaid - p.AdvanceUsed) > 0)
                .OrderByDescending(p => p.Date)
                .AsNoTracking()
                .ToListAsync();

            return invoices.Select(inv => new OutstandingInvoiceDto
            {
                InvoiceDate = inv.Date,
                InvoiceNumber = inv.PurchaseNumber,
                VoucherNumber = inv.VoucherNumber,
                PaymentTerms = inv.PaymentTerms,
                TotalAmount = inv.TotalAmount,
                PaidAmount = inv.AmountPaid,
                AdvanceUsed = inv.AdvanceUsed,
                BalanceDue = inv.BalanceDue,
                Status = inv.BalanceDue > 0 ? "Outstanding" : "Settled"
            }).ToList();
        }
    }
}
