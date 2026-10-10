using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

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
    }
}