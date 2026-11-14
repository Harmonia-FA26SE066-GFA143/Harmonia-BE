using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class PracticeFeedbackProfile : Profile
{
    public PracticeFeedbackProfile()
    {
        CreateMap<PracticeFeedback, PracticeFeedbackDto>()
            .ForMember(dest => dest.ReviewerName, opt => opt.MapFrom(src => src.Reviewer == null ? null : src.Reviewer.FullName));
    }
}
