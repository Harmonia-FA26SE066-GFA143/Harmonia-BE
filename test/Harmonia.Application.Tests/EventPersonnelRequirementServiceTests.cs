using System.Linq.Expressions;
using AutoMapper;
using Harmonia.Application.DTOs;
using Harmonia.Application.Interfaces.IRepositories;
using Harmonia.Application.Mappings;
using Harmonia.Application.Services;
using Harmonia.Domain.Common;
using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Harmonia.Application.Tests;

public class EventPersonnelRequirementServiceTests
{
    private readonly IServiceRosterRepository _repository = Substitute.For<IServiceRosterRepository>();
    private readonly IGenericRepository<Skill> _skills = Substitute.For<IGenericRepository<Skill>>();
    private readonly EventPersonnelRequirementService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly Skill _organ = new() { Id = Guid.NewGuid(), Name = "Organ", CategoryId = SkillCategoryIds.Instrument };
    private readonly Skill _guitar = new() { Id = Guid.NewGuid(), Name = "Guitar", CategoryId = SkillCategoryIds.Instrument };
    private readonly LiturgicalEvent _event = new() { Id = Guid.NewGuid(), EventDate = VietnamTime.Today.AddDays(3), Status = EventStatus.Published };

    public EventPersonnelRequirementServiceTests()
    {
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<EventPersonnelRequirementProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new EventPersonnelRequirementService(_repository, _skills, ChoirScopeFakes.Unrestricted(), mapper);

        _repository.GetEventWithRosterAsync(_event.Id, _ct).Returns(_event);
        Skill[] skills = [_organ, _guitar];
        _skills.ListAsync(Arg.Any<Expression<Func<Skill, bool>>>(), _ct)
            .Returns(call => skills.Where(call.Arg<Expression<Func<Skill, bool>>>().Compile()).ToList());
    }

    private Task<Harmonia.Application.Common.Models.Result<List<EventPersonnelRequirementDto>>> UpdateAsync(
        params (Guid SkillId, int Count)[] requirements) =>
        _sut.UpdateAsync(_event.Id, new UpdateEventPersonnelRequirementsRequest
        {
            Requirements = requirements.Select(x => new UpdateEventPersonnelRequirementRequest { SkillId = x.SkillId, RequiredCount = x.Count }).ToList(),
        }, _ct);

    [Fact]
    public async Task Update_ReplacesTheWholeSet_Async()
    {
        _event.PersonnelRequirements.Add(new EventPersonnelRequirement { SkillId = _organ.Id, Skill = _organ, RequiredCount = 1 });

        var result = await UpdateAsync((_guitar.Id, 2));

        var requirement = Assert.Single(_event.PersonnelRequirements);
        Assert.Equal((_guitar.Id, 2), (requirement.SkillId, requirement.RequiredCount));
        Assert.Equal("Guitar", Assert.Single(result.Value!).SkillName);
        await _repository.Received(1).SaveChangesAsync(_ct);
    }

    [Fact]
    public async Task Update_UnknownSkill_FinalizedRoster_OrPastEvent_AreRejected_Async()
    {
        var unknown = await UpdateAsync((Guid.NewGuid(), 1));
        _event.ServiceRoster = new ServiceRoster { Status = RosterStatus.Finalized };
        var finalized = await UpdateAsync((_organ.Id, 1));
        _event.EventDate = VietnamTime.Today.AddDays(-1);
        var past = await UpdateAsync((_organ.Id, 1));

        Assert.Equal(
            [ErrorCodes.SkillNotFound, ErrorCodes.RosterAlreadyFinalized, ErrorCodes.LiturgicalEventAlreadyPassed],
            new[] { unknown.Code, finalized.Code, past.Code });
        Assert.Empty(_event.PersonnelRequirements);
    }

    [Fact]
    public async Task Update_RemovingARequirementSomeoneHolds_ReturnsInUse_Async()
    {
        _event.PersonnelRequirements.Add(new EventPersonnelRequirement { SkillId = _organ.Id, Skill = _organ, RequiredCount = 1 });
        _event.ServiceRoster = new ServiceRoster();
        _event.ServiceRoster.Assignments.Add(new RosterAssignment { SkillId = _organ.Id, Status = RosterAssignmentStatus.Active });

        var result = await UpdateAsync((_guitar.Id, 1));

        Assert.Equal(ErrorCodes.PersonnelRequirementInUse, result.Code);
        Assert.Equal(_organ.Id, Assert.Single(_event.PersonnelRequirements).SkillId);
    }
}
