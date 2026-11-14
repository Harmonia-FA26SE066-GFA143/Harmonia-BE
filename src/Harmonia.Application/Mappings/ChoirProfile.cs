using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class ChoirProfile : Profile
{
    public ChoirProfile()
    {
        CreateMap<Choir, ChoirDto>();
    }
}
