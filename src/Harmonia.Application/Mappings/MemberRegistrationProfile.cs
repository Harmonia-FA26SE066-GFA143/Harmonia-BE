using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class MemberRegistrationProfile : Profile
{
    public MemberRegistrationProfile()
    {
        CreateMap<MemberRegistration, MemberRegistrationDto>()
            .ForMember(x => x.PossibleDuplicates, o => o.Ignore())
            .ForMember(x => x.PreferredChoirName,
                o => o.MapFrom(src => src.PreferredChoir == null ? null : src.PreferredChoir.Name));
    }
}
