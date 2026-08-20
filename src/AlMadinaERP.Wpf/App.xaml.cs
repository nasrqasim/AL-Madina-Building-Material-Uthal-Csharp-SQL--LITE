using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;
using AlMadinaERP.Services;
using AlMadinaERP.Wpf.ViewModels;
using AlMadinaERP.Wpf.Views;

namespace AlMadinaERP.Wpf
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        private static bool _hasShownUnhandledErrorDialog = false;

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Register universal mouse wheel and touchpad scrolling handler across entire application
            AlMadinaERP.Wpf.Helpers.UniversalScrollHelper.RegisterGlobalScrollHandler();

            // PRIORITY 1 & 8: Global exception handling with infinite loop prevention
            DispatcherUnhandledException += (s, ev) =>
            {
                LogError("DispatcherUnhandledException", ev.Exception);
                ev.Handled = true; // Prevents crash

                // Do NOT show front-end popup dialog for harmless WPF internal binding/text-search backspace glitches
                bool isWpfInternalGlitch = ev.Exception is NullReferenceException &&
                    (ev.Exception.StackTrace?.Contains("PropertyPathWorker") == true ||
                     ev.Exception.StackTrace?.Contains("TextUpdated") == true ||
                     ev.Exception.StackTrace?.Contains("OnBackspace") == true);

                if (isWpfInternalGlitch)
                {
                    return; // Handled silently, no modal popup on front-end
                }

                // Prevent infinite modal popup loops
                if (!_hasShownUnhandledErrorDialog)
                {
                    _hasShownUnhandledErrorDialog = true;
                    MessageBox.Show(
                        $"An unexpected UI issue occurred:\n{ev.Exception.Message}\n\nDetails have been saved to error.log. You can continue using the application.",
                        "AL Madina ERP - System Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is Exception ex)
                {
                    LogError("UnhandledException", ex);
                }
            };

            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                LogError("UnobservedTaskException", ev.Exception);
                ev.SetObserved();
            };

            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);

                ServiceProvider = services.BuildServiceProvider();

                // Initialize Database asynchronously on startup
                using (var scope = ServiceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await EnsureDatabaseSchemaAsync(dbContext);

                    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                    await authService.EnsureSuperadminExistsAsync();
                }

                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                LogError("StartupError", ex);
                MessageBox.Show($"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "AL Madina ERP - Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private static void LogError(string context, Exception ex)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                var logFile = Path.Combine(folder, "error.log");
                var innerMsg = ex.InnerException != null ? $"\nINNER: {ex.InnerException.Message} | {ex.InnerException.InnerException?.Message}" : "";
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex.Message}{innerMsg}\n{ex.StackTrace}\n\n");
            }
            catch { }
        }

        private static async Task EnsureDatabaseSchemaAsync(AppDbContext dbContext)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                dbContext.EnableOptimizations();

                await dbContext.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS CustomerOrders (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OrderNumber TEXT,
                        CustomerName TEXT,
                        Address TEXT,
                        ContactNumber TEXT,
                        OrderDate TEXT,
                        ReceivingDate TEXT,
                        Status TEXT,
                        TotalAmount REAL DEFAULT 0,
                        PaidAmount REAL DEFAULT 0,
                        CreatedAt TEXT,
                        UpdatedAt TEXT
                    );
                ");

                await dbContext.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS CustomerOrderItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        CustomerOrderId INTEGER,
                        ItemId INTEGER,
                        ItemNameSnapshot TEXT,
                        ItemCode TEXT,
                        Unit TEXT,
                        Quantity REAL DEFAULT 0,
                        LengthFeet REAL DEFAULT 0,
                        Rate REAL DEFAULT 0,
                        LineTotal REAL DEFAULT 0
                    );
                ");

                var columns = await dbContext.Database.SqlQueryRaw<string>("SELECT name FROM pragma_table_info('CustomerOrders');").ToListAsync();
                if (!columns.Contains("PaidAmount", StringComparer.OrdinalIgnoreCase))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE CustomerOrders ADD COLUMN PaidAmount REAL DEFAULT 0;");
                }
                if (!columns.Contains("TotalAmount", StringComparer.OrdinalIgnoreCase))
                {
                    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE CustomerOrders ADD COLUMN TotalAmount REAL DEFAULT 0;");
                }
            }
            catch (Exception ex)
            {
                LogError("SchemaMigrationError", ex);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Database Context & DbContextFactory for per-operation DbContext lifetime
            services.AddDbContextFactory<AppDbContext>();
            services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);

            // Repositories
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Business Services
            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IVendorService, VendorService>();
            services.AddScoped<IInventoryService, InventoryService>();
            services.AddScoped<ISaleService, SaleService>();
            services.AddScoped<IPurchaseService, PurchaseService>();
            services.AddScoped<IReceiptPaymentService, ReceiptPaymentService>();
            services.AddScoped<ISalaryService, SalaryService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<ICustomerOrderService, CustomerOrderService>();
            services.AddScoped<IBackupService, BackupService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IPrintService, PrintService>();
            services.AddScoped<IDatabaseSeederAndVerifierService, DatabaseSeederAndVerifierService>();

            // ViewModels (Transient so navigation gets fresh services & DbContext)
            services.AddTransient<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<SalesViewModel>();
            services.AddTransient<CustomerOrdersViewModel>();
            services.AddTransient<PosViewModel>();
            services.AddTransient<PurchasesViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<VendorsViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<ChartOfInventoryViewModel>();
            services.AddTransient<ReceiptsPaymentsViewModel>();
            services.AddTransient<BanksViewModel>();
            services.AddTransient<SalaryViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Views
            services.AddTransient<MainWindow>();
            services.AddTransient<CustomerOrdersView>();
            services.AddTransient<LoginWindow>();
        }
    }
}
