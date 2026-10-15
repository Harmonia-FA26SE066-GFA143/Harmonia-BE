using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Mappings;

public class LiturgicalEventProfile : Profile
{
    public LiturgicalEventProfile()
    {
        CreateMap<LiturgicalEvent, LiturgicalEventSummaryDto>()
            .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location.Name));

        CreateMap<LiturgicalEvent, LiturgicalEventDto>()
            .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location.Name));

        // The "!" member chains are null-safe: AutoMapper maps null when the catalog is not set.
        CreateMap<LiturgicalEvent, LiturgicalEventListItemDto>()
            .IncludeBase<LiturgicalEvent, LiturgicalEventDto>()
            .ForMember(dest => dest.LiturgicalSeasonName, opt => opt.MapFrom(src => src.LiturgicalSeason!.Name))
            .ForMember(dest => dest.MassTypeName, opt => opt.MapFrom(src => src.MassType!.Name))
            .ForMember(dest => dest.CeremonyTypeName, opt => opt.MapFrom(src => src.CeremonyType!.Name))
            .ForMember(dest => dest.LatestSongListStatus, opt => opt.MapFrom(src => src.SongLists
                .OrderByDescending(s => s.Version).Select(s => (SongListStatus?)s.Status).FirstOrDefault()));
    }
}