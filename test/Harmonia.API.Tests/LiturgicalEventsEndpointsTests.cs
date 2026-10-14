using System.Net;
using System.Net.Http.Json;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;

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
}
