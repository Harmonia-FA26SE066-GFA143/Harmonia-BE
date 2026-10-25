using System.Linq.Expressions;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Domain.Exceptions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class SystemSettingServiceTests
{
    private readonly IGenericRepository<SystemSetting> _repository = Substitute.For<IGenericRepository<SystemSetting>>();
    private readonly SystemSettingService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly SystemSetting _days = new()
    {
        Id = Guid.NewGuid(), Key = SystemSettingKeys.RosterRecentServiceDays, Value = "60", DataType = SettingDataType.Int,
    };

    private readonly SystemSetting _parish = new()
    {
        Id = Guid.NewGuid(), Key = SystemSettingKeys.ParishName, Value = "St. Joseph", DataType = SettingDataType.String,
    };

    public SystemSettingServiceTests()
    {
        _sut = new SystemSettingService(_repository);
        Rows(_days, _parish);
        _repository.GetByIdAsync(_days.Id, _ct).Returns(_days);
    }

    /// <summary>Runs the service's predicate against the given rows, like the database would.</summary>
    private void Rows(params SystemSetting[] rows) =>
        _repository.ListAsync(Arg.Any<Expression<Func<SystemSetting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => rows.Where(call.Arg<Expression<Func<SystemSetting, bool>>>().Compile()).ToList());

    [Fact]
    public async Task GetAll_ReturnsEverySettingInPageOrder_WithTheLabelRangeAndVisibilityOfItsDefinition_Async()
    {
        var all = (await _sut.GetAllAsync(_ct)).Value!;

        Assert.Equal([SystemSettingKeys.ParishName, SystemSettingKeys.RosterRecentServiceDays], all.Select(x => x.Key));
        Assert.Equal((true, null, null), (all[0].IsPublic, all[0].MinValue, all[0].MaxValue));
        Assert.Equal(("Tên giáo xứ", "Thông tin chung", 0), (all[0].DisplayName, all[0].Group, all[0].Order));
        Assert.Equal((_days.Id, "60", SettingDataType.Int, false, 1, 365),
            (all[1].Id, all[1].Value, all[1].DataType, all[1].IsPublic, all[1].MinValue!.Value, all[1].MaxValue!.Value));
        Assert.Equal("Phân công", all[1].Group);
        Assert.True(all[1].Order > all[0].Order);
    }

    [Fact]
    public async Task GetPublic_SignedIn_ReturnsOnlyKeysMarkedPublicInCode_Async()
    {
        var visible = (await _sut.GetPublicAsync(isSignedIn: true, _ct)).Value!;

        Assert.Equal(SystemSettingKeys.ParishName, Assert.Single(visible).Key);
    }

    [Fact]
    public async Task GetPublic_Anonymous_HidesPublicKeysNotMarkedAnonymous_Async()
    {
        var announcement = new SystemSetting
        {
            Id = Guid.NewGuid(), Key = SystemSettingKeys.SystemAnnouncement, Value = "Hop ca doan", DataType = SettingDataType.String,
        };
        Rows(_days, _parish, announcement);

        var signedIn = (await _sut.GetPublicAsync(isSignedIn: true, _ct)).Value!;
        var anonymous = (await _sut.GetPublicAsync(isSignedIn: false, _ct)).Value!;

        Assert.Equal([SystemSettingKeys.ParishName, SystemSettingKeys.SystemAnnouncement], signedIn.Select(x => x.Key));
        Assert.Equal(SystemSettingKeys.ParishName, Assert.Single(anonymous).Key);
    }

    [Fact]
    public void Definitions_AnonymousImpliesPublic_AndEveryKeyHasALabelAndGroup()
    {
        Assert.All(SystemSettingKeys.All, x =>
        {
            Assert.True(!x.IsAnonymous || x.IsPublic, x.Key);
            Assert.False(string.IsNullOrWhiteSpace(x.DisplayName), x.Key);
            Assert.False(string.IsNullOrWhiteSpace(x.Group), x.Key);
        });
    }

    [Fact]
    public async Task Update_ValueInsideTheRange_IsNormalisedAndSaved_Async()
    {
        var result = await _sut.UpdateAsync(_days.Id, new UpdateSystemSettingRequest { Value = " 030 " }, _ct);

        Assert.Equal("30", result.Value!.Value);
        Assert.Equal("30", _days.Value);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    public async Task Update_ValueOutsideTheRange_ReturnsOutOfRange_AndKeepsTheOldValue_Async(string value)
    {
        var result = await _sut.UpdateAsync(_days.Id, new UpdateSystemSettingRequest { Value = value }, _ct);

        Assert.Equal(ErrorCodes.SettingValueOutOfRange, result.Code);
        Assert.Equal("60", _days.Value);
        await _repository.DidNotReceive().SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_ValueOfTheWrongType_Throws_AndUnknownIdReturnsNotFound_Async()
    {
        await Assert.ThrowsAsync<SettingValueTypeMismatchException>(
            () => _sut.UpdateAsync(_days.Id, new UpdateSystemSettingRequest { Value = "many" }, _ct));
        Assert.Equal("60", _days.Value);

        var missing = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateSystemSettingRequest { Value = "1" }, _ct);
        Assert.Equal(ErrorCodes.SettingNotFound, missing.Code);
    }

    [Fact]
    public async Task GetIntAndGetBool_ReadTheStoredValue_Async()
    {
        Rows(_days, new SystemSetting { Key = SystemSettingKeys.PracticeAllowLateSubmission, Value = "true", DataType = SettingDataType.Bool });

        Assert.Equal(60, await _sut.GetIntAsync(SystemSettingKeys.RosterRecentServiceDays, _ct));
        Assert.True(await _sut.GetBoolAsync(SystemSettingKeys.PracticeAllowLateSubmission, _ct));
    }

    [Fact]
    public async Task GetIntAndGetBool_MissingOrDamagedRow_FallBackToTheDefault_Async()
    {
        Rows(new SystemSetting { Key = SystemSettingKeys.RosterRecentServiceDays, Value = "broken", DataType = SettingDataType.Int });

        Assert.Equal(60, await _sut.GetIntAsync(SystemSettingKeys.RosterRecentServiceDays, _ct));
        Assert.True(await _sut.GetBoolAsync(SystemSettingKeys.RosterAiSuggestionEnabled, _ct));
        Assert.False(await _sut.GetBoolAsync(SystemSettingKeys.PracticeAllowLateSubmission, _ct));
    }
}
