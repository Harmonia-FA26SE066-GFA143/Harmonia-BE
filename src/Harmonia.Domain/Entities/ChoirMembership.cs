using Harmonia.Domain.Common;

namespace Harmonia.Domain.Entities;

public class ChoirMembership : BaseEntity
{
    public Guid MemberId { get; set; }

    public Guid ChoirId { get; set; }

    public DateOnly JoinedDate { get; set; }

    // False once the member leaves the choir; the row stays as history.
    public bool IsActive { get; set; } = true;

    public MemberProfile Member { get; set; } = null!;

    public Choir Choir { get; set; } = null!;
}
