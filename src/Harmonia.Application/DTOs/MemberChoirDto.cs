namespace Harmonia.Application.DTOs;

/// <summary>A choir the member currently belongs to, on a member-profile row.</summary>
public class MemberChoirDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
