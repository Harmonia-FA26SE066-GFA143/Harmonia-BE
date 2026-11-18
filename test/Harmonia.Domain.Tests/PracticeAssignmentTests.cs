using Harmonia.Domain.Entities;
using Harmonia.Domain.Enums;

namespace Harmonia.Domain.Tests;

public class PracticeAssignmentTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, null, true)]
    [InlineData(-1, SubmissionStatus.Submitted, true)]
    [InlineData(-1, SubmissionStatus.NeedsRevision, true)]
    [InlineData(-1, SubmissionStatus.Passed, false)]
    [InlineData(0, null, false)]
    [InlineData(1, null, false)]
    [InlineData(1, SubmissionStatus.NeedsRevision, false)]
    public void IsOverdue_DuePassedWithoutPassedAttempt(int dueInMinutes, SubmissionStatus? latestStatus, bool expected)
    {
        Assert.Equal(expected, PracticeAssignment.IsOverdue(Now.AddMinutes(dueInMinutes), latestStatus, Now));
    }
}
