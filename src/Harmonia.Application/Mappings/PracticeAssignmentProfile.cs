using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Mappings;

public class PracticeAssignmentProfile : Profile
{
    public PracticeAssignmentProfile()
    {
        CreateMap<PracticeAssignment, PracticeAssignmentDto>()
            .ForMember(dest => dest.SkillIds, opt => opt.MapFrom(src => src.Targets
                .Where(t => t.TargetType == TargetType.Skill)
                .Select(t => t.SkillId!.Value)))
            .ForMember(dest => dest.MemberIds, opt => opt.MapFrom(src => src.Targets
                .Where(t => t.TargetType == TargetType.Member)
                .Select(t => t.MemberId!.Value)));

        // Submissions arrives filtered to the calling member's newest row; the ordering keeps it safe if not.
        CreateMap<PracticeAssignment, PracticeAssignmentDetailDto>()
            .ForMember(dest => dest.EventDate, opt => opt.MapFrom(src =>
                src.LiturgicalEvent == null ? (DateOnly?)null : src.LiturgicalEvent.EventDate))
            .ForMember(dest => dest.EventTime, opt => opt.MapFrom(src =>
                src.LiturgicalEvent == null ? (TimeOnly?)null : src.LiturgicalEvent.Time))
            .ForMember(dest => dest.EventTitle, opt => opt.MapFrom(src =>
                src.LiturgicalEvent == null ? null : src.LiturgicalEvent.Title))
            .ForMember(dest => dest.SongTitle, opt => opt.MapFrom(src => src.Song == null ? null : src.Song.Title))
            .ForMember(dest => dest.MaterialTitle, opt => opt.MapFrom(src => src.Material == null ? null : src.Material.Title))
            .ForMember(dest => dest.MaterialType, opt => opt.MapFrom(src =>
                src.Material == null ? (MaterialType?)null : src.Material.MaterialType))
            .ForMember(dest => dest.LatestSubmissionStatus, opt => opt.MapFrom(src => src.Submissions
                .OrderByDescending(s => s.AttemptNo).Select(s => (SubmissionStatus?)s.Status).FirstOrDefault()))
            .ForMember(dest => dest.LatestSubmittedAt, opt => opt.MapFrom(src => src.Submissions
                .OrderByDescending(s => s.AttemptNo).Select(s => (DateTime?)s.SubmittedAt).FirstOrDefault()))
            .ForMember(dest => dest.IsOverdue, opt => opt.MapFrom(src => PracticeAssignment.IsOverdue(
                src.DueDate,
                src.Submissions.OrderByDescending(s => s.AttemptNo).Select(s => (SubmissionStatus?)s.Status).FirstOrDefault(),
                DateTime.UtcNow)));
    }
}
