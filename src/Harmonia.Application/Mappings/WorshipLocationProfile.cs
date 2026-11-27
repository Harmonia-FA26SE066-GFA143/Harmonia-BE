using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class WorshipLocationProfile : Profile
{
    public WorshipLocationProfile()
    {
        CreateMap<WorshipLocation, WorshipLocationDto>();
    }
}
