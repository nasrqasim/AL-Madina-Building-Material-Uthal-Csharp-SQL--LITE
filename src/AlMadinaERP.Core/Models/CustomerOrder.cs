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
        public string Status { get; set; } = "Pending"; // Pending, Completed
        public decimal TotalAmount { get; set; } = 0m;
        public decimal PaidAmount { get; set; } = 0m;
        
        [NotMapped]
        public decimal RemainingAmount => Math.Max(0m, TotalAmount - PaidAmount);

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public ObservableCollection<CustomerOrderItem> Items { get; set; } = new();

        [NotMapped]
        public int TotalItems => Items?.Count ?? 0;
    }

    public class CustomerOrderItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }
        public int CustomerOrderId { get; set; }
        public int? ItemId { get; set; }
        public string ItemNameSnapshot { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        private decimal _quantity;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private double _lengthFeet;
        public double LengthFeet
        {
            get => _lengthFeet;
            set
            {
                if (_lengthFeet == value) return;
                _lengthFeet = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private decimal _rate;
        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value) return;
                _rate = value;
                OnPropertyChanged();
                Recalculate();
            }
        }

        private decimal _lineTotal;
        public decimal LineTotal
        {
            get => _lineTotal;
            set
            {
                if (_lineTotal == value) return;
                _lineTotal = value;
                OnPropertyChanged();
            }
        }

        [NotMapped]
        public bool IsLengthBased
        {
            get
            {
                var nameLower = (ItemNameSnapshot ?? "").ToLower();
                var unitLower = (Unit ?? "").ToLower();
                return nameLower.Contains("tear") || nameLower.Contains("girder") ||
                       unitLower.Contains("feet") || unitLower.Contains("foot") || LengthFeet > 0;
            }
        }

        public void Recalculate()
        {
            if (IsLengthBased && LengthFeet > 0)
            {
                LineTotal = Quantity * (decimal)LengthFeet * Rate;
            }
            else
            {
                LineTotal = Quantity * Rate;
            }
        }
    }
}
