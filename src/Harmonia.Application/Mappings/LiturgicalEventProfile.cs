using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LiturgicalEventProfile : Profile
{
    public LiturgicalEventProfile()
    {
        CreateMap<LiturgicalEvent, LiturgicalEventSummaryDto>()
            .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location.Name));

        CreateMap<LiturgicalEvent, LiturgicalEventDto>()
            .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location.Name));
    }
}