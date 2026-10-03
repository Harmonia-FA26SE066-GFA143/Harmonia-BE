using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class CeremonyTypeProfile : Profile
{
    public CeremonyTypeProfile()
    {
        CreateMap<CeremonyType, LookupDto>();
    }
}
