namespace Harmonia.Application.DTOs;

/// <summary>Body of POST api/choirs. The name is unique across choirs.</summary>
public class CreateChoirRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
