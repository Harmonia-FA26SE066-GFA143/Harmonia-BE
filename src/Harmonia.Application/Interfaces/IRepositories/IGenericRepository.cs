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

    Task AddAsync(T entity, CancellationToken cancellationToken);

    /// <summary>Marks for deletion; the row is removed on <see cref="SaveChangesAsync"/>.</summary>
    void Remove(T entity);

    /// <summary>Saves every pending change in the request's DbContext, not only this entity type.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
