using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Interfaces;
using AlMadinaERP.Core.Models;
using AlMadinaERP.Data;

using Microsoft.Extensions.DependencyInjection;

namespace AlMadinaERP.Services
{
    public class SalaryService : ISalaryService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SalaryService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task<Salary> ProcessSalaryAsync(Salary salary)
        {
            using var _context = CreateContext();
            salary.NetPaid = (salary.BasicSalary + salary.Bonus) - (salary.AdvanceDeduction + salary.LoanDeduction);
            if (salary.Date == default)
            {
                salary.Date = DateTime.Now;
            }

            salary.Staff = null;
            if (salary.Id == 0)
                await _context.Salaries.AddAsync(salary);
            else
                _context.Salaries.Update(salary);

            if (salary.StaffId.HasValue && salary.StaffId.Value > 0)
            {
                var staff = await _context.Staffs.FindAsync(salary.StaffId.Value);
                if (staff != null)
                {
                    staff.TotalSalaryPaid += salary.NetPaid;
                    
                    if (salary.AdvanceDeduction > 0)
                    {
                        var repayment = new SalaryAdvance
                        {
                            VoucherNumber = $"SADR-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}",
                            StaffId = staff.Id,
                            StaffName = staff.FullName,
                            Department = staff.Department,
                            Amount = -salary.AdvanceDeduction,
                            Date = salary.Date,
                            RecoveryMonth = salary.SalaryMonth,
                            Status = "Approved",
                            Remarks = $"Advance Deducted in Salary {salary.SalaryMonth}"
                        };
                        await _context.SalaryAdvances.AddAsync(repayment);
                        await _context.SaveChangesAsync();
                    }

                    staff.TotalAdvances = (decimal)(await _context.SalaryAdvances
                        .Where(sa => sa.StaffId == staff.Id)
                        .SumAsync(sa => (double?)sa.Amount) ?? 0);

                    _context.Staffs.Update(staff);
                }
            }

            await _context.SaveChangesAsync();
            return salary;
        }

        public async Task<List<Salary>> GetSalariesAsync(string staffName = "", string salaryMonth = "")
        {
            using var _context = CreateContext();
            var q = _context.Salaries.Include(s => s.Staff).AsQueryable();

            if (!string.IsNullOrWhiteSpace(staffName))
            {
                var term = staffName.Trim().ToLower();
                q = q.Where(s => s.StaffName.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(salaryMonth))
            {
                q = q.Where(s => s.SalaryMonth == salaryMonth);
            }

            return await q.OrderByDescending(s => s.Date).Take(200).AsNoTracking().ToListAsync();
        }

        public async Task<List<Staff>> GetStaffsAsync(string query = "")
        {
            using var _context = CreateContext();
            var q = _context.Staffs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(s => (s.FullName != null && s.FullName.ToLower().Contains(term)) ||
                                 (s.StaffCode != null && s.StaffCode.ToLower().Contains(term)) ||
                                 (s.Designation != null && s.Designation.ToLower().Contains(term)) ||
                                 (s.Department != null && s.Department.ToLower().Contains(term)) ||
                                 (s.Phone != null && s.Phone.ToLower().Contains(term)) ||
                                 (s.CNIC != null && s.CNIC.ToLower().Contains(term)));
            }

            return await q.OrderBy(s => s.FullName).AsNoTracking().ToListAsync();
        }

        public async Task<Staff> SaveStaffAsync(Staff staff)
        {
            using var _context = CreateContext();
            if (string.IsNullOrWhiteSpace(staff.StaffCode))
            {
                staff.StaffCode = "STF-" + DateTime.Now.ToString("fffSSmm");
            }

            if (staff.Id == 0)
            {
                await _context.Staffs.AddAsync(staff);
            }
            else
            {
                var trackedStaff = await _context.Staffs.FindAsync(staff.Id);
                if (trackedStaff != null)
                {
                    _context.Entry(trackedStaff).CurrentValues.SetValues(staff);
                }
                else
                {
                    _context.Staffs.Update(staff);
                }

                var existingSalaries = await _context.Salaries.Where(s => s.StaffId == staff.Id).ToListAsync();
                foreach (var sal in existingSalaries)
                {
                    sal.StaffName = staff.FullName;
                }

                var existingAdvances = await _context.SalaryAdvances.Where(sa => sa.StaffId == staff.Id).ToListAsync();
                foreach (var adv in existingAdvances)
                {
                    adv.StaffName = staff.FullName;
                    adv.Department = staff.Department;
                }
            }

            await _context.SaveChangesAsync();
            return staff;
        }

        public async Task DeleteStaffAsync(int id)
        {
            using var _context = CreateContext();
            var item = await _context.Staffs.FindAsync(id);
            if (item != null)
            {
                var salaries = await _context.Salaries.Where(s => s.StaffId == id).ToListAsync();
                if (salaries.Any())
                    _context.Salaries.RemoveRange(salaries);

                var advances = await _context.SalaryAdvances.Where(sa => sa.StaffId == id).ToListAsync();
                if (advances.Any())
                    _context.SalaryAdvances.RemoveRange(advances);

                _context.Staffs.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<SalaryAdvance>> GetSalaryAdvancesAsync(string query = "")
        {
            using var _context = CreateContext();
            var q = _context.SalaryAdvances.Include(sa => sa.Staff).AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();
                q = q.Where(sa => sa.VoucherNumber.ToLower().Contains(query) ||
                                  sa.StaffName.ToLower().Contains(query) ||
                                  sa.Department.ToLower().Contains(query));
            }

            return await q.OrderByDescending(sa => sa.Date).Take(200).AsNoTracking().ToListAsync();
        }

        public async Task<SalaryAdvance> SaveSalaryAdvanceAsync(SalaryAdvance advance)
        {
            using var _context = CreateContext();
            if (string.IsNullOrWhiteSpace(advance.VoucherNumber))
            {
                advance.VoucherNumber = "ADV-" + DateTime.Now.ToString("fffSSmm");
            }
            if (advance.Date == default)
            {
                advance.Date = DateTime.Now;
            }

            advance.Staff = null;
            if (advance.Id == 0)
            {
                await _context.SalaryAdvances.AddAsync(advance);
            }
            else
            {
                var trackedAdv = await _context.SalaryAdvances.FindAsync(advance.Id);
                if (trackedAdv != null)
                {
                    _context.Entry(trackedAdv).CurrentValues.SetValues(advance);
                }
                else
                {
                    _context.SalaryAdvances.Update(advance);
                }
            }

            await _context.SaveChangesAsync();

            if (advance.StaffId.HasValue && advance.StaffId.Value > 0)
            {
                var staff = await _context.Staffs.FindAsync(advance.StaffId.Value);
                if (staff != null)
                {
                    staff.TotalAdvances = (decimal)(await _context.SalaryAdvances
                        .Where(sa => sa.StaffId == staff.Id)
                        .SumAsync(sa => (double?)sa.Amount) ?? 0);
                    advance.StaffName = staff.FullName;
                    advance.Department = staff.Department;
                    await _context.SaveChangesAsync();
                }
            }

            return advance;
        }

        public async Task DeleteSalaryAdvanceAsync(int id)
        {
            using var _context = CreateContext();
            var item = await _context.SalaryAdvances.FindAsync(id);
            if (item != null)
            {
                int? staffId = item.StaffId;
                _context.SalaryAdvances.Remove(item);
                await _context.SaveChangesAsync();

                if (staffId.HasValue && staffId.Value > 0)
                {
                    var staff = await _context.Staffs.FindAsync(staffId.Value);
                    if (staff != null)
                    {
                        staff.TotalAdvances = (decimal)(await _context.SalaryAdvances
                            .Where(sa => sa.StaffId == staff.Id)
                            .SumAsync(sa => (double?)sa.Amount) ?? 0);
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        public async Task<List<JournalEntry>> GetJournalEntriesAsync(string query = "")
        {
            using var _context = CreateContext();
            var q = _context.JournalEntries.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLower();
                q = q.Where(j => j.VoucherNumber.ToLower().Contains(query) ||
                                 j.AccountName.ToLower().Contains(query) ||
                                 j.Remarks.ToLower().Contains(query));
            }

            return await q.OrderByDescending(j => j.Date).Take(200).AsNoTracking().ToListAsync();
        }

        public async Task<JournalEntry> SaveJournalEntryAsync(JournalEntry entry)
        {
            using var _context = CreateContext();
            if (string.IsNullOrWhiteSpace(entry.VoucherNumber))
            {
                entry.VoucherNumber = "JV-" + DateTime.Now.ToString("fffSSmm");
            }
            if (entry.Date == default)
            {
                entry.Date = DateTime.Now;
            }

            if (entry.Id == 0)
                await _context.JournalEntries.AddAsync(entry);
            else
                _context.JournalEntries.Update(entry);

            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task DeleteJournalEntryAsync(int id)
        {
            using var _context = CreateContext();
            var item = await _context.JournalEntries.FindAsync(id);
            if (item != null)
            {
                _context.JournalEntries.Remove(item);
                await _context.SaveChangesAsync();
            }
        }
    }
}
