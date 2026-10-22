using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Services;

public class MassScheduleService(
    IGenericRepository<MassSchedule> scheduleRepository,
    IGenericRepository<MassType> massTypeRepository,
    IGenericRepository<WorshipLocation> locationRepository,
    IGenericRepository<LiturgicalSeason> seasonRepository,
    ILiturgicalEventRepository liturgicalEventRepository,
    IMapper mapper) : IMassScheduleService
{
    public async Task<Result<MassScheduleDto>> CreateAsync(
        CreateMassScheduleRequest request, CancellationToken cancellationToken)
    {
        var error = await CheckAsync(
            request.DayOfWeek, request.Time, request.MassTypeId, request.LocationId, null, cancellationToken);
        if (error is not null)
        {
            return Result<MassScheduleDto>.Failure(error);
        }

        var schedule = new MassSchedule
        {
            Id = Guid.NewGuid(),
            DayOfWeek = request.DayOfWeek,
            Time = request.Time,
            MassTypeId = request.MassTypeId,
            LocationId = request.LocationId,
            Title = TrimToNull(request.Title),
        };
        await scheduleRepository.AddAsync(schedule, cancellationToken);
        await scheduleRepository.SaveChangesAsync(cancellationToken);

        return Result<MassScheduleDto>.Success(mapper.Map<MassScheduleDto>(schedule));
    }

    public async Task<Result<List<MassScheduleDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var schedules = await scheduleRepository.ListAsync(_ => true, cancellationToken);

        return Result<List<MassScheduleDto>>.Success(mapper.Map<List<MassScheduleDto>>(
            schedules.OrderBy(x => x.DayOfWeek).ThenBy(x => x.Time).ToList()));
    }

    public async Task<Result<MassScheduleDto>> UpdateAsync(
        Guid id, UpdateMassScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = await scheduleRepository.GetByIdAsync(id, cancellationToken);
        if (schedule is null)
        {
            return Result<MassScheduleDto>.Failure(ErrorCodes.MassScheduleNotFound);
        }

        var error = await CheckAsync(
            request.DayOfWeek, request.Time, request.MassTypeId, request.LocationId, schedule, cancellationToken);
        if (error is not null)
        {
            return Result<MassScheduleDto>.Failure(error);
        }

        schedule.DayOfWeek = request.DayOfWeek;
        schedule.Time = request.Time;
        schedule.MassTypeId = request.MassTypeId;
        schedule.LocationId = request.LocationId;
        schedule.Title = TrimToNull(request.Title);
        schedule.IsActive = request.IsActive;
        await scheduleRepository.SaveChangesAsync(cancellationToken);

        return Result<MassScheduleDto>.Success(mapper.Map<MassScheduleDto>(schedule));
    }

    public async Task<Result<List<LiturgicalEventDto>>> GenerateEventsAsync(
        GenerateLiturgicalEventsRequest request, CancellationToken cancellationToken)
    {
        var (from, to) = (request.FromDate, request.ToDate);
        var schedules = await scheduleRepository.ListAsync(
            x => x.IsActive && x.MassType.IsActive && x.Location.IsActive, cancellationToken);
        var seasons = await seasonRepository.ListAsync(
            x => x.IsActive && x.StartDate <= to && x.EndDate >= from, cancellationToken);
        var taken = (await liturgicalEventRepository.ListAsync(
                x => x.EventDate >= from && x.EventDate <= to && x.Status != EventStatus.Cancelled, cancellationToken))
            .Select(x => (x.EventDate, x.Time, x.LocationId))
            .ToHashSet();

        var created = new List<LiturgicalEvent>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var schedule in schedules.Where(x => x.DayOfWeek == date.DayOfWeek).OrderBy(x => x.Time))
            {
                if (taken.Contains((date, schedule.Time, schedule.LocationId))) continue;

                created.Add(new LiturgicalEvent
                {
                    Id = Guid.NewGuid(),
                    EventDate = date,
                    Time = schedule.Time,
                    LiturgicalSeasonId = seasons.FirstOrDefault(s => s.StartDate <= date && date <= s.EndDate)?.Id,
                    MassTypeId = schedule.MassTypeId,
                    LocationId = schedule.LocationId,
                    Title = schedule.Title,
                    Status = EventStatus.Draft,
                });
            }
        }

        foreach (var liturgicalEvent in created)
        {
            await liturgicalEventRepository.AddAsync(liturgicalEvent, cancellationToken);
        }

        if (created.Count > 0)
        {
            await liturgicalEventRepository.SaveChangesAsync(cancellationToken);
        }

        return Result<List<LiturgicalEventDto>>.Success(mapper.Map<List<LiturgicalEventDto>>(created));
    }

    /// <summary>
    /// The Mass type and location must exist and be active, unless the schedule already uses that switched-off row;
    /// no other schedule may hold the same day + time + location.
    /// </summary>
    private async Task<string?> CheckAsync(
        DayOfWeek dayOfWeek, TimeOnly time, Guid massTypeId, Guid locationId, MassSchedule? current,
        CancellationToken cancellationToken)
    {
        var massType = await massTypeRepository.GetByIdAsync(massTypeId, cancellationToken);
        var location = await locationRepository.GetByIdAsync(locationId, cancellationToken);
        if (massType is null || location is null) return ErrorCodes.LookupNotFound;
        if ((!massType.IsActive && massTypeId != current?.MassTypeId)
            || (!location.IsActive && locationId != current?.LocationId))
        {
            return ErrorCodes.LookupInactive;
        }

        var currentId = current?.Id;
        var clashes = await scheduleRepository.ListAsync(
            x => x.DayOfWeek == dayOfWeek && x.Time == time && x.LocationId == locationId && x.Id != currentId,
            cancellationToken);
        return clashes.Count > 0 ? ErrorCodes.MassScheduleSlotTaken : null;
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
