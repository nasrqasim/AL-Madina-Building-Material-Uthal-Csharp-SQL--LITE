using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

namespace AlMadinaERP.Services
{
    public class BackupService : IBackupService
    {
        private readonly AppDbContext _context;

        public BackupService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> CreateBackupAsync(string targetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(targetFolderPath))
            {
                targetFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP", "Backups");
            }

            if (!Directory.Exists(targetFolderPath))
            {
                Directory.CreateDirectory(targetFolderPath);
            }

            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP");
            var sourceDbPath = Path.Combine(appDataFolder, "Company.db");
            var backupFileName = $"Company_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destPath = Path.Combine(targetFolderPath, backupFileName);

            // Use native online SQLite Backup API to safely copy active databases
            using (var sourceConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sourceDbPath};Foreign Keys=False;"))
            using (var destinationConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={destPath};Foreign Keys=False;"))
            {
                await sourceConnection.OpenAsync();
                await destinationConnection.OpenAsync();
                sourceConnection.BackupDatabase(destinationConnection);
            }

            return destPath;
        }

        public async Task RestoreBackupAsync(string backupFilePath)
        {
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found", backupFilePath);

            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP");
            var destDbPath = Path.Combine(appDataFolder, "Company.db");

            // Close connection & checkpoint before overwrite
            await _context.Database.CloseConnectionAsync();

            File.Copy(backupFilePath, destDbPath, overwrite: true);

            // Safely delete stale WAL/SHM log files if they exist to prevent recovery corruption
            var walPath = destDbPath + "-wal";
            var shmPath = destDbPath + "-shm";
            try
            {
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);
            }
            catch (Exception)
            {
                // Log and swallow gracefully if files are locked/unavailable
            }

            await _context.Database.OpenConnectionAsync();
            await _context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        public async Task PerformAutoBackupIfEnabledAsync(CompanySetting setting)
        {
            if (setting != null && setting.AutoBackupDaily)
            {
                var backupDir = string.IsNullOrWhiteSpace(setting.BackupPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP", "Backups")
                    : setting.BackupPath;

                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Prevent duplicates if already backed up today
                var todayPattern = $"Company_Backup_{DateTime.Now:yyyyMMdd}_*.db";
                try
                {
                    var files = Directory.GetFiles(backupDir, todayPattern);
                    if (files.Length > 0)
                    {
                        return; // Already backed up today
                    }
                }
                catch
                {
                    // Fallback to continue backup if file listing fails
                }

                await CreateBackupAsync(backupDir);
            }
        }
    }
}
