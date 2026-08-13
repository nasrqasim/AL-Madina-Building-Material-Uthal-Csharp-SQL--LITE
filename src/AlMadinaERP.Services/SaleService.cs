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
    public class SaleService : ISaleService
    {
        private readonly AppDbContext _context;

        public SaleService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            var company = await _context.CompanySettings.FirstOrDefaultAsync();
            var prefix = company?.InvoicePrefix ?? "INV";
            var count = await _context.SaleInvoices.CountAsync();
            return $"{prefix}-{(count + 1):D5}";
        }

        public async Task<SaleInvoice> CreateSaleInvoiceAsync(SaleInvoice invoice)
        {
            return await SaveSaleInvoiceAsync(invoice);
        }

        public async Task<SaleInvoice> SaveSaleInvoiceAsync(SaleInvoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // SANITIZE & RESOLVE CUSTOMER ID FIRST
                Customer? customer = null;
                if (invoice.CustomerId.HasValue && invoice.CustomerId.Value > 0)
                {
                    customer = await _context.Customers.FindAsync(invoice.CustomerId.Value);
                    if (customer != null)
                    {
                        invoice.CustomerName = customer.Name;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(invoice.CustomerName) &&
                         !invoice.CustomerName.Equals("WALK-IN CUSTOMER", StringComparison.OrdinalIgnoreCase))
                {
                    var custNameTrimmed = invoice.CustomerName.Trim().ToLower();
                    customer = await _context.Customers.FirstOrDefaultAsync(c => c.Name.ToLower() == custNameTrimmed);
                    if (customer != null)
                    {
                        invoice.CustomerId = customer.Id;
                        invoice.CustomerName = customer.Name;
                    }
                }

                if (customer != null)
                {
                    invoice.CustomerId = customer.Id;
                    invoice.CustomerName = customer.Name;
                }
                else
                {
                    invoice.CustomerId = null;
                    if (string.IsNullOrWhiteSpace(invoice.CustomerName))
                    {
                        invoice.CustomerName = "WALK-IN CUSTOMER";
                    }
                }

                // SANITIZE & RESOLVE ALL LINE ITEMS FIRST TO PREVENT SQLITE FK ERRORS
                var sanitizedItems = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>();
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
                    var existing = await _context.SaleInvoices
                        .AsNoTracking()
                        .Include(s => s.Items)
                        .FirstOrDefaultAsync(s => s.Id == invoice.Id);

                    if (existing != null)
                    {
                        // 1. Revert Old Stock using AsNoTracking DB values
                        var oldItems = await _context.SaleInvoiceItems.AsNoTracking().Where(i => i.SaleInvoiceId == existing.Id).ToListAsync();
                        foreach (var oldItem in oldItems)
                        {
                            if (oldItem.ItemId <= 0) continue;
                            var dbItem = await _context.Items.FindAsync(oldItem.ItemId);
                            if (dbItem != null)
                            {
                                if (existing.Type == InvoiceType.SaleReturn)
                                {
                                    dbItem.CurrentStock -= oldItem.Quantity;
                                    dbItem.StockIn -= oldItem.Quantity;
                                }
                                else
                                {
                                    dbItem.CurrentStock += oldItem.Quantity;
                                    dbItem.StockOut -= oldItem.Quantity;
                                }
                                if (dbItem.CurrentStock < 0) dbItem.CurrentStock = 0;
                                if (dbItem.StockIn < 0) dbItem.StockIn = 0;
                                if (dbItem.StockOut < 0) dbItem.StockOut = 0;
                                _context.Items.Update(dbItem);
                            }
                        }

                        // 2. Revert Old Customer Financials
                        if (existing.CustomerId.HasValue && existing.CustomerId.Value > 0 && !existing.IsCashSale)
                        {
                            var oldCustomer = await _context.Customers.FindAsync(existing.CustomerId.Value);
                            if (oldCustomer != null)
                            {
                                if (existing.Type == InvoiceType.SaleReturn)
                                {
                                    if (oldCustomer.AdvanceAvailable >= existing.TotalAmount)
                                        oldCustomer.AdvanceAvailable -= existing.TotalAmount;
                                    else
                                    {
                                        oldCustomer.OwesAmount += (existing.TotalAmount - oldCustomer.AdvanceAvailable);
                                        oldCustomer.AdvanceAvailable = 0;
                                    }
                                }
                                else
                                {
                                    if (oldCustomer.OwesAmount >= existing.OutstandingAmount)
                                        oldCustomer.OwesAmount -= existing.OutstandingAmount;
                                    else
                                        oldCustomer.OwesAmount = 0;

                                    oldCustomer.AdvanceAvailable += existing.AdvanceUsed;
                                }
                                _context.Customers.Update(oldCustomer);
                            }
                        }

                        // 3. Remove Old Ledgers & Items
                        var oldCustLedgers = await _context.CustomerLedgers.Where(cl => cl.SaleInvoiceId == existing.Id).ToListAsync();
                        _context.CustomerLedgers.RemoveRange(oldCustLedgers);

                        var oldInvLedgers = await _context.InventoryLedgers.Where(il => il.SaleInvoiceId == existing.Id).ToListAsync();
                        _context.InventoryLedgers.RemoveRange(oldInvLedgers);

                        var trackedOldItems = await _context.SaleInvoiceItems.Where(i => i.SaleInvoiceId == existing.Id).ToListAsync();
                        _context.SaleInvoiceItems.RemoveRange(trackedOldItems);
                        await _context.SaveChangesAsync();

                        // Prepare new items with Id = 0 to avoid EF identity tracking conflicts
                        var newItems = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>();
                        foreach (var item in invoice.Items)
                        {
                            newItems.Add(new SaleInvoiceItem
                            {
                                Id = 0,
                                SaleInvoiceId = existing.Id,
                                ItemId = item.ItemId,
                                ItemCode = item.ItemCode,
                                ItemName = item.ItemName,
                                Quantity = item.Quantity,
                                Rate = item.Rate,
                                UnitName = item.UnitName,
                                DiscountPercent = item.DiscountPercent,
                                DiscountAmount = item.DiscountAmount,
                                TotalPrice = item.TotalPrice,
                                Reason = item.Reason,
                                IsReceived = item.IsReceived
                            });
                        }

                        // Copy properties to existing tracked instance
                        existing.CustomerId = invoice.CustomerId;
                        existing.CustomerName = invoice.CustomerName;
                        existing.Date = invoice.Date != default ? invoice.Date : DateTime.Now;
                        existing.Status = invoice.Status;
                        existing.Type = invoice.Type;
                        existing.IsCashSale = invoice.IsCashSale;
                        existing.Subtotal = invoice.Subtotal;
                        existing.DiscountAmount = invoice.DiscountAmount;
                        existing.ExtraCharges = invoice.ExtraCharges;
                        existing.AdditionalDiscount = invoice.AdditionalDiscount;
                        existing.Remarks = invoice.Remarks;
                        existing.Items = newItems;

                        invoice = existing; // Use tracked entity
                    }
                }

                if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
                {
                    invoice.InvoiceNumber = await GenerateNextInvoiceNumberAsync();
                }
                if (string.IsNullOrWhiteSpace(invoice.VoucherNumber))
                {
                    invoice.VoucherNumber = $"VCH-{invoice.InvoiceNumber}";
                }
                if (invoice.Date == default)
                {
                    invoice.Date = DateTime.Now;
                }

                // Total Calculation
                invoice.TotalAmount = Math.Max(0m, (invoice.Subtotal - invoice.DiscountAmount) + invoice.ExtraCharges - invoice.AdditionalDiscount);

                // Preserve user-specified PaidAmount if set, otherwise default for full cash sale
                if (invoice.PaidAmount <= 0 && invoice.IsCashSale)
                {
                    invoice.PaidAmount = invoice.TotalAmount;
                }

                decimal remainingDue = Math.Max(0m, invoice.TotalAmount - invoice.PaidAmount);

                // Customer Advance & Owes Logic for Credit / Advance / Partial Sales
                if (customer != null)
                {
                    if (invoice.Type == InvoiceType.SaleInvoice || invoice.Type == InvoiceType.POSCounterSale)
                    {
                        if (customer.AdvanceAvailable >= remainingDue)
                        {
                            invoice.AdvanceUsed = remainingDue;
                            customer.AdvanceAvailable -= remainingDue;
                            invoice.OutstandingAmount = 0;
                        }
                        else
                        {
                            invoice.AdvanceUsed = customer.AdvanceAvailable;
                            invoice.OutstandingAmount = Math.Max(0m, remainingDue - customer.AdvanceAvailable);
                            customer.AdvanceAvailable = 0;
                            customer.OwesAmount += invoice.OutstandingAmount;
                        }
                    }
                    else if (invoice.Type == InvoiceType.SaleReturn)
                    {
                        if (customer.OwesAmount >= invoice.TotalAmount)
                        {
                            customer.OwesAmount -= invoice.TotalAmount;
                        }
                        else
                        {
                            decimal remainingReturn = invoice.TotalAmount - customer.OwesAmount;
                            customer.OwesAmount = 0;
                            customer.AdvanceAvailable += remainingReturn;
                        }
                    }
                    _context.Customers.Update(customer);
                }
                else
                {
                    invoice.AdvanceUsed = 0;
                    invoice.OutstandingAmount = remainingDue;
                }

                // Clear navigation property references to avoid EF Core entity tracking collisions
                invoice.Customer = null;
                foreach (var item in invoice.Items)
                {
                    item.Item = null;
                    item.SaleInvoice = null;
                }

                if (invoice.Id == 0)
                {
                    await _context.SaleInvoices.AddAsync(invoice);
                }
                await _context.SaveChangesAsync(); // Generates invoice.Id & assigns to items

                // Inventory Stock Updates + Inventory Ledger
                foreach (var item in invoice.Items)
                {
                    var dbItem = await _context.Items.FindAsync(item.ItemId);
                    if (dbItem != null)
                    {
                        decimal qtyIn = 0, qtyOut = 0;
                        if (invoice.Type == InvoiceType.SaleReturn)
                        {
                            dbItem.CurrentStock += item.Quantity;
                            dbItem.StockIn += item.Quantity;
                            qtyIn = item.Quantity;
                        }
                        else
                        {
                            dbItem.CurrentStock -= item.Quantity;
                            dbItem.StockOut += item.Quantity;
                            qtyOut = item.Quantity;
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
                            Reference = invoice.InvoiceNumber,
                            Remarks = $"Invoice #{invoice.InvoiceNumber} - {dbItem.Name}",
                            SaleInvoiceId = invoice.Id
                        };
                        await _context.InventoryLedgers.AddAsync(invLedger);
                    }
                }

                // Customer Ledger Posting
                if (customer != null)
                {
                    var runningBal = customer.OwesAmount - customer.AdvanceAvailable;
                    var ledger = new CustomerLedger
                    {
                        CustomerId = customer.Id,
                        Date = invoice.Date,
                        TransactionType = invoice.Type.ToString(),
                        VoucherNumber = invoice.VoucherNumber,
                        Debit = (invoice.Type == InvoiceType.SaleInvoice || invoice.Type == InvoiceType.POSCounterSale) ? invoice.TotalAmount : 0,
                        Credit = (invoice.Type == InvoiceType.SaleReturn) ? invoice.TotalAmount : invoice.AdvanceUsed,
                        RunningBalance = runningBal,
                        Remarks = string.IsNullOrWhiteSpace(invoice.Remarks) ? $"Invoice #{invoice.InvoiceNumber}" : invoice.Remarks,
                        SaleInvoiceId = invoice.Id
                    };
                    await _context.CustomerLedgers.AddAsync(ledger);
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

        public async Task<SaleInvoice?> GetSaleInvoiceByIdAsync(int id)
        {
            return await _context.SaleInvoices
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .ThenInclude(i => i.Item)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<SaleInvoice>> SearchInvoicesAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var q = _context.SaleInvoices
                .Include(s => s.Customer)
                .Include(s => s.Items)
                .AsQueryable();

            if (fromDate.HasValue)
                q = q.Where(s => s.Date >= fromDate.Value);

            if (toDate.HasValue)
                q = q.Where(s => s.Date <= toDate.Value);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(s => s.InvoiceNumber.ToLower().Contains(term) || s.CustomerName.ToLower().Contains(term));
            }

            return await q
                .OrderByDescending(s => s.Date)
                .Take(200)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task DeleteSaleInvoiceAsync(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var invoice = await _context.SaleInvoices
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (invoice != null)
                {
                    // Revert stock
                    foreach (var item in invoice.Items)
                    {
                        if (item.ItemId <= 0) continue;
                        var dbItem = await _context.Items.FindAsync(item.ItemId);
                        if (dbItem != null)
                        {
                            if (invoice.Type == InvoiceType.SaleReturn)
                                dbItem.CurrentStock -= item.Quantity;
                            else
                                dbItem.CurrentStock += item.Quantity;
                            _context.Items.Update(dbItem);
                        }
                    }

                    // Remove customer ledger entries
                    var custLedgers = await _context.CustomerLedgers
                        .Where(cl => cl.SaleInvoiceId == id).ToListAsync();
                    _context.CustomerLedgers.RemoveRange(custLedgers);

                    // Remove inventory ledger entries for this sale invoice
                    var invLedgers = await _context.InventoryLedgers
                        .Where(il => il.SaleInvoiceId == id).ToListAsync();
                    _context.InventoryLedgers.RemoveRange(invLedgers);

                    // Reverse customer owes/advance if credit sale
                    if (invoice.CustomerId.HasValue && invoice.CustomerId.Value > 0 && !invoice.IsCashSale)
                    {
                        var customer = await _context.Customers.FindAsync(invoice.CustomerId.Value);
                        if (customer != null)
                        {
                            if (invoice.Type == InvoiceType.SaleReturn)
                            {
                                // Reversing a return: reduce advance or increase owes
                                if (customer.AdvanceAvailable >= invoice.TotalAmount)
                                    customer.AdvanceAvailable -= invoice.TotalAmount;
                                else
                                    customer.OwesAmount += invoice.TotalAmount - customer.AdvanceAvailable;
                            }
                            else
                            {
                                // Reversing a sale: reduce owes or restore advance
                                if (customer.OwesAmount >= invoice.OutstandingAmount)
                                    customer.OwesAmount -= invoice.OutstandingAmount;
                                else
                                    customer.OwesAmount = 0;
                                customer.AdvanceAvailable += invoice.AdvanceUsed;
                            }
                            _context.Customers.Update(customer);
                        }
                    }

                    _context.SaleInvoices.Remove(invoice);
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
