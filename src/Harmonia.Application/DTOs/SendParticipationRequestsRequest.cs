using System;
using System.Collections.Generic;

namespace Harmonia.Application.DTOs;

public class SendParticipationRequestsRequest
{
    public Guid EventId { get; set; }

    public List<Guid>? MemberIds { get; set; }
}