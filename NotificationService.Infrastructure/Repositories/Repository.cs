using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using System;
using System.Linq.Expressions;

namespace NotificationService.Infrastructure.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly NotificationDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public Repository(NotificationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id, cancellationToken], cancellationToken: cancellationToken);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await _dbSet.CountAsync(cancellationToken)
            : await _dbSet.CountAsync(predicate, cancellationToken);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        _dbSet.UpdateRange(entities);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        _dbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public virtual Task SoftDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        // If TEntity supports soft delete, set the appropriate property (e.g., IsDeleted) here.
        // Otherwise, fallback to Update as before.
        var softDeleteProp = typeof(TEntity).GetProperty("IsDeleted");
        if (softDeleteProp != null && softDeleteProp.PropertyType == typeof(bool))
        {
            softDeleteProp.SetValue(entity, true);
        }
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual IQueryable<TEntity> Query()
    {
        return _dbSet.AsQueryable();
    }

    public virtual IQueryable<TEntity> QueryNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    public virtual IQueryable<TEntity> GetAllQueryable(
            Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            string includeProperties = "")
    {
        IQueryable<TEntity> query = _context.Set<TEntity>().AsNoTracking();
        if (filter != null)
        {
            query = query.Where(filter);
        }
        if (!string.IsNullOrEmpty(includeProperties))
        {
            foreach (var includeProperty in includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(includeProperty);
            }
        }
        if (orderBy != null)
        {
            return orderBy(query);
        }
        if (typeof(BaseEntity<TEntity>).IsAssignableFrom(typeof(TEntity)))
        {
            query = query.OrderByDescending(e => ((BaseEntity<TEntity>)(object)e).CreatedAt);
        }

        return query;
    }
}
