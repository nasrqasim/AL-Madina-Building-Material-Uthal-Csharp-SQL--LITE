using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Core.Enums;

namespace AlMadinaERP.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Vendor> Vendors => Set<Vendor>();
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Subcategory> Subcategories => Set<Subcategory>();
        public DbSet<Item> Items => Set<Item>();
        public DbSet<SaleInvoice> SaleInvoices => Set<SaleInvoice>();
        public DbSet<SaleInvoiceItem> SaleInvoiceItems => Set<SaleInvoiceItem>();
        public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
        public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
        public DbSet<CustomerLedger> CustomerLedgers => Set<CustomerLedger>();
        public DbSet<VendorLedger> VendorLedgers => Set<VendorLedger>();
        public DbSet<InventoryLedger> InventoryLedgers => Set<InventoryLedger>();
        public DbSet<Receipt> Receipts => Set<Receipt>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Bank> Banks => Set<Bank>();
        public DbSet<AccountCategory> AccountCategories => Set<AccountCategory>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<Salary> Salaries => Set<Salary>();
        public DbSet<SalaryAdvance> SalaryAdvances => Set<SalaryAdvance>();
        public DbSet<Staff> Staffs => Set<Staff>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();
        public DbSet<User> Users => Set<User>();
        public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
        public DbSet<CustomerOrderItem> CustomerOrderItems => Set<CustomerOrderItem>();

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                var dbPath = Path.Combine(folder, "Company.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath};Foreign Keys=True;")
                              .AddInterceptors(new SqlitePragmasInterceptor());
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes for extreme performance
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Name);
            modelBuilder.Entity<Customer>()
                .HasIndex(c => c.Code);

            modelBuilder.Entity<Vendor>()
                .HasIndex(v => v.Name);
            modelBuilder.Entity<Vendor>()
                .HasIndex(v => v.Code);

            modelBuilder.Entity<Item>()
                .HasIndex(i => i.Name);
            modelBuilder.Entity<Item>()
                .HasIndex(i => i.Code);

            modelBuilder.Entity<SaleInvoice>()
                .HasIndex(s => s.InvoiceNumber);
            modelBuilder.Entity<SaleInvoice>()
                .HasIndex(s => s.Date);

            modelBuilder.Entity<PurchaseInvoice>()
                .HasIndex(p => p.PurchaseNumber);
            modelBuilder.Entity<PurchaseInvoice>()
                .HasIndex(p => p.Date);

            modelBuilder.Entity<CustomerLedger>()
                .HasIndex(cl => new { cl.CustomerId, cl.Date });

            modelBuilder.Entity<VendorLedger>()
                .HasIndex(vl => new { vl.VendorId, vl.Date });

            modelBuilder.Entity<SaleInvoiceItem>()
                .HasIndex(sii => new { sii.SaleInvoiceId, sii.ItemId });
            modelBuilder.Entity<SaleInvoiceItem>()
                .HasIndex(sii => sii.ItemId);

            modelBuilder.Entity<CustomerOrderItem>()
                .HasIndex(coi => coi.ItemId);

            modelBuilder.Entity<PurchaseInvoiceItem>()
                .HasIndex(pii => new { pii.PurchaseInvoiceId, pii.ItemId });

            modelBuilder.Entity<Receipt>()
                .HasIndex(r => new { r.CustomerId, r.Date });
            modelBuilder.Entity<Receipt>()
                .HasIndex(r => new { r.PaymentMethod, r.Date });

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.VendorId, p.Date });
            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.PaymentMethod, p.Date });

            modelBuilder.Entity<Expense>()
                .HasIndex(e => new { e.Date, e.Category });

            modelBuilder.Entity<Salary>()
                .HasIndex(s => new { s.StaffId, s.Date });

            // Decimal precision / SQLite numeric storage
            modelBuilder.Entity<Customer>().Property(c => c.OwesAmount).HasConversion<double>();
            modelBuilder.Entity<Customer>().Property(c => c.AdvanceAvailable).HasConversion<double>();
            modelBuilder.Entity<Vendor>().Property(v => v.OwesAmount).HasConversion<double>();
            modelBuilder.Entity<Vendor>().Property(v => v.AdvanceAvailable).HasConversion<double>();

            // Seed default Units
            modelBuilder.Entity<Item>(entity =>
            {
                entity.HasOne(i => i.PurchaseUnit)
                      .WithMany()
                      .HasForeignKey(i => i.PurchaseUnitId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(i => i.SaleUnit)
                      .WithMany()
                      .HasForeignKey(i => i.SaleUnitId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Ignore(i => i.AvailableStock);
            });

            modelBuilder.Entity<InventoryLedger>(entity =>
            {
                entity.HasOne(il => il.Item)
                      .WithMany()
                      .HasForeignKey(il => il.ItemId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(il => new { il.ItemId, il.Date });
            });

            modelBuilder.Entity<Unit>().HasData(
                new Unit { Id = 1, Name = "Bag", ShortCode = "Bag" },
                new Unit { Id = 2, Name = "Kilogram", ShortCode = "Kg" },
                new Unit { Id = 3, Name = "Cubic Feet", ShortCode = "CFT" },
                new Unit { Id = 4, Name = "Piece", ShortCode = "Pcs" },
                new Unit { Id = 5, Name = "Box", ShortCode = "Box" },
                new Unit { Id = 6, Name = "Meter", ShortCode = "Mtr" },
                new Unit { Id = 7, Name = "Feet", ShortCode = "Ft" },
                new Unit { Id = 8, Name = "Square Feet", ShortCode = "SqFt" },
                new Unit { Id = 9, Name = "Litre", ShortCode = "Ltr" },
                new Unit { Id = 10, Name = "Ton", ShortCode = "Ton" },
                new Unit { Id = 11, Name = "Sheet", ShortCode = "Sht" }
            );

            // Seed default Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Cement & Aggregates" },
                new Category { Id = 2, Name = "Steel & Metal" },
                new Category { Id = 3, Name = "Paints & Chemicals" },
                new Category { Id = 4, Name = "Sanitary & Plumbing" },
                new Category { Id = 5, Name = "Tiles & Flooring" }
            );

            // Seed Default Company Setting
            modelBuilder.Entity<CompanySetting>().HasData(
                new CompanySetting
                {
                    Id = 1,
                    CompanyName = "AL Madina Building Material Uthal",
                    Tagline = "Wholesale & Retail Building Material Supplies",
                    Phone = "03351279963",
                    Address = "Main Bazaar, Uthal, District Lasbela, Balochistan",
                    InvoicePrefix = "INV",
                    PurchasePrefix = "PUR",
                    ReceiptPrefix = "RCT",
                    PaymentPrefix = "PAY",
                    VoucherPrefix = "VCH",
                    FinancialYearStart = new DateTime(2026, 1, 1),
                    FinancialYearEnd = new DateTime(2026, 12, 31),
                    HeaderNotes = "Bismillah-ir-Rahman-ir-Rahim",
                    FooterNotes = "Thank you for choosing AL Madina Building Material Uthal!",
                    AutoBackupDaily = true
                }
            );

            // Seed Default Admin User (Password: admin1234)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "9joSQJEtDxGqAaiwtw+KV0CncXvuzj3LxwpMILDEDSY=",
                    FullName = "System Administrator",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1)
                }
            );

            // Seed Default Bank
            modelBuilder.Entity<Bank>().HasData(
                new Bank
                {
                    Id = 1,
                    BankName = "Meezan Bank",
                    AccountName = "AL Madina Building Material",
                    AccountNumber = "01010102030405",
                    Branch = "Uthal Branch",
                    CurrentBalance = 0,
                    IsActive = true
                }
            );
        }

        public void EnableOptimizations()
        {
            try
            {
                Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
                Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
                Database.ExecuteSqlRaw("PRAGMA cache_size=-64000;");
                Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
                Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");
                Database.ExecuteSqlRaw("PRAGMA optimize;");

                // Auto-migrate newly added columns
                EnsureSchemaUpToDate();
            }
            catch
            {
                // Fallback gracefully if database initialization handles pragmas automatically
            }
        }

        private void EnsureSchemaUpToDate()
        {
            string[] migrationSql = new string[]
            {
                // Customers
                "ALTER TABLE Customers ADD COLUMN ContactPerson TEXT DEFAULT '';",
                "ALTER TABLE Customers ADD COLUMN Category TEXT DEFAULT 'Cash Customer';",
                "ALTER TABLE Customers ADD COLUMN NTN TEXT DEFAULT '';",
                "ALTER TABLE Customers ADD COLUMN STRN TEXT DEFAULT '';",
                "ALTER TABLE Customers ADD COLUMN Region TEXT DEFAULT 'Select Region';",
                "ALTER TABLE Customers ADD COLUMN Area TEXT DEFAULT '';",
                "ALTER TABLE Customers ADD COLUMN PostalCode TEXT DEFAULT '';",
                "ALTER TABLE Customers ADD COLUMN Country TEXT DEFAULT 'Pakistan';",
                "ALTER TABLE Customers ADD COLUMN CreditLimit REAL DEFAULT 0;",
                "ALTER TABLE Customers ADD COLUMN CreditDays INTEGER DEFAULT 30;",
                "ALTER TABLE Customers ADD COLUMN Notes TEXT DEFAULT '';",

                // Staffs
                "ALTER TABLE Staffs ADD COLUMN JoiningDate TEXT DEFAULT CURRENT_TIMESTAMP;",

                // Vendors
                "ALTER TABLE Vendors ADD COLUMN ContactPerson TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN VendorType TEXT DEFAULT 'Supplier';",
                "ALTER TABLE Vendors ADD COLUMN NTN TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN GSTNumber TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN Region TEXT DEFAULT 'Select Region';",
                "ALTER TABLE Vendors ADD COLUMN Area TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN PostalCode TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN Country TEXT DEFAULT 'Pakistan';",
                "ALTER TABLE Vendors ADD COLUMN CreditLimit REAL DEFAULT 0;",
                "ALTER TABLE Vendors ADD COLUMN CreditDays INTEGER DEFAULT 30;",
                "ALTER TABLE Vendors ADD COLUMN BankName TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN BankAccountNumber TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN BankBranch TEXT DEFAULT '';",
                "ALTER TABLE Vendors ADD COLUMN DeductWithholdingTax INTEGER DEFAULT 0;",
                "ALTER TABLE Vendors ADD COLUMN Notes TEXT DEFAULT '';",

                // Items
                "ALTER TABLE Items ADD COLUMN CategoryName TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN SubcategoryName TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN SellingUnit TEXT DEFAULT 'Per Piece';",
                "ALTER TABLE Items ADD COLUMN BaseUnit TEXT DEFAULT 'Per Piece';",
                "ALTER TABLE Items ADD COLUMN PurchaseUnitName TEXT DEFAULT 'Per Piece';",
                "ALTER TABLE Items ADD COLUMN SaleUnitName TEXT DEFAULT 'Per Piece';",
                "ALTER TABLE Items ADD COLUMN ConversionFactor REAL DEFAULT 1.0;",
                "ALTER TABLE Items ADD COLUMN Brand TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Model TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Color TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Thickness TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Grade TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Length TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Width TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN WeightKg TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Barcode TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Quality TEXT DEFAULT 'Premium';",
                "ALTER TABLE Items ADD COLUMN WholesalePrice REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN DealerPrice REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN ContractPrice REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN SalesTaxPercent REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN DefaultDiscountPercent REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN Status TEXT DEFAULT 'Active';",
                "ALTER TABLE Items ADD COLUMN MaxStockLimit REAL DEFAULT 1000;",
                "ALTER TABLE Items ADD COLUMN Warehouse TEXT DEFAULT 'Godown A';",
                "ALTER TABLE Items ADD COLUMN LocationAisle TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN RackNumber TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Notes TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN Description TEXT DEFAULT '';",
                "ALTER TABLE Items ADD COLUMN StockIn REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN StockOut REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN ReservedStock REAL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN CreatedDate TEXT DEFAULT '2026-01-01 00:00:00';",
                "ALTER TABLE Items ADD COLUMN LastUpdated TEXT DEFAULT '2026-01-01 00:00:00';",
                "UPDATE Items SET CreatedDate = '2026-01-01 00:00:00' WHERE CreatedDate IS NULL OR CreatedDate = '';",
                "UPDATE Items SET LastUpdated = '2026-01-01 00:00:00' WHERE LastUpdated IS NULL OR LastUpdated = '';",

                // InventoryLedgers table
                @"CREATE TABLE IF NOT EXISTS InventoryLedgers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ItemId INTEGER NOT NULL,
                    ItemCode TEXT DEFAULT '',
                    ItemName TEXT DEFAULT '',
                    Date TEXT NOT NULL,
                    VoucherNumber TEXT DEFAULT '',
                    TransactionType TEXT DEFAULT '',
                    Unit TEXT DEFAULT '',
                    QuantityIn REAL DEFAULT 0,
                    QuantityOut REAL DEFAULT 0,
                    RunningBalance REAL DEFAULT 0,
                    Warehouse TEXT DEFAULT 'Godown A',
                    User TEXT DEFAULT 'Admin',
                    Reference TEXT DEFAULT '',
                    Remarks TEXT DEFAULT '',
                    PurchaseInvoiceId INTEGER,
                    SaleInvoiceId INTEGER,
                    FOREIGN KEY (ItemId) REFERENCES Items(Id)
                );",

                // Banks
                "ALTER TABLE Banks ADD COLUMN Code TEXT DEFAULT '';",
                "ALTER TABLE Banks ADD COLUMN BranchCode TEXT DEFAULT '';",
                "ALTER TABLE Banks ADD COLUMN AccountType TEXT DEFAULT 'Current Account';",
                "ALTER TABLE Banks ADD COLUMN IBAN TEXT DEFAULT '';",
                "ALTER TABLE Banks ADD COLUMN SwiftCode TEXT DEFAULT '';",
                "ALTER TABLE Banks ADD COLUMN IsDefault INTEGER DEFAULT 0;",

                // Expenses
                "ALTER TABLE Expenses ADD COLUMN VoucherNumber TEXT DEFAULT '';",
                "ALTER TABLE Expenses ADD COLUMN Category TEXT DEFAULT 'Utility';",
                "ALTER TABLE Expenses ADD COLUMN ExpenseType TEXT DEFAULT 'Operating';",
                "ALTER TABLE Expenses ADD COLUMN Title TEXT DEFAULT '';",
                "ALTER TABLE Expenses ADD COLUMN PaidFrom TEXT DEFAULT 'Cash';",
                "ALTER TABLE Expenses ADD COLUMN ReferenceNumber TEXT DEFAULT '';",
                "ALTER TABLE Expenses ADD COLUMN Status TEXT DEFAULT 'Paid';",
                "ALTER TABLE Expenses ADD COLUMN Notes TEXT DEFAULT '';",

                // PurchaseInvoices
                "ALTER TABLE PurchaseInvoices ADD COLUMN VendorInvoiceNo TEXT DEFAULT '';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN VendorInvoiceDate TEXT DEFAULT NULL;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN DueDate TEXT DEFAULT NULL;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN PaymentTerms TEXT DEFAULT 'net 30 days';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Job TEXT DEFAULT 'General Job';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Location TEXT DEFAULT 'Main Warehouse';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Status TEXT DEFAULT 'Draft';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Currency TEXT DEFAULT 'PKR';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN LinkedRef TEXT DEFAULT '';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Reason TEXT DEFAULT '';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN IsCashPurchase INTEGER DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN PaymentMethod TEXT DEFAULT 'Cash';",
                "ALTER TABLE PurchaseInvoices ADD COLUMN AmountPaid REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN BalanceDue REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Subtotal REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN DiscountAmount REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN TaxAmount REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN ExtraExpenses REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN VehicleCharges REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN TotalAmount REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN AdvanceUsed REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN OutstandingAmount REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN CreatedByUserId INTEGER DEFAULT 0;",
                "ALTER TABLE PurchaseInvoices ADD COLUMN Remarks TEXT DEFAULT '';",

                // PurchaseInvoiceItems
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN ItemCode TEXT DEFAULT '';",
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN DiscountPercent REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN DiscountAmount REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN TaxPercent REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN TaxAmount REAL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN Reason TEXT DEFAULT '';",

                // SaleInvoices
                "ALTER TABLE SaleInvoices ADD COLUMN IsCashSale INTEGER DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN VehicleNo TEXT DEFAULT '';",
                "ALTER TABLE SaleInvoices ADD COLUMN DriverKm TEXT DEFAULT '';",
                "ALTER TABLE SaleInvoices ADD COLUMN SaleCategory TEXT DEFAULT 'Casual';",
                "ALTER TABLE SaleInvoices ADD COLUMN AgainstInvoiceNo TEXT DEFAULT '';",
                "ALTER TABLE SaleInvoices ADD COLUMN Salesman TEXT DEFAULT 'Admin';",
                "ALTER TABLE SaleInvoices ADD COLUMN Location TEXT DEFAULT 'Main Warehouse';",
                "ALTER TABLE SaleInvoices ADD COLUMN Employee TEXT DEFAULT 'System Admin';",
                "ALTER TABLE SaleInvoices ADD COLUMN Status TEXT DEFAULT 'Posted';",
                "ALTER TABLE SaleInvoices ADD COLUMN Subtotal REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN DiscountAmount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN ExtraCharges REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN VehicleCharges REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN TotalAmount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN GrossRefund REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN AdditionalDiscount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN CarServiceCharge REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN CarWashDiscount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN NetRefund REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN AmountRefunded REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN PaidAmount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN AdvanceUsed REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN OutstandingAmount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN DeliveryStatus INTEGER DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN CreatedByUserId INTEGER DEFAULT 0;",
                "ALTER TABLE SaleInvoices ADD COLUMN Remarks TEXT DEFAULT '';",
                "ALTER TABLE SaleInvoices ADD COLUMN Notes TEXT DEFAULT '';",

                // SaleInvoiceItems
                "ALTER TABLE SaleInvoiceItems ADD COLUMN ItemCode TEXT DEFAULT '';",
                "ALTER TABLE SaleInvoiceItems ADD COLUMN DiscountPercent REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoiceItems ADD COLUMN DiscountAmount REAL DEFAULT 0;",
                "ALTER TABLE SaleInvoiceItems ADD COLUMN Reason TEXT DEFAULT '';",

                // Receipts
                "ALTER TABLE Receipts ADD COLUMN BankName TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN BankAccountNo TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN ChequeNo TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN ReceivedBy TEXT DEFAULT 'Cash';",
                "ALTER TABLE Receipts ADD COLUMN IsAdvance INTEGER DEFAULT 0;",
                "ALTER TABLE Receipts ADD COLUMN Status TEXT DEFAULT 'Posted';",
                "ALTER TABLE Receipts ADD COLUMN ReferenceNumber TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN Description TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN InternalNotes TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN IncomeTitle TEXT DEFAULT '';",
                "ALTER TABLE Receipts ADD COLUMN IncomeType TEXT DEFAULT 'One Time';",

                // Payments
                "ALTER TABLE Payments ADD COLUMN PaymentCategory TEXT DEFAULT 'Party Payment';",
                "ALTER TABLE Payments ADD COLUMN PayToCategory TEXT DEFAULT 'Vendor';",
                "ALTER TABLE Payments ADD COLUMN WhtRatePercent REAL DEFAULT 0;",
                "ALTER TABLE Payments ADD COLUMN WhtAmount REAL DEFAULT 0;",
                "ALTER TABLE Payments ADD COLUMN NetAmountToPay REAL DEFAULT 0;",
                "ALTER TABLE Payments ADD COLUMN BankName TEXT DEFAULT '';",
                "ALTER TABLE Payments ADD COLUMN BankAccountNo TEXT DEFAULT '';",
                "ALTER TABLE Payments ADD COLUMN ChequeNo TEXT DEFAULT '';",
                "ALTER TABLE Payments ADD COLUMN ChequeDate TEXT DEFAULT NULL;",
                "ALTER TABLE Payments ADD COLUMN PaidFrom TEXT DEFAULT 'Cashier / Counter';",
                "ALTER TABLE Payments ADD COLUMN IsAdvance INTEGER DEFAULT 0;",
                "ALTER TABLE Payments ADD COLUMN Narration TEXT DEFAULT '';",
                "ALTER TABLE Payments ADD COLUMN Remarks TEXT DEFAULT '';",
                "ALTER TABLE Payments ADD COLUMN ReferenceNumber TEXT DEFAULT '';",
                "ALTER TABLE Payments ADD COLUMN Status TEXT DEFAULT 'Posted';",
                "ALTER TABLE Payments ADD COLUMN InternalNotes TEXT DEFAULT '';",

                // Salaries
                "ALTER TABLE Salaries ADD COLUMN StaffId INTEGER NULL;",

                // SaleInvoiceItems
                "ALTER TABLE SaleInvoiceItems ADD COLUMN IsReceived INTEGER NOT NULL DEFAULT 1;",
                "ALTER TABLE SaleInvoiceItems ADD COLUMN LengthFeet REAL NOT NULL DEFAULT 0;",
                "ALTER TABLE SaleInvoiceItems ADD COLUMN RatePerFoot REAL NOT NULL DEFAULT 0;",

                // PurchaseInvoiceItems
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN LengthFeet REAL NOT NULL DEFAULT 0;",
                "ALTER TABLE PurchaseInvoiceItems ADD COLUMN RatePerFoot REAL NOT NULL DEFAULT 0;",

                // Items
                "ALTER TABLE Items ADD COLUMN LengthFeet REAL NOT NULL DEFAULT 0;",
                "ALTER TABLE Items ADD COLUMN RatePerFoot REAL NOT NULL DEFAULT 0;"
            };

            foreach (var sql in migrationSql)
            {
                try
                {
                    Database.ExecuteSqlRaw(sql);
                }
                catch
                {
                    // Ignore column already exists errors
                }
            }

            try
            {
                // Auto-link unlinked PurchaseInvoices with Vendors by Name match
                Database.ExecuteSqlRaw(@"UPDATE PurchaseInvoices 
                    SET VendorId = (SELECT v.Id FROM Vendors v WHERE LOWER(TRIM(v.Name)) = LOWER(TRIM(PurchaseInvoices.VendorName)) LIMIT 1)
                    WHERE (VendorId IS NULL OR VendorId <= 0) AND VendorName IS NOT NULL AND VendorName != '' AND VendorName != 'Direct / Walk-in Purchase (No Vendor)';"
                );

                // Auto-link unlinked SaleInvoices with Customers by Name match
                Database.ExecuteSqlRaw(@"UPDATE SaleInvoices 
                    SET CustomerId = (SELECT c.Id FROM Customers c WHERE LOWER(TRIM(c.Name)) = LOWER(TRIM(SaleInvoices.CustomerName)) LIMIT 1)
                    WHERE (CustomerId IS NULL OR CustomerId <= 0) AND CustomerName IS NOT NULL AND CustomerName != '' AND CustomerName != 'WALK-IN CUSTOMER';"
                );
            }
            catch
            {
            }
        }
    }

    public class SingleInstanceDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly AppDbContext _context;
        public SingleInstanceDbContextFactory(AppDbContext context) => _context = context;
        public AppDbContext CreateDbContext() => _context;
    }

    public class SqlitePragmasInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
    {
        public override void ConnectionOpened(System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY;";
            command.ExecuteNonQuery();
        }

        public override async Task ConnectionOpenedAsync(System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData, System.Threading.CancellationToken cancellationToken = default)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout = 5000; PRAGMA synchronous = NORMAL; PRAGMA temp_store = MEMORY;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
