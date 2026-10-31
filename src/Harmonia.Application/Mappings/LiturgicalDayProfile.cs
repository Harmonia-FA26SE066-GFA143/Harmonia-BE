using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LiturgicalDayProfile : Profile
{
    public LiturgicalDayProfile()
    {
        // The Vietnamese names come from the translation table, so the service fills them.
        CreateMap<LiturgicalDay, LiturgicalDayDto>()
            .ForMember(dest => dest.CelebrationNameVi, opt => opt.Ignore())
            .ForMember(dest => dest.RankVi, opt => opt.Ignore())
            .ForMember(dest => dest.SeasonNameVi, opt => opt.Ignore());
    }
}