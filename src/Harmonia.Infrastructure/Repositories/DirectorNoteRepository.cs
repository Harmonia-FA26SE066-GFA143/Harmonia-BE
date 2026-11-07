using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Domain.Entities;
using Harmonia.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.Infrastructure.Repositories;

public class DirectorNoteRepository(HarmoniaDbContext dbContext)
    : GenericRepository<DirectorNote>(dbContext), IDirectorNoteRepository
{
    public async Task<PagedList<DirectorNote>> SearchForUserAsync(
        Guid userId, SearchDirectorNotesRequest filter, CancellationToken cancellationToken)
    {
        var query = DbContext.DirectorNotes
            .AsNoTracking()
            .Where(x => x.FromUserId == userId || x.ToUserId == userId);

        if (filter.EventId is { } eventId)
        {
            query = query.Where(x => x.EventId == eventId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await WithDetails(query)
            .OrderByDescending(x => x.SentAt)
            .ThenBy(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<DirectorNote>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<DirectorNote?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        WithDetails(DbContext.DirectorNotes.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private static IQueryable<DirectorNote> WithDetails(IQueryable<DirectorNote> query) =>
        query
            .Include(x => x.LiturgicalEvent)
            .Include(x => x.FromUser)
            .Include(x => x.ToUser);
}
