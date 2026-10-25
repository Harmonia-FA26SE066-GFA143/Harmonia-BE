namespace Harmonia.Application.DTOs;

/// <summary>A choir serving an event, from <c>LiturgicalEventChoir</c> and its <c>Choir</c> (D7, D8).</summary>
public class LiturgicalEventChoirDto
{
    public Guid ChoirId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>The lead choir plans the event: song list, roster and invitations.</summary>
    public bool IsLead { get; set; }

    public Guid? DirectorId { get; set; }

    public string? DirectorFullName { get; set; }
}
