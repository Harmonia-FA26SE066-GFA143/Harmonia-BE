using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LyricsExtractionJobProfile : Profile
{
    public LyricsExtractionJobProfile()
    {
        CreateMap<LyricsExtractionJob, LyricsExtractionJobDto>();
    }
}
