using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class SongSuggestionServiceTests
{
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly ISongRepository _songs = Substitute.For<ISongRepository>();
    private readonly SongSuggestionService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public SongSuggestionServiceTests() =>
        _sut = new SongSuggestionService(_events, _songs, ChoirScopeFakes.Unrestricted());

    [Fact]
    public async Task GetForEvent_SameScore_MoreSpecificSongFirst_Async()
    {
        var liturgicalEvent = new LiturgicalEvent { Id = Guid.NewGuid(), LiturgicalSeasonId = Guid.NewGuid(), MassTypeId = Guid.NewGuid() };
        _events.GetByIdAsync(liturgicalEvent.Id, _ct).Returns(liturgicalEvent);
        var slotId = Guid.NewGuid();
        // Both score 5 (slot + one more). "A generic" never names a season, so it fits any; "B specific" names this one.
        var generic = new Song { Id = Guid.NewGuid(), Title = "A generic" };
        var specific = new Song { Id = Guid.NewGuid(), Title = "B specific" };
        _songs.GetSuggestionMatchesAsync(liturgicalEvent.LiturgicalSeasonId, liturgicalEvent.MassTypeId, null, slotId, _ct).Returns(
        [
            (generic, false, true, true, false, true, true),
            (specific, true, false, true, true, true, true),
        ]);
        _songs.GetStatsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), _ct).Returns(new Dictionary<Guid, SongStats>());

        var result = await _sut.GetForEventAsync(liturgicalEvent.Id, new SearchSongSuggestionsRequest { SlotId = slotId }, _ct);

        Assert.Equal(["B specific", "A generic"], result.Value!.Items.Select(x => x.Title));
        Assert.All(result.Value.Items, x => Assert.Equal(5, x.Score));
    }

    [Fact]
    public async Task GetForEvent_MissingEvent_ReturnsEventNotFound_Async()
    {
        var result = await _sut.GetForEventAsync(Guid.NewGuid(), new SearchSongSuggestionsRequest(), _ct);

        Assert.Equal(ErrorCodes.LiturgicalEventNotFound, result.Code);
    }
}
