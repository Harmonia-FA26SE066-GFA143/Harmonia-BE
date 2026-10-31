using Harmonia.Application.Interfaces.IServices;
using NSubstitute;

namespace Harmonia.Application.Tests;

/// <summary>Choir scope fakes for service tests that are not about the D8 director scope.</summary>
public static class ChoirScopeFakes
{
    /// <summary>
    /// Behaves like a caller who is not a Choir Director, or data with no choir: everything is allowed and nothing is
    /// filtered. Tests about the scope override single calls.
    /// </summary>
    public static IChoirScopeService Unrestricted()
    {
        var scope = Substitute.For<IChoirScopeService>();
        scope.GetDirectedChoirIdsAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyCollection<Guid>?)null);
        scope.CanManageChoirAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);
        scope.CheckChoirForNewRecordAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        scope.CanViewEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        scope.CanLeadEventAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        scope.CheckCanLeadEventAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        scope.GetLeadChoirIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
        scope.GetEventMemberIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<Guid>?)null);
        scope.GetChoirMemberIdsAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<Guid>?)null);
        scope.GetMemberChoirIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Guid>());
        return scope;
    }
}
