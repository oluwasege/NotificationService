using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Data;
using System;

namespace NotificationService.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NotificationDbContext _context;
    private IDbContextTransaction _transaction;
    private bool _disposed;
    private readonly Dictionary<Type, object> repositories = [];

    public UnitOfWork(IDbContextFactory<NotificationDbContext> contextFactory)
    {
        // Create one context per UoW scope to share across repositories/transactions
        _context = contextFactory.CreateDbContext();
    }

    public IRepository<TEntity> GetRepository<TEntity>() where TEntity : class
    {
        if (repositories.TryGetValue(typeof(TEntity), out var repo))
        {
            return (IRepository<TEntity>)repo;
        }

        var newRepo = new Repository<TEntity>(_context);
        repositories[typeof(TEntity)] = newRepo;
        return newRepo;
    }
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, ILogger logger = null, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                await operation();
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.LogError(ex, "Error occurred during transactional operation");
                }
                else
                {
                    Console.WriteLine($"Error occurred during transactional operation: {ex.Message}");
                }
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
        _disposed = true;
    }
}
