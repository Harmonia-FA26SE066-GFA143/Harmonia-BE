using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.Mappings;

public class MemberProfileProfile : Profile
{
    public MemberProfileProfile()
    {
        CreateMap<MemberProfile, MemberProfileDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.AvatarUrl))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone));

        // MemberSkills must hold only the approved, active skills (see IMemberProfileRepository.SearchAsync).
        CreateMap<MemberProfile, MemberProfileSummaryDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.AvatarUrl))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
            .ForMember(dest => dest.ApprovedSkills, opt => opt.MapFrom(src => src.MemberSkills));

        // MemberSkills must hold only the approved, active skills (see GetByUserIdWithApprovedSkillsAsync).
        CreateMap<MemberProfile, MemberProfileDetailDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.AvatarUrl))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.User.Role.Name))
            .ForMember(dest => dest.ApprovedSkills, opt => opt.MapFrom(src => src.MemberSkills));

        // LearningProgresses must hold only the row of one material (see GetLearnersOfMaterialAsync).
        CreateMap<MemberProfile, MaterialLearningProgressDetailDto>()
            .ForMember(dest => dest.MemberId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                src.LearningProgresses.Select(p => (LearningStatus?)p.Status).FirstOrDefault() ?? LearningStatus.NotStarted))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src =>
                src.LearningProgresses.Select(p => (DateTime?)p.UpdatedAt).FirstOrDefault()));

        // RehearsalAttendances must hold only the row of one rehearsal (see GetAttendanceRosterAsync).
        CreateMap<MemberProfile, RehearsalAttendanceDto>()
            .ForMember(dest => dest.MemberId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                src.RehearsalAttendances.Select(a => (AttendanceStatus?)a.Status).FirstOrDefault()))
            .ForMember(dest => dest.CheckedAt, opt => opt.MapFrom(src =>
                src.RehearsalAttendances.Select(a => (DateTime?)a.CheckedAt).FirstOrDefault()));
    }
}
