using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

namespace AlMadinaERP.Services
{
    public class CustomerOrderService : ICustomerOrderService
    {
        private readonly AppDbContext _context;

        public CustomerOrderService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CustomerOrder>> GetCustomerOrdersAsync(string searchQuery = "", string statusFilter = "All")
        {
            var query = _context.CustomerOrders
                .Include(o => o.Items)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                var sf = statusFilter.Trim();
                query = query.Where(o => o.Status.ToLower() == sf.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();

                bool isNumeric = int.TryParse(q, out int serialNo);

                query = query.Where(o =>
                    (o.CustomerName != null && o.CustomerName.ToLower().Contains(q)) ||
                    (o.OrderNumber != null && o.OrderNumber.ToLower().Contains(q)) ||
                    (o.ContactNumber != null && o.ContactNumber.ToLower().Contains(q)) ||
                    (isNumeric && o.Id == serialNo)
                );
            }

            return await query
                .OrderByDescending(o => o.Id)
                .ToListAsync();
        }

        public async Task<CustomerOrder?> GetCustomerOrderByIdAsync(int id)
        {
            return await _context.CustomerOrders
                .Include(o => o.Items)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<string> GenerateNextOrderNumberAsync()
        {
            var existingNumbers = await _context.CustomerOrders
                .AsNoTracking()
                .Select(o => o.OrderNumber)
                .ToListAsync();

            int maxNum = 0;
            foreach (var numStr in existingNumbers)
            {
                if (!string.IsNullOrWhiteSpace(numStr) && numStr.StartsWith("CO-", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(numStr.Substring(3), out int num))
                    {
                        if (num > maxNum) maxNum = num;
                    }
                }
            }

            return $"CO-{(maxNum + 1):D5}";
        }

        public async Task<CustomerOrder> SaveCustomerOrderAsync(CustomerOrder order)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));

            // Clean line items and calculate totals safely
            order.CustomerName ??= string.Empty;
            order.Address ??= string.Empty;
            order.ContactNumber ??= string.Empty;
            order.Status = string.IsNullOrWhiteSpace(order.Status) ? "Pending" : order.Status;

            decimal totalAmount = 0m;
            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    item.ItemNameSnapshot ??= string.Empty;
                    item.ItemCode ??= string.Empty;
                    item.Unit ??= string.Empty;
                    if (item.Quantity < 0) item.Quantity = 0;
                    if (item.Rate < 0) item.Rate = 0;
                    item.LineTotal = item.Quantity * item.Rate;
                    totalAmount += item.LineTotal;
                }
            }

            order.TotalAmount = totalAmount;
            order.UpdatedAt = DateTime.Now;

            if (order.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(order.OrderNumber))
                {
                    order.OrderNumber = await GenerateNextOrderNumberAsync();
                }
                order.CreatedAt = DateTime.Now;
                _context.CustomerOrders.Add(order);
            }
            else
            {
                var existingOrder = await _context.CustomerOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == order.Id);

                if (existingOrder != null)
                {
                    existingOrder.OrderNumber = order.OrderNumber;
                    existingOrder.CustomerName = order.CustomerName;
                    existingOrder.Address = order.Address;
                    existingOrder.ContactNumber = order.ContactNumber;
                    existingOrder.OrderDate = order.OrderDate;
                    existingOrder.ReceivingDate = order.ReceivingDate;
                    existingOrder.Status = order.Status;
                    existingOrder.TotalAmount = order.TotalAmount;
                    existingOrder.UpdatedAt = DateTime.Now;

                    _context.CustomerOrderItems.RemoveRange(existingOrder.Items);

                    existingOrder.Items = new System.Collections.ObjectModel.ObservableCollection<CustomerOrderItem>();
                    if (order.Items != null)
                    {
                        foreach (var item in order.Items)
                        {
                            item.Id = 0; // Reset Id for new insertion
                            item.CustomerOrderId = existingOrder.Id;
                            existingOrder.Items.Add(item);
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return order;
        }

        public async Task DeleteCustomerOrderAsync(int id)
        {
            var order = await _context.CustomerOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null)
            {
                _context.CustomerOrders.Remove(order);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<CustomerOrder?> ToggleOrderStatusAsync(int id)
        {
            var order = await _context.CustomerOrders
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null)
            {
                order.Status = order.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ? "Pending" : "Completed";
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return order;
        }
    }
}
