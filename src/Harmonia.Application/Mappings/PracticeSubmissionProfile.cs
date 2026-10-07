using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class PracticeSubmissionProfile : Profile
{
    public PracticeSubmissionProfile()
    {
        // AudioUrl is a signed URL the service builds after the ownership check.
        CreateMap<PracticeSubmission, PracticeSubmissionDto>()
            .ForMember(dest => dest.AudioUrl, opt => opt.Ignore());
    }
}
