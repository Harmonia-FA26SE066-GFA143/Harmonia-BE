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
