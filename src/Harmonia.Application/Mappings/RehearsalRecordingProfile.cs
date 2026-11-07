using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class RehearsalRecordingProfile : Profile
{
    public RehearsalRecordingProfile()
    {
        // The service signs the audio URL after the access check.
        CreateMap<RehearsalRecording, RehearsalRecordingDto>()
            .ForMember(dest => dest.SongTitle, opt => opt.MapFrom(src => src.Song!.Title))
            .ForMember(dest => dest.AudioUrl, opt => opt.Ignore());
    }
}
