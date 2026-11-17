using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class RehearsalDto
{
    public Guid Id { get; set; }

    /// <summary>Null on rehearsals created before choirs were scoped (D8).</summary>
    public Guid? ChoirId { get; set; }

    /// <summary>A rehearsal of every choir serving the event; ChoirId is then null.</summary>
    public bool IsJoint { get; set; }

    public Guid? EventId { get; set; }

    public Guid? LocationId { get; set; }

    public string? LocationName { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Note { get; set; }

    public List<RehearsalSongDto> Songs { get; set; } = [];

    /// <summary>
    /// The caller may change the rehearsal and record its attendance; false on a joint rehearsal whose event another
    /// choir leads.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Members of the rehearsal's choir serving an event that clashes with it; filled only by create and update, a
    /// warning that does not block the save.
    /// </summary>
    public List<RehearsalMemberConflictDto> MemberConflicts { get; set; } = [];
}