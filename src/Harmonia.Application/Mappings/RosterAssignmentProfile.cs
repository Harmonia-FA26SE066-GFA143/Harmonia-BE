using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;

namespace Harmonia.Application.Mappings;

public class RosterAssignmentProfile : Profile
{
    public RosterAssignmentProfile()
    {
        // SkillName flattens from Skill.Name.
        CreateMap<RosterAssignment, RosterAssignmentDto>()
            .ForMember(d => d.MemberName, o => o.MapFrom(s => s.Member.User.FullName))
            .ForMember(d => d.SongTitle, o => o.MapFrom(s => s.SongListItem != null ? s.SongListItem.Song.Title : null));
    }
}
