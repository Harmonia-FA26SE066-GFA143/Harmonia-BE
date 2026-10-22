using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LiturgicalDayProfile : Profile
{
    public LiturgicalDayProfile()
    {
        CreateMap<LiturgicalDay, LiturgicalDayDto>();
    }
}