using System.Net;
using System.Net.Http.Json;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Harmonia.API.Tests;

public class MemberProfilesEndpointsTests(HarmoniaApiFactory factory) : IClassFixture<HarmoniaApiFactory>
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private sealed record Page(List<MemberProfileSummaryDto> Items, int TotalCount);

    /// <summary>Creates the account through the API, the way Admin does, so the profile comes from UserService.</summary>
    private async Task<UserDto> CreateUserAsync(
        string email, string fullName, string roleName = RoleNames.ChoirMember, CancellationToken cancellationToken = default)
    {
        var admin = await factory.CreateClientAsAsync("admin@test.com", cancellationToken);
        var response = await admin.PostAsJsonAsync(
            "api/users", new { email, fullName, password = HarmoniaApiFactory.Password, roleName }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>(TestJson.Options, cancellationToken))!;
    }

    [Fact]
    public async Task CreateChoirMember_CreatesProfile_MemberCanReadAndEditOwn_Async()
    {
        await CreateUserAsync("mp-own@test.com", "Teresa Tran", cancellationToken: _ct);
        var member = await factory.CreateClientAsAsync("mp-own@test.com", _ct);

        var put = await member.PutAsJsonAsync(
            "api/member-profiles/me",
            new { fullName = " Teresa T. Tran ", phone = " 0901234567 ", dateOfBirth = "2001-05-20" },
            _ct);
        var mine = (await member.GetFromJsonAsync<MemberProfileDto>("api/member-profiles/me", TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Teresa T. Tran", mine.FullName);
        Assert.Equal("0901234567", mine.Phone);
        Assert.Equal(new DateOnly(2001, 5, 20), mine.DateOfBirth);
        Assert.Equal(MemberStatus.Active, mine.Status);
    }

    [Fact]
    public async Task CreateChoirDirector_HasNoProfile_AssignRoleToMember_CreatesOne_Async()
    {
        var user = await CreateUserAsync("mp-switch@test.com", "Phero Le", RoleNames.ChoirDirector, _ct);
        var admin = await factory.CreateClientAsAsync("admin@test.com", _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var before = (await director.GetFromJsonAsync<Page>("api/member-profiles?keyword=Phero", TestJson.Options, _ct))!;

        var assign = await admin.PutAsJsonAsync($"api/users/{user.Id}/role", new { roleName = RoleNames.ChoirMember }, _ct);
        var after = (await director.GetFromJsonAsync<Page>("api/member-profiles?keyword=Phero", TestJson.Options, _ct))!;

        Assert.Empty(before.Items);
        Assert.True(assign.IsSuccessStatusCode);
        Assert.Equal("mp-switch@test.com", Assert.Single(after.Items).Email);
    }

    [Fact]
    public async Task Director_UpdatesStatusAndJoinedDate_Async()
    {
        await CreateUserAsync("mp-managed@test.com", "Anna Pham", cancellationToken: _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var id = Assert.Single((await director.GetFromJsonAsync<Page>(
            "api/member-profiles?keyword=mp-managed", TestJson.Options, _ct))!.Items).Id;

        var put = await director.PutAsJsonAsync(
            $"api/member-profiles/{id}", new { joinedDate = "2020-01-15", status = "Inactive" }, _ct);
        var fetched = (await director.GetFromJsonAsync<MemberProfileDto>($"api/member-profiles/{id}", TestJson.Options, _ct))!;

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(new DateOnly(2020, 1, 15), fetched.JoinedDate);
        Assert.Equal(MemberStatus.Inactive, fetched.Status);
    }

    [Fact]
    public async Task Director_JoinedDateInFuture_Returns400WithCode_Async()
    {
        await CreateUserAsync("mp-future@test.com", "Future Member", cancellationToken: _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);
        var id = Assert.Single((await director.GetFromJsonAsync<Page>(
            "api/member-profiles?keyword=mp-future", TestJson.Options, _ct))!.Items).Id;
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var response = await director.PutAsJsonAsync(
            $"api/member-profiles/{id}", new { joinedDate = tomorrow, status = "Active" }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ErrorResponse>(TestJson.Options, _ct))!;
        Assert.Contains(ErrorCodes.MemberJoinedDateInFuture, error.Errors!.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task Member_CannotListOrReadOthers_Async()
    {
        await CreateUserAsync("mp-nosy@test.com", "Nosy Member", cancellationToken: _ct);
        var member = await factory.CreateClientAsAsync("mp-nosy@test.com", _ct);

        var list = await member.GetAsync("api/member-profiles", _ct);
        var other = await member.GetAsync($"api/member-profiles/{Guid.NewGuid()}", _ct);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
    }

    [Fact]
    public async Task Director_ListsMembersWithApprovedSkills_FiltersBySkill_Async()
    {
        var user = await CreateUserAsync("mp-skill@test.com", "Skilled Singer", cancellationToken: _ct);
        var tenor = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "mp-Tenor" };
        var organ = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Instrument, Name = "mp-Organ" };
        await factory.WithDbAsync(async (db, ct) =>
        {
            var memberId = (await db.MemberProfiles.SingleAsync(x => x.UserId == user.Id, ct)).Id;
            db.Skills.AddRange(tenor, organ);
            db.MemberSkills.AddRange(
                new MemberSkill { Id = Guid.NewGuid(), MemberId = memberId, SkillId = tenor.Id, Status = ApprovalStatus.Approved },
                new MemberSkill { Id = Guid.NewGuid(), MemberId = memberId, SkillId = organ.Id, Status = ApprovalStatus.Pending });
            return await db.SaveChangesAsync(ct);
        }, _ct);
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var byTenor = (await director.GetFromJsonAsync<Page>($"api/member-profiles?skillId={tenor.Id}", TestJson.Options, _ct))!;
        var byOrgan = (await director.GetFromJsonAsync<Page>($"api/member-profiles?skillId={organ.Id}", TestJson.Options, _ct))!;

        var skill = Assert.Single(Assert.Single(byTenor.Items).ApprovedSkills);
        Assert.Equal("mp-Tenor", skill.SkillName);
        Assert.Equal(SkillCategoryIds.Vocal, skill.CategoryId);
        Assert.False(string.IsNullOrEmpty(skill.CategoryName));
        Assert.Empty(byOrgan.Items);
    }

    [Fact]
    public async Task Member_GetMine_ReturnsRoleAndApprovedSkills_Async()
    {
        var user = await CreateUserAsync("mp-mine-skill@test.com", "Mine Singer", cancellationToken: _ct);
        var alto = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "mp-mine-Alto" };
        var guitar = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Instrument, Name = "mp-mine-Guitar" };
        await factory.WithDbAsync(async (db, ct) =>
        {
            var memberId = (await db.MemberProfiles.SingleAsync(x => x.UserId == user.Id, ct)).Id;
            db.Skills.AddRange(alto, guitar);
            db.MemberSkills.AddRange(
                new MemberSkill { Id = Guid.NewGuid(), MemberId = memberId, SkillId = alto.Id, Status = ApprovalStatus.Approved },
                new MemberSkill { Id = Guid.NewGuid(), MemberId = memberId, SkillId = guitar.Id, Status = ApprovalStatus.Rejected });
            return await db.SaveChangesAsync(ct);
        }, _ct);
        var member = await factory.CreateClientAsAsync("mp-mine-skill@test.com", _ct);

        var mine = (await member.GetFromJsonAsync<MemberProfileDetailDto>("api/member-profiles/me", TestJson.Options, _ct))!;

        Assert.Equal(RoleNames.ChoirMember, mine.RoleName);
        Assert.Equal("mp-mine-Alto", Assert.Single(mine.ApprovedSkills).SkillName);
    }

    [Fact]
    public async Task Director_UnknownId_Returns404_Async()
    {
        var director = await factory.CreateClientAsAsync("director@test.com", _ct);

        var response = await director.GetAsync($"api/member-profiles/{Guid.NewGuid()}", _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
