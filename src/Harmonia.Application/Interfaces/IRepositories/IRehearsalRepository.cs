using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface IRehearsalRepository : IGenericRepository<Rehearsal>
{
    /// <summary>
    /// Rehearsals not ended at <paramref name="fromTime"/> (an ongoing one included), earliest first, that are standalone
    /// or belong to a Published event. When <paramref name="choirIds"/> is set, only rehearsals of those choirs or of
    /// no choir.
    /// </summary>
    Task<List<Rehearsal>> GetUpcomingAsync(
        DateTime fromTime, IReadOnlyCollection<Guid>? choirIds, CancellationToken cancellationToken);

    /// <summary>
    /// Same visibility as <see cref="GetUpcomingAsync"/>, but rehearsals starting between the parish dates
    /// <paramref name="fromDate"/> and <paramref name="toDate"/> (inclusive), past ones included, earliest first.
    /// </summary>
    Task<List<Rehearsal>> GetStartingBetweenAsync(
        DateOnly fromDate, DateOnly toDate, IReadOnlyCollection<Guid>? choirIds, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only rehearsal that is standalone or belongs to a Published event, and belongs to one of
    /// <paramref name="choirIds"/> or to no choir when that is set; null otherwise. Location and Songs.Song
    /// are loaded; Attendances holds only the member's row.
    /// </summary>
    Task<Rehearsal?> GetVisibleForMemberAsync(
        Guid id, Guid memberId, IReadOnlyCollection<Guid>? choirIds, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page, newest start first, with Location and Songs (with Song) loaded. When <paramref name="choirIds"/>
    /// is set, only rehearsals of those choirs or of no choir.
    /// </summary>
    Task<PagedList<Rehearsal>> SearchAsync(
        SearchRehearsalsRequest filter, IReadOnlyCollection<Guid>? choirIds, CancellationToken cancellationToken);

    /// <summary>Tracked, with Location and Songs (with Song) loaded.</summary>
    Task<Rehearsal?> GetWithDetailsForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="Rehearsal.Attendances"/> loaded.</summary>
    Task<Rehearsal?> GetWithAttendancesForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves pending attendance changes. Returns false when another request recorded the same member
    /// first (unique (RehearsalId, MemberId)); nothing is saved then.
    /// </summary>
    Task<bool> TrySaveAttendancesAsync(Rehearsal rehearsal, CancellationToken cancellationToken);

    /// <summary>Tracked, with <see cref="Rehearsal.Songs"/> loaded.</summary>
    Task<Rehearsal?> GetWithSongsForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only programme of the rehearsal in display order, each with its <see cref="RehearsalSong.Song"/>.</summary>
    Task<List<RehearsalSong>> GetSongsAsync(Guid rehearsalId, CancellationToken cancellationToken);

    /// <summary>Read-only rehearsals of the event, with <see cref="Rehearsal.Attendances"/> loaded.</summary>
    Task<List<Rehearsal>> GetByEventWithAttendancesAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// True when a rehearsal at the location overlaps [start, end); touching ends do not conflict.
    /// <paramref name="excludeId"/> skips the rehearsal being edited.
    /// </summary>
    Task<bool> HasConflictAsync(
        Guid locationId, DateTime start, DateTime end, Guid? excludeId, CancellationToken cancellationToken);
}