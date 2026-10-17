namespace Harmonia.Application.DTOs;

/// <summary>Create / update body of a worship location (UC-32 / FE-50).</summary>
public class SaveWorshipLocationRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;
}
