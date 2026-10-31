using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class RehearsalSongProfile : Profile
{
    public RehearsalSongProfile()
    {
        CreateMap<RehearsalSong, RehearsalSongDto>()
            .ForMember(dest => dest.SongTitle, opt => opt.MapFrom(src => src.Song.Title));
    }
}
