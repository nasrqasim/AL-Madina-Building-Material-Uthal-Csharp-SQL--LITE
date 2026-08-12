using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using AlMadinaERP.Core.Enums;

namespace AlMadinaERP.Core.Models
{
    public class PurchaseInvoice : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }
        public string PurchaseNumber { get; set; } = string.Empty;
        public string VoucherNumber { get; set; } = string.Empty;
        public PurchaseType Type { get; set; } = PurchaseType.PurchaseInvoice; // PurchaseInvoice or PurchaseReturn
        
        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }
        public string VendorName { get; set; } = string.Empty;

        [NotMapped]
        public string DisplayVendorName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(VendorName)) return VendorName;
                if (Vendor != null && !string.IsNullOrWhiteSpace(Vendor.Name)) return Vendor.Name;
                return "Direct / Walk-in Purchase (No Vendor)";
            }
        }
        
        public string VendorInvoiceNo { get; set; } = string.Empty;
        public DateTime? VendorInvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string PaymentTerms { get; set; } = "net 30 days";
        public string Job { get; set; } = "General Job";
        public string Location { get; set; } = "Main Warehouse";
        public string Status { get; set; } = "Draft"; // Draft, Posted, Paid
        public string Currency { get; set; } = "PKR";
        public string LinkedRef { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        
        public bool IsCashPurchase { get; set; } = false; // true = Cash, false = Credit
        public string PaymentMethod { get; set; } = "Cash";

        private decimal _amountPaid;
        public decimal AmountPaid
        {
            get => _amountPaid;
            set { if (_amountPaid == value) return; _amountPaid = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        public decimal BalanceDue { get; set; }

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

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            set { if (_taxAmount == value) return; _taxAmount = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _extraExpenses;
        public decimal ExtraExpenses
        {
            get => _extraExpenses;
            set { if (_extraExpenses == value) return; _extraExpenses = value; RecalculateTotals(); OnPropertyChanged(); }
        }

        private decimal _vehicleCharges;
        public decimal VehicleCharges
        {
            get => _vehicleCharges;
            set { if (_vehicleCharges == value) return; _vehicleCharges = value; RecalculateTotals(); OnPropertyChanged(); }
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
            TotalAmount = Math.Max(0m, (Subtotal - DiscountAmount) + TaxAmount + ExtraExpenses + VehicleCharges);
            BalanceDue = Math.Max(0m, TotalAmount - AmountPaid);
        }
        
        public decimal AdvanceUsed { get; set; }
        public decimal OutstandingAmount { get; set; }
        
        public DateTime Date { get; set; } = DateTime.Now;
        public int CreatedByUserId { get; set; }
        public string Remarks { get; set; } = string.Empty;
        
        public ObservableCollection<PurchaseInvoiceItem> Items { get; set; } = new();
    }

    public class PurchaseInvoiceItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; }
        public int PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }
        
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
                               !string.IsNullOrWhiteSpace(value.PurchaseUnitName) ? value.PurchaseUnitName :
                               !string.IsNullOrWhiteSpace(value.SellingUnit) ? value.SellingUnit : "Pcs";
                    Rate = value.PurchasePrice;
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

        private decimal _taxPercent;
        public decimal TaxPercent
        {
            get => _taxPercent;
            set
            {
                if (_taxPercent == value) return;
                _taxPercent = value;
                Recalculate();
                OnPropertyChanged();
            }
        }

        private decimal _taxAmount;
        public decimal TaxAmount
        {
            get => _taxAmount;
            set { if (_taxAmount == value) return; _taxAmount = value; OnPropertyChanged(); }
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

        public void Recalculate()
        {
            var gross = Quantity * Rate;
            var disc = (gross * DiscountPercent) / 100m;
            var tax = ((gross - disc) * TaxPercent) / 100m;
            var total = gross - disc + tax;

            if (DiscountAmount != disc) DiscountAmount = disc;
            if (TaxAmount != tax) TaxAmount = tax;
            if (TotalPrice != total) TotalPrice = total;
        }
    }
}
