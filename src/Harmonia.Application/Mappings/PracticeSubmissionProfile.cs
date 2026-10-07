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

        CreateMap<PracticeSubmission, PracticeSubmissionDetailDto>()
            .IncludeBase<PracticeSubmission, PracticeSubmissionDto>()
            .ForMember(dest => dest.MemberName, opt => opt.MapFrom(src => src.Member.User.FullName))
            .ForMember(dest => dest.MemberAvatarUrl, opt => opt.MapFrom(src => src.Member.User.AvatarUrl))
            .ForMember(dest => dest.AssignmentTitle, opt => opt.MapFrom(src => src.PracticeAssignment.Title))
            .ForMember(dest => dest.AssignmentDueDate, opt => opt.MapFrom(src => src.PracticeAssignment.DueDate));

        // The service sets Feedback to the review it just recorded.
        CreateMap<PracticeSubmission, PracticeSubmissionReviewDto>()
            .IncludeBase<PracticeSubmission, PracticeSubmissionDetailDto>()
            .ForMember(dest => dest.Feedback, opt => opt.Ignore());

        CreateMap<PracticeFeedback, PracticeFeedbackDto>();
    }
}
