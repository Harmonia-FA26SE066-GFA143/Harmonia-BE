using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class EventPreparationService(
    ILiturgicalEventRepository liturgicalEventRepository,
    IMemberProfileRepository memberProfileRepository,
    IRehearsalRepository rehearsalRepository,
    IPracticeAssignmentRepository practiceAssignmentRepository,
    IRosterService rosterService) : IEventPreparationService
{
    public async Task<Result<EventPreparationStatusDto>> GetStatusAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var liturgicalEvent = await liturgicalEventRepository.GetWithPreparationAsync(eventId, cancellationToken);
        if (liturgicalEvent is null) return Result<EventPreparationStatusDto>.Failure(ErrorCodes.EventNotFound);

        var progress = await GetProgressAsync(eventId, cancellationToken);
        if (!progress.IsSuccess) return Result<EventPreparationStatusDto>.Failure(progress.Code!);
        var rows = progress.Value!;

        // Without an approved song list the staffing needs are unknown, which is not an error here.
        var shortages = await rosterService.GetShortagesAsync(eventId, cancellationToken);
        if (!shortages.IsSuccess && shortages.Code != ErrorCodes.RosterSongListNotApproved)
        {
            return Result<EventPreparationStatusDto>.Failure(shortages.Code!);
        }

        var participations = liturgicalEvent.EventParticipations;
        var roster = liturgicalEvent.ServiceRoster;
        var rehearsalsHeld = liturgicalEvent.Rehearsals.Count(r => r.StartTime <= DateTime.UtcNow);

        return Result<EventPreparationStatusDto>.Success(new EventPreparationStatusDto
        {
            EventId = liturgicalEvent.Id,
            Title = liturgicalEvent.Title,
            EventDate = liturgicalEvent.EventDate,
            EventStatus = liturgicalEvent.Status,
            SongListStatus = liturgicalEvent.SongLists
                .OrderByDescending(s => s.Version).Select(s => (SongListStatus?)s.Status).FirstOrDefault(),
            ParticipationInvited = participations.Count(p => p.Status == ParticipationStatus.Invited),
            ParticipationConfirmed = participations.Count(p => p.Status == ParticipationStatus.Confirmed),
            ParticipationDeclined = participations.Count(p => p.Status == ParticipationStatus.Declined),
            ParticipationUnsure = participations.Count(p => p.Status == ParticipationStatus.Unsure),
            RosterStatus = roster?.Status,
            RosterActiveAssignments = roster?.Assignments.Count(a => a.Status == RosterAssignmentStatus.Active) ?? 0,
            RosterShortages = shortages.IsSuccess ? shortages.Value : null,
            RehearsalsTotal = liturgicalEvent.Rehearsals.Count,
            RehearsalsHeld = rehearsalsHeld,
            AttendanceExpected = rehearsalsHeld * rows.Count,
            AttendancePresent = rows.Sum(r => r.RehearsalsAttended),
            PracticeExpected = rows.Sum(r => r.AssignmentsTotal),
            PracticePassed = rows.Sum(r => r.AssignmentsPassed),
            PracticeOverdue = rows.Sum(r => r.AssignmentsOverdue),
        });
    }

    public async Task<Result<List<EventPreparationProgressDto>>> GetProgressAsync(
        Guid eventId, CancellationToken cancellationToken)
    {
        if (await liturgicalEventRepository.GetByIdAsync(eventId, cancellationToken) is null)
        {
            return Result<List<EventPreparationProgressDto>>.Failure(ErrorCodes.EventNotFound);
        }

        var members = await memberProfileRepository.GetActiveForEventAsync(eventId, cancellationToken);
        var rehearsals = await rehearsalRepository.GetByEventWithAttendancesAsync(eventId, cancellationToken);
        var assignments = await practiceAssignmentRepository.GetByEventWithSubmissionsAsync(eventId, cancellationToken);

        var now = DateTime.UtcNow;
        var held = rehearsals.Where(r => r.StartTime <= now).ToList();

        var rows = members.Select(member =>
        {
            var received = assignments.Where(a => Receives(a, member)).ToList();
            var latestStatuses = received
                .Select(a => (a.DueDate, Latest: a.Submissions.Where(s => s.MemberId == member.Id)
                    .OrderByDescending(s => s.AttemptNo).Select(s => (SubmissionStatus?)s.Status).FirstOrDefault()))
                .ToList();

            return new EventPreparationProgressDto
            {
                MemberId = member.Id,
                FullName = member.User.FullName,
                ParticipationStatus = member.EventParticipations.Select(p => (ParticipationStatus?)p.Status).FirstOrDefault(),
                RehearsalsHeld = held.Count,
                RehearsalsAttended = held.Count(r => r.Attendances.Any(a => a.MemberId == member.Id
                    && a.Status is AttendanceStatus.Present or AttendanceStatus.Late)),
                AssignmentsTotal = received.Count,
                AssignmentsPassed = latestStatuses.Count(x => x.Latest == SubmissionStatus.Passed),
                AssignmentsOverdue = latestStatuses.Count(x => PracticeAssignment.IsOverdue(x.DueDate, x.Latest, now)),
            };
        }).ToList();

        return Result<List<EventPreparationProgressDto>>.Success(rows);
    }

    // Same rule as PracticeAssignmentRepository.VisibleToMember: scope All, a target naming the member,
    // or a target skill the member holds approved today. MemberSkills holds only approved skills here.
    private static bool Receives(PracticeAssignment assignment, MemberProfile member) =>
        assignment.Scope == AssignmentScope.All
        || assignment.Targets.Any(t => t.MemberId == member.Id
            || member.MemberSkills.Any(ms => ms.SkillId == t.SkillId));
}
