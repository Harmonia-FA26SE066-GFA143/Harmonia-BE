namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of POST api/choirs. The name is unique across choirs; <see cref="DirectorId"/>, when set, is the
/// <c>User</c> id of an active Choir Director.
/// </summary>
public class CreateChoirRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? DirectorId { get; set; }
}
