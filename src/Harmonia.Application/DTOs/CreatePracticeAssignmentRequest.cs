using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class CreatePracticeAssignmentRequest
{
    /// <summary>
    /// A choir the calling director leads (D8); when <see cref="EventId"/> is set it must serve that event. Every scope
    /// stays inside the choir: All is the whole choir, and skill or member targets reach only its members.
    /// </summary>
    public Guid ChoirId { get; set; }

    public Guid? EventId { get; set; }

    public Guid? SongId { get; set; }

    /// <summary>
    /// Older single-material form; ignored when <see cref="MaterialIds"/> is sent. When <see cref="SongId"/> is also
    /// set, the material must belong to that song.
    /// </summary>
    public Guid? MaterialId { get; set; }

    /// <summary>Materials to attach, in display order, e.g. sheet music and sample audio; each follows the rule of <see cref="MaterialId"/>.</summary>
    public List<Guid>? MaterialIds { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    /// <summary>UTC.</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Required when <see cref="Scope"/> is SkillGroup; ignored otherwise.</summary>
    public List<Guid>? SkillIds { get; set; }

    /// <summary>Member profile ids. Required when <see cref="Scope"/> is Individual; ignored otherwise.</summary>
    public List<Guid>? MemberIds { get; set; }
}
