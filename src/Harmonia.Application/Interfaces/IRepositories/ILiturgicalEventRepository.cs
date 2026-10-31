using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IRepositories;

public interface ILiturgicalEventRepository : IGenericRepository<LiturgicalEvent>
{
    /// <summary>True when another event already uses the date + time + location; <paramref name="excludeId"/> skips the event being edited.</summary>
    Task<bool> ExistsBySlotAsync(
        DateOnly eventDate, TimeOnly time, Guid locationId, Guid? excludeId, CancellationToken cancellationToken);

    Task<List<LiturgicalEvent>> GetUpcomingPublishedAsync(DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only Published events dated from <paramref name="fromDate"/>, soonest first, on whose Finalized roster the
    /// member has an Active, notified line. Location is loaded; ServiceRoster.Assignments holds only those lines,
    /// each with Skill and SongListItem.Song.
    /// </summary>
    Task<List<LiturgicalEvent>> GetUpcomingAssignedToMemberAsync(
        Guid memberId, DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only Published event, null otherwise. Location and the catalog names are loaded; EventParticipations,
    /// ServiceRoster.Assignments (Active and notified, with Skill and SongListItem.Song) and Rehearsals.Attendances
    /// hold only the member's rows. Rehearsals also carry Location and Songs.Song.
    /// </summary>
    Task<LiturgicalEvent?> GetPublishedForMemberAsync(Guid id, Guid memberId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of events ordered by date then time; Draft events only when <paramref name="includeDrafts"/>.
    /// When <paramref name="choirIds"/> is set, only events one of those choirs serves or no choir serves yet (D8).
    /// Location, LiturgicalSeason, MassType, CeremonyType and every SongLists version are loaded.
    /// </summary>
    Task<PagedList<LiturgicalEvent>> SearchAsync(
        SearchLiturgicalEventsRequest filter, bool includeDrafts, IReadOnlyCollection<Guid>? choirIds,
        CancellationToken cancellationToken);

    /// <summary>Tracked event with its Location loaded.</summary>
    Task<LiturgicalEvent?> GetWithLocationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only event with every SongLists version, EventParticipations, Rehearsals and
    /// ServiceRoster (with its Assignments) loaded.
    /// </summary>
    Task<LiturgicalEvent?> GetWithPreparationAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of Published events before <paramref name="today"/> that involve the member (a participation
    /// row or an Active line on a Finalized roster), newest first, narrowed by the season and date range of
    /// <paramref name="filter"/>. LiturgicalSeason is loaded; EventParticipations, ServiceRoster.Assignments
    /// (Active only, with Skill) and Rehearsals.Attendances hold only the member's rows. ServiceRoster is
    /// loaded whatever its status; the caller checks for Finalized.
    /// </summary>
    Task<PagedList<LiturgicalEvent>> GetHistoryForMemberAsync(
        Guid memberId, SearchMyParticipationHistoryRequest filter, DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only page of Published events before <paramref name="today"/>, newest first, narrowed by the event,
    /// season and date range of <paramref name="filter"/>. LiturgicalSeason, Location, EventParticipations,
    /// ServiceRoster.Assignments and Rehearsals.Attendances are loaded; SongLists holds only Approved
    /// versions, with their Items.
    /// </summary>
    Task<PagedList<LiturgicalEvent>> GetServiceHistoryAsync(
        SearchReportsRequest filter, DateOnly today, CancellationToken cancellationToken);
}
