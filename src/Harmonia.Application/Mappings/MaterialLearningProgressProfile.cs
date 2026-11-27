using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class MaterialLearningProgressProfile : Profile
{
    public MaterialLearningProgressProfile()
    {
        CreateMap<MaterialLearningProgress, MaterialLearningProgressDto>();
    }
}
