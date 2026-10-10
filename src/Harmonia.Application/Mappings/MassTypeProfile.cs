using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class MassTypeProfile : Profile
{
    public MassTypeProfile()
    {
        CreateMap<MassType, MassTypeDto>();
    }
}
