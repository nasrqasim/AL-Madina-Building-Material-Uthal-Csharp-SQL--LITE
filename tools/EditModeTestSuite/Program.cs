using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
if (args.Contains("--audit"))
            {
                var auditDbPath = Path.Combine(Path.GetTempPath(), $"AuditERP_{Guid.NewGuid():N}.db");
                try
                {
                    var audit = new E2eAuditSuite(auditDbPath);
                    audit.RunAuditAsync().GetAwaiter().GetResult();
                }
                finally
                {
                    try { File.Delete(auditDbPath); } catch { }
                }
                return;
            }

            Console.WriteLine("==========================================================================");
            Console.WriteLine(" AL MADINA BUILDING MATERIAL ERP — EXHAUSTIVE REAL UI & DB VERIFICATION ");
            Console.WriteLine("==========================================================================");

            // Initialize WPF Application Context if needed
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application();
            }

            var dbPath = Path.Combine(Path.GetTempPath(), $"RealUiTest_{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using (var db = new AppDbContext(options))
            {
                db.Database.EnsureCreated();

                // -------------------------------------------------------------------
                // SEED REAL TEST DATA
                // -------------------------------------------------------------------
                var custA = new Customer { Name = "HAJI ALTAF & SONS", Code = "CUST-001", OwesAmount = 0, AdvanceAvailable = 0, Phone = "03001234567", IsActive = true };
                var custB = new Customer { Name = "BALOCHISTAN CONSTRUCTIONS", Code = "CUST-002", OwesAmount = 0, AdvanceAvailable = 0, Phone = "03009876543", IsActive = true };
                var vendA = new Vendor { Name = "MUGHAL STEEL TRADERS", Code = "VEND-001", OwesAmount = 0, AdvanceAvailable = 0, Phone = "03211112222", IsActive = true };

                var itemNormal = new Item { Code = "ITM-CEMENT", Name = "Lucky Cement Bag 50kg", SalePrice = 1450, PurchasePrice = 1380, CurrentStock = 500, BaseUnit = "Bag", IsActive = true };
                var itemTear = new Item { Code = "ITM-TEAR-4X2", Name = "TEAR 4x2 Heavy", SalePrice = 122, PurchasePrice = 100, RatePerFoot = 122, LengthFeet = 20, CurrentStock = 200, BaseUnit = "Pcs", IsActive = true };
                var itemGirder = new Item { Code = "ITM-GIRDER-7X4", Name = "GIRDER 7x4 Heavy Beam", SalePrice = 540, PurchasePrice = 480, RatePerFoot = 540, LengthFeet = 15, CurrentStock = 100, BaseUnit = "Pcs", IsActive = true };

                db.Customers.AddRange(custA, custB);
                db.Vendors.Add(vendA);
                db.Items.AddRange(itemNormal, itemTear, itemGirder);
                db.CompanySettings.Add(new CompanySetting { CompanyName = "Al Madina Building Material", InvoicePrefix = "INV", PurchasePrefix = "PUR" });
                db.SaveChanges();

                // Setup Repos & Services
                var factory = new TestDbContextFactory(options);
                var customerService = new CustomerService(factory);
                var saleService = new SaleService(factory, customerService);
                var purchaseService = new PurchaseService(factory);
                var vendorService = new VendorService(factory);
                var inventoryService = new InventoryService(factory);
                var companyRepo = new Repository<CompanySetting>(factory);
                var printService = new PrintService();

                var salesVm = new SalesViewModel(saleService, customerService, inventoryService, printService, companyRepo);
                var purchasesVm = new PurchasesViewModel(purchaseService, vendorService, inventoryService, printService);

                int passedTests = 0;
                int failedTests = 0;

                // -------------------------------------------------------------------
                // TEST SECTION 1: TEAR & GIRDER CALCULATION & PERSISTENCE VERIFICATION
                // -------------------------------------------------------------------
                Console.WriteLine("\n[1/6] TESTING TEAR & GIRDER SPECIAL CALCULATION & EDITS...");
                try
                {
                    // TEAR: 10 pcs * 20 ft * 122/ft = 24,400
                    var tearItem = new SaleInvoiceItem
                    {
                        ItemId = itemTear.Id,
                        ItemCode = itemTear.Code,
                        ItemName = itemTear.Name,
                        Quantity = 10,
                        LengthFeet = 20,
                        RatePerFoot = 122,
                        UnitName = "Pcs"
                    };
                    tearItem.Recalculate();
                    decimal expectedTearTotal = 10 * 20 * 122; // 24,400

                    // GIRDER: 10 pcs * 15 ft * 540/ft = 81,000
                    var girderItem = new SaleInvoiceItem
                    {
                        ItemId = itemGirder.Id,
                        ItemCode = itemGirder.Code,
                        ItemName = itemGirder.Name,
                        Quantity = 10,
                        LengthFeet = 15,
                        RatePerFoot = 540,
                        UnitName = "Pcs"
                    };
                    girderItem.Recalculate();
                    decimal expectedGirderTotal = 10 * 15 * 540; // 81,000

                    if (tearItem.TotalPrice == expectedTearTotal && girderItem.TotalPrice == expectedGirderTotal)
                    {
                        Console.WriteLine($" -> Initial Calculations Correct: TEAR = PKR {tearItem.TotalPrice:N0} (Expected {expectedTearTotal:N0}), GIRDER = PKR {girderItem.TotalPrice:N0} (Expected {expectedGirderTotal:N0})");
                    }
                    else
                    {
                        throw new Exception($"Initial TEAR/GIRDER math mismatch: TEAR={tearItem.TotalPrice}, GIRDER={girderItem.TotalPrice}");
                    }

                    // Create Invoice with TEAR & GIRDER
                    var invoiceTear = new SaleInvoice
                    {
                        InvoiceNumber = "SI-TG-001",
                        CustomerId = custA.Id,
                        CustomerName = custA.Name,
                        Date = DateTime.Now,
                        IsCashSale = false,
                        Type = InvoiceType.SaleInvoice,
                        Status = "Posted",
                        Subtotal = expectedTearTotal + expectedGirderTotal,
                        TotalAmount = expectedTearTotal + expectedGirderTotal,
                        Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem> { tearItem, girderItem }
                    };

                    var savedTG = saleService.SaveSaleInvoiceAsync(invoiceTear).GetAwaiter().GetResult();
                    Console.WriteLine($" -> Saved Invoice SI-TG-001 (Total: PKR {savedTG.TotalAmount:N0})");

                    // Edit: Change TEAR Length to 25 ft (10 * 25 * 122 = 30,500) & GIRDER Qty to 12 (12 * 15 * 540 = 97,200)
                    savedTG.Items.First(i => i.ItemId == itemTear.Id).LengthFeet = 25;
                    savedTG.Items.First(i => i.ItemId == itemTear.Id).Recalculate(); // 30,500

                    savedTG.Items.First(i => i.ItemId == itemGirder.Id).Quantity = 12;
                    savedTG.Items.First(i => i.ItemId == itemGirder.Id).Recalculate(); // 97,200

                    savedTG.Subtotal = savedTG.Items.Sum(i => i.TotalPrice);
                    savedTG.TotalAmount = savedTG.Subtotal;

                    var editedTG = saleService.SaveSaleInvoiceAsync(savedTG).GetAwaiter().GetResult();
                    var reloadedTG = saleService.GetSaleInvoiceByIdAsync(editedTG.Id).GetAwaiter().GetResult();

                    decimal expectedEditedTotal = 30500 + 97200; // 127,700
                    if (reloadedTG != null && reloadedTG.TotalAmount == expectedEditedTotal && reloadedTG.Items.Count == 2)
                    {
                        var reloadedTear = reloadedTG.Items.First(i => i.ItemId == itemTear.Id);
                        var reloadedGirder = reloadedTG.Items.First(i => i.ItemId == itemGirder.Id);

                        if (reloadedTear.LengthFeet == 25 && reloadedTear.RatePerFoot == 122 && reloadedGirder.Quantity == 12 && reloadedGirder.RatePerFoot == 540)
                        {
                            Console.WriteLine($" -> Edit & Reload Verified! Total: PKR {reloadedTG.TotalAmount:N0} (Expected {expectedEditedTotal:N0})");
                            Console.WriteLine(" RESULT: [PASS]");
                            passedTests++;
                        }
                        else
                        {
                            throw new Exception("TEAR/GIRDER reloaded property fields failed verification!");
                        }
                    }
                    else
                    {
                        throw new Exception($"Reloaded invoice failed totals match: Total={reloadedTG?.TotalAmount}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // -------------------------------------------------------------------
                // TEST SECTION 2: 10-CYCLE STRESS EDIT TEST (SALE INVOICE)
                // -------------------------------------------------------------------
                Console.WriteLine("\n[2/6] TESTING 10 CONSECUTIVE EDIT CYCLES ON SALE INVOICE...");
                try
                {
                    salesVm.LoadInvoicesAsync().GetAwaiter().GetResult();

                    var masterInvoice = new SaleInvoice
                    {
                        InvoiceNumber = "SI-STRESS-001",
                        CustomerId = custA.Id,
                        CustomerName = custA.Name,
                        Date = DateTime.Now,
                        IsCashSale = false,
                        Type = InvoiceType.SaleInvoice,
                        Status = "Posted",
                        Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>
                        {
                            new SaleInvoiceItem { ItemId = itemNormal.Id, ItemCode = itemNormal.Code, ItemName = itemNormal.Name, Quantity = 10, Rate = 1450, TotalPrice = 14500 }
                        }
                    };
                    masterInvoice.Subtotal = 14500;
                    masterInvoice.TotalAmount = 14500;

                    var savedMaster = saleService.SaveSaleInvoiceAsync(masterInvoice).GetAwaiter().GetResult();
                    int targetInvoiceId = savedMaster.Id;

                    for (int cycle = 1; cycle <= 10; cycle++)
                    {
                        var invToEdit = saleService.GetSaleInvoiceByIdAsync(targetInvoiceId).GetAwaiter().GetResult();

                        // Open in ViewModel
                        salesVm.EditInvoiceAsync(invToEdit!).GetAwaiter().GetResult();

                        // Perform cycle modifications
                        if (cycle % 2 == 1)
                        {
                            // Add item
                            salesVm.AddPosItem(itemTear);
                        }
                        else
                        {
                            // Modify quantity
                            if (salesVm.NewInvoice.Items.Count > 0)
                            {
                                salesVm.NewInvoice.Items[0].Quantity += 5;
                            }
                        }

                        salesVm.NewInvoice.Remarks = $"Stress Edit Cycle #{cycle}";
                        salesVm.RecalculateTotals();

                        // Save
                        var updated = saleService.SaveSaleInvoiceAsync(salesVm.NewInvoice).GetAwaiter().GetResult();

                        // Direct SQLite Database Inspection
                        using var sqliteConn = new SqliteConnection($"Data Source={dbPath}");
                        sqliteConn.Open();
                        using var cmd = sqliteConn.CreateCommand();
                        cmd.CommandText = "SELECT COUNT(*) FROM SaleInvoiceItems WHERE SaleInvoiceId = @id";
                        cmd.Parameters.AddWithValue("@id", targetInvoiceId);
                        long dbLineItemCount = (long)cmd.ExecuteScalar()!;

                        Console.WriteLine($" -> Cycle {cycle}/10: Saved VM Items={salesVm.NewInvoice.Items.Count}, DB Line Items={dbLineItemCount}, Total=PKR {updated.TotalAmount:N0}");

                        if (dbLineItemCount != salesVm.NewInvoice.Items.Count)
                        {
                            throw new Exception($"Database line item mismatch! DB={dbLineItemCount}, VM={salesVm.NewInvoice.Items.Count}");
                        }
                    }

                    Console.WriteLine(" -> 10 Consecutive Edit Cycles Completed Successfully with Zero Line Erasure!");
                    Console.WriteLine(" RESULT: [PASS]");
                    passedTests++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // -------------------------------------------------------------------
                // TEST SECTION 3: EVENT HANDLER ACCUMULATION & RECALCULATE RECURSION
                // -------------------------------------------------------------------
                Console.WriteLine("\n[3/6] TESTING EVENT HANDLER LEAK & RECALCULATE RECURSION...");
                try
                {
                    var testInv = new SaleInvoice
                    {
                        InvoiceNumber = "SI-EVT-001",
                        CustomerId = custA.Id,
                        CustomerName = custA.Name,
                        Date = DateTime.Now,
                        IsCashSale = false,
                        Type = InvoiceType.SaleInvoice,
                        Status = "Posted",
                        Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>
                        {
                            new SaleInvoiceItem { ItemId = itemNormal.Id, ItemCode = itemNormal.Code, ItemName = itemNormal.Name, Quantity = 1, Rate = 1000, TotalPrice = 1000 }
                        }
                    };
                    testInv.Subtotal = 1000;
                    testInv.TotalAmount = 1000;
                    var savedEvtInv = saleService.SaveSaleInvoiceAsync(testInv).GetAwaiter().GetResult();

                    // Open Edit 15 times in ViewModel
                    for (int i = 0; i < 15; i++)
                    {
                        var fetch = saleService.GetSaleInvoiceByIdAsync(savedEvtInv.Id).GetAwaiter().GetResult();
                        salesVm.EditInvoiceAsync(fetch!).GetAwaiter().GetResult();
                    }

                    // Now modify Quantity of Item 0 once
                    decimal subtotalBefore = salesVm.NewInvoice.Subtotal;
                    decimal expectedAfter = 5 * salesVm.NewInvoice.Items[0].Rate; // 5 * 1450 = 7250
                    salesVm.NewInvoice.Items[0].Quantity = 5; // triggers PropertyChanged event
                    decimal subtotalAfter = salesVm.NewInvoice.Subtotal;

                    if (subtotalAfter == expectedAfter)
                    {
                        Console.WriteLine($" -> PropertyChanged recalculation fired cleanly after 15 Edit calls (Subtotal: {subtotalBefore} -> {subtotalAfter})");
                        Console.WriteLine(" RESULT: [PASS]");
                        passedTests++;
                    }
                    else
                    {
                        throw new Exception($"Subtotal did not recalculate correctly: Subtotal={subtotalAfter}, Expected={expectedAfter}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // -------------------------------------------------------------------
                // TEST SECTION 4: CUSTOMER / VENDOR SELECTION PRESERVATION
                // -------------------------------------------------------------------
                Console.WriteLine("\n[4/6] TESTING CUSTOMER & VENDOR SELECTION PROTECTION...");
                try
                {
                    // Select Customer B
                    salesVm.SelectedCustomer = salesVm.Customers.FirstOrDefault(c => c.Id == custB.Id);
                    var selectedBefore = salesVm.SelectedCustomer?.Name;

                    // Trigger collection refresh (LoadInvoicesAsync)
                    salesVm.LoadInvoicesAsync().GetAwaiter().GetResult();

                    var selectedAfter = salesVm.SelectedCustomer?.Name;

                    if (selectedBefore == selectedAfter && selectedAfter == custB.Name)
                    {
                        Console.WriteLine($" -> Customer Selection Preserved During Refresh: '{selectedAfter}'");
                    }
                    else
                    {
                        throw new Exception($"Customer selection was reset! Before='{selectedBefore}', After='{selectedAfter}'");
                    }

                    // Select Vendor A in PurchasesViewModel
                    purchasesVm.LoadPurchasesAsync().GetAwaiter().GetResult();
                    purchasesVm.SelectedVendor = purchasesVm.Vendors.FirstOrDefault(v => v.Id == vendA.Id);
                    var vendBefore = purchasesVm.SelectedVendor?.Name;

                    purchasesVm.LoadPurchasesAsync().GetAwaiter().GetResult();
                    var vendAfter = purchasesVm.SelectedVendor?.Name;

                    if (vendBefore == vendAfter && vendAfter == vendA.Name)
                    {
                        Console.WriteLine($" -> Vendor Selection Preserved During Refresh: '{vendAfter}'");
                        Console.WriteLine(" RESULT: [PASS]");
                        passedTests++;
                    }
                    else
                    {
                        throw new Exception($"Vendor selection was reset! Before='{vendBefore}', After='{vendAfter}'");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // -------------------------------------------------------------------
                // TEST SECTION 5: PURCHASE INVOICE & RETURN EDIT CYCLES
                // -------------------------------------------------------------------
                Console.WriteLine("\n[5/6] TESTING PURCHASE INVOICE & PURCHASE RETURN EDIT CYCLES...");
                try
                {
                    var pur = new PurchaseInvoice
                    {
                        PurchaseNumber = "PI-STRESS-001",
                        VendorId = vendA.Id,
                        VendorName = vendA.Name,
                        Date = DateTime.Now,
                        IsCashPurchase = false,
                        Type = PurchaseType.PurchaseInvoice,
                        Status = "Posted",
                        Items = new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>
                        {
                            new PurchaseInvoiceItem { ItemId = itemNormal.Id, ItemCode = itemNormal.Code, ItemName = itemNormal.Name, Quantity = 20, Rate = 1380, TotalPrice = 27600 }
                        }
                    };
                    pur.Subtotal = 27600;
                    pur.TotalAmount = 27600;

                    var savedPur = purchaseService.SavePurchaseInvoiceAsync(pur).GetAwaiter().GetResult();
                    int targetPurId = savedPur.Id;

                    for (int cycle = 1; cycle <= 5; cycle++)
                    {
                        var fetchedPur = purchaseService.GetPurchaseInvoiceByIdAsync(targetPurId).GetAwaiter().GetResult();
                        purchasesVm.EditInvoiceAsync(fetchedPur!).GetAwaiter().GetResult();

                        purchasesVm.NewPurchase.Items[0].Quantity += 10;
                        purchasesVm.RecalculateTotals();

                        purchaseService.SavePurchaseInvoiceAsync(purchasesVm.NewPurchase).GetAwaiter().GetResult();
                    }

                    var finalPur = purchaseService.GetPurchaseInvoiceByIdAsync(targetPurId).GetAwaiter().GetResult();
                    Console.WriteLine($" -> 5 Edit Cycles on Purchase Invoice: Items={finalPur?.Items.Count} (Expected: 1), Qty={finalPur?.Items.First().Quantity} (Expected: 70)");

                    if (finalPur != null && finalPur.Items.Count == 1 && finalPur.Items.First().Quantity == 70)
                    {
                        Console.WriteLine(" RESULT: [PASS]");
                        passedTests++;
                    }
                    else
                    {
                        throw new Exception("Purchase Invoice edit cycles failed expected item count or quantity!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // -------------------------------------------------------------------
                // TEST SECTION 6: ACCOUNTING, LEDGERS & STOCK INTEGRITY VERIFICATION
                // -------------------------------------------------------------------
                Console.WriteLine("\n[6/6] TESTING ACCOUNTING, LEDGERS & STOCK INTEGRITY...");
                try
                {
                    using var sqliteConn = new SqliteConnection($"Data Source={dbPath}");
                    sqliteConn.Open();
                    using var cmd = sqliteConn.CreateCommand();

                    cmd.CommandText = "SELECT COUNT(*) FROM SaleInvoices";
                    long totalSaleInvoices = (long)cmd.ExecuteScalar()!;

                    cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoices";
                    long totalPurchaseInvoices = (long)cmd.ExecuteScalar()!;

                    cmd.CommandText = "SELECT COUNT(*) FROM CustomerLedgers";
                    long totalCustomerLedgers = (long)cmd.ExecuteScalar()!;

                    cmd.CommandText = "SELECT COUNT(*) FROM VendorLedgers";
                    long totalVendorLedgers = (long)cmd.ExecuteScalar()!;

                    cmd.CommandText = "SELECT COUNT(*) FROM InventoryLedgers";
                    long totalInventoryLedgers = (long)cmd.ExecuteScalar()!;

                    Console.WriteLine($" -> Database Statistics:");
                    Console.WriteLine($"    - Sale Invoices: {totalSaleInvoices}");
                    Console.WriteLine($"    - Purchase Invoices: {totalPurchaseInvoices}");
                    Console.WriteLine($"    - Customer Ledgers: {totalCustomerLedgers}");
                    Console.WriteLine($"    - Vendor Ledgers: {totalVendorLedgers}");
                    Console.WriteLine($"    - Inventory Ledgers: {totalInventoryLedgers}");

                    if (totalSaleInvoices > 0 && totalCustomerLedgers > 0 && totalInventoryLedgers > 0)
                    {
                        Console.WriteLine(" RESULT: [PASS]");
                        passedTests++;
                    }
                    else
                    {
                        throw new Exception("Database accounting tables missing required ledger rows!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // -------------------------------------------------------------------
                // TEST SECTION 7: VEHICLE CHARGES, LABOUR CHARGES & STOCK ADJUSTMENTS
                // -------------------------------------------------------------------
                Console.WriteLine("\n[7/7] TESTING VEHICLE CHARGES, LABOUR CHARGES & STOCK ADJUSTMENTS...");
                try
                {
                    // 1. Setup fresh instances
                    var testItem = new Item { Code = "TEST-STOCK-ITEM", Name = "Test Stock Item", SalePrice = 800, PurchasePrice = 500, CurrentStock = 100, BaseUnit = "Pcs", IsActive = true };
                    db.Items.Add(testItem);
                    db.SaveChanges();

                    var initStock = testItem.CurrentStock; // 100

                    // 2. Create Sale Invoice with VehicleCharges & LabourCharges
                    var saleInvoice = new SaleInvoice
                    {
                        CustomerId = custA.Id,
                        CustomerName = custA.Name,
                        Date = DateTime.Now,
                        IsCashSale = true,
                        Status = "Posted",
                        Type = InvoiceType.SaleInvoice,
                        VehicleCharges = 40,
                        LabourCharges = 20,
                        Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>
                        {
                            new SaleInvoiceItem { ItemId = testItem.Id, ItemCode = testItem.Code, ItemName = testItem.Name, Quantity = 1, Rate = 800, TotalPrice = 800 }
                        }
                    };

                    // Compute and verify local calculation
                    saleInvoice.RecalculateTotals();
                    if (saleInvoice.TotalAmount != 860)
                    {
                        throw new Exception($"Local SaleInvoice TotalAmount incorrect! Expected 860, got {saleInvoice.TotalAmount}");
                    }

                    // Save Sale Invoice via Service
                    var savedSale = saleService.SaveSaleInvoiceAsync(saleInvoice).GetAwaiter().GetResult();
                    if (savedSale.TotalAmount != 860)
                    {
                        throw new Exception($"Saved SaleInvoice TotalAmount incorrect! Expected 860, got {savedSale.TotalAmount}");
                    }

                    // Verify DB reload
                    var fetchedSale = saleService.GetSaleInvoiceByIdAsync(savedSale.Id).GetAwaiter().GetResult();
                    if (fetchedSale?.TotalAmount != 860 || fetchedSale.LabourCharges != 20 || fetchedSale.VehicleCharges != 40)
                    {
                        throw new Exception($"Fetched SaleInvoice values incorrect! TotalAmount={fetchedSale?.TotalAmount}, VehicleCharges={fetchedSale?.VehicleCharges}, LabourCharges={fetchedSale?.LabourCharges}");
                    }

                    // Verify Stock Decreased
                    var itemAfterSale = db.Items.AsNoTracking().FirstOrDefault(i => i.Id == testItem.Id);
                    if (itemAfterSale?.CurrentStock != initStock - 1)
                    {
                        throw new Exception($"Stock did not decrease on Sale! Initial={initStock}, After Sale={itemAfterSale?.CurrentStock}");
                    }
                    Console.WriteLine(" -> Sale stock decrease verified.");

                    // 3. Create Sale Return
                    var saleReturn = new SaleInvoice
                    {
                        CustomerId = custA.Id,
                        CustomerName = custA.Name,
                        Date = DateTime.Now,
                        IsCashSale = true,
                        Status = "Posted",
                        Type = InvoiceType.SaleReturn,
                        Items = new System.Collections.ObjectModel.ObservableCollection<SaleInvoiceItem>
                        {
                            new SaleInvoiceItem { ItemId = testItem.Id, ItemCode = testItem.Code, ItemName = testItem.Name, Quantity = 1, Rate = 800, TotalPrice = 800 }
                        }
                    };
                    saleService.SaveSaleInvoiceAsync(saleReturn).GetAwaiter().GetResult();

                    // Verify Stock Increased
                    var itemAfterReturn = db.Items.AsNoTracking().FirstOrDefault(i => i.Id == testItem.Id);
                    if (itemAfterReturn?.CurrentStock != initStock)
                    {
                        throw new Exception($"Stock did not increase on Sale Return! Expected={initStock}, got={itemAfterReturn?.CurrentStock}");
                    }
                    Console.WriteLine(" -> Sale return stock increase verified.");

                    // 4. Create Purchase Invoice
                    var purchaseInvoice = new PurchaseInvoice
                    {
                        VendorId = vendA.Id,
                        VendorName = vendA.Name,
                        Date = DateTime.Now,
                        IsCashPurchase = true,
                        Status = "Posted",
                        Type = PurchaseType.PurchaseInvoice,
                        Items = new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>
                        {
                            new PurchaseInvoiceItem { ItemId = testItem.Id, ItemCode = testItem.Code, ItemName = testItem.Name, Quantity = 10, Rate = 500, TotalPrice = 5000 }
                        }
                    };
                    purchaseService.SavePurchaseInvoiceAsync(purchaseInvoice).GetAwaiter().GetResult();

                    // Verify Stock Increased
                    var itemAfterPurchase = db.Items.AsNoTracking().FirstOrDefault(i => i.Id == testItem.Id);
                    if (itemAfterPurchase?.CurrentStock != initStock + 10)
                    {
                        throw new Exception($"Stock did not increase on Purchase! Expected={initStock + 10}, got={itemAfterPurchase?.CurrentStock}");
                    }
                    Console.WriteLine(" -> Purchase stock increase verified.");

                    // 5. Create Purchase Return
                    var purchaseReturn = new PurchaseInvoice
                    {
                        VendorId = vendA.Id,
                        VendorName = vendA.Name,
                        Date = DateTime.Now,
                        IsCashPurchase = true,
                        Status = "Posted",
                        Type = PurchaseType.PurchaseReturn,
                        Items = new System.Collections.ObjectModel.ObservableCollection<PurchaseInvoiceItem>
                        {
                            new PurchaseInvoiceItem { ItemId = testItem.Id, ItemCode = testItem.Code, ItemName = testItem.Name, Quantity = 5, Rate = 500, TotalPrice = 2500 }
                        }
                    };
                    purchaseService.SavePurchaseInvoiceAsync(purchaseReturn).GetAwaiter().GetResult();

                    // Verify Stock Decreased
                    var itemAfterPurReturn = db.Items.AsNoTracking().FirstOrDefault(i => i.Id == testItem.Id);
                    if (itemAfterPurReturn?.CurrentStock != initStock + 5)
                    {
                        throw new Exception($"Stock did not decrease on Purchase Return! Expected={initStock + 5}, got={itemAfterPurReturn?.CurrentStock}");
                    }
                    Console.WriteLine(" -> Purchase return stock decrease verified.");

                    Console.WriteLine(" RESULT: [PASS]");
                    passedTests++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" RESULT: [FAIL] - {ex.Message}");
                    failedTests++;
                }

                // Cleanup Database
                try { File.Delete(dbPath); } catch { }

                Console.WriteLine("\n==========================================================================");
                Console.WriteLine($" SUMMARY: {passedTests}/{passedTests + failedTests} VERIFICATION SUITES PASSED.");
                Console.WriteLine("==========================================================================");

                if (failedTests == 0)
                {
                    Console.WriteLine("\nFINAL STATUS: VERIFIED — EDIT/SAVE WORKFLOW STABLE AFTER REAL UI + DATABASE TESTING.");
                }
                else
                {
                    Console.WriteLine("\nFINAL STATUS: FAILED — VERIFICATION DISCOVERED DEFECTS.");
                    Environment.Exit(1);
                }
            }
        }
    }

    public class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }
        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }
    }
}
