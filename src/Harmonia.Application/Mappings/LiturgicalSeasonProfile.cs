using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LiturgicalSeasonProfile : Profile
{
    public LiturgicalSeasonProfile()
    {
        CreateMap<LiturgicalSeason, LiturgicalSeasonDto>();
    }
}
