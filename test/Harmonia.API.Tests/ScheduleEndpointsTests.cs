using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.API.Tests;

public class ScheduleEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private sealed record Seeded(Guid Published, Guid Draft, Guid Standalone, Guid OfPublished, Guid OfDraft);

    /// <summary>
    /// Two members on the same Published event, plus rows that must stay hidden from the caller: the other member's
    /// rows, a Replaced line, a line not notified yet, and everything belonging to a Draft event.
    /// </summary>
    private async Task<Seeded> SeedAsync(CancellationToken cancellationToken)
    {
        var me = await factory.AddUserAsync("schedule-me@test.com", cancellationToken: cancellationToken);
        var other = await factory.AddUserAsync("schedule-other@test.com", cancellationToken: cancellationToken);

        return await factory.WithDbAsync(
            async (db, ct) =>
            {
                var director = await db.Users.SingleAsync(u => u.Email == "director@test.com", ct);
                var skill = new Skill
                {
                    Id = Guid.NewGuid(), Name = "Schedule-test alto",
                    Category = new SkillCategory { Id = Guid.NewGuid(), Name = "Schedule-test vocal" },
                };
                var location = new WorshipLocation { Id = Guid.NewGuid(), Name = "Schedule-test church" };
                var mine = new MemberProfile { Id = Guid.NewGuid(), UserId = me.Id, Status = MemberStatus.Active };
                var theirs = new MemberProfile { Id = Guid.NewGuid(), UserId = other.Id, Status = MemberStatus.Active };
                LiturgicalEvent Event(EventStatus status, int day) => new()
                {
                    Id = Guid.NewGuid(), EventDate = new DateOnly(2045, 12, day), Time = new TimeOnly(8, 0),
                    LocationId = location.Id, Status = status, Title = $"Schedule-test {status} {day}",
                };
                var published = Event(EventStatus.Published, 24);
                var draft = Event(EventStatus.Draft, 25);
                var notNotified = Event(EventStatus.Published, 26);
                Rehearsal Rehearsal(Guid? eventId, int day) => new()
                {
                    Id = Guid.NewGuid(), EventId = eventId,
                    StartTime = new DateTime(2045, 12, day, 12, 0, 0, DateTimeKind.Utc),
                    EndTime = new DateTime(2045, 12, day, 14, 0, 0, DateTimeKind.Utc),
                };
                var standalone = Rehearsal(null, 20);
                var ofPublished = Rehearsal(published.Id, 21);
                var ofDraft = Rehearsal(draft.Id, 22);
                var roster = new ServiceRoster { Id = Guid.NewGuid(), EventId = published.Id, Status = RosterStatus.Finalized };
                var pendingRoster = new ServiceRoster { Id = Guid.NewGuid(), EventId = notNotified.Id, Status = RosterStatus.Finalized };
                RosterAssignment Line(ServiceRoster r, MemberProfile m, RosterAssignmentStatus status, bool notified) => new()
                {
                    Id = Guid.NewGuid(), RosterId = r.Id, MemberId = m.Id, SkillId = skill.Id, Status = status,
                    Source = AssignmentSource.Manual, NotifiedAt = notified ? DateTime.UtcNow : null,
                };
                EventParticipation Invite(LiturgicalEvent e, MemberProfile m, ParticipationStatus status) => new()
                {
                    Id = Guid.NewGuid(), EventId = e.Id, MemberId = m.Id, Status = status, RequestedAt = DateTime.UtcNow,
                };
                RehearsalAttendance Attendance(MemberProfile m, AttendanceStatus status) => new()
                {
                    Id = Guid.NewGuid(), RehearsalId = ofPublished.Id, MemberId = m.Id, Status = status,
                    CheckedBy = director.Id, CheckedAt = DateTime.UtcNow,
                };

                db.AddRange(skill, location, mine, theirs, published, draft, notNotified, standalone, ofPublished, ofDraft);
                db.AddRange(roster, pendingRoster);
                db.AddRange(
                    Line(roster, mine, RosterAssignmentStatus.Active, notified: true),
                    Line(roster, mine, RosterAssignmentStatus.Replaced, notified: true),
                    Line(roster, theirs, RosterAssignmentStatus.Active, notified: true),
                    Line(pendingRoster, mine, RosterAssignmentStatus.Active, notified: false));
                db.AddRange(
                    Invite(published, mine, ParticipationStatus.Invited),
                    Invite(published, theirs, ParticipationStatus.Confirmed),
                    Invite(draft, mine, ParticipationStatus.Invited));
                db.AddRange(Attendance(mine, AttendanceStatus.Present), Attendance(theirs, AttendanceStatus.Absent));
                await db.SaveChangesAsync(ct);

                return new Seeded(published.Id, draft.Id, standalone.Id, ofPublished.Id, ofDraft.Id);
            },
            cancellationToken);
    }

    [Fact]
    public async Task Member_SeesOnlyOwnRows_AndNothingOfDraftEvents_Async()
    {
        var seeded = await SeedAsync(_ct);
        var member = await factory.CreateClientAsAsync("schedule-me@test.com", _ct);

        var invitations = (await member.GetFromJsonAsync<List<MyParticipationDto>>(
            "api/event-participations/mine", TestJson.Options, _ct))!;
        var assignments = (await member.GetFromJsonAsync<List<MyAssignedEventDto>>(
            "api/schedule/assignments", TestJson.Options, _ct))!;
        var detail = (await member.GetFromJsonAsync<MemberEventDetailDto>(
            $"api/schedule/events/{seeded.Published}", TestJson.Options, _ct))!;
        var upcoming = (await member.GetFromJsonAsync<List<RehearsalSummaryDto>>(
            "api/schedule/rehearsals", TestJson.Options, _ct))!;
        var draftEvent = await member.GetAsync($"api/schedule/events/{seeded.Draft}", _ct);
        var draftRehearsal = await member.GetAsync($"api/schedule/rehearsals/{seeded.OfDraft}", _ct);
        var standalone = await member.GetAsync($"api/schedule/rehearsals/{seeded.Standalone}", _ct);

        var invitation = Assert.Single(invitations);
        Assert.Equal((seeded.Published, ParticipationStatus.Invited, true), (invitation.EventId, invitation.Status, invitation.CanRespond));
        Assert.Equal(seeded.Published, Assert.Single(assignments).EventId);
        Assert.Single(assignments[0].Assignments);

        Assert.Equal(invitation.ParticipationId, detail.Participation!.ParticipationId);
        Assert.Single(detail.Assignments);
        var rehearsal = Assert.Single(detail.Rehearsals);
        Assert.Equal((seeded.OfPublished, (AttendanceStatus?)AttendanceStatus.Present), (rehearsal.Id, rehearsal.MyAttendanceStatus));

        Assert.Contains(upcoming, r => r.Id == seeded.Standalone);
        Assert.Contains(upcoming, r => r.Id == seeded.OfPublished);
        Assert.DoesNotContain(upcoming, r => r.Id == seeded.OfDraft);
        Assert.Equal(HttpStatusCode.NotFound, draftEvent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draftRehearsal.StatusCode);
        Assert.Equal(HttpStatusCode.OK, standalone.StatusCode);
    }

    [Fact]
    public async Task MemberOnlyReads_RejectOtherRoles_Async()
    {
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var invitations = await director.GetAsync("api/event-participations/mine", _ct);
        var assignments = await director.GetAsync("api/schedule/assignments", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, invitations.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assignments.StatusCode);
    }
}
