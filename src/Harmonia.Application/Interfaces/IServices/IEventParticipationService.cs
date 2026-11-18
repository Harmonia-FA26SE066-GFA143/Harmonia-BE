using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Harmonia.Application.Interfaces.IServices;

public interface IEventParticipationService
{
    
    Task<Result<List<EventParticipationDto>>> SendRequestsAsync(SendParticipationRequestsRequest request, CancellationToken cancellationToken);

    Task<Result<EventParticipationSummaryDto>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>The caller's own requests for upcoming Published events; an event they were not asked about is absent.</summary>
    Task<Result<List<MyParticipationDto>>> GetMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Lead-choir director confirms every Invited request of active members on their behalf (demo shortcut);
    /// Declined and Unsure answers are kept.
    /// </summary>
    Task<Result<EventParticipationSummaryDto>> ConfirmAllAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// Reminds the event's members still Invited, skipping those reminded within Participation.ReminderCooldownHours;
    /// EVENT_PARTICIPATION_REMINDER_TOO_SOON when every one of them was.
    /// </summary>
    Task<Result<ParticipationReminderResponse>> RemindAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>
    /// The daily run: reminds members still Invited to published events of the next
    /// Participation.AutoReminderDaysBefore days, under the same cooldown and at most once a day each. Returns how many
    /// were reminded.
    /// </summary>
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken);

    Task<Result<EventParticipationDto>> RespondAsync(Guid userId, Guid eventId, RespondParticipationRequest request, CancellationToken cancellationToken);
}