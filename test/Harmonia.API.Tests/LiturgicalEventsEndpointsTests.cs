using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.API.Tests;

public class LiturgicalEventsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ChecksTheCatalogRowsItPointsAt_Async()
    {
        var location = new WorshipLocation { Id = Guid.NewGuid(), Name = "Event-test church" };
        var closed = new WorshipLocation { Id = Guid.NewGuid(), Name = "Event-test closed chapel", IsActive = false };
        var massType = new MassType { Id = Guid.NewGuid(), Name = "Event-test mass" };
        var season = new LiturgicalSeason
        {
            Id = Guid.NewGuid(), Name = "Event-test season", StartDate = new DateOnly(2045, 12, 1), EndDate = new DateOnly(2045, 12, 31),
        };
        await factory.WithDbAsync(
            (db, ct) =>
            {
                db.AddRange(location, closed, massType, season);
                return db.SaveChangesAsync(ct);
            },
            _ct);
        var priest = await factory.CreateClientAsAsync("priest@test.com", _ct);
        object Body(Guid locationId, Guid massTypeId, string eventDate = "2045-12-25") => new
        {
            eventDate, time = "07:00:00", locationId, massTypeId, liturgicalSeasonId = season.Id,
        };

        var unknownMassType = await priest.PostAsJsonAsync("api/liturgical-events", Body(location.Id, Guid.NewGuid()), _ct);
        var closedLocation = await priest.PostAsJsonAsync("api/liturgical-events", Body(closed.Id, massType.Id), _ct);
        var outsideSeason = await priest.PostAsJsonAsync("api/liturgical-events", Body(location.Id, massType.Id, "2046-01-05"), _ct);
        var valid = await priest.PostAsJsonAsync("api/liturgical-events", Body(location.Id, massType.Id), _ct);

        Assert.Equal(HttpStatusCode.NotFound, unknownMassType.StatusCode);
        Assert.Contains(ErrorCodes.LookupNotFound, await unknownMassType.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.Conflict, closedLocation.StatusCode);
        Assert.Contains(ErrorCodes.LookupInactive, await closedLocation.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.BadRequest, outsideSeason.StatusCode);
        Assert.Contains(ErrorCodes.LiturgicalEventDateOutsideLiturgicalSeason, await outsideSeason.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task SongSuggestions_RankByMatch_AndLeaveOutOtherSeasonOrSlotOnlySongs_Async()
    {
        var location = new WorshipLocation { Id = Guid.NewGuid(), Name = "Suggestion-test church" };
        var massType = new MassType { Id = Guid.NewGuid(), Name = "Suggestion-test mass" };
        var advent = new LiturgicalSeason
        {
            Id = Guid.NewGuid(), Name = "Suggestion-test advent", StartDate = new DateOnly(2047, 12, 1), EndDate = new DateOnly(2047, 12, 20),
        };
        var lent = new LiturgicalSeason
        {
            Id = Guid.NewGuid(), Name = "Suggestion-test lent", StartDate = new DateOnly(2048, 2, 1), EndDate = new DateOnly(2048, 3, 20),
        };
        var communion = new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Suggestion-test communion", DefaultOrder = 9 };
        var entrance = new LiturgicalSlot { Id = Guid.NewGuid(), Name = "Suggestion-test entrance", DefaultOrder = 1 };
        var liturgicalEvent = new LiturgicalEvent
        {
            Id = Guid.NewGuid(), EventDate = new DateOnly(2047, 12, 5), Time = new TimeOnly(7, 0), LocationId = location.Id,
            MassTypeId = massType.Id, LiturgicalSeasonId = advent.Id, Status = EventStatus.Draft,
        };
        Song Classified(string title, params (ClassificationTarget Type, Guid Id)[] targets)
        {
            var song = new Song { Id = Guid.NewGuid(), Title = title };
            foreach (var (type, id) in targets)
                song.Classifications.Add(new SongClassification { Id = Guid.NewGuid(), SongId = song.Id, TargetType = type, TargetId = id });
            return song;
        }

        var best = Classified("Suggestion-test best", (ClassificationTarget.LiturgicalSeason, advent.Id), (ClassificationTarget.LiturgicalSlot, communion.Id));
        var typeOnly = Classified("Suggestion-test type only", (ClassificationTarget.MassType, massType.Id));
        var lentOnly = Classified("Suggestion-test lent only", (ClassificationTarget.LiturgicalSeason, lent.Id), (ClassificationTarget.MassType, massType.Id));
        var entranceOnly = Classified("Suggestion-test entrance only", (ClassificationTarget.LiturgicalSlot, entrance.Id), (ClassificationTarget.MassType, massType.Id));
        var unclassified = Classified("Suggestion-test unclassified");
        await factory.WithDbAsync(
            (db, ct) =>
            {
                db.AddRange(location, massType, advent, lent, communion, entrance, liturgicalEvent, best, typeOnly, lentOnly, entranceOnly, unclassified);
                return db.SaveChangesAsync(ct);
            },
            _ct);
        var priest = await factory.CreateClientAsAsync("priest@test.com", _ct);

        var page = await priest.GetFromJsonAsync<JsonElement>(
            $"api/liturgical-events/{liturgicalEvent.Id}/song-suggestions?slotId={communion.Id}", _ct);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Suggestion-test best", "Suggestion-test type only"], items.Select(x => x.GetProperty("title").GetString()));
        Assert.Equal([5, 2], items.Select(x => x.GetProperty("score").GetInt32()));
    }
}
