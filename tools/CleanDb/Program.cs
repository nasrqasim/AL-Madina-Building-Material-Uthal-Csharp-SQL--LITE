using Microsoft.Data.Sqlite;
using System;
using System.IO;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP", "Company.db");

if (!File.Exists(dbPath))
{
    Console.WriteLine($"Database not found at: {dbPath}");
    return;
}

// Create backup first
var backupPath = dbPath.Replace(".db", $"_Backup_BeforeClean_{DateTime.Now:yyyyMMdd_HHmmss}.db");
File.Copy(dbPath, backupPath, true);
Console.WriteLine($"Backup created: {Path.GetFileName(backupPath)}");

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

// Clean all transaction/data tables (children first due to foreign keys)
var tablesToClean = new[]
{
    "SaleInvoiceItems",
    "PurchaseInvoiceItems",
    "CustomerLedgers",
    "VendorLedgers",
    "InventoryLedgers",
    "JournalEntries",
    "SaleInvoices",
    "PurchaseInvoices",
    "Receipts",
    "Payments",
    "Expenses",
    "Salaries",
    "SalaryAdvances",
    "Items",
    "Subcategories",
    "Categories",
    "Units",
    "Customers",
    "Vendors",
    "Banks",
    "AccountCategories",
    "Staffs",
};

using var cmd = conn.CreateCommand();

cmd.CommandText = "PRAGMA foreign_keys = OFF;";
cmd.ExecuteNonQuery();

int totalDeleted = 0;
foreach (var table in tablesToClean)
{
    try
    {
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        var count = Convert.ToInt32(cmd.ExecuteScalar());

        cmd.CommandText = $"DELETE FROM {table}";
        cmd.ExecuteNonQuery();

        Console.WriteLine($"  Cleaned {table}: {count} rows deleted");
        totalDeleted += count;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Skipped {table}: {ex.Message}");
    }
}

// Reset auto-increment counters
try
{
    cmd.CommandText = "DELETE FROM sqlite_sequence;";
    cmd.ExecuteNonQuery();
    Console.WriteLine("  Reset auto-increment counters");
}
catch { }

cmd.CommandText = "PRAGMA foreign_keys = ON;";
cmd.ExecuteNonQuery();

cmd.CommandText = "VACUUM;";
cmd.ExecuteNonQuery();

Console.WriteLine($"\nTotal rows deleted: {totalDeleted}");
Console.WriteLine("Database cleaned successfully! Ready for production use.");
Console.WriteLine("CompanySettings and Users have been preserved.");
