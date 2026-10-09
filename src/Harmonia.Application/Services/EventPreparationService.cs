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
    IPracticeAssignmentRepository practiceAssignmentRepository) : IEventPreparationService
{
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
