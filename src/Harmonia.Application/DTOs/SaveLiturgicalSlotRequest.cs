namespace Harmonia.Application.DTOs;

/// <summary>Create / update body of a liturgical slot (UC-32 / FE-50).</summary>
public class SaveLiturgicalSlotRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Position of the slot in a Mass; slots are listed in this order.</summary>
    public int DefaultOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
