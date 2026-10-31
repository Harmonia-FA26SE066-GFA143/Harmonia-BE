using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Entities;

public class PracticeAssignment : BaseAuditableEntity
{
    public Guid? EventId { get; set; }

    public Guid? SongId { get; set; }

    /// <summary>
    /// The first of <see cref="Materials"/>, kept for clients that read a single material. Every attached material,
    /// this one included, is in <see cref="Materials"/>.
    /// </summary>
    public Guid? MaterialId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Instruction { get; set; }

    public AssignmentScope Scope { get; set; }

    public DateTime DueDate { get; set; }

    public Guid? ChoirId { get; set; }

    public LiturgicalEvent? LiturgicalEvent { get; set; }

    public Song? Song { get; set; }

    public MusicMaterial? Material { get; set; }

    public Choir? Choir { get; set; }

    public ICollection<PracticeAssignmentTarget> Targets { get; set; } = [];

    public ICollection<PracticeAssignmentMaterial> Materials { get; set; } = [];

    public ICollection<PracticeSubmission> Submissions { get; set; } = [];

    /// <summary>
    /// The overdue rule (UC-10 / FE-12): the due date has passed and the member's newest attempt is not Passed,
    /// including never having submitted. Late submissions are refused, so no submission is ever stored as Overdue;
    /// the state is computed from this rule wherever it is read. PracticeAssignmentRepository repeats it in SQL.
    /// </summary>
    public static bool IsOverdue(DateTime dueDate, SubmissionStatus? latestStatus, DateTime now) =>
        dueDate < now && latestStatus != SubmissionStatus.Passed;
}
