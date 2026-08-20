using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AlMadinaERP.Core.Interfaces;

namespace AlMadinaERP.Data
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public Repository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private AppDbContext CreateContext() => _contextFactory.CreateDbContext();

        public async Task<T?> GetByIdAsync(int id)
        {
            using var context = CreateContext();
            return await context.Set<T>().FindAsync(id);
        }

        public async Task<List<T>> GetAllAsync()
        {
            using var context = CreateContext();
            return await context.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            using var context = CreateContext();
            return await context.Set<T>().AsNoTracking().Where(predicate).ToListAsync();
        }

        public async Task AddAsync(T entity)
        {
            using var context = CreateContext();
            await context.Set<T>().AddAsync(entity);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            using var context = CreateContext();
            context.Set<T>().Update(entity);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(T entity)
        {
            using var context = CreateContext();
            context.Set<T>().Remove(entity);
            await context.SaveChangesAsync();
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            using var context = CreateContext();
            if (predicate == null)
                return await context.Set<T>().CountAsync();
            return await context.Set<T>().CountAsync(predicate);
        }
    }
}
