namespace Harmonia.Application.DTOs;

/// <summary>
/// Body of PUT api/lookups/liturgical-slots/{id}/requirements: the full set of people the slot needs whatever song
/// fills it, replacing what it had. Only songs put in the slot afterwards get them.
/// </summary>
public class UpdateLiturgicalSlotRequirementsRequest
{
    public List<UpdateLiturgicalSlotRequirementRequest> Requirements { get; set; } = [];
}
