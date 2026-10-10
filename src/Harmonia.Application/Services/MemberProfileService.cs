using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class MemberProfileService(
    IMemberProfileRepository memberProfileRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IPracticeAssignmentRepository practiceAssignmentRepository,
    IMapper mapper) : IMemberProfileService
{
    public async Task<Result<PagedList<ParticipationHistoryDto>>> GetMyHistoryAsync(
        Guid userId, SearchMyParticipationHistoryRequest request, CancellationToken cancellationToken)
    {
        var member = await memberProfileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return Result<PagedList<ParticipationHistoryDto>>.Failure(ErrorCodes.MemberNotFound);

        var page = await liturgicalEventRepository.GetHistoryForMemberAsync(
            member.Id, request, VietnamTime.Today, cancellationToken);
        var assignments = await practiceAssignmentRepository.GetForMemberByEventsAsync(
            member.Id, page.Items.Select(e => e.Id).ToList(), cancellationToken);

        var now = DateTime.UtcNow;
        var items = page.Items.Select(e =>
        {
            var held = e.Rehearsals.Where(r => r.StartTime <= now).ToList();
            var received = assignments.Where(a => a.EventId == e.Id).ToList();

            return new ParticipationHistoryDto
            {
                EventId = e.Id,
                EventDate = e.EventDate,
                Title = e.Title,
                LiturgicalSeasonName = e.LiturgicalSeason?.Name,
                ParticipationStatus = e.EventParticipations.Select(p => (ParticipationStatus?)p.Status).FirstOrDefault(),
                // A roster still in Draft or Suggested is a plan, not service that happened.
                ServedSkills = e.ServiceRoster is { Status: RosterStatus.Finalized } roster
                    ? roster.Assignments.Select(a => a.Skill.Name).Distinct().ToList()
                    : [],
                RehearsalsHeld = held.Count,
                RehearsalsAttended = held.Count(r => r.Attendances.Any(
                    a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late)),
                AssignmentsTotal = received.Count,
                AssignmentsPassed = received.Count(a => a.Submissions.Any(s => s.Status == SubmissionStatus.Passed)),
            };
        }).ToList();

        return Result<PagedList<ParticipationHistoryDto>>.Success(
            new PagedList<ParticipationHistoryDto>(items, page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<MemberProfileDetailDto>> GetMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdWithApprovedSkillsAsync(userId, cancellationToken);

        return profile is null
            ? Result<MemberProfileDetailDto>.Failure(ErrorCodes.MemberNotFound)
            : Result<MemberProfileDetailDto>.Success(mapper.Map<MemberProfileDetailDto>(profile));
    }

    public async Task<Result<MemberProfileDto>> UpdateMineAsync(
        Guid userId, UpdateMyMemberProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetByUserIdForUpdateAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound);
        }

        profile.User.FullName = request.FullName.Trim();
        profile.User.Phone = NormalizePhone(request.Phone);
        profile.DateOfBirth = request.DateOfBirth;
        await memberProfileRepository.SaveChangesAsync(cancellationToken);

        return Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }

    public async Task<Result<PagedList<MemberProfileSummaryDto>>> SearchAsync(
        SearchMemberProfilesRequest request, CancellationToken cancellationToken)
    {
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var page = await memberProfileRepository.SearchAsync(keyword, request, cancellationToken);

        return Result<PagedList<MemberProfileSummaryDto>>.Success(new PagedList<MemberProfileSummaryDto>(
            mapper.Map<List<MemberProfileSummaryDto>>(page.Items), page.PageNumber, page.PageSize, page.TotalCount));
    }

    public async Task<Result<MemberProfileDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetWithUserAsync(id, cancellationToken);

        return profile is null
            ? Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound)
            : Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }

    public async Task<Result<MemberProfileDto>> UpdateAsync(
        Guid id, UpdateMemberProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await memberProfileRepository.GetWithUserAsync(id, cancellationToken);
        if (profile is null)
        {
            return Result<MemberProfileDto>.Failure(ErrorCodes.MemberNotFound);
        }

        profile.User.Phone = NormalizePhone(request.Phone);
        profile.DateOfBirth = request.DateOfBirth;
        profile.JoinedDate = request.JoinedDate;
        profile.Status = request.Status;
        await memberProfileRepository.SaveChangesAsync(cancellationToken);

        return Result<MemberProfileDto>.Success(mapper.Map<MemberProfileDto>(profile));
    }

    private static string? NormalizePhone(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}
