using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlMadinaERP.Core.Models
{
    public class CustomerOrder
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public DateTime? ReceivingDate { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal TotalAmount { get; set; } = 0m;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ObservableCollection<CustomerOrderItem> Items { get; set; } = new();

        [NotMapped]
        public int TotalItems => Items?.Count ?? 0;
    }

    public class CustomerOrderItem
    {
        public int Id { get; set; }
        public int CustomerOrderId { get; set; }
        public int? ItemId { get; set; }
        public string ItemNameSnapshot { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 0m;
        public decimal Rate { get; set; } = 0m;
        public decimal LineTotal { get; set; } = 0m;
    }
}
