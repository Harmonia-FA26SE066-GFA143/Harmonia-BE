using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.ValueObjects;

namespace Harmonia.Application.Mappings;

public class TimedLyricsProfile : Profile
{
    public TimedLyricsProfile()
    {
        // AudioMaterial must be loaded for SongId; MusicMaterialService attaches the material it already read.
        CreateMap<TimedLyrics, TimedLyricsDto>()
            .ForMember(dest => dest.SongId, opt => opt.MapFrom(src => src.AudioMaterial != null ? src.AudioMaterial.SongId : Guid.Empty))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt ?? src.CreatedAt));
        CreateMap<TimedLyricLine, TimedLyricLineDto>();
        CreateMap<TimedLyricLineRequest, TimedLyricLine>();
    }
}
