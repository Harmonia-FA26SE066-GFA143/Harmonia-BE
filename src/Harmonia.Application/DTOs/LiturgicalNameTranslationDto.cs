using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

public class LiturgicalNameTranslationDto
{
    public Guid Id { get; set; }

    public LiturgicalNameKind Kind { get; set; }

    public string EnglishName { get; set; } = string.Empty;

    public string VietnameseName { get; set; } = string.Empty;
}
