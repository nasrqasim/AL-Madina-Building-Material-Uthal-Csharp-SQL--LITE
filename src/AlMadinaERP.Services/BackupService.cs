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

            // Checkpoint WAL log into main DB file
            try
            {
                await _context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);");
            }
            catch
            {
            }

            var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP");
            var sourceDbPath = Path.Combine(appDataFolder, "Company.db");
            var backupFileName = $"Company_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destPath = Path.Combine(targetFolderPath, backupFileName);

            if (File.Exists(sourceDbPath))
            {
                File.Copy(sourceDbPath, destPath, overwrite: true);
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

            await _context.Database.OpenConnectionAsync();
            await _context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        public async Task PerformAutoBackupIfEnabledAsync(CompanySetting setting)
        {
            if (setting.AutoBackupDaily)
            {
                var backupDir = string.IsNullOrWhiteSpace(setting.BackupPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlMadinaERP", "Backups")
                    : setting.BackupPath;

                await CreateBackupAsync(backupDir);
            }
        }
    }
}
