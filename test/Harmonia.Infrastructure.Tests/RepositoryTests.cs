using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Harmonia.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Harmonia.Infrastructure.Tests;

public sealed class RepositoryTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    // ---- UserRepository ----

    [Fact]
    public async Task GetByEmailAsync_LoadsRole_Async()
    {
        await _db.AddUserAsync("a@test.com", RoleNames.ChoirDirector, cancellationToken: _ct);
        await using var context = _db.NewContext();

        var user = await new UserRepository(context).GetByEmailAsync("a@test.com", _ct);

        Assert.NotNull(user);
        Assert.Equal(RoleNames.ChoirDirector, user.Role.Name);
    }

    [Fact]
    public async Task GetByEmailAsync_Unknown_ReturnsNull_Async()
    {
        await using var context = _db.NewContext();

        Assert.Null(await new UserRepository(context).GetByEmailAsync("nobody@test.com", _ct));
    }

    [Fact]
    public async Task GetRefreshTokenByHashAsync_LoadsUserAndRole_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.RefreshTokens.Add(NewRefreshToken(user.Id, "h1"));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var token = await new UserRepository(context).GetRefreshTokenByHashAsync("h1", _ct);

        Assert.NotNull(token);
        Assert.Equal(user.Id, token.User.Id);
        Assert.Equal(RoleNames.ChoirMember, token.User.Role.Name);
    }

    [Fact]
    public async Task RevokeAllRefreshTokensAsync_RevokesOnlyThatUsersActiveTokens_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var alreadyRevokedAt = DateTime.UtcNow.AddDays(-2);
        await using (var seed = _db.NewContext())
        {
            seed.RefreshTokens.AddRange(
                NewRefreshToken(user.Id, "u1"),
                NewRefreshToken(user.Id, "u2"),
                NewRefreshToken(user.Id, "u3", revokedAt: alreadyRevokedAt),
                NewRefreshToken(other.Id, "o1"));
            await seed.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new UserRepository(context);
            await repository.RevokeAllRefreshTokensAsync(user.Id, _ct);
            await repository.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        var tokens = await verify.RefreshTokens.ToDictionaryAsync(t => t.TokenHash, _ct);
        Assert.NotNull(tokens["u1"].RevokedAt);
        Assert.NotNull(tokens["u2"].RevokedAt);
        Assert.Equal(alreadyRevokedAt, tokens["u3"].RevokedAt);
        Assert.Null(tokens["o1"].RevokedAt);
    }

    [Fact]
    public async Task RemoveUnusedPasswordResetTokensAsync_KeepsUsedAndOtherUsersTokens_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.PasswordResetTokens.AddRange(
                NewResetToken(user.Id, "unused1"),
                NewResetToken(user.Id, "unused2"),
                NewResetToken(user.Id, "used", usedAt: DateTime.UtcNow.AddHours(-1)),
                NewResetToken(other.Id, "other"));
            await seed.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new UserRepository(context);
            await repository.RemoveUnusedPasswordResetTokensAsync(user.Id, _ct);
            await repository.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        var remaining = await verify.PasswordResetTokens.Select(t => t.TokenHash).OrderBy(h => h).ToListAsync(_ct);
        Assert.Equal(["other", "used"], remaining);
    }

    [Fact]
    public async Task GetPasswordResetTokenByHashAsync_LoadsUser_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.PasswordResetTokens.Add(NewResetToken(user.Id, "r1"));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var token = await new UserRepository(context).GetPasswordResetTokenByHashAsync("r1", _ct);

        Assert.NotNull(token);
        Assert.Equal("a@test.com", token.User.Email);
    }

    [Fact]
    public async Task Users_EmailIsUnique_Async()
    {
        await _db.AddUserAsync("a@test.com", cancellationToken: _ct);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.AddUserAsync("a@test.com", cancellationToken: _ct));
    }

    // ---- NotificationRepository ----

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyOwnRowsNewestFirstAndPaged_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var seed = _db.NewContext())
        {
            for (var i = 0; i < 5; i++)
            {
                seed.Notifications.Add(NewNotification($"mine-{i}", start.AddHours(i), user.Id));
            }

            seed.Notifications.Add(NewNotification("theirs", start.AddDays(1), other.Id));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var page = await new NotificationRepository(context)
            .GetForUserAsync(user.Id, new PagingRequest { PageNumber = 2, PageSize = 2 }, _ct);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["mine-2", "mine-1"], page.Items.Select(r => r.Notification.Title));
        Assert.All(page.Items, r => Assert.Equal(user.Id, r.UserId));
    }

    [Fact]
    public async Task GetRecipientAsync_AnotherUsersNotification_ReturnsNull_Async()
    {
        var owner = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var stranger = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var notification = NewNotification("n", DateTime.UtcNow, owner.Id);
        await using (var seed = _db.NewContext())
        {
            seed.Notifications.Add(notification);
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new NotificationRepository(context);

        Assert.Null(await repository.GetRecipientAsync(notification.Id, stranger.Id, _ct));
        Assert.NotNull(await repository.GetRecipientAsync(notification.Id, owner.Id, _ct));
    }

    [Fact]
    public async Task CountUnreadAsync_CountsOnlyOwnUnread_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.Notifications.AddRange(
                NewNotification("1", DateTime.UtcNow, user.Id),
                NewNotification("2", DateTime.UtcNow, user.Id),
                NewNotification("3", DateTime.UtcNow, user.Id, isRead: true),
                NewNotification("4", DateTime.UtcNow, other.Id));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();

        Assert.Equal(2, await new NotificationRepository(context).CountUnreadAsync(user.Id, _ct));
    }

    // ---- MusicMaterialRepository.GetActiveForMemberAsync ----

    [Fact]
    public async Task GetActiveForMember_NoFilter_ReturnsGeneralAndApprovedSkillMaterialsOrdered_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(seed.MemberId, null, new SearchMusicMaterialsRequest(), _ct);

        // Excluded: Bass (skill pending), inactive material, material of an inactive song.
        Assert.Equal(["Guitar chords", "General sheet", "Alto audio"], page.Items.Select(m => m.Title));
        Assert.Equal(3, page.TotalCount);
        Assert.NotNull(page.Items.Single(m => m.Title == "Alto audio").TargetSkill);
    }

    [Theory]
    [InlineData("Hoa Binh", new[] { "General sheet", "Alto audio" })]
    [InlineData("Guitar", new[] { "Guitar chords" })]
    [InlineData("nothing matches", new string[0])]
    public async Task GetActiveForMember_Keyword_MatchesSongOrMaterialTitle_Async(string keyword, string[] expected)
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(seed.MemberId, keyword, new SearchMusicMaterialsRequest(), _ct);

        Assert.Equal(expected, page.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task GetActiveForMember_SkillId_ReturnsOnlyMaterialsTargetedAtThatSkill_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(seed.MemberId, null, new SearchMusicMaterialsRequest { SkillId = seed.AltoId }, _ct);

        Assert.Equal(["Alto audio"], page.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task GetActiveForMember_SkillNotApproved_ReturnsNothing_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(seed.MemberId, null, new SearchMusicMaterialsRequest { SkillId = seed.BassId }, _ct);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task GetActiveForMember_LiturgicalSeasonId_ReturnsMaterialsOfSongsClassifiedInThatSeason_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(
            seed.MemberId, null, new SearchMusicMaterialsRequest { LiturgicalSeasonId = seed.SeasonId }, _ct);

        Assert.Equal(["General sheet", "Alto audio"], page.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task GetActiveForMember_MaterialType_ReturnsOnlyThatType_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(
            seed.MemberId, null, new SearchMusicMaterialsRequest { MaterialType = MaterialType.SheetMusic }, _ct);

        Assert.Equal(["Guitar chords", "General sheet"], page.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task GetActiveForMember_FiltersCombineWithAnd_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(seed.MemberId, "Hoa Binh", new SearchMusicMaterialsRequest
        {
            SongId = seed.KinhHoaBinhId,
            MaterialType = MaterialType.SampleAudio,
        }, _ct);

        Assert.Equal(["Alto audio"], page.Items.Select(m => m.Title));
    }

    [Fact]
    public async Task GetActiveForMember_Paging_CountsWholeResultAndSkips_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);

        var page = await SearchMaterialsAsync(
            seed.MemberId, null, new SearchMusicMaterialsRequest { PageNumber = 2, PageSize = 2 }, _ct);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["Alto audio"], page.Items.Select(m => m.Title));
    }

    [Theory]
    [InlineData("General sheet", true)]
    [InlineData("Alto audio", true)]
    [InlineData("Bass audio", false)]
    [InlineData("Deleted lyrics", false)]
    [InlineData("Old lyrics", false)]
    public async Task IsVisibleToMember_MatchesTheMineListScope_Async(string title, bool expected)
    {
        var seed = await SeedMaterialsAsync(_ct);
        await using var context = _db.NewContext();
        var materialId = await context.MusicMaterials.Where(m => m.Title == title).Select(m => m.Id).SingleAsync(_ct);

        Assert.Equal(expected, await new MusicMaterialRepository(context).IsVisibleToMemberAsync(materialId, seed.MemberId, _ct));
    }

    [Fact]
    public async Task IsVisibleToMember_UnknownMaterial_ReturnsFalse_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        await using var context = _db.NewContext();

        Assert.False(await new MusicMaterialRepository(context).IsVisibleToMemberAsync(Guid.NewGuid(), seed.MemberId, _ct));
    }

    [Fact]
    public async Task GetActiveForMember_LoadsOnlyTheMembersOwnLearningProgress_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        var binh = await AddMemberAsync("Binh", MemberStatus.Active, [seed.AltoId], _ct);
        var altoAudio = await MaterialIdAsync("Alto audio", _ct);
        await AddProgressAsync(seed.MemberId, altoAudio, LearningStatus.NeedsPractice, _ct);
        await AddProgressAsync(binh, altoAudio, LearningStatus.Learned, _ct);
        await AddProgressAsync(binh, await MaterialIdAsync("General sheet", _ct), LearningStatus.Learned, _ct);

        var page = await SearchMaterialsAsync(seed.MemberId, null, new SearchMusicMaterialsRequest(), _ct);

        var progressByTitle = page.Items.ToDictionary(m => m.Title, m => m.LearningProgresses.Select(p => p.Status).ToArray());
        Assert.Equal([LearningStatus.NeedsPractice], progressByTitle["Alto audio"]);
        Assert.Empty(progressByTitle["General sheet"]);
        Assert.Empty(progressByTitle["Guitar chords"]);
    }

    [Theory]
    [InlineData(LearningStatus.NotStarted, new[] { "Guitar chords", "General sheet" })]
    [InlineData(LearningStatus.NeedsPractice, new[] { "Alto audio" })]
    [InlineData(LearningStatus.Learned, new string[0])]
    public async Task GetActiveForMember_LearningStatus_FiltersByTheMembersOwnStatus_Async(
        LearningStatus status, string[] expected)
    {
        var seed = await SeedMaterialsAsync(_ct);
        var binh = await AddMemberAsync("Binh", MemberStatus.Active, [seed.AltoId], _ct);
        var altoAudio = await MaterialIdAsync("Alto audio", _ct);
        await AddProgressAsync(seed.MemberId, altoAudio, LearningStatus.NeedsPractice, _ct);
        // Another member's progress must not count for this member.
        await AddProgressAsync(binh, await MaterialIdAsync("General sheet", _ct), LearningStatus.Learned, _ct);

        var page = await SearchMaterialsAsync(
            seed.MemberId, null, new SearchMusicMaterialsRequest { LearningStatus = status }, _ct);

        Assert.Equal(expected, page.Items.Select(m => m.Title));
        Assert.Equal(expected.Length, page.TotalCount);
    }

    // ---- MemberProfileRepository.GetLearnersOfMaterialAsync ----

    [Fact]
    public async Task GetLearnersOfMaterial_MaterialForEveryone_ReturnsActiveMembersByName_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        await AddMemberAsync("An", MemberStatus.Active, [], _ct);
        await AddMemberAsync("Cuong", MemberStatus.Inactive, [], _ct);
        await AddMemberAsync("Dung", MemberStatus.Left, [], _ct);

        var page = await GetLearnersAsync(
            await MaterialIdAsync("General sheet", _ct), null, new SearchMaterialLearningProgressRequest(), _ct);

        Assert.Equal(["An", "Member"], page.Items.Select(m => m.User.FullName));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task GetLearnersOfMaterial_SkillMaterial_ReturnsOnlyMembersWithThatSkillApproved_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        await AddMemberAsync("An", MemberStatus.Active, [], _ct);
        await AddMemberAsync("Binh", MemberStatus.Active, [seed.AltoId], _ct);
        var dung = await AddMemberAsync("Dung", MemberStatus.Active, [], _ct);
        await using (var context = _db.NewContext())
        {
            context.MemberSkills.Add(NewMemberSkill(dung, seed.AltoId, ApprovalStatus.Pending));
            await context.SaveChangesAsync(_ct);
        }

        var page = await GetLearnersAsync(
            await MaterialIdAsync("Alto audio", _ct), seed.AltoId, new SearchMaterialLearningProgressRequest(), _ct);

        Assert.Equal(["Binh", "Member"], page.Items.Select(m => m.User.FullName));
    }

    [Theory]
    [InlineData(null, new[] { "Binh", "Member" })]
    [InlineData(LearningStatus.NotStarted, new[] { "Binh" })]
    [InlineData(LearningStatus.NeedsPractice, new[] { "Member" })]
    [InlineData(LearningStatus.Learned, new string[0])]
    public async Task GetLearnersOfMaterial_Status_FiltersAndLoadsOnlyThatMaterialsRow_Async(
        LearningStatus? status, string[] expected)
    {
        var seed = await SeedMaterialsAsync(_ct);
        await AddMemberAsync("Binh", MemberStatus.Active, [seed.AltoId], _ct);
        var altoAudio = await MaterialIdAsync("Alto audio", _ct);
        await AddProgressAsync(seed.MemberId, altoAudio, LearningStatus.NeedsPractice, _ct);
        // A row on another material must neither match the filter nor be loaded.
        await AddProgressAsync(seed.MemberId, await MaterialIdAsync("General sheet", _ct), LearningStatus.Learned, _ct);

        var page = await GetLearnersAsync(altoAudio, seed.AltoId, new SearchMaterialLearningProgressRequest { Status = status }, _ct);

        Assert.Equal(expected, page.Items.Select(m => m.User.FullName));
        Assert.All(page.Items.SelectMany(m => m.LearningProgresses), p => Assert.Equal(altoAudio, p.MaterialId));
    }

    [Fact]
    public async Task GetLearnersOfMaterial_Paging_CountsWholeResultAndSkips_Async()
    {
        await SeedMaterialsAsync(_ct);
        await AddMemberAsync("An", MemberStatus.Active, [], _ct);
        await AddMemberAsync("Binh", MemberStatus.Active, [], _ct);

        var page = await GetLearnersAsync(await MaterialIdAsync("General sheet", _ct), null,
            new SearchMaterialLearningProgressRequest { PageNumber = 2, PageSize = 2 }, _ct);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["Member"], page.Items.Select(m => m.User.FullName));
    }

    // ---- MemberProfileRepository.SearchAsync ----

    [Fact]
    public async Task SearchMembers_LoadsOnlyApprovedActiveSkillsWithCategory_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        var retired = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Retired", IsActive = false };
        await using (var context = _db.NewContext())
        {
            context.Skills.Add(retired);
            context.MemberSkills.Add(NewMemberSkill(seed.MemberId, retired.Id, ApprovalStatus.Approved));
            await context.SaveChangesAsync(_ct);
        }

        var member = Assert.Single((await SearchMembersAsync(new SearchMemberProfilesRequest(), _ct)).Items);

        // Bass is pending, Retired is inactive.
        Assert.Equal(["Alto", "Guitar"], member.MemberSkills.Select(s => s.Skill.Name).Order());
        Assert.All(member.MemberSkills, s => Assert.NotNull(s.Skill.Category));
    }

    [Fact]
    public async Task SearchMembers_SkillId_KeepsOnlyMembersWithThatSkillApproved_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        await AddMemberAsync("Binh", MemberStatus.Active, [seed.BassId], _ct);
        await AddMemberAsync("An", MemberStatus.Active, [], _ct);

        // "Member" has Bass only pending.
        var page = await SearchMembersAsync(new SearchMemberProfilesRequest { SkillId = seed.BassId }, _ct);

        Assert.Equal(["Binh"], page.Items.Select(m => m.User.FullName));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task SearchMembers_SkillIdOfInactiveSkill_ReturnsEmpty_Async()
    {
        await SeedMaterialsAsync(_ct);
        var retired = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Retired", IsActive = false };
        await using (var context = _db.NewContext())
        {
            context.Skills.Add(retired);
            await context.SaveChangesAsync(_ct);
        }
        await AddMemberAsync("Binh", MemberStatus.Active, [retired.Id], _ct);

        var page = await SearchMembersAsync(new SearchMemberProfilesRequest { SkillId = retired.Id }, _ct);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    // ---- MemberProfileRepository.GetByUserIdWithApprovedSkillsAsync ----

    [Fact]
    public async Task GetByUserIdWithApprovedSkills_LoadsRoleAndOnlyApprovedActiveSkills_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        var retired = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Retired", IsActive = false };
        Guid userId;
        await using (var context = _db.NewContext())
        {
            context.Skills.Add(retired);
            context.MemberSkills.Add(NewMemberSkill(seed.MemberId, retired.Id, ApprovalStatus.Approved));
            await context.SaveChangesAsync(_ct);
            userId = (await context.MemberProfiles.SingleAsync(x => x.Id == seed.MemberId, _ct)).UserId;
        }

        await using var readContext = _db.NewContext();
        var member = await new MemberProfileRepository(readContext).GetByUserIdWithApprovedSkillsAsync(userId, _ct);

        Assert.NotNull(member);
        Assert.False(string.IsNullOrEmpty(member.User.Role.Name));
        // Bass is pending, Retired is inactive.
        Assert.Equal(["Alto", "Guitar"], member.MemberSkills.Select(s => s.Skill.Name).Order());
        Assert.All(member.MemberSkills, s => Assert.NotNull(s.Skill.Category));
    }

    private async Task<PagedList<MemberProfile>> SearchMembersAsync(
        SearchMemberProfilesRequest filter, CancellationToken cancellationToken = default)
    {
        await using var context = _db.NewContext();
        return await new MemberProfileRepository(context).SearchAsync(null, filter, cancellationToken);
    }

    private async Task<PagedList<MemberProfile>> GetLearnersAsync(Guid materialId, Guid? targetSkillId,
        SearchMaterialLearningProgressRequest filter, CancellationToken cancellationToken = default)
    {
        await using var context = _db.NewContext();
        return await new MemberProfileRepository(context)
            .GetLearnersOfMaterialAsync(materialId, targetSkillId, filter, cancellationToken);
    }

    /// <summary>Adds a member whose given skills are approved; returns the member id.</summary>
    private async Task<Guid> AddMemberAsync(
        string fullName, MemberStatus status, Guid[] approvedSkillIds, CancellationToken cancellationToken = default)
    {
        var user = await _db.AddUserAsync(
            $"{fullName.ToLowerInvariant()}@test.com", cancellationToken: cancellationToken, fullName: fullName);
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id, Status = status };
        await using var context = _db.NewContext();
        context.MemberProfiles.Add(member);
        context.MemberSkills.AddRange(approvedSkillIds.Select(id => NewMemberSkill(member.Id, id, ApprovalStatus.Approved)));
        await context.SaveChangesAsync(cancellationToken);
        return member.Id;
    }

    // ---- MaterialLearningProgressRepository ----

    [Fact]
    public async Task UpsertAsync_FirstMark_CreatesRow_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        var materialId = await MaterialIdAsync("General sheet", _ct);

        await using (var context = _db.NewContext())
        {
            var progress = await new MaterialLearningProgressRepository(context)
                .UpsertAsync(seed.MemberId, materialId, LearningStatus.Learned, _ct);
            Assert.NotEqual(Guid.Empty, progress.Id);
        }

        await using var verify = _db.NewContext();
        var row = await verify.MaterialLearningProgresses.SingleAsync(_ct);
        Assert.Equal((seed.MemberId, materialId, LearningStatus.Learned), (row.MemberId, row.MaterialId, row.Status));
    }

    [Fact]
    public async Task UpsertAsync_ExistingRow_UpdatesItAndLeavesOtherRowsAlone_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        var materialId = await MaterialIdAsync("General sheet", _ct);
        var otherMaterialId = await MaterialIdAsync("Alto audio", _ct);
        var existing = await AddProgressAsync(seed.MemberId, materialId, LearningStatus.Learned, _ct);
        var other = await AddProgressAsync(seed.MemberId, otherMaterialId, LearningStatus.Learned, _ct);

        await using (var context = _db.NewContext())
        {
            await new MaterialLearningProgressRepository(context)
                .UpsertAsync(seed.MemberId, materialId, LearningStatus.NeedsPractice, _ct);
        }

        await using var verify = _db.NewContext();
        var rows = await verify.MaterialLearningProgresses.ToDictionaryAsync(p => p.Id, _ct);
        Assert.Equal(2, rows.Count);
        Assert.Equal(LearningStatus.NeedsPractice, rows[existing.Id].Status);
        Assert.True(rows[existing.Id].UpdatedAt > existing.UpdatedAt);
        Assert.Equal(LearningStatus.Learned, rows[other.Id].Status);
    }

    [Fact]
    public async Task UpsertAsync_ConcurrentFirstMark_UpdatesTheRowTheOtherRequestInserted_Async()
    {
        var seed = await SeedMaterialsAsync(_ct);
        var materialId = await MaterialIdAsync("General sheet", _ct);
        MaterialLearningProgress? raced = null;
        // Runs after the repository has read "no row" and before it inserts, like a second request would.
        var race = new BeforeFirstSaveInterceptor(async () =>
            raced = await AddProgressAsync(seed.MemberId, materialId, LearningStatus.NeedsPractice, _ct));

        await using (var context = _db.NewContext(race))
        {
            var progress = await new MaterialLearningProgressRepository(context)
                .UpsertAsync(seed.MemberId, materialId, LearningStatus.Learned, _ct);

            Assert.Equal(raced!.Id, progress.Id);
            Assert.Equal(LearningStatus.Learned, progress.Status);
        }

        await using var verify = _db.NewContext();
        var row = await verify.MaterialLearningProgresses.SingleAsync(_ct);
        Assert.Equal((raced.Id, LearningStatus.Learned), (row.Id, row.Status));
    }

    private async Task<Guid> MaterialIdAsync(string title, CancellationToken cancellationToken = default)
    {
        await using var context = _db.NewContext();
        return await context.MusicMaterials.Where(m => m.Title == title).Select(m => m.Id).SingleAsync(cancellationToken);
    }

    private async Task<MaterialLearningProgress> AddProgressAsync(
        Guid memberId, Guid materialId, LearningStatus status, CancellationToken cancellationToken = default)
    {
        var progress = new MaterialLearningProgress
        {
            Id = Guid.NewGuid(), MemberId = memberId, MaterialId = materialId,
            Status = status, UpdatedAt = DateTime.UtcNow.AddDays(-1),
        };
        await using var context = _db.NewContext();
        context.MaterialLearningProgresses.Add(progress);
        await context.SaveChangesAsync(cancellationToken);
        return progress;
    }

    /// <summary>Runs <paramref name="action"/> once, just before the context's first save hits the database.</summary>
    private sealed class BeforeFirstSaveInterceptor(Func<Task> action) : SaveChangesInterceptor
    {
        private bool _hasRun;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_hasRun)
            {
                _hasRun = true;
                await action();
            }

            return result;
        }
    }

    private async Task<PagedList<MusicMaterial>> SearchMaterialsAsync(
        Guid memberId, string? keyword, SearchMusicMaterialsRequest filter, CancellationToken cancellationToken = default)
    {
        await using var context = _db.NewContext();
        return await new MusicMaterialRepository(context).GetActiveForMemberAsync(memberId, keyword, filter, cancellationToken);
    }

    private sealed record MaterialSeed(Guid MemberId, Guid AltoId, Guid BassId, Guid SeasonId, Guid KinhHoaBinhId);

    /// <summary>
    /// One member with Alto and Guitar approved and Bass pending. "Kinh Hoa Binh" is classified in one season;
    /// "Ave Maria" has no classification; "Old song" is inactive.
    /// </summary>
    private async Task<MaterialSeed> SeedMaterialsAsync(CancellationToken cancellationToken = default)
    {
        var user = await _db.AddUserAsync("member@test.com", cancellationToken: cancellationToken, fullName: "Member");
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id };
        var alto = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Alto" };
        var bass = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Bass" };
        var guitar = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Instrument, Name = "Guitar" };
        var seasonId = Guid.NewGuid();
        var kinhHoaBinh = new Song { Id = Guid.NewGuid(), Title = "Kinh Hoa Binh" };
        var aveMaria = new Song { Id = Guid.NewGuid(), Title = "Ave Maria" };
        var oldSong = new Song { Id = Guid.NewGuid(), Title = "Old song", IsActive = false };

        await using var context = _db.NewContext();
        context.MemberProfiles.Add(member);
        context.Skills.AddRange(alto, bass, guitar);
        context.Songs.AddRange(kinhHoaBinh, aveMaria, oldSong);
        context.MemberSkills.AddRange(
            NewMemberSkill(member.Id, alto.Id, ApprovalStatus.Approved),
            NewMemberSkill(member.Id, guitar.Id, ApprovalStatus.Approved),
            NewMemberSkill(member.Id, bass.Id, ApprovalStatus.Pending));
        context.SongClassifications.Add(new SongClassification
        {
            Id = Guid.NewGuid(), SongId = kinhHoaBinh.Id,
            TargetType = ClassificationTarget.LiturgicalSeason, TargetId = seasonId,
        });
        context.MusicMaterials.AddRange(
            NewMaterial(kinhHoaBinh.Id, MaterialType.SheetMusic, "General sheet", null),
            NewMaterial(kinhHoaBinh.Id, MaterialType.SampleAudio, "Alto audio", alto.Id),
            NewMaterial(aveMaria.Id, MaterialType.SheetMusic, "Guitar chords", guitar.Id),
            NewMaterial(aveMaria.Id, MaterialType.SampleAudio, "Bass audio", bass.Id),
            NewMaterial(aveMaria.Id, MaterialType.Lyrics, "Deleted lyrics", null, isActive: false),
            NewMaterial(oldSong.Id, MaterialType.Lyrics, "Old lyrics", null));
        await context.SaveChangesAsync(cancellationToken);

        return new MaterialSeed(member.Id, alto.Id, bass.Id, seasonId, kinhHoaBinh.Id);
    }

    private static MemberSkill NewMemberSkill(Guid memberId, Guid skillId, ApprovalStatus status) =>
        new() { Id = Guid.NewGuid(), MemberId = memberId, SkillId = skillId, Status = status, DeclaredAt = DateTime.UtcNow };

    private static MusicMaterial NewMaterial(
        Guid songId, MaterialType type, string title, Guid? targetSkillId, bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(), SongId = songId, MaterialType = type, Title = title, TargetSkillId = targetSkillId,
            FilePublicId = $"harmonia/{Guid.NewGuid()}", FileName = "file.pdf", IsActive = isActive,
        };

    // ---- MemberSkillRepository ----

    private async Task<(Guid MemberId, Guid SkillId)> SeedMemberAndSkillAsync(CancellationToken cancellationToken = default)
    {
        var user = await _db.AddUserAsync("ms@test.com", cancellationToken: cancellationToken);
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id };
        var tenor = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Tenor" };

        await using var context = _db.NewContext();
        context.MemberProfiles.Add(member);
        context.Skills.Add(tenor);
        await context.SaveChangesAsync(cancellationToken);
        return (member.Id, tenor.Id);
    }

    [Fact]
    public async Task MemberSkill_RejectedRows_DoNotBlockNewDeclaration_Async()
    {
        var (memberId, skillId) = await SeedMemberAndSkillAsync(_ct);
        await using (var seed = _db.NewContext())
        {
            seed.MemberSkills.AddRange(
                NewMemberSkill(memberId, skillId, ApprovalStatus.Rejected),
                NewMemberSkill(memberId, skillId, ApprovalStatus.Rejected));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new MemberSkillRepository(context);
        var hasLiveBefore = await repository.HasActiveDeclarationAsync(memberId, skillId, _ct);
        var added = await repository.TryAddAsync(NewMemberSkill(memberId, skillId, ApprovalStatus.Pending), _ct);

        Assert.False(hasLiveBefore);
        Assert.True(added);
        Assert.True(await repository.HasActiveDeclarationAsync(memberId, skillId, _ct));
    }

    [Fact]
    public async Task MemberSkill_TryAdd_SecondLiveDeclaration_ReturnsFalse_Async()
    {
        var (memberId, skillId) = await SeedMemberAndSkillAsync(_ct);
        await using (var seed = _db.NewContext())
        {
            seed.MemberSkills.Add(NewMemberSkill(memberId, skillId, ApprovalStatus.Approved));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var added = await new MemberSkillRepository(context)
            .TryAddAsync(NewMemberSkill(memberId, skillId, ApprovalStatus.Pending), _ct);

        Assert.False(added);
        await using var verify = _db.NewContext();
        Assert.Equal(1, await verify.MemberSkills.CountAsync(x => x.MemberId == memberId, _ct));
    }

    [Fact]
    public async Task MemberSkill_GetByMember_ReturnsHistoryNewestFirstWithCategory_Async()
    {
        var (memberId, skillId) = await SeedMemberAndSkillAsync(_ct);
        var older = NewMemberSkill(memberId, skillId, ApprovalStatus.Rejected);
        older.DeclaredAt = DateTime.UtcNow.AddDays(-10);
        var newer = NewMemberSkill(memberId, skillId, ApprovalStatus.Pending);
        await using (var seed = _db.NewContext())
        {
            seed.MemberSkills.AddRange(older, newer);
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new MemberSkillRepository(context);
        var page = await repository.GetByMemberAsync(memberId, null, new PagingRequest(), _ct);
        var rejectedOnly = await repository.GetByMemberAsync(memberId, ApprovalStatus.Rejected, new PagingRequest(), _ct);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer.Id, older.Id], page.Items.Select(x => x.Id));
        Assert.All(page.Items, x => Assert.NotNull(x.Skill.Category));
        Assert.Equal(1, rejectedOnly.TotalCount);
        Assert.Equal(older.Id, Assert.Single(rejectedOnly.Items).Id);
    }

    [Fact]
    public async Task MemberSkill_TrySaveReview_ConcurrentReview_SecondReturnsFalseAndFirstStands_Async()
    {
        var (memberId, skillId) = await SeedMemberAndSkillAsync(_ct);
        var director = await _db.AddUserAsync("director@test.com", RoleNames.ChoirDirector, _ct);
        var row = NewMemberSkill(memberId, skillId, ApprovalStatus.Pending);
        await using (var seed = _db.NewContext())
        {
            seed.MemberSkills.Add(row);
            await seed.SaveChangesAsync(_ct);
        }

        // Both directors read the row while it is still Pending.
        await using var first = _db.NewContext();
        await using var second = _db.NewContext();
        var firstRepository = new MemberSkillRepository(first);
        var secondRepository = new MemberSkillRepository(second);
        var firstRow = (await firstRepository.GetForReviewAsync(row.Id, _ct))!;
        var secondRow = (await secondRepository.GetForReviewAsync(row.Id, _ct))!;

        firstRow.Status = ApprovalStatus.Approved;
        firstRow.ApprovedBy = director.Id;
        secondRow.Status = ApprovalStatus.Rejected;
        secondRow.ApprovedBy = director.Id;
        secondRow.RejectReason = "Not ready";

        Assert.True(await firstRepository.TrySaveReviewAsync(firstRow, _ct));
        Assert.False(await secondRepository.TrySaveReviewAsync(secondRow, _ct));

        await using var verify = _db.NewContext();
        var saved = await verify.MemberSkills.SingleAsync(x => x.Id == row.Id, _ct);
        Assert.Equal(ApprovalStatus.Approved, saved.Status);
        Assert.Null(saved.RejectReason);
    }

    // ---- PracticeAssignmentRepository ----

    private sealed record PracticeSeed(
        Guid MemberId, Guid ForAll, Guid ForMember, Guid ForApprovedSkill, Guid ForPendingSkill, Guid ForOtherMember, Guid Overdue);

    /// <summary>
    /// One member who holds Soprano approved and Alto pending. Of six assignments the member receives four:
    /// everyone, named directly, Soprano, and an overdue one for everyone; not Alto or another member's.
    /// </summary>
    private async Task<PracticeSeed> SeedPracticeAsync()
    {
        var soprano = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Soprano" };
        var alto = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Alto" };
        var user = await _db.AddUserAsync("member@test.com", cancellationToken: _ct);
        var otherUser = await _db.AddUserAsync("other@test.com", cancellationToken: _ct);
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id, Status = MemberStatus.Active };
        var other = new MemberProfile { Id = Guid.NewGuid(), UserId = otherUser.Id, Status = MemberStatus.Active };
        member.MemberSkills.Add(new MemberSkill { Id = Guid.NewGuid(), SkillId = soprano.Id, Status = ApprovalStatus.Approved });
        member.MemberSkills.Add(new MemberSkill { Id = Guid.NewGuid(), SkillId = alto.Id, Status = ApprovalStatus.Pending });

        PracticeAssignment Assignment(int dueInDays, AssignmentScope scope, params PracticeAssignmentTarget[] targets) => new()
        {
            Id = Guid.NewGuid(), Title = $"due {dueInDays}", Scope = scope,
            DueDate = DateTime.UtcNow.AddDays(dueInDays), Targets = [.. targets],
        };
        PracticeAssignmentTarget SkillTarget(Guid skillId) => new() { Id = Guid.NewGuid(), TargetType = TargetType.Skill, SkillId = skillId };
        PracticeAssignmentTarget MemberTarget(Guid memberId) => new() { Id = Guid.NewGuid(), TargetType = TargetType.Member, MemberId = memberId };

        var forAll = Assignment(3, AssignmentScope.All);
        var forMember = Assignment(1, AssignmentScope.Individual, MemberTarget(member.Id));
        var forApprovedSkill = Assignment(2, AssignmentScope.SkillGroup, SkillTarget(soprano.Id));
        var forPendingSkill = Assignment(2, AssignmentScope.SkillGroup, SkillTarget(alto.Id));
        var forOtherMember = Assignment(2, AssignmentScope.Individual, MemberTarget(other.Id));
        var overdue = Assignment(-1, AssignmentScope.All);

        SubmissionStatus[] attempts = [SubmissionStatus.NeedsRevision, SubmissionStatus.Submitted];
        forAll.Submissions = attempts
            .Select((status, i) => new PracticeSubmission
            {
                Id = Guid.NewGuid(), MemberId = member.Id, AttemptNo = i + 1, Status = status, AudioPublicId = "a",
            })
            .Append(new PracticeSubmission { Id = Guid.NewGuid(), MemberId = other.Id, AttemptNo = 5, AudioPublicId = "b" })
            .ToList();

        await using var context = _db.NewContext();
        context.AddRange(soprano, alto, member, other, forAll, forMember, forApprovedSkill, forPendingSkill, forOtherMember, overdue);
        await context.SaveChangesAsync(_ct);

        return new PracticeSeed(member.Id, forAll.Id, forMember.Id, forApprovedSkill.Id, forPendingSkill.Id, forOtherMember.Id, overdue.Id);
    }

    [Fact]
    public async Task PracticeAssignment_GetForMember_ReturnsReceivedAssignmentsByDueDate_Async()
    {
        var seed = await SeedPracticeAsync();
        await using var context = _db.NewContext();

        var page = await new PracticeAssignmentRepository(context)
            .GetForMemberAsync(seed.MemberId, new SearchMyPracticeAssignmentsRequest(), _ct);

        Assert.Equal([seed.Overdue, seed.ForMember, seed.ForApprovedSkill, seed.ForAll], page.Items.Select(x => x.Id));
        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task PracticeAssignment_GetForMember_LoadsOnlyMembersNewestSubmission_Async()
    {
        var seed = await SeedPracticeAsync();
        await using var context = _db.NewContext();

        var page = await new PracticeAssignmentRepository(context)
            .GetForMemberAsync(seed.MemberId, new SearchMyPracticeAssignmentsRequest(), _ct);

        var submission = Assert.Single(page.Items.Single(x => x.Id == seed.ForAll).Submissions);
        Assert.Equal(seed.MemberId, submission.MemberId);
        Assert.Equal(2, submission.AttemptNo);
    }

    [Theory]
    [InlineData(true, 3)]
    [InlineData(false, 1)]
    public async Task PracticeAssignment_GetForMember_IsOpenFiltersByDueDate_Async(bool isOpen, int expected)
    {
        var seed = await SeedPracticeAsync();
        await using var context = _db.NewContext();

        var page = await new PracticeAssignmentRepository(context)
            .GetForMemberAsync(seed.MemberId, new SearchMyPracticeAssignmentsRequest { IsOpen = isOpen }, _ct);

        Assert.Equal(expected, page.TotalCount);
    }

    [Fact]
    public async Task PracticeAssignment_GetByIdForMember_NotReceived_ReturnsNull_Async()
    {
        var seed = await SeedPracticeAsync();
        await using var context = _db.NewContext();
        var repository = new PracticeAssignmentRepository(context);

        Assert.NotNull(await repository.GetByIdForMemberAsync(seed.ForApprovedSkill, seed.MemberId, _ct));
        Assert.Null(await repository.GetByIdForMemberAsync(seed.ForPendingSkill, seed.MemberId, _ct));
        Assert.Null(await repository.GetByIdForMemberAsync(seed.ForOtherMember, seed.MemberId, _ct));
    }

    // ---- PracticeSubmissionRepository ----

    [Fact]
    public async Task PracticeSubmission_TryAdd_SameAttemptTwice_SecondReturnsFalse_Async()
    {
        var seed = await SeedPracticeAsync();
        PracticeSubmission Attempt(int attemptNo) => new()
        {
            Id = Guid.NewGuid(), PracticeAssignmentId = seed.ForMember, MemberId = seed.MemberId,
            AttemptNo = attemptNo, AudioPublicId = "audio", SubmittedAt = DateTime.UtcNow,
        };

        await using (var first = _db.NewContext())
        {
            Assert.True(await new PracticeSubmissionRepository(first).TryAddAsync(Attempt(1), _ct));
        }

        await using var second = _db.NewContext();
        var repository = new PracticeSubmissionRepository(second);
        Assert.False(await repository.TryAddAsync(Attempt(1), _ct));
        Assert.True(await repository.TryAddAsync(Attempt(2), _ct));
    }

    // ---- GenericRepository ----

    [Fact]
    public async Task GenericRepository_AddGetRemove_RoundTrips_Async()
    {
        var id = Guid.NewGuid();
        await using (var context = _db.NewContext())
        {
            var repository = new GenericRepository<Role>(context);
            await repository.AddAsync(new Role { Id = id, Name = "Temp" }, _ct);
            await repository.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new GenericRepository<Role>(context);
            var role = await repository.GetByIdAsync(id, _ct);
            Assert.NotNull(role);
            repository.Remove(role);
            await repository.SaveChangesAsync(_ct);
        }

        await using var verify = _db.NewContext();
        Assert.Null(await new GenericRepository<Role>(verify).GetByIdAsync(id, _ct));
    }

    // ---- ServiceRosterRepository ----

    private sealed record RosterSeed(Guid EventId, Guid SopranoId, Guid ConfirmedId, Guid DeclinedId, Guid InactiveId, Guid PendingId);

    /// <summary>
    /// One upcoming event plus two past ones 10 and 90 days earlier. Of four sopranos only "confirmed" is a
    /// candidate: the others declined, are inactive, or still wait for skill approval. "confirmed" served the
    /// 10-day-old event twice (two skills) and the 90-day-old one once.
    /// </summary>
    private async Task<RosterSeed> SeedRosterAsync()
    {
        var soprano = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Soprano" };
        var alto = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Alto" };
        var location = new WorshipLocation { Id = Guid.NewGuid(), Name = "Main church" };
        var today = VietnamTime.Today;
        LiturgicalEvent NewEvent(int days) => new() { Id = Guid.NewGuid(), EventDate = today.AddDays(days), LocationId = location.Id };
        var upcoming = NewEvent(5);
        var recent = NewEvent(-10);
        var old = NewEvent(-90);

        async Task<MemberProfile> MemberAsync(string email, MemberStatus status, ParticipationStatus participation, ApprovalStatus skillStatus)
        {
            var user = await _db.AddUserAsync(email, fullName: email, cancellationToken: _ct);
            var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id, Status = status };
            member.EventParticipations.Add(new EventParticipation { Id = Guid.NewGuid(), EventId = upcoming.Id, Status = participation });
            member.MemberSkills.Add(new MemberSkill { Id = Guid.NewGuid(), SkillId = soprano.Id, Status = skillStatus });
            return member;
        }

        var confirmed = await MemberAsync("confirmed@test.com", MemberStatus.Active, ParticipationStatus.Confirmed, ApprovalStatus.Approved);
        var declined = await MemberAsync("declined@test.com", MemberStatus.Active, ParticipationStatus.Declined, ApprovalStatus.Approved);
        var inactive = await MemberAsync("inactive@test.com", MemberStatus.Inactive, ParticipationStatus.Confirmed, ApprovalStatus.Approved);
        var pending = await MemberAsync("pending@test.com", MemberStatus.Active, ParticipationStatus.Confirmed, ApprovalStatus.Pending);

        RosterAssignment Assign(Guid skillId) => new() { Id = Guid.NewGuid(), MemberId = confirmed.Id, SkillId = skillId };
        await using var context = _db.NewContext();
        context.AddRange(soprano, alto, location, upcoming, recent, old, confirmed, declined, inactive, pending);
        context.AddRange(
            new ServiceRoster { Id = Guid.NewGuid(), EventId = recent.Id, Assignments = [Assign(soprano.Id), Assign(alto.Id)] },
            new ServiceRoster { Id = Guid.NewGuid(), EventId = old.Id, Assignments = [Assign(soprano.Id)] });
        await context.SaveChangesAsync(_ct);

        return new RosterSeed(upcoming.Id, soprano.Id, confirmed.Id, declined.Id, inactive.Id, pending.Id);
    }

    [Fact]
    public async Task GetCandidateSkills_ReturnsOnlyApprovedSkillsOfActiveConfirmedMembers_Async()
    {
        var seed = await SeedRosterAsync();
        await using var context = _db.NewContext();

        var candidates = await new ServiceRosterRepository(context).GetCandidateSkillsAsync(seed.EventId, [seed.SopranoId], _ct);

        Assert.Equal(seed.ConfirmedId, Assert.Single(candidates).MemberId);
    }

    [Fact]
    public async Task CountRecentServices_CountsDistinctEventsInsideTheWindow_Async()
    {
        var seed = await SeedRosterAsync();
        var today = VietnamTime.Today;
        await using var context = _db.NewContext();

        var counts = await new ServiceRosterRepository(context).CountRecentServicesAsync(
            [seed.ConfirmedId, seed.DeclinedId], today.AddDays(-60), today, _ct);

        // Two assignments at the 10-day-old event count once; the 90-day-old event is outside the window.
        Assert.Equal(1, Assert.Single(counts, x => x.Key == seed.ConfirmedId).Value);
        Assert.DoesNotContain(seed.DeclinedId, counts.Keys);
    }

    [Fact]
    public async Task GetEventWithRoster_LoadsRosterAssignmentsTracked_Async()
    {
        var seed = await SeedRosterAsync();
        Guid recentEventId;
        await using (var lookup = _db.NewContext())
            recentEventId = (await lookup.ServiceRosters.SingleAsync(x => x.Assignments.Count == 2, _ct)).EventId;
        await using var context = _db.NewContext();

        var liturgicalEvent = await new ServiceRosterRepository(context).GetEventWithRosterAsync(recentEventId, _ct);

        Assert.Equal(2, liturgicalEvent!.ServiceRoster!.Assignments.Count);
        Assert.Equal(EntityState.Unchanged, context.Entry(liturgicalEvent.ServiceRoster).State);
        Assert.NotEqual(seed.EventId, recentEventId);
    }

    private static RefreshToken NewRefreshToken(Guid userId, string hash, DateTime? revokedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(7), RevokedAt = revokedAt,
        };

    private static PasswordResetToken NewResetToken(Guid userId, string hash, DateTime? usedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddHours(1), UsedAt = usedAt,
        };

    private static Notification NewNotification(string title, DateTime createdAt, Guid userId, bool isRead = false) =>
        new()
        {
            Id = Guid.NewGuid(), Title = title, Content = "c", CreatedAt = createdAt,
            Recipients = [new NotificationRecipient { Id = Guid.NewGuid(), UserId = userId, IsRead = isRead }],
        };
}
