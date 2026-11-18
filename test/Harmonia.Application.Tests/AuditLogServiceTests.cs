using AutoMapper;
using Harmonia.Application.Common.Models;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class AuditLogServiceTests
{
    private readonly IAuditLogRepository _logs = Substitute.For<IAuditLogRepository>();
    private readonly AuditLogService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public AuditLogServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AuditLogProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new AuditLogService(_logs, mapper);
    }

    [Fact]
    public async Task Search_MapsEntriesAndKeepsPaging_WithOrWithoutAUser_Async()
    {
        var request = new SearchAuditLogsRequest();
        var byAdmin = new AuditLog
        {
            Id = Guid.NewGuid(), Action = "Updated", EntityType = nameof(User), EntityId = Guid.NewGuid(),
            OldValue = """{"IsActive":true}""", NewValue = """{"IsActive":false}""", IpAddress = "10.0.0.1",
            CreatedAt = DateTime.UtcNow, UserId = Guid.NewGuid(),
            User = new User { FullName = "Admin", Email = "admin@test.com" },
        };
        var anonymous = new AuditLog { Id = Guid.NewGuid(), Action = "PasswordChanged", EntityType = nameof(User) };
        _logs.SearchAsync(request, _ct).Returns(new PagedList<AuditLog>([byAdmin, anonymous], 2, 20, 22));

        var page = (await _sut.SearchAsync(request, _ct)).Value!;

        Assert.Equal((2, 20, 22), (page.PageNumber, page.PageSize, page.TotalCount));
        var first = page.Items[0];
        Assert.Equal(
            (byAdmin.Id, byAdmin.UserId, "Admin", "admin@test.com", "Updated", "User", byAdmin.EntityId),
            (first.Id, first.UserId, first.UserFullName, first.UserEmail, first.Action, first.EntityType, first.EntityId));
        Assert.Equal(
            (byAdmin.OldValue, byAdmin.NewValue, "10.0.0.1", byAdmin.CreatedAt),
            (first.OldValue, first.NewValue, first.IpAddress, first.CreatedAt));
        Assert.Equal((null, null, null), (page.Items[1].UserId, page.Items[1].UserFullName, page.Items[1].UserEmail));
    }
}
