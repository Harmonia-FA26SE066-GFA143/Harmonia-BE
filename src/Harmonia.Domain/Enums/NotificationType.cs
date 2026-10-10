namespace Harmonia.Domain.Enums;

// Stored as its number, so new values go at the end; reordering would relabel saved notifications.
public enum NotificationType
{
    EventPublished,
    SongListDecision,
    ParticipationRequest,
    AssignmentNotice,
    PracticeFeedback,
    DirectorNote,
    SkillReview,
    EventCancelled,
    SongListSubmitted
}
