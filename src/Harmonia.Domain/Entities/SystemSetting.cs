using System.Globalization;
using System.Text.Json;
using Harmonia.Domain.Common;
using Harmonia.Domain.Enums;
using Harmonia.Domain.Exceptions;

namespace Harmonia.Domain.Entities;

public class SystemSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public SettingDataType DataType { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Stores <paramref name="value"/> in the canonical text of <see cref="DataType"/> ("42", "true", ...),
    /// so every reader can parse it without guessing.
    /// </summary>
    /// <exception cref="SettingValueTypeMismatchException">The text is not a value of <see cref="DataType"/>.</exception>
    public void SetValue(string value)
    {
        var text = value.Trim();

        Value = DataType switch
        {
            SettingDataType.Int when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                => number.ToString(CultureInfo.InvariantCulture),
            SettingDataType.Bool when bool.TryParse(text, out var flag) => flag ? "true" : "false",
            SettingDataType.Json when IsJson(text) => text,
            SettingDataType.String => text,
            _ => throw new SettingValueTypeMismatchException(),
        };
    }

    private static bool IsJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
