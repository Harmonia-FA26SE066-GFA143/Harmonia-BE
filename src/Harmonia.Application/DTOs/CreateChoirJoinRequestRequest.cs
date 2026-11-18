namespace Harmonia.Application.DTOs;

/// <summary>A Choir Member asks to join a choir, or to transfer to it from their current one.</summary>
public class CreateChoirJoinRequestRequest
{
    public Guid ChoirId { get; set; }

    public string? Message { get; set; }
}
