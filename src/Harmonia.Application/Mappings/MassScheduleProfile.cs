using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class MassScheduleProfile : Profile
{
    public MassScheduleProfile()
    {
        CreateMap<MassSchedule, MassScheduleDto>();
    }
}
