using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongListItemProfile : Profile
{
    public SongListItemProfile()
    {
        CreateMap<SongListItem, SongListItemDto>()
            .ForMember(dest => dest.SongTitle, opt => opt.MapFrom(src => src.Song.Title))
            .ForMember(dest => dest.SlotName, opt => opt.MapFrom(src => src.Slot.Name));
    }
}
