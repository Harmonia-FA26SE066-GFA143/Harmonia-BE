using System.Linq.Expressions;
using Harmonia.Application.Common.Models;
using Harmonia.Domain.Common;

namespace Harmonia.Application.Interfaces.IRepositories;

/// <summary>
/// Operations every entity needs. Inject it directly for plain CRUD; entities with specific
/// queries get their own <c>I&lt;Entity&gt;Repository</c> that extends this interface.
/// </summary>
public interface IGenericRepository<T> where T : BaseEntity
{
    /// <summary>Tracked, so the caller can modify the entity and then call <see cref="SaveChangesAsync"/>.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only rows matching <paramref name="predicate"/>, unordered.</summary>
    Task<List<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken);

    Task AddAsync(T entity, CancellationToken cancellationToken);

    /// <summary>Marks for deletion; the row is removed on <see cref="SaveChangesAsync"/>.</summary>
    void Remove(T entity);

    /// <summary>Saves every pending change in the request's DbContext, not only this entity type.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/>, and every save it makes, in one database transaction: committed when it
    /// returns success, rolled back when it fails or throws. It may run again on a transient database error.
    /// </summary>
    Task<Result> ExecuteInTransactionAsync(Func<Task<Result>> operation, CancellationToken cancellationToken);
}
