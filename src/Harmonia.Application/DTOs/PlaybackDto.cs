using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>What the mobile piano roll needs to play a MIDI material (Harmonia_MIDI_Backend_Spec §3.2).</summary>
public class PlaybackDto
{
    public Guid Id { get; set; }

    public Guid SongId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Always "Ready": rendering finishes inside the upload request. Kept so a later background render
    /// can add "Processing" / "Failed" without breaking the mobile client.
    /// </summary>
    public string Status { get; set; } = "Ready";

    public int SchemaVersion { get; set; } = 1;

    public double DurationSeconds { get; set; }

    public PlaybackInstrument Instrument { get; set; }

    /// <summary>Short-lived signed URL of a WAV file; request this endpoint again once it expires.</summary>
    public string AudioUrl { get; set; } = string.Empty;

    /// <summary>Short-lived signed URL of notes.json.</summary>
    public string NotesUrl { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
