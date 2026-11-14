using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class LiturgicalNameTranslationServiceTests
{
    private readonly ILiturgicalNameTranslationRepository _repository = Substitute.For<ILiturgicalNameTranslationRepository>();
    private readonly LiturgicalNameTranslationService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public LiturgicalNameTranslationServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<LiturgicalNameTranslationProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new LiturgicalNameTranslationService(_repository, mapper);
    }

    private static SaveLiturgicalNameTranslationRequest Request(string english = "  Saint Joseph  ") =>
        new() { Kind = LiturgicalNameKind.Celebration, EnglishName = english, VietnameseName = " Thánh Giuse " };

    [Fact]
    public async Task Create_New_TrimsAndSaves_Async()
    {
        var result = await _sut.CreateAsync(Request(), _ct);

        Assert.Equal(("Saint Joseph", "Thánh Giuse"), (result.Value!.EnglishName, result.Value.VietnameseName));
        await _repository.Received(1).AddAsync(Arg.Is<LiturgicalNameTranslation>(x => x.EnglishName == "Saint Joseph"), _ct);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Create_SameKindAndName_ReturnsDuplicate_Async()
    {
        _repository.ExistsAsync(LiturgicalNameKind.Celebration, "Saint Joseph", null, _ct).Returns(true);

        var result = await _sut.CreateAsync(Request(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalNameTranslationDuplicate, result.Code);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound_Async()
    {
        Assert.Equal(
            ErrorCodes.LiturgicalNameTranslationNotFound,
            (await _sut.UpdateAsync(Guid.NewGuid(), Request(), _ct)).Code);
    }

    [Fact]
    public async Task Delete_Existing_RemovesTheRow_Async()
    {
        var row = new LiturgicalNameTranslation { Id = Guid.NewGuid(), EnglishName = "Saint Joseph", VietnameseName = "Thánh Giuse" };
        _repository.GetByIdAsync(row.Id, _ct).Returns(row);

        Assert.True((await _sut.DeleteAsync(row.Id, _ct)).IsSuccess);
        _repository.Received(1).Remove(row);
    }

    [Fact]
    public async Task GetMissing_SkipsNamesWithARowOrAPattern_Async()
    {
        _repository.GetAllAsync(_ct).Returns(new Dictionary<(LiturgicalNameKind, string), string>
        {
            [(LiturgicalNameKind.Celebration, "Christmas")] = "Chúa Giáng Sinh",
        });
        _repository.GetCalendarNamesAsync(_ct).Returns(
        [
            (LiturgicalNameKind.Celebration, "Christmas"),
            (LiturgicalNameKind.Celebration, "29th Sunday of Ordinary Time"),
            (LiturgicalNameKind.Celebration, "Saint New"),
            // Translated as a celebration only, so the season still misses.
            (LiturgicalNameKind.Season, "Christmas"),
        ]);

        var missing = (await _sut.GetMissingAsync(_ct)).Value!;

        Assert.Equal(
            [(LiturgicalNameKind.Celebration, "Saint New"), (LiturgicalNameKind.Season, "Christmas")],
            missing.Select(x => (x.Kind, x.EnglishName)));
    }
}
