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
    public class InventoryService : IInventoryService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public InventoryService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task<List<Item>> SearchItemsAsync(string query, int? categoryId = null)
        {
            using var _context = CreateContext();
            var q = _context.Items
                .Include(i => i.Category)
                .Include(i => i.Subcategory)
                .Include(i => i.PurchaseUnit)
                .Include(i => i.SaleUnit)
                .Where(i => i.IsActive);

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                q = q.Where(i => i.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(i => i.Name.ToLower().Contains(term) || i.Code.ToLower().Contains(term));
            }

            return await q
                .OrderBy(i => i.Name)
                .Take(200)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Item?> GetItemByIdAsync(int id)
        {
            using var _context = CreateContext();
            return await _context.Items
                .Include(i => i.Category)
                .Include(i => i.Subcategory)
                .Include(i => i.PurchaseUnit)
                .Include(i => i.SaleUnit)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Item> SaveItemAsync(Item item)
        {
            using var _context = CreateContext();
            // Ensure default Unit
            var defaultUnit = await _context.Units.FirstOrDefaultAsync();
            if (defaultUnit == null)
            {
                defaultUnit = new Unit { Name = "PCS" };
                await _context.Units.AddAsync(defaultUnit);
                await _context.SaveChangesAsync();
            }

            if (!item.PurchaseUnitId.HasValue || item.PurchaseUnitId.Value <= 0)
                item.PurchaseUnitId = defaultUnit.Id;
            if (!item.SaleUnitId.HasValue || item.SaleUnitId.Value <= 0)
                item.SaleUnitId = defaultUnit.Id;

            // Ensure default Category
            var defaultCategory = await _context.Categories.FirstOrDefaultAsync();
            if (defaultCategory == null)
            {
                defaultCategory = new Category { Name = "General" };
                await _context.Categories.AddAsync(defaultCategory);
                await _context.SaveChangesAsync();
            }

            if (!item.CategoryId.HasValue || item.CategoryId.Value <= 0)
                item.CategoryId = defaultCategory.Id;

            // Detach navigation objects to avoid EF tracking conflicts
            item.Category = null;
            item.Subcategory = null;
            item.PurchaseUnit = null;
            item.SaleUnit = null;

            if (item.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(item.Code))
                {
                    var count = await _context.Items.CountAsync();
                    item.Code = $"ITM-{(count + 1):D5}";
                }
                if (item.OpeningStock == 0 && item.CurrentStock > 0)
                {
                    item.OpeningStock = item.CurrentStock;
                }
                else
                {
                    item.CurrentStock = item.OpeningStock;
                }
                item.CreatedDate = DateTime.Now;
                item.LastUpdated = DateTime.Now;
                await _context.Items.AddAsync(item);
            }
            else
            {
                var existing = await _context.Items.FindAsync(item.Id);
                if (existing != null)
                {
                    _context.Entry(existing).CurrentValues.SetValues(item);
                    existing.LastUpdated = DateTime.Now;
                }
                else
                {
                    item.LastUpdated = DateTime.Now;
                    _context.Items.Update(item);
                }
            }

            await _context.SaveChangesAsync();
            return item;
        }

        public async Task DeleteItemAsync(int id)
        {
            using var _context = CreateContext();
            var hasSales = await _context.SaleInvoiceItems.AnyAsync(sii => sii.ItemId == id);
            if (hasSales)
                throw new InvalidOperationException("Cannot delete this item. It is referenced in one or more Sale Invoices. Please deactivate it instead.");

            var hasLedgerRefs = await _context.InventoryLedgers.AnyAsync(il => il.ItemId == id);
            if (hasLedgerRefs)
                throw new InvalidOperationException("Cannot delete this item. It has Inventory Ledger entries. Please deactivate it instead.");

            var item = await _context.Items.FindAsync(id);
            if (item != null)
            {
                item.IsActive = false;
                item.Status = "Inactive";
                item.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }
        }

        public async Task<List<Category>> GetCategoriesAsync()
        {
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            return await _context.Categories.OrderBy(c => c.Name).AsNoTracking().ToListAsync();
        }

        public async Task<Category> SaveCategoryAsync(Category category)
        {
            using var _context = CreateContext();
            if (category.Id == 0)
                await _context.Categories.AddAsync(category);
            else
                _context.Categories.Update(category);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            return category;
        }

        public async Task DeleteCategoryAsync(int id)
        {
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                var catNameLower = category.Name.Trim().ToLower();
                var hasItems = await _context.Items.AnyAsync(i => i.IsActive && (i.CategoryId == id || (i.CategoryName != null && i.CategoryName.Trim().ToLower() == catNameLower)));
                if (hasItems)
                {
                    throw new InvalidOperationException("Cannot delete this category because items are currently assigned to it. Reassign or remove those items first.");
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
            }
        }

        public async Task<List<Subcategory>> GetSubcategoriesAsync(int? categoryId = null)
        {
            using var _context = CreateContext();
            var q = _context.Subcategories.Include(s => s.Category).AsQueryable();
            if (categoryId.HasValue)
                q = q.Where(s => s.CategoryId == categoryId.Value);

            return await q.OrderBy(s => s.Name).AsNoTracking().ToListAsync();
        }

        public async Task<Subcategory> SaveSubcategoryAsync(Subcategory subcategory)
        {
            using var _context = CreateContext();
            if (subcategory.Id == 0)
                await _context.Subcategories.AddAsync(subcategory);
            else
                _context.Subcategories.Update(subcategory);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            return subcategory;
        }

        public async Task<List<Unit>> GetUnitsAsync()
        {
            using var _context = CreateContext();
            return await _context.Units.OrderBy(u => u.Name).AsNoTracking().ToListAsync();
        }

        public async Task<Unit> SaveUnitAsync(Unit unit)
        {
            using var _context = CreateContext();
            if (unit.Id == 0)
                await _context.Units.AddAsync(unit);
            else
                _context.Units.Update(unit);

            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            return unit;
        }

        public async Task<List<LowStockItemDto>> GetLowStockAlertsAsync()
        {
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            var list = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.SaleUnit)
                .Where(i => i.IsActive && (
                    i.CurrentStock <= (i.LowStockAlert > 0 ? i.LowStockAlert : 5) ||
                    (i.CurrentStock - i.ReservedStock) <= (i.LowStockAlert > 0 ? i.LowStockAlert : 5)
                ))
                .AsNoTracking()
                .ToListAsync();

            return list.Select(i => new LowStockItemDto
            {
                ItemId = i.Id,
                Code = i.Code ?? string.Empty,
                Name = i.Name ?? string.Empty,
                CategoryName = i.Category != null ? i.Category.Name : (!string.IsNullOrWhiteSpace(i.CategoryName) ? i.CategoryName : "General"),
                CurrentStock = i.CurrentStock,
                LowStockAlert = i.LowStockAlert > 0 ? i.LowStockAlert : 5,
                Unit = !string.IsNullOrWhiteSpace(i.SellingUnit) ? i.SellingUnit : (!string.IsNullOrWhiteSpace(i.BaseUnit) ? i.BaseUnit : (i.SaleUnit != null ? i.SaleUnit.Name : "Pcs"))
            }).ToList();
        }

        public async Task<List<InventoryLedger>> GetInventoryLedgerAsync(int itemId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            var q = _context.InventoryLedgers
                .Include(l => l.Item)
                .Where(l => l.ItemId == itemId);

            if (fromDate.HasValue)
                q = q.Where(l => l.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(l => l.Date <= toDate.Value);

            return await q
                .OrderBy(l => l.Date)
                .ThenBy(l => l.Id)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<InventoryLedger>> GetAllInventoryLedgerAsync(string searchQuery = "", DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var _context = CreateContext();
            _context.ChangeTracker.Clear();
            var q = _context.InventoryLedgers
                .Include(l => l.Item)
                .AsQueryable();

            if (fromDate.HasValue)
                q = q.Where(l => l.Date >= fromDate.Value);
            if (toDate.HasValue)
                q = q.Where(l => l.Date <= toDate.Value);

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var term = searchQuery.Trim().ToLower();
                q = q.Where(l => (l.ItemName != null && l.ItemName.ToLower().Contains(term)) ||
                                 (l.ItemCode != null && l.ItemCode.ToLower().Contains(term)) ||
                                 (l.VoucherNumber != null && l.VoucherNumber.ToLower().Contains(term)) ||
                                 (l.TransactionType != null && l.TransactionType.ToLower().Contains(term)) ||
                                 (l.Remarks != null && l.Remarks.ToLower().Contains(term)) ||
                                 (l.Item != null && l.Item.Name.ToLower().Contains(term)) ||
                                 (l.Item != null && l.Item.Code.ToLower().Contains(term)) ||
                                 (l.Item != null && l.Item.CategoryName != null && l.Item.CategoryName.ToLower().Contains(term)));
            }

            return await q
                .OrderByDescending(l => l.Date)
                .ThenByDescending(l => l.Id)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
