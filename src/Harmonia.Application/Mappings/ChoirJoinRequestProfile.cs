using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class ChoirJoinRequestProfile : Profile
{
    public ChoirJoinRequestProfile()
    {
        // The repository loads only the member's active memberships, so the first one is their current choir.
        CreateMap<ChoirJoinRequest, ChoirJoinRequestDto>()
            .ForMember(dest => dest.MemberUserId, opt => opt.MapFrom(src => src.Member.UserId))
            .ForMember(dest => dest.MemberFullName, opt => opt.MapFrom(src => src.Member.User.FullName))
            .ForMember(dest => dest.ChoirName, opt => opt.MapFrom(src => src.Choir.Name))
            .ForMember(dest => dest.CurrentChoirId, opt => opt.MapFrom(src =>
                src.Member.ChoirMemberships.Select(m => (Guid?)m.ChoirId).FirstOrDefault()))
            .ForMember(dest => dest.CurrentChoirName, opt => opt.MapFrom(src =>
                src.Member.ChoirMemberships.Select(m => m.Choir.Name).FirstOrDefault()));
    }
}
