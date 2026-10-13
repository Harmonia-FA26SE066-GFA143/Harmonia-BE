using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class EventParticipationProfile : Profile
{
    public EventParticipationProfile()
    {
        CreateMap<EventParticipation, EventParticipationDto>().ForMember(dest => dest.MemberFullName, opt => opt.MapFrom(src => src.Member.User.FullName));
    }
}