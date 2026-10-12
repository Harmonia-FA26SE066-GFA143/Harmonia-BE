using Harmonia.Domain.Enums;

namespace Harmonia.Application.DTOs;

/// <summary>One system setting (UC-33 / FE-51).</summary>
public class SystemSettingDto
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    /// <summary>Always text; read it according to <see cref="DataType"/> ("42", "true", ...).</summary>
    public string Value { get; set; } = string.Empty;

    public SettingDataType DataType { get; set; }

    public string? Description { get; set; }

    /// <summary>True when every signed-in role may read the setting.</summary>
    public bool IsPublic { get; set; }

    /// <summary>Lowest value an Int setting accepts; null when unbounded or not an Int.</summary>
    public int? MinValue { get; set; }

    /// <summary>Highest value an Int setting accepts; null when unbounded or not an Int.</summary>
    public int? MaxValue { get; set; }
}
