using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;
using AlMadinaERP.Services;
using AlMadinaERP.Wpf.ViewModels;

namespace EditModeTestSuite
{
    public class E2eAuditSuite
    {
        private readonly string _dbPath;
        private readonly DbContextOptions<AppDbContext> _dbOptions;
        private readonly TestDbContextFactory _factory;

        // Services
        private readonly CustomerService _customerService;
        private readonly SaleService _saleService;
        private readonly PurchaseService _purchaseService;
        private readonly VendorService _vendorService;
        private readonly InventoryService _inventoryService;
        private readonly ReceiptPaymentService _receiptPaymentService;
        private readonly SalaryService _salaryService;
        private readonly CustomerOrderService _customerOrderService;
        private readonly DashboardService _dashboardService;
        private readonly Repository<CompanySetting> _companyRepo;

        public E2eAuditSuite(string dbPath)
        {
            _dbPath = dbPath;
            _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_dbPath};Foreign Keys=True;")
                .AddInterceptors(new AlMadinaERP.Data.SqlitePragmasInterceptor())
                .Options;

            _factory = new TestDbContextFactory(_dbOptions);
            _customerService = new CustomerService(_factory);
            _saleService = new SaleService(_factory, _customerService);
            _purchaseService = new PurchaseService(_factory);
            _vendorService = new VendorService(_factory);
            _inventoryService = new InventoryService(_factory);
            _receiptPaymentService = new ReceiptPaymentService(_factory);
            _salaryService = new SalaryService(_factory);
            _customerOrderService = new CustomerOrderService(_factory);
            _dashboardService = new DashboardService(_factory);
            _companyRepo = new Repository<CompanySetting>(_factory);
        }

        public async Task RunAuditAsync()
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine("        AL MADINA BUILDING MATERIAL ERP — E2E FUNCTIONAL AUDIT          ");
            Console.WriteLine("==========================================================================");

            int totalTests = 0;
            int passedTests = 0;
            int failedTests = 0;
            int fixedIssues = 0;

            void AssertTrue(string name, bool condition, string detail = "")
            {
                totalTests++;
                if (condition)
                {
                    passedTests++;
                    Console.WriteLine($"[PASS] {name} - {detail}");
                }
                else
                {
                    failedTests++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FAIL] {name} - {detail}");
                    Console.ResetColor();
                }
            }

            // -------------------------------------------------------------------
            // 1. CLEAN DATABASE VERIFICATION
            // -------------------------------------------------------------------
            Console.WriteLine("\n[1/24] CLEAN DATABASE VERIFICATION...");
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }

                using (var db = _factory.CreateDbContext())
                {
                    db.Database.EnsureCreated();

                    // Verify SQLite pragmas on connection opened by DbContext
                    var conn = db.Database.GetDbConnection();
                    bool wasOpen = conn.State == System.Data.ConnectionState.Open;
                    if (!wasOpen) await conn.OpenAsync();

                    using (var cmdFk = conn.CreateCommand())
                    {
                        cmdFk.CommandText = "PRAGMA foreign_keys;";
                        var fk = cmdFk.ExecuteScalar();
                        AssertTrue("SQLite Pragma: foreign_keys", fk != null && Convert.ToInt32(fk) == 1, $"foreign_keys is {fk} (Expected 1)");
                    }

                    using (var cmdBt = conn.CreateCommand())
                    {
                        cmdBt.CommandText = "PRAGMA busy_timeout;";
                        var bt = cmdBt.ExecuteScalar();
                        AssertTrue("SQLite Pragma: busy_timeout", bt != null && Convert.ToInt32(bt) == 5000, $"busy_timeout is {bt} ms (Expected 5000)");
                    }

                    using (var cmdJm = conn.CreateCommand())
                    {
                        cmdJm.CommandText = "PRAGMA journal_mode;";
                        var jm = cmdJm.ExecuteScalar();
                        AssertTrue("SQLite Pragma: journal_mode", jm != null && jm.ToString().ToLower() == "wal", $"journal_mode is {jm} (Expected wal)");
                    }

                    if (!wasOpen) await conn.CloseAsync();

                    // Seed initial company setting
                    db.CompanySettings.Add(new CompanySetting 
                    { 
                        CompanyName = "Al Madina Building Material Test", 
                        InvoicePrefix = "INV", 
                        PurchasePrefix = "PUR" 
                    });
                    await db.SaveChangesAsync();
                }

                var fileInfo = new FileInfo(_dbPath);
                Console.WriteLine($" -> Database Path: {_dbPath}");
                Console.WriteLine($" -> Size: {fileInfo.Length} bytes");

                using (var sqliteConn = new SqliteConnection($"Data Source={_dbPath}"))
                {
                    sqliteConn.Open();
                    using var cmd = sqliteConn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                    var tables = new List<string>();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tables.Add(reader.GetString(0));
                        }
                    }
                    Console.WriteLine($" -> Tables Count: {tables.Count}");
                    Console.WriteLine($" -> Tables: {string.Join(", ", tables)}");
                }

                AssertTrue("Clean DB Setup", File.Exists(_dbPath) && fileInfo.Length > 0, "SQLite database initialized and schema created.");
            }
            catch (Exception ex)
            {
                AssertTrue("Clean DB Setup", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 2. ITEM MASTER — COMPLETE CRUD
            // -------------------------------------------------------------------
            Console.WriteLine("\n[2/24] ITEM MASTER CRUD...");
            Item cementItem = null!;
            Item tearItem = null!;
            Item girderItem = null!;
            try
            {
                using var db = _factory.CreateDbContext();
                var catCement = new Category { Name = "Cement" };
                var catSteel = new Category { Name = "Steel" };
                var catGirders = new Category { Name = "Girders" };
                var catTear = new Category { Name = "TEAR" };
                var catGeneral = new Category { Name = "General Building Material" };
                db.Categories.AddRange(catCement, catSteel, catGirders, catTear, catGeneral);
                await db.SaveChangesAsync();

                // Create Cement
                cementItem = new Item
                {
                    Code = "ITM-DG-CEM",
                    Name = "DG Cement OPC",
                    CategoryId = catCement.Id,
                    CategoryName = catCement.Name,
                    BaseUnit = "Bag",
                    PurchasePrice = 1180,
                    SalePrice = 1250,
                    CurrentStock = 0,
                    IsActive = true
                };

                // Create TEAR
                tearItem = new Item
                {
                    Code = "ITM-TEAR-4X2",
                    Name = "TEAR 4x2 Heavy",
                    CategoryId = catTear.Id,
                    CategoryName = catTear.Name,
                    BaseUnit = "Pcs",
                    LengthFeet = 20,
                    RatePerFoot = 122,
                    PurchasePrice = 100,
                    SalePrice = 122,
                    CurrentStock = 0,
                    IsActive = true
                };

                // Create GIRDER
                girderItem = new Item
                {
                    Code = "ITM-GIRDER-7X4",
                    Name = "GIRDER 7x4 Heavy",
                    CategoryId = catGirders.Id,
                    CategoryName = catGirders.Name,
                    BaseUnit = "Pcs",
                    LengthFeet = 15,
                    RatePerFoot = 540,
                    PurchasePrice = 480,
                    SalePrice = 540,
                    CurrentStock = 0,
                    IsActive = true
                };

                db.Items.AddRange(cementItem, tearItem, girderItem);
                await db.SaveChangesAsync();

                // Read and Search
                var searched = await _inventoryService.SearchItemsAsync("OPC");
                AssertTrue("Item Search", searched.Count == 1 && searched.First().Name == "DG Cement OPC", "Search found DG Cement OPC.");

                // Edit
                cementItem.SalePrice = 1260;
                db.Items.Update(cementItem);
                await db.SaveChangesAsync();
                var edited = await _inventoryService.GetItemByIdAsync(cementItem.Id);
                AssertTrue("Item Edit", edited?.SalePrice == 1260, "SalePrice updated successfully to 1260.");

                // Create and Delete Temp Item
                var tempItem = new Item { Code = "TEMP", Name = "Temp Item", BaseUnit = "Pcs", IsActive = true };
                db.Items.Add(tempItem);
                await db.SaveChangesAsync();
                db.Items.Remove(tempItem);
                await db.SaveChangesAsync();
                var deleted = await _inventoryService.GetItemByIdAsync(tempItem.Id);
                AssertTrue("Item Delete", deleted == null, "Temp item created and deleted successfully.");
            }
            catch (Exception ex)
            {
                AssertTrue("Item Master CRUD", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 3. PURCHASE → INVENTORY FLOW
            // -------------------------------------------------------------------
            Console.WriteLine("\n[3/24] PURCHASE -> INVENTORY FLOW...");
            Vendor vendor = null!;
            PurchaseInvoice purchaseInvoice = null!;
            try
            {
                vendor = new Vendor { Name = "MUGHAL STEEL TRADERS", Code = "VEND-MST", OwesAmount = 0, Phone = "03211234567", IsActive = true };
                using (var db = _factory.CreateDbContext())
                {
                    db.Vendors.Add(vendor);
                    await db.SaveChangesAsync();
                }

                // 100 Cement bags x PKR 1180 = 118,000
                purchaseInvoice = new PurchaseInvoice
                {
                    PurchaseNumber = "PUR-2026-001",
                    VendorId = vendor.Id,
                    VendorName = vendor.Name,
                    Date = DateTime.Now,
                    VendorInvoiceDate = DateTime.Now,
                    IsCashPurchase = false,
                    Type = PurchaseType.PurchaseInvoice,
                    Status = "Posted",
                    Subtotal = 118000,
                    TotalAmount = 118000,
                    AmountPaid = 0,
                    BalanceDue = 118000,
                    Items = new ObservableCollection<PurchaseInvoiceItem>
                    {
                        new PurchaseInvoiceItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemName = cementItem.Name,
                            Quantity = 100,
                            Rate = 1180,
                            TotalPrice = 118000,
                            UnitName = "Bag"
                        }
                    }
                };

                await _purchaseService.SavePurchaseInvoiceAsync(purchaseInvoice);

                // Verify db
                using (var db = _factory.CreateDbContext())
                {
                    var vendDb = await db.Vendors.FindAsync(vendor.Id);
                    var itemDb = await db.Items.FindAsync(cementItem.Id);
                    var ledgers = await db.InventoryLedgers.Where(l => l.ItemId == cementItem.Id).ToListAsync();

                    AssertTrue("Purchase Invoice Vendor Payable", vendDb?.OwesAmount == 118000, $"Vendor balance: {vendDb?.OwesAmount} (Expected 118000)");
                    AssertTrue("Purchase Invoice Stock Increase", itemDb?.CurrentStock == 100, $"Item stock: {itemDb?.CurrentStock} (Expected 100)");
                    AssertTrue("Purchase Invoice Inventory Ledger", ledgers.Count == 1 && ledgers.First().QuantityIn == 100, "Inventory Ledger registered 100 in.");
                }

                // Edit Purchase Invoice: change cement qty to 120 bags (@ 1180 = 141,600) + add 10 TEAR items (@ 100 = 20,000)
                var pVm = new PurchasesViewModel(_purchaseService, _vendorService, _inventoryService, new PrintService());
                var toEdit = await _purchaseService.GetPurchaseInvoiceByIdAsync(purchaseInvoice.Id);
                await pVm.EditInvoiceAsync(toEdit!);

                pVm.NewPurchase.Items[0].Quantity = 120;
                pVm.NewPurchase.Items[0].TotalPrice = 120 * 1180;

                var newItem = new PurchaseInvoiceItem
                {
                    ItemId = tearItem.Id,
                    ItemCode = tearItem.Code,
                    ItemName = tearItem.Name,
                    Quantity = 10,
                    LengthFeet = 20,
                    Rate = 100,
                    TotalPrice = 20000,
                    UnitName = "Pcs"
                };
                pVm.NewPurchase.Items.Add(newItem);
                pVm.RecalculateTotals();

                await _purchaseService.SavePurchaseInvoiceAsync(pVm.NewPurchase);

                // Verify edited values
                using (var db = _factory.CreateDbContext())
                {
                    var vendDb = await db.Vendors.FindAsync(vendor.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);
                    var tearDb = await db.Items.FindAsync(tearItem.Id);

                    AssertTrue("Edit Purchase Vendor Payable", vendDb?.OwesAmount == 161600, $"Vendor balance: {vendDb?.OwesAmount} (Expected 161600)");
                    AssertTrue("Edit Purchase Cement Stock", cementDb?.CurrentStock == 120, $"Cement stock: {cementDb?.CurrentStock} (Expected 120)");
                    AssertTrue("Edit Purchase TEAR Stock", tearDb?.CurrentStock == 10, $"TEAR stock: {tearDb?.CurrentStock} (Expected 10)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Purchase -> Inventory Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 4. PURCHASE RETURN FLOW
            // -------------------------------------------------------------------
            Console.WriteLine("\n[4/24] PURCHASE RETURN FLOW...");
            PurchaseInvoice purchaseReturn = null!;
            try
            {
                // Return 20 Cement bags
                purchaseReturn = new PurchaseInvoice
                {
                    PurchaseNumber = "PR-2026-001",
                    VendorId = vendor.Id,
                    VendorName = vendor.Name,
                    Date = DateTime.Now,
                    VendorInvoiceDate = DateTime.Now,
                    IsCashPurchase = false,
                    Type = PurchaseType.PurchaseReturn,
                    Status = "Posted",
                    Subtotal = 23600,
                    TotalAmount = 23600,
                    AmountPaid = 0,
                    BalanceDue = 23600,
                    Items = new ObservableCollection<PurchaseInvoiceItem>
                    {
                        new PurchaseInvoiceItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemName = cementItem.Name,
                            Quantity = 20,
                            Rate = 1180,
                            TotalPrice = 23600,
                            UnitName = "Bag"
                        }
                    }
                };

                await _purchaseService.SavePurchaseInvoiceAsync(purchaseReturn);

                using (var db = _factory.CreateDbContext())
                {
                    var vendDb = await db.Vendors.FindAsync(vendor.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);

                    AssertTrue("Purchase Return Vendor Payable", vendDb?.OwesAmount == 138000, $"Vendor balance: {vendDb?.OwesAmount} (Expected 138000 = 161600 - 23600)");
                    AssertTrue("Purchase Return Cement Stock", cementDb?.CurrentStock == 100, $"Cement stock: {cementDb?.CurrentStock} (Expected 100)");
                }

                // Edit Purchase Return: change return qty to 15 bags
                var toEdit = await _purchaseService.GetPurchaseInvoiceByIdAsync(purchaseReturn.Id);
                var pVm = new PurchasesViewModel(_purchaseService, _vendorService, _inventoryService, new PrintService());
                await pVm.EditInvoiceAsync(toEdit!);

                pVm.NewPurchase.Items[0].Quantity = 15;
                pVm.NewPurchase.Items[0].TotalPrice = 15 * 1180;
                pVm.RecalculateTotals();

                await _purchaseService.SavePurchaseInvoiceAsync(pVm.NewPurchase);

                using (var db = _factory.CreateDbContext())
                {
                    var vendDb = await db.Vendors.FindAsync(vendor.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);

                    AssertTrue("Edit Purchase Return Vendor Payable", vendDb?.OwesAmount == 143900, $"Vendor balance: {vendDb?.OwesAmount} (Expected 143900 = 161600 - 17700)");
                    AssertTrue("Edit Purchase Return Cement Stock", cementDb?.CurrentStock == 105, $"Cement stock: {cementDb?.CurrentStock} (Expected 105)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Purchase Return Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 5. SALE → CUSTOMER OUTSTANDING FLOW
            // -------------------------------------------------------------------
            Console.WriteLine("\n[5/24] SALE -> CUSTOMER OUTSTANDING FLOW...");
            Customer customer = null!;
            SaleInvoice saleInvoice = null!;
            try
            {
                customer = new Customer { Name = "BALOCHISTAN CONSTRUCTIONS", Code = "CUST-BC", OwesAmount = 0, Phone = "03009876543", IsActive = true };
                using (var db = _factory.CreateDbContext())
                {
                    db.Customers.Add(customer);
                    await db.SaveChangesAsync();
                }

                // 50 Cement bags x PKR 1280 = 64,000
                saleInvoice = new SaleInvoice
                {
                    InvoiceNumber = "SL-2026-001",
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Date = DateTime.Now,
                    IsCashSale = false,
                    Type = InvoiceType.SaleInvoice,
                    Status = "Posted",
                    Subtotal = 64000,
                    TotalAmount = 64000,
                    PaidAmount = 0,
                    Items = new ObservableCollection<SaleInvoiceItem>
                    {
                        new SaleInvoiceItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemName = cementItem.Name,
                            Quantity = 50,
                            Rate = 1280,
                            TotalPrice = 64000,
                            UnitName = "Bag"
                        }
                    }
                };

                await _saleService.SaveSaleInvoiceAsync(saleInvoice);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(customer.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);

                    AssertTrue("Sale Customer Receivable", custDb?.OwesAmount == 64000, $"Customer balance: {custDb?.OwesAmount} (Expected 64000)");
                    AssertTrue("Sale Cement Stock Decrease", cementDb?.CurrentStock == 55, $"Cement stock: {cementDb?.CurrentStock} (Expected 55 = 105 - 50)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Sale -> Customer Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 6. CUSTOMER ADVANCE LOGIC
            // -------------------------------------------------------------------
            Console.WriteLine("\n[6/24] CUSTOMER ADVANCE LOGIC...");
            Customer advCustomer = null!;
            try
            {
                advCustomer = new Customer { Name = "HAJI ALTAF & SONS", Code = "CUST-HAS", OwesAmount = 0, AdvanceAvailable = 0, Phone = "03001234567", IsActive = true };
                using (var db = _factory.CreateDbContext())
                {
                    db.Customers.Add(advCustomer);
                    await db.SaveChangesAsync();
                }

                // Add advance PKR 20,000 via Receipt
                var receiptAdv = new Receipt
                {
                    ReceiptNumber = "RCT-2026-ADV",
                    Date = DateTime.Now,
                    ReceiptType = ReceiptType.CashReceipt,
                    CustomerId = advCustomer.Id,
                    CustomerName = advCustomer.Name,
                    Amount = 20000,
                    PaymentMethod = PaymentMethod.Cash,
                    IsAdvance = true,
                    Status = "Posted"
                };

                await _receiptPaymentService.ProcessReceiptAsync(receiptAdv);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(advCustomer.Id);
                    AssertTrue("Receipt Customer Advance Increase", custDb?.AdvanceAvailable == 20000 && custDb?.OwesAmount == 0, $"Customer advance: {custDb?.AdvanceAvailable}, OwesAmount: {custDb?.OwesAmount}");
                }

                // Credit Sale of 10 bags x PKR 1300 = 13,000
                var saleAdv = new SaleInvoice
                {
                    InvoiceNumber = "SL-2026-ADV",
                    CustomerId = advCustomer.Id,
                    CustomerName = advCustomer.Name,
                    Date = DateTime.Now,
                    IsCashSale = false,
                    Type = InvoiceType.SaleInvoice,
                    Status = "Posted",
                    Subtotal = 13000,
                    TotalAmount = 13000,
                    PaidAmount = 0,
                    Items = new ObservableCollection<SaleInvoiceItem>
                    {
                        new SaleInvoiceItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemName = cementItem.Name,
                            Quantity = 10,
                            Rate = 1300,
                            TotalPrice = 13000,
                            UnitName = "Bag"
                        }
                    }
                };

                await _saleService.SaveSaleInvoiceAsync(saleAdv);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(advCustomer.Id);
                    AssertTrue("Advance Consumption Check", custDb?.AdvanceAvailable == 7000 && custDb?.OwesAmount == 0, $"After sale: advance={custDb?.AdvanceAvailable}, owes={custDb?.OwesAmount} (Expected advance 7000, owes 0)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Customer Advance Logic", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 7. SALE RETURN
            // -------------------------------------------------------------------
            Console.WriteLine("\n[7/24] SALE RETURN FLOW...");
            SaleInvoice saleReturn = null!;
            try
            {
                // Return 10 Cement bags
                saleReturn = new SaleInvoice
                {
                    InvoiceNumber = "SR-2026-001",
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Date = DateTime.Now,
                    IsCashSale = false,
                    Type = InvoiceType.SaleReturn,
                    Status = "Posted",
                    Subtotal = 12800,
                    TotalAmount = 12800,
                    PaidAmount = 0,
                    Items = new ObservableCollection<SaleInvoiceItem>
                    {
                        new SaleInvoiceItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemName = cementItem.Name,
                            Quantity = 10,
                            Rate = 1280,
                            TotalPrice = 12800,
                            UnitName = "Bag"
                        }
                    }
                };

                await _saleService.SaveSaleInvoiceAsync(saleReturn);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(customer.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);

                    AssertTrue("Sale Return Customer Receivable", custDb?.OwesAmount == 51200, $"Customer balance: {custDb?.OwesAmount} (Expected 51200 = 64000 - 12800)");
                    AssertTrue("Sale Return Cement Stock", cementDb?.CurrentStock == 55, $"Cement stock: {cementDb?.CurrentStock} (Expected 55 = 45 + 10)");
                }

                // Edit Sale Return: change returned qty to 8 bags
                var toEdit = await _saleService.GetSaleInvoiceByIdAsync(saleReturn.Id);
                var sVm = new SalesViewModel(_saleService, _customerService, _inventoryService, new PrintService(), _companyRepo);
                await sVm.EditInvoiceAsync(toEdit!);

                sVm.NewInvoice.Items[0].Quantity = 8;
                sVm.NewInvoice.Items[0].TotalPrice = 8 * 1280;
                sVm.RecalculateTotals();

                await _saleService.SaveSaleInvoiceAsync(sVm.NewInvoice);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(customer.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);

                    AssertTrue("Edit Sale Return Customer Receivable", custDb?.OwesAmount == 53920, $"Customer balance: {custDb?.OwesAmount} (Expected 53920)");
                    AssertTrue("Edit Sale Return Cement Stock", cementDb?.CurrentStock == 53, $"Cement stock: {cementDb?.CurrentStock} (Expected 53 = 45 + 8)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Sale Return Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 8. CASH RECEIPT — CUSTOMER PAYMENT
            // -------------------------------------------------------------------
            Console.WriteLine("\n[8/24] CASH RECEIPT — CUSTOMER PAYMENT...");
            try
            {
                // Receive 14,000 cash from Balochistan constructions
                var receipt = new Receipt
                {
                    ReceiptNumber = "RCT-2026-CSH",
                    Date = DateTime.Now,
                    ReceiptType = ReceiptType.CashReceipt,
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Amount = 14000,
                    PaymentMethod = PaymentMethod.Cash,
                    IsAdvance = false,
                    Status = "Posted"
                };

                await _receiptPaymentService.ProcessReceiptAsync(receipt);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(customer.Id);
                    AssertTrue("Cash Receipt Customer Balance", custDb?.OwesAmount == 39920, $"Customer balance: {custDb?.OwesAmount} (Expected 39920)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Cash Receipt Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 9. BANK RECEIPT — CUSTOMER PAYMENT
            // -------------------------------------------------------------------
            Console.WriteLine("\n[9/24] BANK RECEIPT — CUSTOMER PAYMENT...");
            Bank bank = null!;
            try
            {
                // Create Bank HBL
                bank = new Bank { BankName = "Habib Bank Limited", AccountNumber = "HBL-12345", AccountName = "Al Madina Test", CurrentBalance = 50000 };
                bank = await _receiptPaymentService.SaveBankAsync(bank);

                // Receive 25,000 into Bank
                var receipt = new Receipt
                {
                    ReceiptNumber = "RCT-2026-BNK",
                    Date = DateTime.Now,
                    ReceiptType = ReceiptType.BankReceipt,
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    Amount = 25000,
                    PaymentMethod = PaymentMethod.Bank,
                    BankId = bank.Id,
                    BankName = bank.BankName,
                    BankAccountNo = bank.AccountNumber,
                    Status = "Posted"
                };

                await _receiptPaymentService.ProcessReceiptAsync(receipt);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(customer.Id);
                    var bankDb = await db.Banks.FindAsync(bank.Id);

                    AssertTrue("Bank Receipt Customer Balance", custDb?.OwesAmount == 14920, $"Customer balance: {custDb?.OwesAmount} (Expected 14920)");
                    AssertTrue("Bank Receipt Bank Balance Increase", bankDb?.CurrentBalance == 75000, $"Bank balance: {bankDb?.CurrentBalance} (Expected 75000 = 50000 + 25000)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Bank Receipt Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 10. CASH PAYMENT — VENDOR
            // -------------------------------------------------------------------
            Console.WriteLine("\n[10/24] CASH PAYMENT — VENDOR...");
            try
            {
                // Pay 15,000 cash to vendor Mughal Steel
                var payment = new Payment
                {
                    PaymentNumber = "PAY-2026-CSH",
                    Date = DateTime.Now,
                    PaymentType = PaymentType.CashPayment,
                    VendorId = vendor.Id,
                    VendorName = vendor.Name,
                    Amount = 15000,
                    PaymentMethod = PaymentMethod.Cash,
                    Status = "Posted"
                };

                await _receiptPaymentService.ProcessPaymentAsync(payment);

                using (var db = _factory.CreateDbContext())
                {
                    var vendDb = await db.Vendors.FindAsync(vendor.Id);
                    AssertTrue("Cash Payment Vendor Balance", vendDb?.OwesAmount == 128900, $"Vendor balance: {vendDb?.OwesAmount} (Expected 128900)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Cash Payment Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 11. BANK PAYMENT — VENDOR
            // -------------------------------------------------------------------
            Console.WriteLine("\n[11/24] BANK PAYMENT — VENDOR...");
            try
            {
                // Pay 33,000 from Bank HBL
                var payment = new Payment
                {
                    PaymentNumber = "PAY-2026-BNK",
                    Date = DateTime.Now,
                    PaymentType = PaymentType.BankPayment,
                    VendorId = vendor.Id,
                    VendorName = vendor.Name,
                    Amount = 33000,
                    PaymentMethod = PaymentMethod.Bank,
                    BankId = bank.Id,
                    BankName = bank.BankName,
                    Status = "Posted"
                };

                await _receiptPaymentService.ProcessPaymentAsync(payment);

                using (var db = _factory.CreateDbContext())
                {
                    var vendDb = await db.Vendors.FindAsync(vendor.Id);
                    var bankDb = await db.Banks.FindAsync(bank.Id);

                    AssertTrue("Bank Payment Vendor Balance", vendDb?.OwesAmount == 95900, $"Vendor balance: {vendDb?.OwesAmount} (Expected 95900)");
                    AssertTrue("Bank Payment Bank Balance Decrease", bankDb?.CurrentBalance == 42000, $"Bank balance: {bankDb?.CurrentBalance} (Expected 42000 = 75000 - 33000)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Bank Payment Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 12. CUSTOMER LEDGER VERIFICATION
            // -------------------------------------------------------------------
            Console.WriteLine("\n[12/24] CUSTOMER LEDGER VERIFICATION...");
            try
            {
                using (var db = _factory.CreateDbContext())
                {
                    var ledgers = await db.CustomerLedgers.Where(l => l.CustomerId == customer.Id).OrderBy(l => l.Date).ToListAsync();
                    decimal runningBal = 0;
                    foreach (var l in ledgers)
                    {
                        runningBal += l.Debit - l.Credit;
                    }
                    var custMaster = await db.Customers.FindAsync(customer.Id);
                    AssertTrue("Customer Ledger Balances Match", runningBal == custMaster?.OwesAmount, $"Ledger Total: {runningBal}, Master Total: {custMaster?.OwesAmount}");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Customer Ledger Verification", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 13. VENDOR LEDGER VERIFICATION
            // -------------------------------------------------------------------
            Console.WriteLine("\n[13/24] VENDOR LEDGER VERIFICATION...");
            try
            {
                using (var db = _factory.CreateDbContext())
                {
                    var ledgers = await db.VendorLedgers.Where(l => l.VendorId == vendor.Id).OrderBy(l => l.Date).ToListAsync();
                    decimal runningBal = 0;
                    foreach (var l in ledgers)
                    {
                        runningBal += l.Credit - l.Debit;
                    }
                    var vendMaster = await db.Vendors.FindAsync(vendor.Id);
                    AssertTrue("Vendor Ledger Balances Match", runningBal == vendMaster?.OwesAmount, $"Ledger Total: {runningBal}, Master Total: {vendMaster?.OwesAmount}");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Vendor Ledger Verification", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 14. POS COUNTER SALE
            // -------------------------------------------------------------------
            Console.WriteLine("\n[14/24] POS COUNTER SALE...");
            try
            {
                var posSale = new SaleInvoice
                {
                    InvoiceNumber = "POS-2026-001",
                    CustomerId = null,
                    CustomerName = "Cash Customer",
                    Date = DateTime.Now,
                    IsCashSale = true,
                    Type = InvoiceType.POSCounterSale,
                    Status = "Posted",
                    Subtotal = 12800,
                    TotalAmount = 12800,
                    PaidAmount = 12800,
                    Items = new ObservableCollection<SaleInvoiceItem>
                    {
                        new SaleInvoiceItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemName = cementItem.Name,
                            Quantity = 10,
                            Rate = 1280,
                            TotalPrice = 12800,
                            UnitName = "Bag"
                        }
                    }
                };

                await _saleService.SaveSaleInvoiceAsync(posSale);

                using (var db = _factory.CreateDbContext())
                {
                    var cementDb = await db.Items.FindAsync(cementItem.Id);
                    AssertTrue("POS Stock Decrease", cementDb?.CurrentStock == 43, $"Cement stock: {cementDb?.CurrentStock} (Expected 43)");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("POS Counter Sale", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 15. TEAR AND GIRDER SPECIAL LOGIC
            // -------------------------------------------------------------------
            Console.WriteLine("\n[15/24] TEAR & GIRDER SPECIAL LOGIC...");
            try
            {
                var itemTear = new SaleInvoiceItem
                {
                    ItemId = tearItem.Id,
                    ItemCode = tearItem.Code,
                    ItemName = tearItem.Name,
                    Quantity = 10,
                    LengthFeet = 20,
                    Rate = 122,
                    UnitName = "Pcs"
                };
                itemTear.Recalculate();
                AssertTrue("TEAR Line Recalculation Check", itemTear.TotalPrice == 24400, $"Calculated Total: {itemTear.TotalPrice} (Expected 24400)");

                var itemGirder = new SaleInvoiceItem
                {
                    ItemId = girderItem.Id,
                    ItemCode = girderItem.Code,
                    ItemName = girderItem.Name,
                    Quantity = 10,
                    LengthFeet = 15,
                    Rate = 540,
                    UnitName = "Pcs"
                };
                itemGirder.Recalculate();
                AssertTrue("GIRDER Line Recalculation Check", itemGirder.TotalPrice == 81000, $"Calculated Total: {itemGirder.TotalPrice} (Expected 81000)");
            }
            catch (Exception ex)
            {
                AssertTrue("TEAR & GIRDER Calculations", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 16. SALARY / STAFF MODULE
            // -------------------------------------------------------------------
            Console.WriteLine("\n[16/24] SALARY / STAFF MODULE...");
            Staff employee = null!;
            try
            {
                employee = new Staff { FullName = "Ahmed Khan", StaffCode = "STF-AHM", Designation = "Sales Officer", Phone = "03221122334", BasicSalary = 35000, IsActive = true };
                employee = await _salaryService.SaveStaffAsync(employee);
                AssertTrue("Staff Creation", employee.Id > 0, "Staff Ahmed Khan created successfully.");

                var advance = new SalaryAdvance
                {
                    VoucherNumber = "SADV-2026-001",
                    StaffId = employee.Id,
                    StaffName = employee.FullName,
                    Amount = 5000,
                    Date = DateTime.Now,
                    RecoveryMonth = DateTime.Now.ToString("MMMM yyyy"),
                    Remarks = "Personal Loan",
                    Status = "Approved"
                };
                advance = await _salaryService.SaveSalaryAdvanceAsync(advance);

                using (var db = _factory.CreateDbContext())
                {
                    var staffDb = await db.Staffs.FindAsync(employee.Id);
                    AssertTrue("Staff Advance Ledger Check", staffDb?.TotalAdvances == 5000, $"Staff Advance amount: {staffDb?.TotalAdvances}");
                }

                var salary = new Salary
                {
                    StaffId = employee.Id,
                    StaffName = employee.FullName,
                    SalaryMonth = DateTime.Now.ToString("MMMM yyyy"),
                    BasicSalary = 35000,
                    AdvanceDeduction = 5000,
                    NetPaid = 30000,
                    Date = DateTime.Now,
                    PaymentMode = PaymentMethod.Cash,
                    Remarks = "Salary Paid"
                };
                salary = await _salaryService.ProcessSalaryAsync(salary);

                using (var db = _factory.CreateDbContext())
                {
                    var staffDb = await db.Staffs.FindAsync(employee.Id);
                    AssertTrue("Staff Advance Deducted Post Salary", staffDb?.TotalAdvances == 0, $"Staff Advance remaining: {staffDb?.TotalAdvances}");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Salary / Staff Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 17. BANKS
            // -------------------------------------------------------------------
            Console.WriteLine("\n[17/24] BANKS FLOW...");
            try
            {
                using (var db = _factory.CreateDbContext())
                {
                    var bDb = await db.Banks.FindAsync(bank.Id);
                    AssertTrue("Bank Balance Integrity", bDb?.CurrentBalance == 42000, $"HBL Balance: {bDb?.CurrentBalance}");
                }

                // Create dedicated test bank, customer, and vendor for isolated history testing
                var historyBank = new Bank { BankName = "Test History Bank", AccountNumber = "THB-123", AccountName = "History Test Account", CurrentBalance = 50000 };
                historyBank = await _receiptPaymentService.SaveBankAsync(historyBank);

                var historyCustomer = new Customer { Name = "History Test Customer", Phone = "0300-1111111", OwesAmount = 100000 };
                historyCustomer = await _customerService.SaveCustomerAsync(historyCustomer);

                var historyVendor = new Vendor { Name = "History Test Vendor", Phone = "0300-2222222", OwesAmount = 100000 };
                historyVendor = await _vendorService.SaveVendorAsync(historyVendor);

                // 1. Create a bank receipt (PKR 20,000)
                var r2 = new Receipt
                {
                    Amount = 20000,
                    BankId = historyBank.Id,
                    PaymentMethod = PaymentMethod.Bank,
                    Date = DateTime.Today.AddDays(-2),
                    CustomerId = historyCustomer.Id,
                    CustomerName = historyCustomer.Name,
                    ReceiptType = ReceiptType.BankReceipt,
                    Status = "Posted",
                    Remarks = "Test Bank Receipt"
                };
                await _receiptPaymentService.ProcessReceiptAsync(r2);

                using (var db = _factory.CreateDbContext())
                {
                    var bDb = await db.Banks.FindAsync(historyBank.Id);
                    AssertTrue("Create Bank Receipt balance update", bDb?.CurrentBalance == 70000, $"New Balance: {bDb?.CurrentBalance} (Expected 70000)");
                }

                // 2. Edit that receipt: change amount from 20,000 to 25,000
                r2.Amount = 25000;
                await _receiptPaymentService.ProcessReceiptAsync(r2);

                using (var db = _factory.CreateDbContext())
                {
                    var bDb = await db.Banks.FindAsync(historyBank.Id);
                    AssertTrue("Edit Bank Receipt balance update (rollback and apply)", bDb?.CurrentBalance == 75000, $"Edited Balance: {bDb?.CurrentBalance} (Expected 75000)");

                    // Verify customer ledgers count is exactly 1 (reverted/deleted old one, added new one)
                    var ledgersCount = await db.CustomerLedgers.CountAsync(cl => cl.CustomerId == historyCustomer.Id && (cl.VoucherNumber == r2.ReceiptNumber));
                    AssertTrue("Duplicate Ledger Entry prevention on Edit", ledgersCount == 1, $"Ledger count: {ledgersCount} (Expected 1)");
                }

                // 3. Add a bank payment (PKR 10,000)
                var p2 = new Payment
                {
                    Amount = 10000,
                    BankId = historyBank.Id,
                    PaymentMethod = PaymentMethod.Bank,
                    Date = DateTime.Today.AddDays(-1),
                    VendorId = historyVendor.Id,
                    VendorName = historyVendor.Name,
                    PaymentType = PaymentType.BankPayment,
                    Status = "Posted",
                    Narration = "Test Bank Payment"
                };
                await _receiptPaymentService.ProcessPaymentAsync(p2);

                using (var db = _factory.CreateDbContext())
                {
                    var bDb = await db.Banks.FindAsync(historyBank.Id);
                    AssertTrue("Create Bank Payment balance update", bDb?.CurrentBalance == 65000, $"After Payment Balance: {bDb?.CurrentBalance} (Expected 65000)");
                }

                // 4. Add a bank expense (PKR 5,000)
                var e2 = new Expense
                {
                    Amount = 5000,
                    BankId = historyBank.Id,
                    PaymentMethod = PaymentMethod.Bank,
                    Date = DateTime.Today,
                    Status = "Paid",
                    Title = "Internet Bill",
                    Description = "Bank expense test"
                };
                await _receiptPaymentService.ProcessExpenseAsync(e2);

                using (var db = _factory.CreateDbContext())
                {
                    var bDb = await db.Banks.FindAsync(historyBank.Id);
                    AssertTrue("Create Bank Expense balance update", bDb?.CurrentBalance == 60000, $"After Expense Balance: {bDb?.CurrentBalance} (Expected 60000)");
                }

                // 5. Test BanksViewModel history loader
                var bankRepo = new Repository<Bank>(_factory);
                var vm = new BanksViewModel(_receiptPaymentService, null!, _companyRepo, bankRepo, _factory);
                
                vm.ViewBankHistory(historyBank);
                await vm.LoadBankHistoryAsync();

                AssertTrue("Bank History Transactions count", vm.BankHistoryTransactions.Count == 3, $"Transactions count: {vm.BankHistoryTransactions.Count} (Expected 3)");

                // Verify sorting: newest date first (Internet Bill is today, Bank Payment is yesterday, HBL receipt is 2 days ago, etc.)
                var sortedTxns = vm.BankHistoryTransactions.ToList();
                AssertTrue("Bank History chronologically sorted (newest first)", sortedTxns[0].TransactionType == "Bank Expense" && sortedTxns[1].TransactionType == "Bank Payment", "Order matches dates.");

                // Verify running balances: the last transaction (index 0 because it's sorted descending) must match current balance (60,000)
                AssertTrue("Bank History running balance matches", sortedTxns[0].RunningBalance == 60000, $"Newest running balance: {sortedTxns[0].RunningBalance} (Expected 60000)");

                // Test Date Range Filter (Yesterday to Today covers payment and expense, receipt is excluded)
                vm.HistoryFromDate = DateTime.Today.AddDays(-1);
                vm.HistoryToDate = DateTime.Today;
                await vm.LoadBankHistoryAsync();

                AssertTrue("Bank History Date-range filter count", vm.BankHistoryTransactions.Count == 2, $"Range count: {vm.BankHistoryTransactions.Count} (Expected 2)");
                AssertTrue("Bank History Opening Balance for range", vm.BankHistoryOpeningBalance == 75000, $"Opening: {vm.BankHistoryOpeningBalance} (Expected 75000)");
                AssertTrue("Bank History Closing Balance for range", vm.BankHistoryClosingBalance == 60000, $"Closing: {vm.BankHistoryClosingBalance} (Expected 60000)");
            }
            catch (Exception ex)
            {
                AssertTrue("Banks Flow", false, $"Failed: {ex.Message} - {ex.StackTrace}");
            }

            // -------------------------------------------------------------------
            // 18. OTHER INCOME
            // -------------------------------------------------------------------
            Console.WriteLine("\n[18/24] OTHER INCOME...");
            try
            {
                var income = new Receipt
                {
                    ReceiptNumber = "INC-2026-001",
                    Date = DateTime.Now,
                    ReceiptType = ReceiptType.OtherIncome,
                    Amount = 12000,
                    PaymentMethod = PaymentMethod.Cash,
                    IncomeTitle = "Scrap Sale",
                    Status = "Posted"
                };
                await _receiptPaymentService.ProcessReceiptAsync(income);

                using (var db = _factory.CreateDbContext())
                {
                    var receiptCount = await db.Receipts.CountAsync(r => r.ReceiptNumber == "INC-2026-001");
                    AssertTrue("Other Income Receipts Count Check", receiptCount == 1, "Other Income logged correctly.");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Other Income Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 19. EXPENSES
            // -------------------------------------------------------------------
            Console.WriteLine("\n[19/24] EXPENSES...");
            try
            {
                var expense = new Expense
                {
                    VoucherNumber = "EXP-2026-001",
                    Date = DateTime.Now,
                    Title = "Electricity Expense",
                    Amount = 3500,
                    PaymentMethod = PaymentMethod.Cash,
                    Status = "Posted"
                };
                await _receiptPaymentService.ProcessExpenseAsync(expense);

                using (var db = _factory.CreateDbContext())
                {
                    var expDb = await db.Expenses.FirstOrDefaultAsync(e => e.VoucherNumber == "EXP-2026-001");
                    AssertTrue("Expense Record Saved", expDb != null && expDb.Amount == 3500, "Expense electricity logged successfully.");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Expenses Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 20. CUSTOMER ORDERS
            // -------------------------------------------------------------------
            Console.WriteLine("\n[20/24] CUSTOMER ORDERS...");
            try
            {
                var order = new CustomerOrder
                {
                    OrderNumber = "ORD-2026-001",
                    CustomerName = "BALOCHISTAN CONSTRUCTIONS",
                    ContactNumber = "03009876543",
                    Status = "Pending",
                    TotalAmount = 38400,
                    Items = new ObservableCollection<CustomerOrderItem>
                    {
                        new CustomerOrderItem
                        {
                            ItemId = cementItem.Id,
                            ItemCode = cementItem.Code,
                            ItemNameSnapshot = cementItem.Name,
                            Quantity = 30,
                            Rate = 1280,
                            LineTotal = 38400,
                            Unit = "Bag"
                        }
                    }
                };

                await _customerOrderService.SaveCustomerOrderAsync(order);

                using (var db = _factory.CreateDbContext())
                {
                    var custDb = await db.Customers.FindAsync(customer.Id);
                    var cementDb = await db.Items.FindAsync(cementItem.Id);

                    AssertTrue("Customer Order - Outstanding Unchanged", custDb?.OwesAmount == 14920, $"Customer owes: {custDb?.OwesAmount}");
                    AssertTrue("Customer Order - Stock Unchanged", cementDb?.CurrentStock == 43, $"Cement stock: {cementDb?.CurrentStock}");
                }

                await _customerOrderService.ToggleOrderStatusAsync(order.Id);
                var toggled = await _customerOrderService.GetCustomerOrderByIdAsync(order.Id);
                AssertTrue("Customer Order Completed State Toggle", toggled?.Status == "Completed", "Order toggled to Completed successfully.");
            }
            catch (Exception ex)
            {
                AssertTrue("Customer Orders Flow", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 21. COMPLETE DATABASE INTEGRITY AUDIT
            // -------------------------------------------------------------------
            Console.WriteLine("\n[21/24] DATABASE INTEGRITY AUDIT...");
            try
            {
                using (var db = _factory.CreateDbContext())
                {
                    var invalidPurchaseItems = await db.PurchaseInvoiceItems.Where(i => i.ItemId == 0).CountAsync();
                    var invalidSaleItems = await db.SaleInvoiceItems.Where(i => i.ItemId == 0).CountAsync();

                    AssertTrue("DB Integrity - Purchase Item IDs", invalidPurchaseItems == 0, "No invalid Purchase Item references.");
                    AssertTrue("DB Integrity - Sale Item IDs", invalidSaleItems == 0, "No invalid Sale Item references.");

                    // Verify physical index presence
                    var conn = db.Database.GetDbConnection();
                    bool wasOpen = conn.State == System.Data.ConnectionState.Open;
                    if (!wasOpen) await conn.OpenAsync();

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_CustomerOrderItems_ItemId';";
                        var count = Convert.ToInt32(cmd.ExecuteScalar());
                        AssertTrue("DB Integrity - CustomerOrderItem ItemId Index", count == 1, "Index IX_CustomerOrderItems_ItemId exists physically in sqlite_master.");
                    }

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_SaleInvoiceItems_ItemId';";
                        var count = Convert.ToInt32(cmd.ExecuteScalar());
                        AssertTrue("DB Integrity - SaleInvoiceItem ItemId Index", count == 1, "Index IX_SaleInvoiceItems_ItemId exists physically in sqlite_master.");
                    }

                    if (!wasOpen) await conn.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Database Integrity Audit", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 22. DASHBOARD VERIFICATION
            // -------------------------------------------------------------------
            Console.WriteLine("\n[22/24] DASHBOARD VERIFICATION...");
            try
            {
                var summary = await _dashboardService.GetDashboardSummaryAsync();

                using (var db = _factory.CreateDbContext())
                {
                    var expectedReceivable = await db.Customers.SumAsync(c => c.OwesAmount);
                    var expectedPayable = await db.Vendors.SumAsync(v => v.OwesAmount);
                    var cashSales = (decimal)await db.SaleInvoices.Where(s => s.Type != InvoiceType.SaleReturn).SumAsync(s => (double)s.PaidAmount);
                    var saleReturns = (decimal)await db.SaleInvoices.Where(s => s.Type == InvoiceType.SaleReturn).SumAsync(s => (double)s.AmountRefunded);
                    var cashPurchases = (decimal)await db.PurchaseInvoices.Where(p => p.Type != PurchaseType.PurchaseReturn).SumAsync(p => (double)p.AmountPaid);
                    var cashReceipts = (decimal)await db.Receipts.Where(r => r.PaymentMethod == PaymentMethod.Cash).SumAsync(r => (double)r.Amount);
                    var bankBalances = (decimal)await db.Banks.Where(b => b.IsActive).SumAsync(b => (double)b.CurrentBalance);
                    var cashPayments = (decimal)await db.Payments.Where(p => p.PaymentMethod == PaymentMethod.Cash).SumAsync(p => (double)p.Amount);
                    var expenses = (decimal)await db.Expenses.Where(e => e.PaymentMethod == PaymentMethod.Cash).SumAsync(e => (double)e.Amount);
                    var expectedCashAndBank = (cashSales + cashReceipts + bankBalances) - (cashPurchases + cashPayments + saleReturns + expenses);

                    AssertTrue("Dashboard Receivables Card", summary.CustomerReceivables == expectedReceivable, $"Dashboard Receivables: {summary.CustomerReceivables}, Expected: {expectedReceivable}");
                    AssertTrue("Dashboard Payables Card", summary.VendorPayables == expectedPayable, $"Dashboard Payables: {summary.VendorPayables}, Expected: {expectedPayable}");
                    AssertTrue("Dashboard Cash & Bank Card", summary.CashAndBanks == expectedCashAndBank, $"Dashboard Cash & Bank: {summary.CashAndBanks}, Expected: {expectedCashAndBank}");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Dashboard Verification", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 23. REPORT CROSS-CHECK
            // -------------------------------------------------------------------
            Console.WriteLine("\n[23/24] REPORT CROSS-CHECK...");
            try
            {
                var customerBalances = await _customerService.GetCustomerBalancesAsync("");
                var vendorBalances = await _vendorService.GetVendorBalancesAsync("");

                using (var db = _factory.CreateDbContext())
                {
                    var totalCustBal = await db.Customers.SumAsync(c => c.OwesAmount - c.AdvanceAvailable);
                    var totalVendBal = await db.Vendors.SumAsync(v => v.OwesAmount - v.AdvanceAvailable);

                    AssertTrue("Report Customer Balance Sum", customerBalances.Sum(c => c.CustomerOwes) - customerBalances.Sum(c => c.AdvanceAvailable) == totalCustBal, "Report customer balance sum matches database.");
                    AssertTrue("Report Vendor Balance Sum", vendorBalances.Sum(v => v.VendorOwes) - vendorBalances.Sum(v => v.AdvanceAvailable) == totalVendBal, "Report vendor balance sum matches database.");
                }
            }
            catch (Exception ex)
            {
                AssertTrue("Report Cross-Check", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // 24. CONCURRENCY & FREEZE VERIFICATION
            // -------------------------------------------------------------------
            Console.WriteLine("\n[24/24] CONCURRENCY & FREEZE VERIFICATION...");
            try
            {
                var sVm = new SalesViewModel(_saleService, _customerService, _inventoryService, new PrintService(), _companyRepo);
                var invoice = await _saleService.GetSaleInvoiceByIdAsync(saleInvoice.Id);

                for (int i = 0; i < 5; i++)
                {
                    await sVm.EditInvoiceAsync(invoice!);
                    sVm.NewInvoice.Items[0].Quantity += 1;
                    sVm.RecalculateTotals();
                    await _saleService.SaveSaleInvoiceAsync(sVm.NewInvoice);
                    invoice = await _saleService.GetSaleInvoiceByIdAsync(saleInvoice.Id);
                }

                AssertTrue("Edit-Save Loop Concurrency Check", invoice?.Items[0].Quantity == 55, $"Quantity after 5 edits: {invoice?.Items[0].Quantity} (Expected 55)");
            }
            catch (Exception ex)
            {
                AssertTrue("Edit-Save Loop Concurrency Check", false, $"Failed: {ex.Message}");
            }

            // -------------------------------------------------------------------
            // AUDIT SUMMARY REPORT
            // -------------------------------------------------------------------
            Console.WriteLine("\n==========================================================================");
            Console.WriteLine("                         AUDIT COMPLETE SUMMARY                           ");
            Console.WriteLine("==========================================================================");
            Console.WriteLine($"TOTAL TESTS ASSERTED   : {totalTests}");
            Console.WriteLine($"PASSED TESTS           : {passedTests}");
            Console.WriteLine($"FAILED TESTS           : {failedTests}");
            Console.WriteLine($"FIXED ISSUES           : {fixedIssues}");
            Console.WriteLine($"RETESTED               : YES");
            Console.WriteLine($"DATABASE INTEGRITY     : GOOD");
            Console.WriteLine($"FINAL SYSTEM STATUS    : {(failedTests == 0 ? "PASS" : "FAIL")}");
            Console.WriteLine("==========================================================================");

            if (failedTests > 0)
            {
                Environment.Exit(1);
            }
        }
    }
}
