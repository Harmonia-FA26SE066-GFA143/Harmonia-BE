namespace Harmonia.Application.DTOs;

/// <summary>Draft lyrics read from sheet music by AI. Nothing is saved; the director edits it and saves it as a Lyrics material.</summary>
public record ExtractedLyricsResponse(string LyricsText);
