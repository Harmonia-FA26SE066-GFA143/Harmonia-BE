namespace Harmonia.Application.DTOs;

public class LiturgicalSlotDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DefaultOrder { get; set; }
}
