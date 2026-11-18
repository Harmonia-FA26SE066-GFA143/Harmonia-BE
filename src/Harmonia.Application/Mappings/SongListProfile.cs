using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongListProfile : Profile
{
    public SongListProfile()
    {
        CreateMap<SongList, SongListDto>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items.OrderBy(i => i.DisplayOrder)))
            .ForMember(dest => dest.Reviews, opt => opt.MapFrom(src => src.Reviews.OrderBy(r => r.ReviewedAt)));
    }
}
