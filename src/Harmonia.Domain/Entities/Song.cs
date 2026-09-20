using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class Song : BaseAuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Composer { get; set; }

    public string? Lyricist { get; set; }

    public string? MusicalKey { get; set; }

    public string? Tempo { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SongClassification> Classifications { get; set; } = [];

    public ICollection<SongVocalRequirement> VocalRequirements { get; set; } = [];

    public ICollection<SongInstrumentRequirement> InstrumentRequirements { get; set; } = [];

    public ICollection<MusicMaterial> MusicMaterials { get; set; } = [];

    public ICollection<SongListItem> SongListItems { get; set; } = [];
}
