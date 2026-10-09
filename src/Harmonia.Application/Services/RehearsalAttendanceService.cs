using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class RehearsalAttendanceService(
    IRehearsalRepository rehearsalRepository,
    IMemberProfileRepository memberProfileRepository,
    IMapper mapper) : IRehearsalAttendanceService
{
    public async Task<Result<List<RehearsalAttendanceDto>>> GetAttendancesAsync(
        Guid rehearsalId, CancellationToken cancellationToken)
    {
        if (await rehearsalRepository.GetByIdAsync(rehearsalId, cancellationToken) is null)
        {
            return Result<List<RehearsalAttendanceDto>>.Failure(ErrorCodes.RehearsalNotFound);
        }

        var members = await memberProfileRepository.GetAttendanceRosterAsync(rehearsalId, cancellationToken);

        return Result<List<RehearsalAttendanceDto>>.Success(mapper.Map<List<RehearsalAttendanceDto>>(members));
    }

    public async Task<Result> RecordAsync(
        Guid directorUserId, Guid rehearsalId, RecordRehearsalAttendancesRequest request, CancellationToken cancellationToken)
    {
        var rehearsal = await rehearsalRepository.GetWithAttendancesForUpdateAsync(rehearsalId, cancellationToken);
        if (rehearsal is null) return Result.Failure(ErrorCodes.RehearsalNotFound);

        var now = DateTime.UtcNow;
        if (now < rehearsal.StartTime) return Result.Failure(ErrorCodes.RehearsalNotStarted);

        var memberIds = request.Items.Select(i => i.MemberId).ToList();
        var members = await memberProfileRepository.ListAsync(x => memberIds.Contains(x.Id), cancellationToken);
        if (members.Count != memberIds.Count) return Result.Failure(ErrorCodes.MemberNotFound);
        if (members.Any(m => m.Status != MemberStatus.Active)) return Result.Failure(ErrorCodes.MemberNotActive);

        foreach (var item in request.Items)
        {
            var attendance = rehearsal.Attendances.FirstOrDefault(a => a.MemberId == item.MemberId);
            if (attendance is null)
            {
                // Id left unset: a preset key on a row reached through a tracked navigation reads to EF as an update.
                attendance = new RehearsalAttendance { RehearsalId = rehearsal.Id, MemberId = item.MemberId };
                rehearsal.Attendances.Add(attendance);
            }

            attendance.Status = item.Status;
            attendance.CheckedBy = directorUserId;
            attendance.CheckedAt = now;
        }

        // Another director recorded one of these members between our read and our save; let the client reload.
        return await rehearsalRepository.TrySaveAttendancesAsync(rehearsal, cancellationToken)
            ? Result.Success()
            : Result.Failure(ErrorCodes.AttendanceAlreadyRecorded);
    }
}
