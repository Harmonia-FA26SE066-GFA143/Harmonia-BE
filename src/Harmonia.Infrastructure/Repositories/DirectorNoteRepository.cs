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

        if (filter.WithUserId is { } withUserId)
        {
            query = query.Where(x => x.FromUserId == withUserId || x.ToUserId == withUserId);
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

    public async Task<List<(DirectorNote LastNote, int UnreadCount)>> GetConversationsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var lastNoteIds = await DbContext.DirectorNotes
            .Where(x => x.FromUserId == userId || x.ToUserId == userId)
            .GroupBy(x => x.FromUserId == userId ? x.ToUserId : x.FromUserId)
            .Select(g => g.OrderByDescending(x => x.SentAt).ThenBy(x => x.Id).Select(x => x.Id).First())
            .ToListAsync(cancellationToken);

        var lastNotes = await WithDetails(DbContext.DirectorNotes.AsNoTracking())
            .Where(x => lastNoteIds.Contains(x.Id))
            .OrderByDescending(x => x.SentAt)
            .ToListAsync(cancellationToken);

        var unreadBySender = await DbContext.DirectorNotes
            .Where(x => x.ToUserId == userId && x.ReadAt == null)
            .GroupBy(x => x.FromUserId)
            .Select(g => new { SenderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SenderId, x => x.Count, cancellationToken);

        return lastNotes
            .Select(n => (n, unreadBySender.GetValueOrDefault(n.FromUserId == userId ? n.ToUserId : n.FromUserId)))
            .ToList();
    }

    public Task<int> MarkReadAsync(Guid toUserId, Guid fromUserId, DateTime readAt, CancellationToken cancellationToken) =>
        DbContext.DirectorNotes
            .Where(x => x.ToUserId == toUserId && x.FromUserId == fromUserId && x.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, readAt), cancellationToken);

    private static IQueryable<DirectorNote> WithDetails(IQueryable<DirectorNote> query) =>
        query
            .Include(x => x.LiturgicalEvent)
            .Include(x => x.FromUser)
            .Include(x => x.ToUser);
}
