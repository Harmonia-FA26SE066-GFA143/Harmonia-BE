using System.Text.RegularExpressions;
using Harmonia.API.Middlewares;
using Harmonia.Domain.Common;
using Harmonia.Domain.Exceptions;

namespace Harmonia.API.Tests;

public partial class ErrorStatusMapTests
{
    private const int NotMapped = -1;

    [Fact]
    public void MappedStatus_MatchesDocErrorCodes()
    {
        // ErrorStatusMap claims to be transcribed from the HTTP column of doc/error-codes.md.
        var mismatches = ReadDocumentedStatuses()
            .Select(d => (d.Code, Doc: d.Status, Map: ErrorStatusMap.StatusFor(d.Code, NotMapped)))
            .Where(x => x.Map != NotMapped && x.Map != x.Doc)
            .Select(x => $"{x.Code}: doc {x.Doc}, map {x.Map}")
            .ToArray();

        Assert.Empty(mismatches);
    }

    [Fact]
    public void EveryDomainExceptionCode_HasExplicitStatus()
    {
        // Otherwise it silently falls back to 409.
        var unmapped = typeof(DomainException).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(DomainException)) && !t.IsAbstract)
            .Select(t => ((DomainException)Activator.CreateInstance(t)!).Code)
            .Where(code => ErrorStatusMap.StatusFor(code, NotMapped) == NotMapped)
            .ToArray();

        Assert.Empty(unmapped);
    }

    [Fact]
    public void UnknownCode_ReturnsFallback()
    {
        Assert.Equal(418, ErrorStatusMap.StatusFor("NOT_A_REAL_CODE", 418));
    }

    [Theory]
    [InlineData(ErrorCodes.AuthInvalidCredentials, 401)]
    [InlineData(ErrorCodes.AuthAccountInactive, 403)]
    [InlineData(ErrorCodes.NotificationNotFound, 404)]
    [InlineData(ErrorCodes.InternalError, 500)]
    public void KeyCodes_HaveExpectedStatus(string code, int status)
    {
        Assert.Equal(status, ErrorStatusMap.StatusFor(code, NotMapped));
    }

    private static IEnumerable<(string Code, int Status)> ReadDocumentedStatuses()
    {
        var doc = File.ReadAllText(Path.Combine(TestPaths.SolutionRoot(), "doc", "error-codes.md"));
        return DocRow().Matches(doc).Select(m => (m.Groups[1].Value, int.Parse(m.Groups[2].Value)));
    }

    [GeneratedRegex(@"^\|\s*`([A-Z0-9_]+)`\s*\|\s*(\d{3})\s*\|", RegexOptions.Multiline)]
    private static partial Regex DocRow();
}
