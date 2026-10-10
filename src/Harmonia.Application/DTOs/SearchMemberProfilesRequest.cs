using Harmonia.Application.Common.Models;
using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>
/// Query string of GET api/member-profiles: paging, a keyword over name and email, optional status
/// and approved-skill filters.
/// </summary>
public class SearchMemberProfilesRequest : PagingRequest
{
    public string? Keyword { get; set; }

    public MemberStatus? Status { get; set; }

    /// <summary>Keeps only members with this skill approved.</summary>
    public Guid? SkillId { get; set; }
}
