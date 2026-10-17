namespace Harmonia.Application.DTOs;

/// <summary>
/// Create / update body shared by the catalogs that only have a name and a description: mass types,
/// ceremony types, event categories and skill categories (UC-32 / FE-49, FE-50).
/// </summary>
public class SaveCatalogItemRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
