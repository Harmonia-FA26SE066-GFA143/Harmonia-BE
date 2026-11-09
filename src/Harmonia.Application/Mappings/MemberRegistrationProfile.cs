using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class MemberRegistrationProfile : Profile
{
    public MemberRegistrationProfile()
    {
        CreateMap<MemberRegistration, MemberRegistrationDto>();
    }
}
