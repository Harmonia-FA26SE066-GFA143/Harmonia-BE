using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Interfaces.IServices;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class DirectorNoteServiceTests
{
    private readonly IDirectorNoteRepository _notes = Substitute.For<IDirectorNoteRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ILiturgicalEventRepository _events = Substitute.For<ILiturgicalEventRepository>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly DirectorNoteService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _priestId = Guid.NewGuid();
    private readonly Guid _director1 = Guid.NewGuid();
    private readonly Guid _director2 = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly List<DirectorNote> _added = [];

    public DirectorNoteServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<DirectorNoteProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new DirectorNoteService(_notes, _users, _events, _notifications, mapper);

        _users.GetActiveUserIdsByRolesAsync(
                Arg.Is<IReadOnlyCollection<string>>(r => r.SequenceEqual(new[] { RoleNames.ChoirDirector })), _ct)
            .Returns([_director1, _director2]);
        _events.GetByIdAsync(_eventId, _ct).Returns(new LiturgicalEvent { Id = _eventId, Title = "Christmas" });
        _notes.AddAsync(Arg.Do<DirectorNote>(_added.Add), _ct).Returns(Task.CompletedTask);
        _notes.GetWithDetailsAsync(Arg.Any<Guid>(), _ct).Returns(call =>
        {
            var note = _added.Single(n => n.Id == call.Arg<Guid>());
            note.FromUser = new User { Id = note.FromUserId, FullName = "Priest" };
            note.ToUser = new User { Id = note.ToUserId, FullName = "Director" };
            return note;
        });
    }

    private CreateDirectorNoteRequest NewRequest(params Guid[] toUserIds) => new()
    {
        EventId = _eventId, ToUserIds = [.. toUserIds], Content = "  Please prepare the psalm.  ",
    };

    [Fact]
    public async Task Create_StoresOneCopyPerDirector_AndNotifiesEachOne_Async()
    {
        var result = await _sut.CreateAsync(_priestId, NewRequest(_director1, _director2, _director1), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal([_director1, _director2], _added.Select(n => n.ToUserId));
        Assert.All(_added, n =>
        {
            Assert.Equal(_priestId, n.FromUserId);
            Assert.Equal("Please prepare the psalm.", n.Content);
        });
        await _notes.Received(1).SaveChangesAsync(_ct);
        foreach (var note in _added)
        {
            await _notifications.Received(1).SendAsync(
                Arg.Is<SendNotificationRequest>(r => r.Type == NotificationType.DirectorNote
                    && r.RecipientUserIds.SequenceEqual(new[] { note.ToUserId })
                    && r.ReferenceType == nameof(DirectorNote) && r.ReferenceId == note.Id),
                _ct);
        }
    }

    [Fact]
    public async Task Create_LongContent_NotificationCarriesAPreview_Async()
    {
        var request = NewRequest(_director1);
        request.Content = new string('a', 500);

        await _sut.CreateAsync(_priestId, request, _ct);

        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.Content.Length == 203 && r.Content.EndsWith("...")), _ct);
        Assert.Equal(500, Assert.Single(_added).Content.Length);
    }

    [Fact]
    public async Task Create_RecipientIsNotAnActiveDirector_ReturnsRecipientInvalid_Async()
    {
        var result = await _sut.CreateAsync(_priestId, NewRequest(_director1, Guid.NewGuid()), _ct);

        Assert.Equal(ErrorCodes.DirectorNoteRecipientInvalid, result.Code);
        Assert.Empty(_added);
        await _notifications.DidNotReceiveWithAnyArgs().SendAsync(default!, default);
    }

    [Fact]
    public async Task Create_EventMissing_ReturnsEventNotFound_Async()
    {
        var request = NewRequest(_director1);
        request.EventId = Guid.NewGuid();

        var result = await _sut.CreateAsync(_priestId, request, _ct);

        Assert.Equal(ErrorCodes.EventNotFound, result.Code);
        Assert.Empty(_added);
    }

    [Fact]
    public async Task GetById_SenderOrRecipient_ReturnsNote_Async()
    {
        await _sut.CreateAsync(_priestId, NewRequest(_director1), _ct);
        var id = Assert.Single(_added).Id;

        Assert.True((await _sut.GetByIdAsync(_priestId, id, _ct)).IsSuccess);
        var asRecipient = await _sut.GetByIdAsync(_director1, id, _ct);
        Assert.Equal(_director1, asRecipient.Value!.ToUserId);
        Assert.Equal("Priest", asRecipient.Value.FromUserName);
    }

    [Fact]
    public async Task GetById_SomeoneElsesNote_ReturnsNotFound_Async()
    {
        await _sut.CreateAsync(_priestId, NewRequest(_director1), _ct);

        var result = await _sut.GetByIdAsync(_director2, Assert.Single(_added).Id, _ct);

        Assert.Equal(ErrorCodes.DirectorNoteNotFound, result.Code);
    }

    [Fact]
    public async Task GetById_Missing_ReturnsNotFound_Async()
    {
        _notes.GetWithDetailsAsync(Arg.Any<Guid>(), _ct).Returns((DirectorNote?)null);

        var result = await _sut.GetByIdAsync(_priestId, Guid.NewGuid(), _ct);

        Assert.Equal(ErrorCodes.DirectorNoteNotFound, result.Code);
    }
}
