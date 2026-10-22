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

        // The repository loads only the member's own rows: their request, their notified roster lines
        // (set only once the roster is Finalized) and their attendance.
        CreateMap<LiturgicalEvent, MyAssignedEventDto>()
            .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location.Name))
            .ForMember(dest => dest.Assignments, opt => opt.MapFrom(src => src.ServiceRoster!.Assignments));

        CreateMap<LiturgicalEvent, MemberEventDetailDto>()
            .ForMember(dest => dest.LocationName, opt => opt.MapFrom(src => src.Location.Name))
            .ForMember(dest => dest.LiturgicalSeasonName, opt => opt.MapFrom(src => src.LiturgicalSeason!.Name))
            .ForMember(dest => dest.MassTypeName, opt => opt.MapFrom(src => src.MassType!.Name))
            .ForMember(dest => dest.CeremonyTypeName, opt => opt.MapFrom(src => src.CeremonyType!.Name))
            // The service sets it, because CanRespond depends on the clock.
            .ForMember(dest => dest.Participation, opt => opt.Ignore())
            .ForMember(dest => dest.Assignments, opt => opt.MapFrom(src => src.ServiceRoster!.Assignments))
            .ForMember(dest => dest.Rehearsals, opt => opt.MapFrom(src => src.Rehearsals.OrderBy(r => r.StartTime)));

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