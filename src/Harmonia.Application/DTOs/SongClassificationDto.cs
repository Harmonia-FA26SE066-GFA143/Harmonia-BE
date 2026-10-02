namespace Harmonia.Application.DTOs;

/// <summary>Every classification of one song (UC-21 / FE-29). Inactive lookups still show, so history stays readable.</summary>
public class SongClassificationDto
{
    public Guid SongId { get; set; }

    public List<SongClassificationItemDto> LiturgicalSeasons { get; set; } = [];

    public List<SongClassificationItemDto> MassTypes { get; set; } = [];

    public List<SongClassificationItemDto> CeremonyTypes { get; set; } = [];

    public List<SongClassificationItemDto> SongThemes { get; set; } = [];

    public List<SongVocalRequirementDto> VocalRequirements { get; set; } = [];

    public List<SongInstrumentRequirementDto> InstrumentRequirements { get; set; } = [];
}
