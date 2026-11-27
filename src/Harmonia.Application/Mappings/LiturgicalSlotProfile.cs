using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LiturgicalSlotProfile : Profile
{
    public LiturgicalSlotProfile()
    {
        CreateMap<LiturgicalSlot, LiturgicalSlotDto>();
    }
}
