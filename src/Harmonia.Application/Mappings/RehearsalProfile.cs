using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Mappings;

public class RehearsalProfile : Profile
{
    public RehearsalProfile()
    {
        CreateMap<Rehearsal, RehearsalSummaryDto>()
            .ForMember(
                dest => dest.LocationName,
                opt => opt.MapFrom(src => src.Location == null ? null : src.Location.Name))
            .ForMember(dest => dest.Songs, opt => opt.MapFrom(src => src.Songs.OrderBy(s => s.DisplayOrder)));

        // The repository loads only the caller's attendance row.
        CreateMap<Rehearsal, MemberRehearsalDetailDto>()
            .IncludeBase<Rehearsal, RehearsalSummaryDto>()
            .ForMember(
                dest => dest.MyAttendanceStatus,
                opt => opt.MapFrom(src => src.Attendances.Select(a => (AttendanceStatus?)a.Status).FirstOrDefault()));

        CreateMap<Rehearsal, RehearsalDto>()
            .ForMember(
                dest => dest.LocationName,
                opt => opt.MapFrom(src => src.Location == null ? null : src.Location.Name))
            .ForMember(dest => dest.Songs, opt => opt.MapFrom(src => src.Songs.OrderBy(s => s.DisplayOrder)));
    }
}
