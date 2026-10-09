using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class DirectorNoteProfile : Profile
{
    public DirectorNoteProfile()
    {
        CreateMap<DirectorNote, DirectorNoteDto>()
            .ForMember(dest => dest.EventTitle, opt => opt.MapFrom(src => src.LiturgicalEvent == null ? null : src.LiturgicalEvent.Title))
            .ForMember(dest => dest.FromUserName, opt => opt.MapFrom(src => src.FromUser.FullName))
            .ForMember(dest => dest.ToUserName, opt => opt.MapFrom(src => src.ToUser.FullName));
    }
}
