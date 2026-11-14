using Harmonia.Application.Common.Models;

namespace Harmonia.Application.Interfaces.IServices;

/// <summary>AI that picks members for roster slots. Fails with EXTERNAL_AI_FAILED; callers fall back to rules.</summary>
public interface IRosterSuggestionGenerator
{
    Task<Result<List<RosterSlotPick>>> GenerateAsync(List<RosterSlotInput> slots, CancellationToken cancellationToken);
}
