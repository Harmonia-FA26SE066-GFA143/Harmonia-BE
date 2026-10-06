namespace Harmonia.Application.DTOs;

/// <summary>Body of PUT api/songs/{id}/classification: the full desired set, replacing whatever the song had.</summary>
public class UpdateSongClassificationRequest
{
    public List<Guid> LiturgicalSeasonIds { get; set; } = [];

    public List<Guid> MassTypeIds { get; set; } = [];

    public List<Guid> CeremonyTypeIds { get; set; } = [];

    public List<Guid> SongThemeIds { get; set; } = [];

    public List<UpdateSongSkillRequirementRequest> VocalRequirements { get; set; } = [];

    public List<UpdateSongSkillRequirementRequest> InstrumentRequirements { get; set; } = [];
}
