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
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
            .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.User.Gender))
            .ForMember(dest => dest.SaintName, opt => opt.MapFrom(src => src.User.SaintName));

        // MemberSkills must hold only the approved, active skills (see IMemberProfileRepository.SearchAsync).
        CreateMap<MemberProfile, MemberProfileSummaryDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.AvatarUrl))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
            .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.User.Gender))
            .ForMember(dest => dest.SaintName, opt => opt.MapFrom(src => src.User.SaintName))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.User.Role.Name))
            .ForMember(dest => dest.ApprovedSkills, opt => opt.MapFrom(src => src.MemberSkills))
            .ForMember(dest => dest.Choirs, opt => opt.MapFrom(src => src.ChoirMemberships
                .Where(m => m.IsActive)
                .OrderBy(m => m.Choir.Name)
                .Select(m => new MemberChoirDto { Id = m.ChoirId, Name = m.Choir.Name })));

        // MemberSkills must hold only the approved, active skills (see GetByUserIdWithApprovedSkillsAsync).
        CreateMap<MemberProfile, MemberProfileDetailDto>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User.Email))
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.User.FullName))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.User.AvatarUrl))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.User.Phone))
            .ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.User.Gender))
            .ForMember(dest => dest.SaintName, opt => opt.MapFrom(src => src.User.SaintName))
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.User.Role.Name))
            .ForMember(dest => dest.ApprovedSkills, opt => opt.MapFrom(src => src.MemberSkills))
            .ForMember(dest => dest.ChoirNames, opt => opt.MapFrom(src => src.ChoirMemberships
                .Where(m => m.IsActive)
                .OrderBy(m => m.Choir.Name)
                .Select(m => m.Choir.Name)));

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
                src.RehearsalAttendances.Select(a => (DateTime?)a.CheckedAt).FirstOrDefault()))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.Status == MemberStatus.Active));
    }
}
