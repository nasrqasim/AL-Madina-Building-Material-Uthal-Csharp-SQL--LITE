namespace AlMadinaERP.Core.Models
{
    public class Unit
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
    }

    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Subcategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public Category? Category { get; set; }
    }

    public class Item
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SellingUnit { get; set; } = "Per Piece";
        
        public string BaseUnit { get; set; } = "Per Piece";
        public string PurchaseUnitName { get; set; } = "Per Piece";
        public string SaleUnitName { get; set; } = "Per Piece";
        public double ConversionFactor { get; set; } = 1.0;

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        public int? SubcategoryId { get; set; }
        public Subcategory? Subcategory { get; set; }
        public string SubcategoryName { get; set; } = string.Empty;

        public int? PurchaseUnitId { get; set; }
        public Unit? PurchaseUnit { get; set; }

        public int? SaleUnitId { get; set; }
        public Unit? SaleUnit { get; set; }

        // Specifications
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Dimensions { get; set; } = string.Empty;
        public string Thickness { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public string Length { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public string WeightKg { get; set; } = string.Empty;
        public string Quality { get; set; } = "Premium";

        // Pricing & Taxes
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; } // Retail Sale Price
        public decimal WholesalePrice { get; set; }
        public decimal DealerPrice { get; set; }
        public decimal ContractPrice { get; set; }
        public double SalesTaxPercent { get; set; } = 0.0;
        public double DefaultDiscountPercent { get; set; } = 0.0;
        public string Status { get; set; } = "Active";

        // Inventory & Locations
        public decimal OpeningStock { get; set; }
        public decimal StockIn { get; set; }
        public decimal StockOut { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ReservedStock { get; set; }
        public decimal AvailableStock => CurrentStock - ReservedStock;

        public decimal LowStockAlert { get; set; } = 5; // Min Stock Reorder
        public decimal MaxStockLimit { get; set; } = 1000;
        public string Warehouse { get; set; } = "Godown A";
        public string LocationAisle { get; set; } = string.Empty;
        public string RackNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public System.DateTime CreatedDate { get; set; } = System.DateTime.Now;
        public System.DateTime LastUpdated { get; set; } = System.DateTime.Now;

        public bool IsActive { get; set; } = true;
    }
}
