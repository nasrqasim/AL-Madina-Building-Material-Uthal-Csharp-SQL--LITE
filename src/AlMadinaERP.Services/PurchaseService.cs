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
    public class PurchaseService : IPurchaseService
    {
        private readonly AppDbContext _context;

        public PurchaseService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextPurchaseNumberAsync()
        {
            var company = await _context.CompanySettings.FirstOrDefaultAsync();
            var prefix = company?.PurchasePrefix ?? "PUR";
            var count = await _context.PurchaseInvoices.CountAsync();
            return $"{prefix}-{(count + 1):D5}";
        }

        public async Task<PurchaseInvoice> CreatePurchaseInvoiceAsync(PurchaseInvoice invoice)
        {
            return await SavePurchaseInvoiceAsync(invoice);
        }

        public async Task<PurchaseInvoice> SavePurchaseInvoiceAsync(PurchaseInvoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // SANITIZE & RESOLVE VENDOR ID FIRST
                Vendor? vendor = null;
                if (invoice.VendorId.HasValue && invoice.VendorId.Value > 0)
                {
                    vendor = await _context.Vendors.FindAsync(invoice.VendorId.Value);
                    if (vendor != null)
                    {
                        invoice.VendorName = vendor.Name;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(invoice.VendorName) &&
                         !invoice.VendorName.Equals("Direct / Walk-in Purchase (No Vendor)", StringComparison.OrdinalIgnoreCase))
                {
                    var vendorNameTrimmed = invoice.VendorName.Trim().ToLower();
                    vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.Name.ToLower() == vendorNameTrimmed);
                    if (vendor != null)
                    {
                        invoice.VendorId = vendor.Id;
                        invoice.VendorName = vendor.Name;
                    }
                }

                if (vendor != null)
                {
                    invoice.VendorId = vendor.Id;
                    invoice.VendorName = vendor.Name;
                }
                else
                {
                    invoice.VendorId = null;
                }

                // SANITIZE & RESOLVE ALL LINE ITEMS FIRST TO PREVENT SQLITE FK ERRORS
                var sanitizedItems = new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>();
                foreach (var item in invoice.Items)
                {
                    if (item == null) continue;
                    if (item.ItemId <= 0)
                    {
                        var dbMatch = await _context.Items.FirstOrDefaultAsync(i =>
                            (!string.IsNullOrEmpty(item.ItemCode) && i.Code == item.ItemCode) ||
                            (!string.IsNullOrEmpty(item.ItemName) && i.Name == item.ItemName) ||
                            (!string.IsNullOrEmpty(item.ItemName) && i.Name.ToLower() == item.ItemName.Trim().ToLower()));

                        if (dbMatch != null)
                        {
                            item.ItemId = dbMatch.Id;
                            item.ItemCode = dbMatch.Code;
                            item.ItemName = dbMatch.Name;
                        }
                        else if (!string.IsNullOrWhiteSpace(item.ItemName))
                        {
                            var newItem = new Item
                            {
                                Code = string.IsNullOrWhiteSpace(item.ItemCode) ? $"ITEM-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}" : item.ItemCode,
                                Name = item.ItemName,
                                PurchasePrice = item.Rate,
                                SalePrice = item.Rate,
                                CurrentStock = 0,
                                IsActive = true
                            };
                            await _context.Items.AddAsync(newItem);
                            await _context.SaveChangesAsync();
                            item.ItemId = newItem.Id;
                        }
                    }

                    if (item.ItemId > 0)
                    {
                        sanitizedItems.Add(item);
                    }
                }
                invoice.Items = sanitizedItems;

                // IF EDITING EXISTING INVOICE (Id > 0): REVERSE PREVIOUS EFFECTS FIRST (RULE 23)
                if (invoice.Id > 0)
                {
                    var existing = await _context.PurchaseInvoices
                        .AsNoTracking()
                        .Include(p => p.Items)
                        .FirstOrDefaultAsync(p => p.Id == invoice.Id);

                    if (existing != null)
                    {
                        // 1. Revert Old Stock using AsNoTracking DB values
                        var oldItems = await _context.PurchaseInvoiceItems.AsNoTracking().Where(i => i.PurchaseInvoiceId == existing.Id).ToListAsync();
                        foreach (var oldItem in oldItems)
                        {
                            if (oldItem.ItemId <= 0) continue;
                            var dbItem = await _context.Items.FindAsync(oldItem.ItemId);
                            if (dbItem != null)
                            {
                                if (existing.Type == PurchaseType.PurchaseReturn)
                                {
                                    dbItem.CurrentStock += oldItem.Quantity;
                                    dbItem.StockOut -= oldItem.Quantity;
                                }
                                else
                                {
                                    dbItem.CurrentStock -= oldItem.Quantity;
                                    dbItem.StockIn -= oldItem.Quantity;
                                }
                                if (dbItem.CurrentStock < 0) dbItem.CurrentStock = 0;
                                if (dbItem.StockIn < 0) dbItem.StockIn = 0;
                                if (dbItem.StockOut < 0) dbItem.StockOut = 0;
                                _context.Items.Update(dbItem);
                            }
                        }

                        // 2. Revert Old Vendor Financials
                        if (existing.VendorId.HasValue && existing.VendorId.Value > 0 && !existing.IsCashPurchase)
                        {
                            var oldVendor = await _context.Vendors.FindAsync(existing.VendorId.Value);
                            if (oldVendor != null)
                            {
                                if (existing.Type == PurchaseType.PurchaseReturn)
                                {
                                    if (oldVendor.AdvanceAvailable >= existing.TotalAmount)
                                        oldVendor.AdvanceAvailable -= existing.TotalAmount;
                                    else
                                    {
                                        oldVendor.OwesAmount += (existing.TotalAmount - oldVendor.AdvanceAvailable);
                                        oldVendor.AdvanceAvailable = 0;
                                    }
                                }
                                else
                                {
                                    if (oldVendor.OwesAmount >= existing.OutstandingAmount)
                                        oldVendor.OwesAmount -= existing.OutstandingAmount;
                                    else
                                        oldVendor.OwesAmount = 0;

                                    oldVendor.AdvanceAvailable += existing.AdvanceUsed;
                                }
                                _context.Vendors.Update(oldVendor);
                            }
                        }

                        // 3. Remove Old Ledgers & Items
                        var oldVendorLedgers = await _context.VendorLedgers.Where(vl => vl.PurchaseInvoiceId == existing.Id).ToListAsync();
                        _context.VendorLedgers.RemoveRange(oldVendorLedgers);

                        var oldInvLedgers = await _context.InventoryLedgers.Where(il => il.PurchaseInvoiceId == existing.Id).ToListAsync();
                        _context.InventoryLedgers.RemoveRange(oldInvLedgers);

                        var trackedOldItems = await _context.PurchaseInvoiceItems.Where(i => i.PurchaseInvoiceId == existing.Id).ToListAsync();
                        _context.PurchaseInvoiceItems.RemoveRange(trackedOldItems);
                        await _context.SaveChangesAsync();

                        // Prepare new items with Id = 0 to avoid EF identity tracking conflicts
                        var newItems = new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>();
                        foreach (var item in invoice.Items)
                        {
                            newItems.Add(new PurchaseInvoiceItem
                            {
                                Id = 0,
                                PurchaseInvoiceId = existing.Id,
                                ItemId = item.ItemId,
                                ItemCode = item.ItemCode,
                                ItemName = item.ItemName,
                                Quantity = item.Quantity,
                                Rate = item.Rate,
                                UnitName = item.UnitName,
                                DiscountPercent = item.DiscountPercent,
                                DiscountAmount = item.DiscountAmount,
                                TaxPercent = item.TaxPercent,
                                TaxAmount = item.TaxAmount,
                                TotalPrice = item.TotalPrice
                            });
                        }

                        // Copy properties to existing tracked instance
                        existing.VendorId = invoice.VendorId;
                        existing.VendorName = invoice.VendorName;
                        existing.Date = invoice.Date != default ? invoice.Date : DateTime.Now;
                        existing.VendorInvoiceNo = invoice.VendorInvoiceNo;
                        existing.Status = invoice.Status;
                        existing.Type = invoice.Type;
                        existing.IsCashPurchase = invoice.IsCashPurchase;
                        existing.Subtotal = invoice.Subtotal;
                        existing.DiscountAmount = invoice.DiscountAmount;
                        existing.ExtraExpenses = invoice.ExtraExpenses;
                        existing.VehicleCharges = invoice.VehicleCharges;
                        existing.Remarks = invoice.Remarks;
                        existing.Items = newItems;

                        invoice = existing; // Use tracked entity
                    }
                }

                if (string.IsNullOrWhiteSpace(invoice.PurchaseNumber))
                {
                    invoice.PurchaseNumber = await GenerateNextPurchaseNumberAsync();
                }
                if (string.IsNullOrWhiteSpace(invoice.VoucherNumber))
                {
                    invoice.VoucherNumber = $"VCH-{invoice.PurchaseNumber}";
                }
                if (invoice.Date == default)
                {
                    invoice.Date = DateTime.Now;
                }

                invoice.TotalAmount = Math.Max(0m, (invoice.Subtotal - invoice.DiscountAmount) + invoice.ExtraExpenses + invoice.VehicleCharges);

                if (invoice.AmountPaid <= 0 && invoice.IsCashPurchase)
                {
                    invoice.AmountPaid = invoice.TotalAmount;
                }

                decimal netToVendor = Math.Max(0m, invoice.TotalAmount - invoice.AmountPaid);

                // Vendor Advance & Owes Logic for Credit / Advance / Partial Purchases
                if (vendor != null)
                {
                    if (invoice.Type == PurchaseType.PurchaseInvoice)
                    {
                        if (vendor.AdvanceAvailable >= netToVendor)
                        {
                            invoice.AdvanceUsed = netToVendor;
                            vendor.AdvanceAvailable -= netToVendor;
                            invoice.OutstandingAmount = 0;
                        }
                        else
                        {
                            invoice.AdvanceUsed = vendor.AdvanceAvailable;
                            invoice.OutstandingAmount = Math.Max(0m, netToVendor - vendor.AdvanceAvailable);
                            vendor.AdvanceAvailable = 0;
                            vendor.OwesAmount += invoice.OutstandingAmount;
                        }
                    }
                    else if (invoice.Type == PurchaseType.PurchaseReturn)
                    {
                        if (vendor.OwesAmount >= invoice.TotalAmount)
                        {
                            vendor.OwesAmount -= invoice.TotalAmount;
                        }
                        else
                        {
                            decimal remainingReturn = invoice.TotalAmount - vendor.OwesAmount;
                            vendor.OwesAmount = 0;
                            vendor.AdvanceAvailable += remainingReturn;
                        }
                    }
                    _context.Vendors.Update(vendor);
                }
                else
                {
                    invoice.AdvanceUsed = 0;
                    invoice.OutstandingAmount = netToVendor;
                }

                // Clear navigation property references to avoid EF Core entity tracking collisions
                invoice.Vendor = null;
                foreach (var item in invoice.Items)
                {
                    item.Item = null;
                    item.PurchaseInvoice = null;
                }

                if (invoice.Id == 0)
                {
                    await _context.PurchaseInvoices.AddAsync(invoice);
                }
                await _context.SaveChangesAsync(); // Generates invoice.Id & assigns to items

                // Stock Update + Inventory Ledger
                foreach (var item in invoice.Items)
                {
                    var dbItem = await _context.Items.FindAsync(item.ItemId);
                    if (dbItem != null)
                    {
                        decimal qtyIn = 0, qtyOut = 0;
                        if (invoice.Type == PurchaseType.PurchaseReturn)
                        {
                            dbItem.CurrentStock -= item.Quantity;
                            dbItem.StockOut += item.Quantity;
                            qtyOut = item.Quantity;
                        }
                        else
                        {
                            dbItem.CurrentStock += item.Quantity;
                            dbItem.StockIn += item.Quantity;
                            qtyIn = item.Quantity;
                        }
                        dbItem.LastUpdated = DateTime.Now;
                        _context.Items.Update(dbItem);

                        // Inventory Ledger Entry
                        var invLedger = new InventoryLedger
                        {
                            ItemId = dbItem.Id,
                            ItemCode = dbItem.Code,
                            ItemName = dbItem.Name,
                            Date = invoice.Date,
                            VoucherNumber = invoice.VoucherNumber,
                            TransactionType = invoice.Type.ToString(),
                            Unit = dbItem.BaseUnit,
                            QuantityIn = qtyIn,
                            QuantityOut = qtyOut,
                            RunningBalance = dbItem.CurrentStock,
                            Warehouse = dbItem.Warehouse,
                            Reference = invoice.PurchaseNumber,
                            Remarks = $"Purchase #{invoice.PurchaseNumber} - {dbItem.Name}",
                            PurchaseInvoiceId = invoice.Id
                        };
                        await _context.InventoryLedgers.AddAsync(invLedger);
                    }
                }

                // Vendor Ledger Posting
                if (vendor != null)
                {
                    var runningBal = vendor.OwesAmount - vendor.AdvanceAvailable;
                    var ledger = new VendorLedger
                    {
                        VendorId = vendor.Id,
                        Date = invoice.Date,
                        TransactionType = invoice.Type.ToString(),
                        VoucherNumber = invoice.VoucherNumber,
                        Credit = (invoice.Type == PurchaseType.PurchaseInvoice) ? invoice.TotalAmount : 0,
                        Debit = (invoice.Type == PurchaseType.PurchaseReturn) ? invoice.TotalAmount : (invoice.AdvanceUsed + invoice.AmountPaid),
                        RunningBalance = runningBal,
                        Remarks = string.IsNullOrWhiteSpace(invoice.Remarks) ? $"Purchase #{invoice.PurchaseNumber}" : invoice.Remarks,
                        PurchaseInvoiceId = invoice.Id
                    };
                    await _context.VendorLedgers.AddAsync(ledger);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return invoice;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PurchaseInvoice?> GetPurchaseInvoiceByIdAsync(int id)
        {
            return await _context.PurchaseInvoices
                .Include(p => p.Vendor)
                .Include(p => p.Items)
                .ThenInclude(i => i.Item)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<PurchaseInvoice>> SearchPurchasesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var q = _context.PurchaseInvoices
                .Include(p => p.Vendor)
                .Include(p => p.Items)
                .AsQueryable();

            if (fromDate.HasValue)
                q = q.Where(p => p.Date >= fromDate.Value);

            if (toDate.HasValue)
                q = q.Where(p => p.Date <= toDate.Value);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(p => p.PurchaseNumber.ToLower().Contains(term) || p.VendorName.ToLower().Contains(term));
            }

            return await q
                .OrderByDescending(p => p.Date)
                .Take(200)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task DeletePurchaseInvoiceAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var invoice = await _context.PurchaseInvoices
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (invoice != null)
                {
                    // Revert stock
                    foreach (var item in invoice.Items)
                    {
                        if (item.ItemId <= 0) continue;
                        var dbItem = await _context.Items.FindAsync(item.ItemId);
                        if (dbItem != null)
                        {
                            if (invoice.Type == PurchaseType.PurchaseReturn)
                                dbItem.CurrentStock += item.Quantity;
                            else
                                dbItem.CurrentStock -= item.Quantity;
                            if (dbItem.CurrentStock < 0) dbItem.CurrentStock = 0;
                            _context.Items.Update(dbItem);
                        }
                    }

                    // Remove vendor ledger entries
                    var vendorLedgers = await _context.VendorLedgers
                        .Where(vl => vl.PurchaseInvoiceId == id).ToListAsync();
                    _context.VendorLedgers.RemoveRange(vendorLedgers);

                    // Remove inventory ledger entries for this purchase invoice
                    var invLedgers = await _context.InventoryLedgers
                        .Where(il => il.PurchaseInvoiceId == id).ToListAsync();
                    _context.InventoryLedgers.RemoveRange(invLedgers);

                    // Reverse vendor owes/advance if credit purchase
                    if (invoice.VendorId.HasValue && invoice.VendorId.Value > 0 && !invoice.IsCashPurchase)
                    {
                        var vendor = await _context.Vendors.FindAsync(invoice.VendorId.Value);
                        if (vendor != null)
                        {
                            if (invoice.Type == PurchaseType.PurchaseReturn)
                            {
                                // Reversing a return: reduce advance or increase owes
                                if (vendor.AdvanceAvailable >= invoice.TotalAmount)
                                    vendor.AdvanceAvailable -= invoice.TotalAmount;
                                else
                                    vendor.OwesAmount += invoice.TotalAmount - vendor.AdvanceAvailable;
                            }
                            else
                            {
                                // Reversing a purchase: reduce owes or restore advance
                                if (vendor.OwesAmount >= invoice.OutstandingAmount)
                                    vendor.OwesAmount -= invoice.OutstandingAmount;
                                else
                                    vendor.OwesAmount = 0;
                                vendor.AdvanceAvailable += invoice.AdvanceUsed;
                            }
                            _context.Vendors.Update(vendor);
                        }
                    }

                    _context.PurchaseInvoices.Remove(invoice);
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
    }
}
