namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/choirs/{id}. Setting <see cref="IsActive"/> to false deactivates the choir: it keeps its members
/// and events, but takes no new members or events. <see cref="DirectorId"/> null leaves the choir without a director.
/// </summary>
public class UpdateChoirRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public Guid? DirectorId { get; set; }
}
