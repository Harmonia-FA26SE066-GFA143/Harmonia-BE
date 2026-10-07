using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.API.Tests;

public class MemberSkillsEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private sealed record Page(List<MemberSkillDto> Items, int TotalCount);

    /// <summary>Creates a choir member through the API, the way Admin does, so the profile comes from UserService.</summary>
    private async Task<HttpClient> CreateMemberClientAsync(string email, CancellationToken cancellationToken = default)
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", cancellationToken);
        var response = await admin.PostAsJsonAsync(
            "api/users",
            new { email, fullName = "Skill Member", password = HarmoniaApiFactory.Password, roleName = RoleNames.ChoirMember },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await factory.ClearPasswordChangeRequiredAsync(email, cancellationToken);
        return await factory.CreateClientAsAsync(email, cancellationToken);
    }

    private async Task<Skill> AddSkillAsync(string name, bool isActive = true, CancellationToken cancellationToken = default)
    {
        var skill = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = name, IsActive = isActive };
        await factory.WithDbAsync(async (db, ct) =>
        {
            db.Skills.Add(skill);
            return await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return skill;
    }

    [Fact]
    public async Task Member_Declares_ThenSeesPendingInMine_Async()
    {
        var member = await CreateMemberClientAsync("ms-declare@test.com", _ct);
        var tenor = await AddSkillAsync("ms-Tenor", cancellationToken: _ct);

        var post = await member.PostAsJsonAsync("api/member-skills", new { skillId = tenor.Id, level = "Intermediate" }, _ct);
        var mine = (await member.GetFromJsonAsync<Page>("api/member-skills/mine", TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var declared = (await post.Content.ReadFromJsonAsync<MemberSkillDto>(TestJson.Options, _ct))!;
        Assert.Equal(ApprovalStatus.Pending, declared.Status);
        Assert.Equal(SkillLevel.Intermediate, declared.Level);
        Assert.Equal("ms-Tenor", declared.SkillName);
        Assert.Equal(declared.Id, Assert.Single(mine.Items).Id);
    }

    [Fact]
    public async Task Member_DeclaresSameSkillTwice_Returns409_Async()
    {
        var member = await CreateMemberClientAsync("ms-twice@test.com", _ct);
        var alto = await AddSkillAsync("ms-Alto", cancellationToken: _ct);

        await member.PostAsJsonAsync("api/member-skills", new { skillId = alto.Id }, _ct);
        var second = await member.PostAsJsonAsync("api/member-skills", new { skillId = alto.Id }, _ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var error = (await second.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct))!;
        Assert.Equal(ErrorCodes.MemberSkillAlreadyDeclared, error.Code);
    }

    [Fact]
    public async Task Member_RedeclaresAfterRejection_KeepsRejectedRowAsHistory_Async()
    {
        var member = await CreateMemberClientAsync("ms-again@test.com", _ct);
        var bass = await AddSkillAsync("ms-Bass", cancellationToken: _ct);
        var first = (await (await member.PostAsJsonAsync("api/member-skills", new { skillId = bass.Id }, _ct))
            .Content.ReadFromJsonAsync<MemberSkillDto>(TestJson.Options, _ct))!;
        await factory.WithDbAsync(async (db, ct) =>
        {
            var row = await db.MemberSkills.SingleAsync(x => x.Id == first.Id, ct);
            row.Status = ApprovalStatus.Rejected;
            row.RejectReason = "Not ready";
            return await db.SaveChangesAsync(ct);
        }, _ct);

        var again = await member.PostAsJsonAsync("api/member-skills", new { skillId = bass.Id }, _ct);
        var mine = (await member.GetFromJsonAsync<Page>("api/member-skills/mine", TestJson.Options, _ct))!;
        var rejected = (await member.GetFromJsonAsync<Page>("api/member-skills/mine?status=Rejected", TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, mine.TotalCount);
        Assert.Contains(mine.Items, x => x.Status == ApprovalStatus.Pending);
        Assert.Contains(mine.Items, x => x.Id == first.Id && x.Status == ApprovalStatus.Rejected && x.RejectReason == "Not ready");
        Assert.Equal(first.Id, Assert.Single(rejected.Items).Id);
    }

    [Fact]
    public async Task Member_GetsOwnDeclarationById_ButNotAnotherMembers_Async()
    {
        var owner = await CreateMemberClientAsync("ms-owner@test.com", _ct);
        var other = await CreateMemberClientAsync("ms-other@test.com", _ct);
        var organ = await AddSkillAsync("ms-Organ", cancellationToken: _ct);
        var declared = (await (await owner.PostAsJsonAsync("api/member-skills", new { skillId = organ.Id }, _ct))
            .Content.ReadFromJsonAsync<MemberSkillDto>(TestJson.Options, _ct))!;

        var own = await owner.GetFromJsonAsync<MemberSkillDto>($"api/member-skills/{declared.Id}", TestJson.Options, _ct);
        var foreign = await other.GetAsync($"api/member-skills/{declared.Id}", _ct);

        Assert.Equal(declared.Id, own!.Id);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var error = (await foreign.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct))!;
        Assert.Equal(ErrorCodes.MemberSkillNotFound, error.Code);
    }

    [Fact]
    public async Task Member_InactiveSkill_Returns409_Async()
    {
        var member = await CreateMemberClientAsync("ms-inactive@test.com", _ct);
        var retired = await AddSkillAsync("ms-Retired", isActive: false, cancellationToken: _ct);

        var response = await member.PostAsJsonAsync("api/member-skills", new { skillId = retired.Id }, _ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct))!;
        Assert.Equal(ErrorCodes.SkillInactive, error.Code);
    }

    [Fact]
    public async Task Member_UnknownSkill_Returns404_Async()
    {
        var member = await CreateMemberClientAsync("ms-unknown@test.com", _ct);

        var response = await member.PostAsJsonAsync("api/member-skills", new { skillId = Guid.NewGuid() }, _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record DetailPage(List<MemberSkillDetailDto> Items, int TotalCount);

    private async Task<MemberSkillDto> DeclareAsync(HttpClient member, Guid skillId, CancellationToken cancellationToken = default) =>
        (await (await member.PostAsJsonAsync("api/member-skills", new { skillId }, cancellationToken))
            .Content.ReadFromJsonAsync<MemberSkillDto>(TestJson.Options, cancellationToken))!;

    private Task<int> CountSkillNotificationsAsync(Guid memberSkillId, CancellationToken cancellationToken = default) =>
        factory.WithDbAsync((db, ct) => db.Notifications.CountAsync(
            x => x.Type == NotificationType.SkillReview && x.ReferenceId == memberSkillId, ct), cancellationToken);

    [Fact]
    public async Task Director_ApprovesPending_MemberSeesApprovedAndIsNotified_Async()
    {
        var member = await CreateMemberClientAsync("ms-approve@test.com", _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var soprano = await AddSkillAsync("ms-Soprano", cancellationToken: _ct);
        var declared = await DeclareAsync(member, soprano.Id, _ct);

        var pending = (await director.GetFromJsonAsync<DetailPage>("api/member-skills/pending?pageSize=100", TestJson.Options, _ct))!;
        var approve = await director.PatchAsync($"api/member-skills/{declared.Id}/approve", null, _ct);
        var again = await director.PatchAsync($"api/member-skills/{declared.Id}/approve", null, _ct);
        var own = (await member.GetFromJsonAsync<MemberSkillDto>($"api/member-skills/{declared.Id}", TestJson.Options, _ct))!;

        Assert.Contains(pending.Items, x => x.Id == declared.Id && x.MemberFullName == "Skill Member");
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ApprovalStatus.Approved, own.Status);
        Assert.NotNull(own.ApprovedAt);
        Assert.Equal(1, await CountSkillNotificationsAsync(declared.Id, _ct));
    }

    [Fact]
    public async Task Director_Rejects_RequiresReason_ThenMemberSeesReason_Async()
    {
        var member = await CreateMemberClientAsync("ms-reject@test.com", _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var guitar = await AddSkillAsync("ms-Guitar", cancellationToken: _ct);
        var declared = await DeclareAsync(member, guitar.Id, _ct);

        var noReason = await director.PatchAsJsonAsync($"api/member-skills/{declared.Id}/reject", new { reason = " " }, _ct);
        var reject = await director.PatchAsJsonAsync($"api/member-skills/{declared.Id}/reject", new { reason = "Not ready" }, _ct);
        var own = (await member.GetFromJsonAsync<MemberSkillDto>($"api/member-skills/{declared.Id}", TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Contains(ErrorCodes.MemberSkillRejectReasonRequired, await noReason.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        Assert.Equal(ApprovalStatus.Rejected, own.Status);
        Assert.Equal("Not ready", own.RejectReason);
        Assert.Equal(1, await CountSkillNotificationsAsync(declared.Id, _ct));
    }

    [Fact]
    public async Task Member_CannotReviewOrListPending_Async()
    {
        var member = await CreateMemberClientAsync("ms-noreview@test.com", _ct);
        var psalmist = await AddSkillAsync("ms-Psalmist", cancellationToken: _ct);
        var declared = await DeclareAsync(member, psalmist.Id, _ct);

        var approve = await member.PatchAsync($"api/member-skills/{declared.Id}/approve", null, _ct);
        var pending = await member.GetAsync("api/member-skills/pending", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
    }

    [Fact]
    public async Task Director_CannotDeclareOrReadMine_Async()
    {
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var post = await director.PostAsJsonAsync("api/member-skills", new { skillId = Guid.NewGuid() }, _ct);
        var mine = await director.GetAsync("api/member-skills/mine", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, mine.StatusCode);
    }
}
