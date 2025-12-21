using Microsoft.Extensions.Logging;

namespace NotificationService.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    IRepository<TEntity> GetRepository<TEntity>() where TEntity : class;
    Task ExecuteInTransactionAsync(Func<Task> operation, ILogger logger = null, CancellationToken cancellationToken = default);
}
