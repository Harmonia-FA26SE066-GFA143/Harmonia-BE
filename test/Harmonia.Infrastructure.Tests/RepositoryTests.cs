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
            .GetForUserAsync(user.Id, null, new PagingRequest { PageNumber = 2, PageSize = 2 }, _ct);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(["mine-2", "mine-1"], page.Items.Select(r => r.Notification.Title));
        Assert.All(page.Items, r => Assert.Equal(user.Id, r.UserId));
    }

    [Fact]
    public async Task GetForUserAsync_FiltersByReadState_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        await using (var seed = _db.NewContext())
        {
            seed.Notifications.AddRange(
                NewNotification("read", DateTime.UtcNow, user.Id, isRead: true),
                NewNotification("unread", DateTime.UtcNow, user.Id));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new NotificationRepository(context);

        Assert.Equal("unread", Assert.Single((await repository.GetForUserAsync(user.Id, false, new PagingRequest(), _ct)).Items).Notification.Title);
        Assert.Equal("read", Assert.Single((await repository.GetForUserAsync(user.Id, true, new PagingRequest(), _ct)).Items).Notification.Title);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksOnlyTheUsersUnreadRows_Async()
    {
        var user = await _db.AddUserAsync("a@test.com", cancellationToken: _ct);
        var other = await _db.AddUserAsync("b@test.com", cancellationToken: _ct);
        var earlier = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var seed = _db.NewContext())
        {
            var alreadyRead = NewNotification("old", DateTime.UtcNow, user.Id, isRead: true);
            alreadyRead.Recipients.Single().ReadAt = earlier;
            seed.Notifications.AddRange(
                alreadyRead,
                NewNotification("a", DateTime.UtcNow, user.Id),
                NewNotification("b", DateTime.UtcNow, user.Id),
                NewNotification("theirs", DateTime.UtcNow, other.Id));
            await seed.SaveChangesAsync(_ct);
        }

        var readAt = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        await using (var context = _db.NewContext())
            await new NotificationRepository(context).MarkAllAsReadAsync(user.Id, readAt, _ct);

        await using var check = _db.NewContext();
        var rows = await check.NotificationRecipients.Include(x => x.Notification).ToListAsync(_ct);
        Assert.All(rows.Where(r => r.UserId == user.Id), r => Assert.True(r.IsRead));
        Assert.Equal(earlier, rows.Single(r => r.Notification.Title == "old").ReadAt);
        Assert.Equal(readAt, rows.Single(r => r.Notification.Title == "a").ReadAt);
        Assert.False(rows.Single(r => r.UserId == other.Id).IsRead);
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

    private sealed record ProgressSeed(
        Guid MemberId, Guid OpenNotSubmitted, Guid OverdueNotSubmitted, Guid OverdueNeedsRevision,
        Guid LatePassed, Guid OpenSubmitted, Guid OnlyOtherMemberSubmitted);

    /// <summary>
    /// Six assignments for everyone, seen by one member. "Late" ones are past due. OverdueNeedsRevision went
    /// Submitted then NeedsRevision; LatePassed went NeedsRevision then Passed, so only the newest attempt counts.
    /// OnlyOtherMemberSubmitted has a Passed attempt from someone else, which must not count for this member.
    /// </summary>
    private async Task<ProgressSeed> SeedProgressAsync()
    {
        var user = await _db.AddUserAsync("progress@test.com", cancellationToken: _ct);
        var otherUser = await _db.AddUserAsync("progress-other@test.com", cancellationToken: _ct);
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = user.Id };
        var other = new MemberProfile { Id = Guid.NewGuid(), UserId = otherUser.Id };

        PracticeAssignment Assignment(int dueInDays, params (MemberProfile By, SubmissionStatus Status)[] attempts) => new()
        {
            Id = Guid.NewGuid(), Title = "t", Scope = AssignmentScope.All, DueDate = DateTime.UtcNow.AddDays(dueInDays),
            Submissions = attempts.Select((a, i) => new PracticeSubmission
            {
                Id = Guid.NewGuid(), MemberId = a.By.Id, AttemptNo = i + 1, Status = a.Status, AudioPublicId = "audio",
            }).ToList(),
        };

        var openNotSubmitted = Assignment(2);
        var overdueNotSubmitted = Assignment(-1);
        var overdueNeedsRevision = Assignment(-1, (member, SubmissionStatus.Submitted), (member, SubmissionStatus.NeedsRevision));
        var latePassed = Assignment(-1, (member, SubmissionStatus.NeedsRevision), (member, SubmissionStatus.Passed));
        var openSubmitted = Assignment(2, (member, SubmissionStatus.Submitted));
        var onlyOther = Assignment(2, (other, SubmissionStatus.Passed));

        await using var context = _db.NewContext();
        context.AddRange(member, other, openNotSubmitted, overdueNotSubmitted, overdueNeedsRevision, latePassed, openSubmitted, onlyOther);
        await context.SaveChangesAsync(_ct);

        return new ProgressSeed(member.Id, openNotSubmitted.Id, overdueNotSubmitted.Id, overdueNeedsRevision.Id,
            latePassed.Id, openSubmitted.Id, onlyOther.Id);
    }

    private async Task<Guid[]> FilterAsync(Guid memberId, SearchMyPracticeAssignmentsRequest filter)
    {
        await using var context = _db.NewContext();
        var page = await new PracticeAssignmentRepository(context).GetForMemberAsync(memberId, filter, _ct);
        return page.Items.Select(x => x.Id).Order().ToArray();
    }

    [Fact]
    public async Task PracticeAssignment_FilterByStatus_MatchesNewestAttemptOfThisMember_Async()
    {
        var seed = await SeedProgressAsync();

        Assert.Equal([seed.OpenSubmitted], await FilterAsync(seed.MemberId, new() { Status = SubmissionStatus.Submitted }));
        Assert.Equal([seed.OverdueNeedsRevision], await FilterAsync(seed.MemberId, new() { Status = SubmissionStatus.NeedsRevision }));
        Assert.Equal([seed.LatePassed], await FilterAsync(seed.MemberId, new() { Status = SubmissionStatus.Passed }));
    }

    [Fact]
    public async Task PracticeAssignment_FilterByHasSubmission_CombinesWithIsOpen_Async()
    {
        var seed = await SeedProgressAsync();

        Assert.Equal(
            new[] { seed.OpenNotSubmitted, seed.OverdueNotSubmitted, seed.OnlyOtherMemberSubmitted }.Order(),
            await FilterAsync(seed.MemberId, new() { HasSubmission = false }));
        Assert.Equal(
            new[] { seed.OpenNotSubmitted, seed.OnlyOtherMemberSubmitted }.Order(),
            await FilterAsync(seed.MemberId, new() { HasSubmission = false, IsOpen = true }));
    }

    [Fact]
    public async Task PracticeAssignment_OverdueFilter_AgreesWithDomainRule_Async()
    {
        var seed = await SeedProgressAsync();
        await using var context = _db.NewContext();
        var repository = new PracticeAssignmentRepository(context);

        // The SQL filter and PracticeAssignment.IsOverdue must pick the same assignments from the same data.
        var filtered = await FilterAsync(seed.MemberId, new() { Status = SubmissionStatus.Overdue });
        var progress = await repository.GetProgressForMemberAsync(seed.MemberId, _ct);
        var now = DateTime.UtcNow;

        Assert.Equal(new[] { seed.OverdueNotSubmitted, seed.OverdueNeedsRevision }.Order(), filtered);
        Assert.Equal(filtered.Length, progress.Count(x => PracticeAssignment.IsOverdue(x.DueDate, x.LatestStatus, now)));
    }

    [Fact]
    public async Task PracticeAssignment_GetProgressForMember_OneRowPerAssignmentWithNewestStatus_Async()
    {
        var seed = await SeedProgressAsync();
        await using var context = _db.NewContext();

        var progress = await new PracticeAssignmentRepository(context).GetProgressForMemberAsync(seed.MemberId, _ct);

        Assert.Equal(
            // Enum order: Submitted, Passed, NeedsRevision.
            new SubmissionStatus?[] { null, null, null, SubmissionStatus.Submitted, SubmissionStatus.Passed, SubmissionStatus.NeedsRevision },
            progress.Select(x => x.LatestStatus).OrderBy(x => x.HasValue).ThenBy(x => x));
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

    private sealed record SubmissionSeed(
        Guid AssignmentId, Guid BinhOld, Guid BinhNewest, Guid AnNewest, Guid OtherAssignmentSubmission);

    /// <summary>
    /// Assignment A: Binh submitted twice (attempt 1 NeedsRevision, attempt 2 Submitted), An once (Passed).
    /// Assignment B: An submitted once (Submitted), earliest of all. Submission times go up in seeding order,
    /// except B's which is the oldest.
    /// </summary>
    private async Task<SubmissionSeed> SeedSubmissionsAsync()
    {
        var anUser = await _db.AddUserAsync("an@test.com", fullName: "An", cancellationToken: _ct);
        var binhUser = await _db.AddUserAsync("binh@test.com", fullName: "Binh", cancellationToken: _ct);
        var an = new MemberProfile { Id = Guid.NewGuid(), UserId = anUser.Id };
        var binh = new MemberProfile { Id = Guid.NewGuid(), UserId = binhUser.Id };
        var due = DateTime.UtcNow.AddDays(2);
        var a = new PracticeAssignment { Id = Guid.NewGuid(), Title = "A", Scope = AssignmentScope.All, DueDate = due };
        var b = new PracticeAssignment { Id = Guid.NewGuid(), Title = "B", Scope = AssignmentScope.All, DueDate = due };
        var start = DateTime.UtcNow.AddHours(-10);

        PracticeSubmission Submission(PracticeAssignment assignment, MemberProfile member, int attemptNo, SubmissionStatus status, int hour) => new()
        {
            Id = Guid.NewGuid(), PracticeAssignmentId = assignment.Id, MemberId = member.Id, AttemptNo = attemptNo,
            Status = status, AudioPublicId = "audio", SubmittedAt = start.AddHours(hour),
        };

        var binhOld = Submission(a, binh, 1, SubmissionStatus.NeedsRevision, 1);
        var binhNewest = Submission(a, binh, 2, SubmissionStatus.Submitted, 2);
        var anNewest = Submission(a, an, 1, SubmissionStatus.Passed, 3);
        var other = Submission(b, an, 1, SubmissionStatus.Submitted, 0);

        await using var context = _db.NewContext();
        context.AddRange(an, binh, a, b, binhOld, binhNewest, anNewest, other);
        await context.SaveChangesAsync(_ct);

        return new SubmissionSeed(a.Id, binhOld.Id, binhNewest.Id, anNewest.Id, other.Id);
    }

    [Fact]
    public async Task PracticeSubmission_SearchByAssignment_NewestPerMemberOrderedByName_Async()
    {
        var seed = await SeedSubmissionsAsync();
        await using var context = _db.NewContext();

        var page = await new PracticeSubmissionRepository(context)
            .SearchAsync(seed.AssignmentId, new SearchPracticeSubmissionsRequest(), _ct);

        Assert.Equal([seed.AnNewest, seed.BinhNewest], page.Items.Select(x => x.Id));
        Assert.Equal("An", page.Items[0].Member.User.FullName);
        Assert.Equal("A", page.Items[0].PracticeAssignment.Title);
    }

    [Fact]
    public async Task PracticeSubmission_SearchByAssignment_AllAttemptsNewestFirstPerMember_Async()
    {
        var seed = await SeedSubmissionsAsync();
        await using var context = _db.NewContext();

        var page = await new PracticeSubmissionRepository(context)
            .SearchAsync(seed.AssignmentId, new SearchPracticeSubmissionsRequest { AllAttempts = true }, _ct);

        Assert.Equal([seed.AnNewest, seed.BinhNewest, seed.BinhOld], page.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task PracticeSubmission_ReviewQueue_FiltersNewestByStatusOldestFirst_Async()
    {
        var seed = await SeedSubmissionsAsync();
        await using var context = _db.NewContext();

        var page = await new PracticeSubmissionRepository(context)
            .SearchAsync(null, new SearchPracticeSubmissionsRequest { Status = SubmissionStatus.Submitted }, _ct);

        Assert.Equal([seed.OtherAssignmentSubmission, seed.BinhNewest], page.Items.Select(x => x.Id));
        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task PracticeSubmission_ReviewQueue_StatusOfOlderAttemptDoesNotMatch_Async()
    {
        await SeedSubmissionsAsync();
        await using var context = _db.NewContext();

        // Binh's attempt 1 is NeedsRevision but attempt 2 supersedes it.
        var page = await new PracticeSubmissionRepository(context)
            .SearchAsync(null, new SearchPracticeSubmissionsRequest { Status = SubmissionStatus.NeedsRevision }, _ct);

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task PracticeSubmission_GetWithMember_LoadsMemberAndAssignment_Async()
    {
        var seed = await SeedSubmissionsAsync();
        await using var context = _db.NewContext();
        var repository = new PracticeSubmissionRepository(context);

        var submission = await repository.GetWithMemberAsync(seed.BinhOld, _ct);

        Assert.NotNull(submission);
        Assert.Equal("Binh", submission.Member.User.FullName);
        Assert.Equal("A", submission.PracticeAssignment.Title);
        Assert.Null(await repository.GetWithMemberAsync(Guid.NewGuid(), _ct));
    }

    [Fact]
    public async Task PracticeSubmission_GetWithMember_LoadsFeedbacksWithReviewer_Async()
    {
        var seed = await SeedSubmissionsAsync();
        var director = await _db.AddUserAsync("director@test.com", RoleNames.ChoirDirector, _ct, fullName: "Director");
        await using (var seedContext = _db.NewContext())
        {
            seedContext.PracticeFeedbacks.AddRange(
                new PracticeFeedback
                {
                    Id = Guid.NewGuid(), SubmissionId = seed.BinhOld, ReviewerId = director.Id,
                    Result = SubmissionStatus.NeedsRevision, Comment = "Breathe earlier", ReviewedAt = DateTime.UtcNow.AddHours(-1),
                },
                new PracticeFeedback
                {
                    Id = Guid.NewGuid(), SubmissionId = seed.BinhOld, ReviewerId = director.Id,
                    Result = SubmissionStatus.NeedsRevision, Comment = "And slower", ReviewedAt = DateTime.UtcNow,
                });
            await seedContext.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var submission = await new PracticeSubmissionRepository(context).GetWithMemberAsync(seed.BinhOld, _ct);

        Assert.Equal(2, submission!.Feedbacks.Count);
        Assert.All(submission.Feedbacks, f => Assert.Equal("Director", f.Reviewer.FullName));
    }

    [Fact]
    public async Task PracticeSubmission_SearchForMember_OnlyOwnAttemptsNewestFirst_Async()
    {
        var seed = await SeedSubmissionsAsync();
        await using var context = _db.NewContext();
        var repository = new PracticeSubmissionRepository(context);
        var binhId = (await context.PracticeSubmissions.SingleAsync(x => x.Id == seed.BinhOld, _ct)).MemberId;

        var all = await repository.SearchForMemberAsync(binhId, new SearchMyPracticeSubmissionsRequest(), _ct);
        var otherAssignment = await repository.SearchForMemberAsync(
            binhId, new SearchMyPracticeSubmissionsRequest { AssignmentId = Guid.NewGuid() }, _ct);

        Assert.Equal([seed.BinhNewest, seed.BinhOld], all.Items.Select(x => x.Id));
        Assert.Equal("A", all.Items[0].PracticeAssignment.Title);
        Assert.Empty(otherAssignment.Items);
    }

    [Fact]
    public async Task PracticeSubmission_IsLatestAttempt_FalseWhenMemberSubmittedAgain_Async()
    {
        var seed = await SeedSubmissionsAsync();
        await using var context = _db.NewContext();
        var repository = new PracticeSubmissionRepository(context);

        var old = await repository.GetForReviewAsync(seed.BinhOld, _ct);
        var newest = await repository.GetForReviewAsync(seed.BinhNewest, _ct);

        Assert.False(await repository.IsLatestAttemptAsync(old!, _ct));
        Assert.True(await repository.IsLatestAttemptAsync(newest!, _ct));
    }

    [Fact]
    public async Task PracticeSubmission_TrySaveReview_ConcurrentReview_SecondReturnsFalseAndFirstStands_Async()
    {
        var seed = await SeedSubmissionsAsync();
        var directorId = (await _db.AddUserAsync("director@test.com", RoleNames.ChoirDirector, _ct)).Id;
        await using var firstContext = _db.NewContext();
        await using var secondContext = _db.NewContext();
        var first = new PracticeSubmissionRepository(firstContext);
        var second = new PracticeSubmissionRepository(secondContext);

        // Both directors read the row while it is still Submitted.
        var firstRead = await first.GetForReviewAsync(seed.BinhNewest, _ct);
        var secondRead = await second.GetForReviewAsync(seed.BinhNewest, _ct);

        PracticeFeedback Review(PracticeSubmission submission, SubmissionStatus result)
        {
            submission.Status = result;
            return new PracticeFeedback
            {
                Id = Guid.NewGuid(), SubmissionId = submission.Id, ReviewerId = directorId, Result = result,
                ReviewedAt = DateTime.UtcNow,
            };
        }

        Assert.True(await first.TrySaveReviewAsync(firstRead!, Review(firstRead!, SubmissionStatus.Passed), _ct));
        Assert.False(await second.TrySaveReviewAsync(secondRead!, Review(secondRead!, SubmissionStatus.NeedsRevision), _ct));

        await using var check = _db.NewContext();
        var saved = await check.PracticeSubmissions.Include(x => x.Feedbacks).SingleAsync(x => x.Id == seed.BinhNewest, _ct);
        Assert.Equal(SubmissionStatus.Passed, saved.Status);
        Assert.Equal(SubmissionStatus.Passed, Assert.Single(saved.Feedbacks).Result);
        // Nothing of the losing review is left pending for a later save in the same request.
        Assert.DoesNotContain(secondContext.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
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

    // ---- Participation history (UC-11) & attendance (UC-30) ----

    private sealed record HistorySeed(
        Guid MemberId, Guid OtherId, Guid InvitedEventId, Guid ServedEventId, Guid SeasonId, Guid SkillId, Guid[] ExcludedEventIds);

    /// <summary>
    /// Past Published events: "invited" (member has a participation row, two rehearsals), "served" (member only on a
    /// Finalized roster), "draft" (member only on a Draft roster) and "other" (only another member involved).
    /// Plus a Cancelled past event and an upcoming one, both with the member invited.
    /// </summary>
    private async Task<HistorySeed> SeedHistoryAsync()
    {
        var location = new WorshipLocation { Id = Guid.NewGuid(), Name = "Main church" };
        var season = new LiturgicalSeason { Id = Guid.NewGuid(), Name = "Advent" };
        var tenor = new Skill { Id = Guid.NewGuid(), CategoryId = SkillCategoryIds.Vocal, Name = "Tenor" };
        var member = new MemberProfile { Id = Guid.NewGuid(), UserId = (await _db.AddUserAsync("m@test.com", cancellationToken: _ct)).Id };
        var other = new MemberProfile { Id = Guid.NewGuid(), UserId = (await _db.AddUserAsync("o@test.com", cancellationToken: _ct)).Id };
        var today = VietnamTime.Today;
        LiturgicalEvent NewEvent(int days, EventStatus status = EventStatus.Published) =>
            new() { Id = Guid.NewGuid(), EventDate = today.AddDays(days), LocationId = location.Id, Status = status };
        EventParticipation Invite(Guid memberId) => new() { Id = Guid.NewGuid(), MemberId = memberId, Status = ParticipationStatus.Confirmed };
        ServiceRoster Roster(RosterStatus status, Guid memberId) => new()
        {
            Id = Guid.NewGuid(), Status = status,
            Assignments = [new RosterAssignment { Id = Guid.NewGuid(), MemberId = memberId, SkillId = tenor.Id }],
        };

        var invited = NewEvent(-3);
        invited.LiturgicalSeasonId = season.Id;
        invited.EventParticipations = [Invite(member.Id), Invite(other.Id)];
        invited.Rehearsals = new[] { 1, 2 }.Select(i => new Rehearsal
        {
            Id = Guid.NewGuid(), StartTime = DateTime.UtcNow.AddDays(-4 - i), EndTime = DateTime.UtcNow.AddDays(-4 - i).AddHours(2),
            Attendances =
            [
                new RehearsalAttendance { Id = Guid.NewGuid(), MemberId = member.Id, CheckedBy = member.UserId, Status = AttendanceStatus.Present },
                new RehearsalAttendance { Id = Guid.NewGuid(), MemberId = other.Id, CheckedBy = member.UserId, Status = AttendanceStatus.Absent },
            ],
        }).ToList();
        var served = NewEvent(-10);
        served.ServiceRoster = Roster(RosterStatus.Finalized, member.Id);
        var draft = NewEvent(-20);
        draft.ServiceRoster = Roster(RosterStatus.Draft, member.Id);
        var otherOnly = NewEvent(-5);
        otherOnly.EventParticipations = [Invite(other.Id)];
        var cancelled = NewEvent(-7, EventStatus.Cancelled);
        cancelled.EventParticipations = [Invite(member.Id)];
        var upcoming = NewEvent(3);
        upcoming.EventParticipations = [Invite(member.Id)];

        await using var context = _db.NewContext();
        context.AddRange(location, season, tenor, member, other, invited, served, draft, otherOnly, cancelled, upcoming);
        await context.SaveChangesAsync(_ct);

        return new HistorySeed(member.Id, other.Id, invited.Id, served.Id, season.Id, tenor.Id,
            [draft.Id, otherOnly.Id, cancelled.Id, upcoming.Id]);
    }

    [Fact]
    public async Task GetHistoryForMember_ReturnsPastPublishedInvolvedEvents_WithOnlyTheMembersRows_Async()
    {
        var seed = await SeedHistoryAsync();
        await using var context = _db.NewContext();

        var page = await new LiturgicalEventRepository(context).GetHistoryForMemberAsync(
            seed.MemberId, new SearchMyParticipationHistoryRequest(), VietnamTime.Today, _ct);

        Assert.Equal([seed.InvitedEventId, seed.ServedEventId], page.Items.Select(e => e.Id));
        Assert.Equal(2, page.TotalCount);
        var invited = page.Items[0];
        Assert.Equal("Advent", invited.LiturgicalSeason!.Name);
        Assert.Equal(seed.MemberId, Assert.Single(invited.EventParticipations).MemberId);
        Assert.Equal(2, invited.Rehearsals.Count);
        Assert.All(invited.Rehearsals, r => Assert.Equal(seed.MemberId, Assert.Single(r.Attendances).MemberId));
        Assert.Equal("Tenor", Assert.Single(page.Items[1].ServiceRoster!.Assignments).Skill.Name);
    }

    [Fact]
    public async Task GetHistoryForMember_FiltersBySeason_Async()
    {
        var seed = await SeedHistoryAsync();
        await using var context = _db.NewContext();

        var page = await new LiturgicalEventRepository(context).GetHistoryForMemberAsync(
            seed.MemberId, new SearchMyParticipationHistoryRequest { LiturgicalSeasonId = seed.SeasonId }, VietnamTime.Today, _ct);

        Assert.Equal(seed.InvitedEventId, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task GetForMemberByEvents_ReturnsReceivedAssignmentsOfThoseEvents_WithNewestSubmission_Async()
    {
        var seed = await SeedHistoryAsync();
        PracticeAssignment Assignment(Guid eventId, AssignmentScope scope) => new()
        {
            Id = Guid.NewGuid(), EventId = eventId, Title = "t", Scope = scope, DueDate = DateTime.UtcNow.AddDays(-1),
        };
        var all = Assignment(seed.InvitedEventId, AssignmentScope.All);
        all.Submissions =
        [
            new PracticeSubmission { Id = Guid.NewGuid(), MemberId = seed.MemberId, AudioPublicId = "a1", AttemptNo = 1, Status = SubmissionStatus.NeedsRevision },
            new PracticeSubmission { Id = Guid.NewGuid(), MemberId = seed.MemberId, AudioPublicId = "a2", AttemptNo = 2, Status = SubmissionStatus.Passed },
            new PracticeSubmission { Id = Guid.NewGuid(), MemberId = seed.OtherId, AudioPublicId = "a3", AttemptNo = 1, Status = SubmissionStatus.Submitted },
        ];
        var forOther = Assignment(seed.InvitedEventId, AssignmentScope.Individual);
        forOther.Targets = [new PracticeAssignmentTarget { Id = Guid.NewGuid(), TargetType = TargetType.Member, MemberId = seed.OtherId }];
        var otherEvent = Assignment(seed.ServedEventId, AssignmentScope.All);
        await using (var seedContext = _db.NewContext())
        {
            seedContext.AddRange(all, forOther, otherEvent);
            await seedContext.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var assignments = await new PracticeAssignmentRepository(context).GetForMemberByEventsAsync(
            seed.MemberId, [seed.InvitedEventId], _ct);

        var received = Assert.Single(assignments);
        Assert.Equal(all.Id, received.Id);
        Assert.Equal(SubmissionStatus.Passed, Assert.Single(received.Submissions).Status);
    }

    [Fact]
    public async Task TrySaveAttendances_InsertsNewRowsAndUpdatesExistingOnes_Async()
    {
        var seed = await SeedHistoryAsync();
        Guid rehearsalId;
        await using (var lookup = _db.NewContext())
            rehearsalId = (await lookup.Rehearsals.FirstAsync(_ct)).Id;
        var newcomer = new MemberProfile { Id = Guid.NewGuid(), UserId = (await _db.AddUserAsync("n@test.com", cancellationToken: _ct)).Id };
        await using (var seedContext = _db.NewContext())
        {
            seedContext.Add(newcomer);
            await seedContext.SaveChangesAsync(_ct);
        }

        await using (var context = _db.NewContext())
        {
            var repository = new RehearsalRepository(context);
            var rehearsal = await repository.GetWithAttendancesForUpdateAsync(rehearsalId, _ct);
            rehearsal!.Attendances.Single(a => a.MemberId == seed.MemberId).Status = AttendanceStatus.Late;
            // Id left unset, exactly as RehearsalAttendanceService adds it.
            rehearsal.Attendances.Add(new RehearsalAttendance
            {
                RehearsalId = rehearsalId, MemberId = newcomer.Id, CheckedBy = newcomer.UserId, Status = AttendanceStatus.Excused,
            });

            Assert.True(await repository.TrySaveAttendancesAsync(rehearsal, _ct));
        }

        await using var check = _db.NewContext();
        var saved = await check.RehearsalAttendances.Where(a => a.RehearsalId == rehearsalId).ToListAsync(_ct);
        Assert.Equal(3, saved.Count);
        Assert.Equal(AttendanceStatus.Late, saved.Single(a => a.MemberId == seed.MemberId).Status);
        Assert.NotEqual(Guid.Empty, saved.Single(a => a.MemberId == newcomer.Id).Id);
    }

    // ---- DirectorNoteRepository & UserRepository.GetActiveByRoleAsync (UC-17) ----

    [Fact]
    public async Task DirectorNotes_SearchForUser_ReturnsOnlySentOrReceived_NewestFirst_WithDetails_Async()
    {
        var priest = await _db.AddUserAsync("priest@test.com", RoleNames.ParishPriest, _ct, "Priest");
        var directorA = await _db.AddUserAsync("a@test.com", RoleNames.ChoirDirector, _ct, "A");
        var directorB = await _db.AddUserAsync("b@test.com", RoleNames.ChoirDirector, _ct, "B");
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        DirectorNote Note(Guid to, int hours) => new()
        {
            Id = Guid.NewGuid(), FromUserId = priest.Id, ToUserId = to, Content = $"to-{to}-{hours}",
            NoteDate = new DateOnly(2026, 10, 1), SentAt = start.AddHours(hours),
        };
        var older = Note(directorA.Id, 1);
        var newer = Note(directorA.Id, 2);
        await using (var seed = _db.NewContext())
        {
            seed.DirectorNotes.AddRange(older, newer, Note(directorB.Id, 3));
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var repository = new DirectorNoteRepository(context);
        var received = await repository.SearchForUserAsync(directorA.Id, new SearchDirectorNotesRequest(), _ct);
        var sent = await repository.SearchForUserAsync(priest.Id, new SearchDirectorNotesRequest(), _ct);

        Assert.Equal([newer.Id, older.Id], received.Items.Select(n => n.Id));
        Assert.All(received.Items, n => Assert.Equal(("Priest", "A"), (n.FromUser.FullName, n.ToUser.FullName)));
        Assert.Equal(3, sent.TotalCount);
    }

    [Fact]
    public async Task GetActiveByRole_ReturnsOnlyActiveUsersOfThatRole_OrderedByName_Async()
    {
        await _db.AddUserAsync("z@test.com", RoleNames.ChoirDirector, _ct, "Zeta");
        await _db.AddUserAsync("a@test.com", RoleNames.ChoirDirector, _ct, "Alpha");
        var inactive = await _db.AddUserAsync("off@test.com", RoleNames.ChoirDirector, _ct, "Off");
        await _db.AddUserAsync("m@test.com", RoleNames.ChoirMember, _ct, "Member");
        await using (var seed = _db.NewContext())
        {
            (await seed.Users.SingleAsync(u => u.Id == inactive.Id, _ct)).IsActive = false;
            await seed.SaveChangesAsync(_ct);
        }

        await using var context = _db.NewContext();
        var directors = await new UserRepository(context).GetActiveByRoleAsync(RoleNames.ChoirDirector, _ct);

        Assert.Equal(["Alpha", "Zeta"], directors.Select(u => u.FullName));
        Assert.All(directors, u => Assert.Equal(RoleNames.ChoirDirector, u.Role.Name));
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
