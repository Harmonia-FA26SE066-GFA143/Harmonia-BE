using System.Linq.Expressions;
using AutoMapper;
using Harmonia.Application.Common.Models;
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

public class DirectorTrainingServiceTests
{
    private readonly IGenericRepository<DirectorTraining> _trainings = Substitute.For<IGenericRepository<DirectorTraining>>();
    private readonly IDirectorNominationRepository _nominations = Substitute.For<IDirectorNominationRepository>();
    private readonly IFileStorageService _storage = Substitute.For<IFileStorageService>();
    private readonly INotificationService _notifications = Substitute.For<INotificationService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly DirectorTrainingService _sut;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly Guid _priestId = Guid.NewGuid();
    private readonly Guid _directorId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly DirectorNomination _nomination;
    private readonly List<DirectorTraining> _stored = [];

    public DirectorTrainingServiceTests()
    {
        var mapper = new MapperConfiguration(
                cfg => cfg.AddProfile<DirectorTrainingProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();
        _sut = new DirectorTrainingService(_trainings, _nominations, _storage, _notifications, _currentUser, mapper);

        _currentUser.RoleName.Returns(RoleNames.ParishPriest);
        _currentUser.UserId.Returns(_priestId);

        _nomination = new DirectorNomination
        {
            Id = Guid.NewGuid(), NominatedById = _directorId, Status = DirectorNominationStatus.Approved,
            Member = new MemberProfile { UserId = _memberUserId },
        };
        _nominations.GetByIdAsync(_nomination.Id, _ct).Returns(_nomination);
        _nominations.GetWithDetailsAsync(_nomination.Id, Arg.Any<Guid?>(), Arg.Any<Guid?>(), _ct).Returns(_nomination);

        _trainings.AddAsync(Arg.Do<DirectorTraining>(_stored.Add), _ct).Returns(Task.CompletedTask);
        _trainings.GetByIdAsync(Arg.Any<Guid>(), _ct)
            .Returns(call => _stored.FirstOrDefault(x => x.Id == call.Arg<Guid>()));
        _trainings.ListAsync(Arg.Any<Expression<Func<DirectorTraining, bool>>>(), _ct)
            .Returns(call => _stored.Where(call.Arg<Expression<Func<DirectorTraining, bool>>>().Compile()).ToList());
        _storage.GetSignedUrl(Arg.Any<string>()).Returns(call => $"https://signed/{call.Arg<string>()}");
    }

    private DirectorTraining StoreTraining(DirectorTrainingResult result)
    {
        var training = new DirectorTraining
        {
            Id = Guid.NewGuid(), NominationId = _nomination.Id, CourseName = "Basic", Result = result,
            StartDate = new DateOnly(2026, 11, 1),
        };
        _stored.Add(training);
        return training;
    }

    private static CreateDirectorTrainingRequest NewRequest() => new()
    {
        CourseName = "  Lớp ca trưởng cơ bản  ", Organizer = " Ban Thánh nhạc ", StartDate = new DateOnly(2026, 12, 1),
    };

    [Fact]
    public async Task Create_ApprovedNomination_StartsTraining_AndNotifiesMemberAndNominator_Async()
    {
        var result = await _sut.CreateAsync(_nomination.Id, NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        var training = Assert.Single(_stored);
        Assert.Equal("Lớp ca trưởng cơ bản", training.CourseName);
        Assert.Equal("Ban Thánh nhạc", training.Organizer);
        Assert.Equal(DirectorTrainingResult.InProgress, training.Result);
        Assert.Equal(DirectorNominationStatus.InTraining, _nomination.Status);
        await _notifications.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.RecipientUserIds.Order().SequenceEqual(
                new[] { _memberUserId, _directorId }.Order())),
            _ct);
    }

    [Fact]
    public async Task Create_TrainedNomination_AllowsAdvancedCourse_Async()
    {
        _nomination.Status = DirectorNominationStatus.Trained;

        var result = await _sut.CreateAsync(_nomination.Id, NewRequest(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectorNominationStatus.InTraining, _nomination.Status);
    }

    [Fact]
    public async Task Create_WhileInTraining_ReturnsInProgress_Async()
    {
        _nomination.Status = DirectorNominationStatus.InTraining;

        var result = await _sut.CreateAsync(_nomination.Id, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.DirectorTrainingInProgress, result.Code);
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task Create_PendingNomination_ReturnsInvalidStatus_Async()
    {
        _nomination.Status = DirectorNominationStatus.Pending;

        var result = await _sut.CreateAsync(_nomination.Id, NewRequest(), _ct);

        Assert.Equal(ErrorCodes.DirectorNominationInvalidStatus, result.Code);
    }

    [Fact]
    public async Task RecordResult_Passed_MovesNominationToTrained_Async()
    {
        _nomination.Status = DirectorNominationStatus.InTraining;
        var training = StoreTraining(DirectorTrainingResult.InProgress);

        var result = await _sut.RecordResultAsync(_nomination.Id, training.Id,
            new RecordDirectorTrainingResultRequest { Result = DirectorTrainingResult.Passed, Note = " Good " }, _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectorTrainingResult.Passed, training.Result);
        Assert.Equal("Good", training.ResultNote);
        Assert.NotNull(training.ResultRecordedAt);
        Assert.Equal(DirectorNominationStatus.Trained, _nomination.Status);
    }

    [Fact]
    public async Task RecordResult_FailedFirstCourse_ReturnsNominationToApproved_Async()
    {
        _nomination.Status = DirectorNominationStatus.InTraining;
        var training = StoreTraining(DirectorTrainingResult.InProgress);

        await _sut.RecordResultAsync(_nomination.Id, training.Id,
            new RecordDirectorTrainingResultRequest { Result = DirectorTrainingResult.Failed }, _ct);

        Assert.Equal(DirectorNominationStatus.Approved, _nomination.Status);
    }

    [Fact]
    public async Task RecordResult_FailedAdvancedAfterPassingBasic_KeepsNominationTrained_Async()
    {
        _nomination.Status = DirectorNominationStatus.InTraining;
        StoreTraining(DirectorTrainingResult.Passed);
        var advanced = StoreTraining(DirectorTrainingResult.InProgress);

        await _sut.RecordResultAsync(_nomination.Id, advanced.Id,
            new RecordDirectorTrainingResultRequest { Result = DirectorTrainingResult.Failed }, _ct);

        Assert.Equal(DirectorNominationStatus.Trained, _nomination.Status);
    }

    [Fact]
    public async Task RecordResult_AlreadyRecorded_ReturnsAlreadyRecorded_Async()
    {
        var training = StoreTraining(DirectorTrainingResult.Passed);

        var result = await _sut.RecordResultAsync(_nomination.Id, training.Id,
            new RecordDirectorTrainingResultRequest { Result = DirectorTrainingResult.Failed }, _ct);

        Assert.Equal(ErrorCodes.DirectorTrainingAlreadyRecorded, result.Code);
    }

    [Fact]
    public async Task RecordResult_TrainingOfAnotherNomination_ReturnsNotFound_Async()
    {
        var training = StoreTraining(DirectorTrainingResult.InProgress);

        var result = await _sut.RecordResultAsync(Guid.NewGuid(), training.Id,
            new RecordDirectorTrainingResultRequest { Result = DirectorTrainingResult.Passed }, _ct);

        Assert.Equal(ErrorCodes.DirectorTrainingNotFound, result.Code);
    }

    [Fact]
    public async Task UploadCertificate_ValidPdf_StoresPrivately_AndReplacesOldFile_Async()
    {
        var training = StoreTraining(DirectorTrainingResult.Passed);
        training.CertificatePublicId = "old.pdf";
        _storage.UploadAsync(Arg.Any<Stream>(), "cert.pdf", Arg.Any<string>(), true, _ct)
            .Returns(Result<FileUploadResponse>.Success(new FileUploadResponse("new.pdf", string.Empty)));

        var result = await _sut.UploadCertificateAsync(
            _nomination.Id, training.Id, new FileContent(Stream.Null, "cert.pdf", 1024), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("new.pdf", training.CertificatePublicId);
        Assert.Equal("https://signed/new.pdf", result.Value!.CertificateUrl);
        await _storage.Received(1).DeleteAsync("old.pdf", true, CancellationToken.None);
    }

    [Theory]
    [InlineData("cert.docx", 1024, ErrorCodes.DirectorTrainingCertificateTypeNotAllowed)]
    [InlineData("cert.pdf", 21 * 1024 * 1024, ErrorCodes.DirectorTrainingCertificateTooLarge)]
    [InlineData("cert.pdf", 0, ErrorCodes.DirectorTrainingCertificateRequired)]
    public async Task UploadCertificate_BadFile_IsRejectedBeforeUpload_Async(string fileName, long length, string code)
    {
        var training = StoreTraining(DirectorTrainingResult.Passed);

        var result = await _sut.UploadCertificateAsync(
            _nomination.Id, training.Id, new FileContent(Stream.Null, fileName, length), _ct);

        Assert.Equal(code, result.Code);
        await _storage.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), _ct);
    }

    [Fact]
    public async Task Delete_InProgress_RemovesIt_AndRestoresApproved_Async()
    {
        _nomination.Status = DirectorNominationStatus.InTraining;
        var training = StoreTraining(DirectorTrainingResult.InProgress);

        var result = await _sut.DeleteAsync(_nomination.Id, training.Id, _ct);

        Assert.True(result.IsSuccess);
        _trainings.Received(1).Remove(training);
        Assert.Equal(DirectorNominationStatus.Approved, _nomination.Status);
    }

    [Fact]
    public async Task Delete_InProgress_AfterWaiver_RestoresTrained_Async()
    {
        _nomination.Status = DirectorNominationStatus.InTraining;
        _nomination.TrainingWaiverReason = "Certificate 2024";
        var training = StoreTraining(DirectorTrainingResult.InProgress);

        await _sut.DeleteAsync(_nomination.Id, training.Id, _ct);

        Assert.Equal(DirectorNominationStatus.Trained, _nomination.Status);
    }

    [Fact]
    public async Task Delete_Recorded_ReturnsAlreadyRecorded_Async()
    {
        var training = StoreTraining(DirectorTrainingResult.Failed);

        var result = await _sut.DeleteAsync(_nomination.Id, training.Id, _ct);

        Assert.Equal(ErrorCodes.DirectorTrainingAlreadyRecorded, result.Code);
        _trainings.DidNotReceive().Remove(Arg.Any<DirectorTraining>());
    }

    [Fact]
    public async Task GetByNomination_NotVisible_ReturnsNominationNotFound_Async()
    {
        var otherDirector = Guid.NewGuid();
        _currentUser.RoleName.Returns(RoleNames.ChoirDirector);
        _currentUser.UserId.Returns(otherDirector);
        _nominations.GetWithDetailsAsync(_nomination.Id, otherDirector, null, _ct).Returns((DirectorNomination?)null);

        var result = await _sut.GetByNominationAsync(_nomination.Id, _ct);

        Assert.Equal(ErrorCodes.DirectorNominationNotFound, result.Code);
    }
}
