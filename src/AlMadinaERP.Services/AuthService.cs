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
            // Legacy/Plaintext check fallback
            if (password == storedHash) return true;

            var computedHash = HashPassword(password);
            return computedHash == storedHash;
        }
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private static User? _currentUser;

        public User? CurrentUser => _currentUser;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public async Task EnsureSuperadminExistsAsync()
        {
            try
            {
                var users = await _context.Users.ToListAsync();
                var superadmin = users.FirstOrDefault(u => u.Username.Equals("Superadmin", StringComparison.OrdinalIgnoreCase));
                
                if (superadmin == null)
                {
                    // Check if default admin exists and rename/update, otherwise create Superadmin
                    var defaultAdmin = users.FirstOrDefault(u => u.Username.Equals("admin", StringComparison.OrdinalIgnoreCase));
                    if (defaultAdmin != null)
                    {
                        defaultAdmin.Username = "Superadmin";
                        defaultAdmin.PasswordHash = PasswordHasher.HashPassword("admin123");
                        defaultAdmin.FullName = "Super Administrator";
                        defaultAdmin.Role = UserRole.Admin;
                        defaultAdmin.IsActive = true;
                    }
                    else
                    {
                        _context.Users.Add(new User
                        {
                            Username = "Superadmin",
                            PasswordHash = PasswordHasher.HashPassword("admin123"),
                            FullName = "Super Administrator",
                            Role = UserRole.Admin,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        });
                    }
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

            await EnsureSuperadminExistsAsync();

            var users = await _context.Users.Where(u => u.IsActive).ToListAsync();
            var user = users.FirstOrDefault(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));

            if (user == null)
                return null;

            if (PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                // Upgrade plain-text hash if needed
                if (user.PasswordHash == password)
                {
                    user.PasswordHash = PasswordHasher.HashPassword(password);
                    await _context.SaveChangesAsync();
                }

                _currentUser = user;
                return user;
            }

            return null;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                return false;

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
