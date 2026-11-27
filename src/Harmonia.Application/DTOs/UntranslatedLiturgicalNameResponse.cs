using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>A name of the synced calendar that still shows in English, ready to be posted as a translation.</summary>
public class UntranslatedLiturgicalNameResponse
{
    public LiturgicalNameKind Kind { get; set; }

    public string EnglishName { get; set; } = string.Empty;
}
