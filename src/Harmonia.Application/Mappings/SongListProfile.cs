using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongListProfile : Profile
{
    public SongListProfile()
    {
        CreateMap<SongList, SongListDto>();

        CreateMap<SongListItem, SongListItemDto>()
            .ForMember(dest => dest.SongTitle, opt => opt.MapFrom(src => src.Song.Title))
            .ForMember(dest => dest.SlotName, opt => opt.MapFrom(src => src.Slot.Name));

        CreateMap<SongListReview, SongListReviewDto>();
    }
}