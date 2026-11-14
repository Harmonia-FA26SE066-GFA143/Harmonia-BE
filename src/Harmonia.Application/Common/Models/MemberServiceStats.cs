namespace Harmonia.Application.Common.Models;

/// <summary>Raw counts behind a director nomination's track record, read by the repository in one go.</summary>
/// <param name="FirstJoinedDate">Earliest <c>ChoirMembership.JoinedDate</c>, left choirs included; null when never in a choir.</param>
/// <param name="AttendanceRecorded">Attendances recorded for the member at rehearsals since the cut-off.</param>
/// <param name="AttendancePresent">Of those, Present or Late.</param>
/// <param name="ServedEventCount">Past, not cancelled events where the member holds an active line of a finalized roster.</param>
/// <param name="HasConductingSkill">An approved skill in the Conducting support category.</param>
public record MemberServiceStats(
    DateOnly? FirstJoinedDate,
    int AttendanceRecorded,
    int AttendancePresent,
    int ServedEventCount,
    bool HasConductingSkill);
