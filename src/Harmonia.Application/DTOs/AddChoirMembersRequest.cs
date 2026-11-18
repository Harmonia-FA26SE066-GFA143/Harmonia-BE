namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of POST api/choirs/{id}/members. Each id is a <c>MemberProfile</c> id of an active member.
/// Members already in the choir are skipped; members who left it rejoin.
/// </summary>
public class AddChoirMembersRequest
{
    public List<Guid> MemberIds { get; set; } = [];
}
