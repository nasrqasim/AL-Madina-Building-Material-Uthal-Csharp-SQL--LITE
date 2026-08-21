using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Enums;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

using Microsoft.Extensions.DependencyInjection;

namespace AlMadinaERP.Services
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password + "AlMadinaSalt2026SecureKey");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            var computedHash = HashPassword(password);
            return computedHash == storedHash;
        }
    }

    public class AuthService : IAuthService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private static User? _currentUser;

        public User? CurrentUser => _currentUser;

        public AuthService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task EnsureSuperadminExistsAsync()
        {
            try
            {
                using var _context = CreateContext();
                var anyUser = await _context.Users.AnyAsync();
                
                if (!anyUser)
                {
                    _context.Users.Add(new User
                    {
                        Username = "Superadmin",
                        PasswordHash = PasswordHasher.HashPassword("admin1234"),
                        FullName = "Super Administrator",
                        Role = UserRole.Admin,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception)
            {
                // Fallback gracefully if database initialization handles it
            }
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            var uTrim = username.Trim();
            var pTrim = password.Trim();

            try
            {
                await EnsureSuperadminExistsAsync();
            }
            catch { }

            User? user = null;
            try
            {
                using var _context = CreateContext();
                _context.ChangeTracker.Clear();
                var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
                user = users.FirstOrDefault(u => u.Username.Equals(uTrim, StringComparison.OrdinalIgnoreCase));
                if (user == null && (uTrim.Equals("admin", StringComparison.OrdinalIgnoreCase) || uTrim.Equals("superadmin", StringComparison.OrdinalIgnoreCase)))
                {
                    user = users.FirstOrDefault(u => u.Username.Equals("Superadmin", StringComparison.OrdinalIgnoreCase) || u.Username.Equals("admin", StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }

            if (user != null)
            {
                if (PasswordHasher.VerifyPassword(pTrim, user.PasswordHash))
                {
                    _currentUser = user;
                    return user;
                }
            }

            return null;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            using var _context = CreateContext();
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                user = _currentUser ?? await _context.Users.FirstOrDefaultAsync(u => u.Username == "Superadmin");
            }

            if (user == null)
                return false;

            if (!PasswordHasher.VerifyPassword(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = PasswordHasher.HashPassword(newPassword.Trim());
            await _context.SaveChangesAsync();
            return true;
        }

        public void Logout()
        {
            _currentUser = null;
        }
    }
}
