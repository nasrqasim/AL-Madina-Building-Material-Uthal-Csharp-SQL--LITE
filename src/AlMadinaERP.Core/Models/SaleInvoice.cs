using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using AlMadinaERP.Core.Enums;

namespace AlMadinaERP.Core.Models
{
    public class SaleInvoice : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string VoucherNumber { get; set; } = string.Empty;
        public InvoiceType Type { get; set; } = InvoiceType.SaleInvoice;
        
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string CustomerName { get; set; } = string.Empty;

        [NotMapped]
        public string DisplayCustomerName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomerName)) return CustomerName;
                if (Customer != null && !string.IsNullOrWhiteSpace(Customer.Name)) return Customer.Name;
                return "WALK-IN CUSTOMER";
            }
        }
        
        public bool IsCashSale { get; set; } = false;
        public string VehicleNo { get; set; } = string.Empty;
        public string DriverKm { get; set; } = string.Empty;
        public string SaleCategory { get; set; } = "Casual";
        public string AgainstInvoiceNo { get; set; } = string.Empty;
        public string Salesman { get; set; } = "Admin";
        public string Location { get; set; } = "Main Warehouse";
        public string Employee { get; set; } = "System Admin";
        public string Status { get; set; } = "Posted";
        
        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set { if (_subtotal == value) return; _subtotal = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set { if (_discountAmount == value) return; _discountAmount = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _extraCharges;
        public decimal ExtraCharges
        {
            get => _extraCharges;
            set { if (_extraCharges == value) return; _extraCharges = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _vehicleCharges;
        public decimal VehicleCharges
        {
            get => _vehicleCharges;
            set { if (_vehicleCharges == value) return; _vehicleCharges = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _additionalDiscount;
        public decimal AdditionalDiscount
        {
            get => _additionalDiscount;
            set { if (_additionalDiscount == value) return; _additionalDiscount = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set { if (_totalAmount == value) return; _totalAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(BalanceDue)); }
        }

        public decimal NetAmount => TotalAmount;

        public void RecalculateTotals()
        {
            TotalAmount = Math.Max(0m, (Subtotal - DiscountAmount) + ExtraCharges + VehicleCharges - AdditionalDiscount);
            GrossRefund = TotalAmount;
            NetRefund = Math.Max(0m, GrossRefund - AdditionalDiscount - CarServiceCharge + CarWashDiscount);
        }
        
        // Refund Specific Fields (Sale Return)
        public decimal GrossRefund { get; set; }
        public decimal CarServiceCharge { get; set; }
        public decimal CarWashDiscount { get; set; }
        public decimal NetRefund { get; set; }
        public decimal AmountRefunded { get; set; }
        
        private decimal _paidAmount;
        public decimal PaidAmount
        {
            get => _paidAmount;
            set { if (_paidAmount == value) return; _paidAmount = value; OnPropertyChanged(); OnPropertyChanged(nameof(BalanceDue)); }
        }

        public decimal BalanceDue => Math.Max(0m, TotalAmount - PaidAmount - AdvanceUsed);
        public string PaymentTerms => IsCashSale ? "Cash" : "Credit Sale";
        public string PaymentMethod => IsCashSale ? "Cash" : "Credit";

        public decimal AdvanceUsed { get; set; }
        public decimal OutstandingAmount { get; set; }
        
        public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Received;
        public DateTime Date { get; set; } = DateTime.Now;
        
        public int CreatedByUserId { get; set; }
        public string Remarks { get; set; } = string.Empty;

        [NotMapped]
        public string Notes { get => Remarks; set => Remarks = value; }

        [NotMapped]
        public string AmountInWords
        {
            get
            {
                var val = (long)Math.Round(TotalAmount > 0 ? TotalAmount : NetRefund);
                if (val <= 0) return "Rupees Zero Only.";
                return $"Rupees {NumberToWords(val)} Only.";
            }
        }

        private static string NumberToWords(long number)
        {
            if (number == 0) return "Zero";
            if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));
            string words = "";
            if ((number / 10000000) > 0) { words += NumberToWords(number / 10000000) + " Crore "; number %= 10000000; }
            if ((number / 100000) > 0) { words += NumberToWords(number / 100000) + " Lakh "; number %= 100000; }
            if ((number / 1000) > 0) { words += NumberToWords(number / 1000) + " Thousand "; number %= 1000; }
            if ((number / 100) > 0) { words += NumberToWords(number / 100) + " Hundred "; number %= 100; }
            if (number > 0)
            {
                var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
                var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
                if (number < 20) words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0) words += " " + unitsMap[number % 10];
                }
            }
            return words.Trim();
        }

        public ObservableCollection<SaleInvoiceItem> Items { get; set; } = new();
    }

    public class SaleInvoiceItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }
        public int SaleInvoiceId { get; set; }
        public SaleInvoice? SaleInvoice { get; set; }
        
        public int ItemId { get; set; }
        
        private Item? _item;
        public Item? Item
        {
            get => _item;
            set
            {
                _item = value;
                if (value != null)
                {
                    ItemId = value.Id;
                    ItemCode = value.Code;
                    ItemName = value.Name;
                    UnitName = !string.IsNullOrWhiteSpace(value.BaseUnit) ? value.BaseUnit :
                               !string.IsNullOrWhiteSpace(value.SaleUnitName) ? value.SaleUnitName :
                               !string.IsNullOrWhiteSpace(value.SellingUnit) ? value.SellingUnit : "Pcs";
                    Rate = value.SalePrice;
                    AvailableStock = value.CurrentStock;
                    ItemWarehouse = value.Warehouse;
                    ItemDescription = value.Description;
                    Recalculate();
                }
                OnPropertyChanged(nameof(Item));
                OnPropertyChanged(nameof(ItemCode));
                OnPropertyChanged(nameof(ItemName));
                OnPropertyChanged(nameof(UnitName));
                OnPropertyChanged(nameof(Rate));
                OnPropertyChanged(nameof(AvailableStock));
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        private string _itemCode = string.Empty;
        public string ItemCode
        {
            get => _itemCode;
            set { if (_itemCode == value) return; _itemCode = value; OnPropertyChanged(); }
        }

        private string _itemName = string.Empty;
        public string ItemName
        {
            get => _itemName;
            set { if (_itemName == value) return; _itemName = value; OnPropertyChanged(); }
        }
        
        private string _unitName = "Pcs";
        public string UnitName
        {
            get => _unitName;
            set { if (_unitName == value) return; _unitName = value; OnPropertyChanged(); }
        }

        [NotMapped]
        public decimal AvailableStock { get; set; }
        [NotMapped]
        public string ItemWarehouse { get; set; } = string.Empty;
        [NotMapped]
        public string ItemDescription { get; set; } = string.Empty;

        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                Recalculate();
                OnPropertyChanged();
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
                Recalculate();
                OnPropertyChanged();
            }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent == value) return;
                _discountPercent = value;
                Recalculate();
                OnPropertyChanged();
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set { if (_discountAmount == value) return; _discountAmount = value; OnPropertyChanged(); }
        }

        private string _reason = string.Empty;
        public string Reason
        {
            get => _reason;
            set { if (_reason == value) return; _reason = value; OnPropertyChanged(); }
        }

        private decimal _totalPrice;
        public decimal TotalPrice
        {
            get => _totalPrice;
            set { if (_totalPrice == value) return; _totalPrice = value; OnPropertyChanged(); }
        }

        private bool _isReceived = true;
        public bool IsReceived
        {
            get => _isReceived;
            set { if (_isReceived == value) return; _isReceived = value; OnPropertyChanged(); }
        }

        public void Recalculate()
        {
            var gross = Quantity * Rate;
            var disc = (gross * DiscountPercent) / 100m;
            var total = gross - disc;

            if (DiscountAmount != disc) DiscountAmount = disc;
            if (TotalPrice != total) TotalPrice = total;
        }
    }
}
