namespace Harmonia.Application.DTOs;

/// <summary>Pushed over SignalR when <see cref="ReaderUserId"/> reads every note <see cref="SenderUserId"/> sent them.</summary>
public class DirectorNotesReadDto
{
    public Guid ReaderUserId { get; set; }

    public Guid SenderUserId { get; set; }

    public DateTime ReadAt { get; set; }
}
