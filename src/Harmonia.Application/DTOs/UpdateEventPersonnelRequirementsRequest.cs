namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/liturgical-events/{id}/personnel-requirements: the full desired set of whole-service
/// requirements, replacing whatever the event had. A skill left out is removed.
/// </summary>
public class UpdateEventPersonnelRequirementsRequest
{
    public List<UpdateEventPersonnelRequirementRequest> Requirements { get; set; } = [];
}
