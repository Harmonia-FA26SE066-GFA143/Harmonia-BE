using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class EventParticipationProfile : Profile
{
    public EventParticipationProfile()
    {
        // HasTimeConflict needs the other events' rosters, so the service sets it.
        CreateMap<EventParticipation, EventParticipationDto>()
            .ForMember(dest => dest.MemberFullName, opt => opt.MapFrom(src => src.Member.User.FullName))
            .ForMember(dest => dest.HasTimeConflict, opt => opt.Ignore());

        // CanRespond and its reason depend on the clock, so the service sets them.
        CreateMap<EventParticipation, MyParticipationDto>()
            .ForMember(dest => dest.ParticipationId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.CanRespond, opt => opt.Ignore())
            .ForMember(dest => dest.CannotRespondReason, opt => opt.Ignore());
    }
}