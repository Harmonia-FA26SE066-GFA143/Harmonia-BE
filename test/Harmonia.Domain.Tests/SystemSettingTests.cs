using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Domain.Exceptions;

namespace Harmonia.Domain.Tests;

public class SystemSettingTests
{
    [Theory]
    [InlineData(SettingDataType.Int, " 042 ", "42")]
    [InlineData(SettingDataType.Int, "-5", "-5")]
    [InlineData(SettingDataType.Bool, "True", "true")]
    [InlineData(SettingDataType.Bool, " false ", "false")]
    [InlineData(SettingDataType.String, "  St. Joseph  ", "St. Joseph")]
    [InlineData(SettingDataType.String, "", "")]
    [InlineData(SettingDataType.Json, " {\"a\":[1,2]} ", "{\"a\":[1,2]}")]
    public void SetValue_ValueOfTheDeclaredType_IsStoredInCanonicalText(SettingDataType type, string input, string stored)
    {
        var setting = new SystemSetting { DataType = type };

        setting.SetValue(input);

        Assert.Equal(stored, setting.Value);
    }

    [Theory]
    [InlineData(SettingDataType.Int, "1.5")]
    [InlineData(SettingDataType.Int, "")]
    [InlineData(SettingDataType.Int, "99999999999")]
    [InlineData(SettingDataType.Bool, "yes")]
    [InlineData(SettingDataType.Bool, "1")]
    [InlineData(SettingDataType.Json, "{not json")]
    [InlineData(SettingDataType.Json, "")]
    public void SetValue_ValueOfAnotherType_ThrowsAndKeepsTheOldValue(SettingDataType type, string input)
    {
        var setting = new SystemSetting { DataType = type, Value = "old" };

        var exception = Assert.Throws<SettingValueTypeMismatchException>(() => setting.SetValue(input));

        Assert.Equal(ErrorCodes.SettingValueTypeMismatch, exception.Code);
        Assert.Equal("old", setting.Value);
    }

    [Fact]
    public void Definitions_HaveUniqueKeys_AndDefaultsThatFitTheirOwnTypeAndRange()
    {
        Assert.Equal(SystemSettingKeys.All.Count, SystemSettingKeys.All.Select(x => x.Key).Distinct().Count());

        foreach (var definition in SystemSettingKeys.All)
        {
            var setting = new SystemSetting { DataType = definition.DataType };
            setting.SetValue(definition.DefaultValue);
            Assert.Equal(definition.DefaultValue, setting.Value);

            if (definition.DataType == SettingDataType.Int)
            {
                Assert.InRange(int.Parse(setting.Value), definition.MinValue ?? int.MinValue, definition.MaxValue ?? int.MaxValue);
            }
        }
    }
}
