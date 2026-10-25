using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class LiturgicalNameTranslationProfile : Profile
{
    public LiturgicalNameTranslationProfile()
    {
        CreateMap<LiturgicalNameTranslation, LiturgicalNameTranslationDto>();
    }
}
