using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class SongThemeProfile : Profile
{
    public SongThemeProfile()
    {
        CreateMap<SongTheme, LookupDto>();
    }
}
