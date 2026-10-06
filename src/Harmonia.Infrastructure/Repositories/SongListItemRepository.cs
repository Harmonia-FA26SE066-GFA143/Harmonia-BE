using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class SongListItemRepository(HarmoniaDbContext dbContext)
    : GenericRepository<SongListItem>(dbContext), ISongListItemRepository
{
    public Task<SongListItem?> GetWithPersonnelRequirementsAsync(Guid id, CancellationToken cancellationToken) =>
        DbContext.SongListItems
            .Include(x => x.PersonnelRequirements).ThenInclude(x => x.Skill)
            .Include(x => x.SongList).ThenInclude(x => x.LiturgicalEvent).ThenInclude(x => x.ServiceRoster)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> IsLatestVersionAsync(Guid eventId, int version, CancellationToken cancellationToken) =>
        !await DbContext.SongLists.AnyAsync(x => x.EventId == eventId && x.Version > version, cancellationToken);

    public Task<Dictionary<Guid, Skill>> GetSkillsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        DbContext.Skills.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
}
