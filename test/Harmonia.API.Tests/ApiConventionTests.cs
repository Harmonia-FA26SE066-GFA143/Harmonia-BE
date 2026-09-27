using System.Reflection;
using System.Text.RegularExpressions;
using Harmonia.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Harmonia.API.Tests;

/// <summary>
/// Rules from 02-naming.md and 03-security.md, checked by reflection over every controller and hub,
/// so endpoints added later are covered without writing new tests.
/// </summary>
public partial class ApiConventionTests
{
    // 03-security.md: no public registration; only these four may skip authentication.
    private static readonly HashSet<string> AnonymousWhitelist =
    [
        $"{nameof(AuthController)}.{nameof(AuthController.LoginAsync)}",
        $"{nameof(AuthController)}.{nameof(AuthController.RefreshAsync)}",
        $"{nameof(AuthController)}.{nameof(AuthController.ForgotPasswordAsync)}",
        $"{nameof(AuthController)}.{nameof(AuthController.ResetPasswordAsync)}",
    ];

    private static readonly Assembly ApiAssembly = typeof(AuthController).Assembly;

    private static readonly Type[] Controllers = ApiAssembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ControllerBase)))
        .ToArray();

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions() =>
        Controllers.SelectMany(c => c
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null)
            .Select(m => (c, m)));

    [Fact]
    public void Controllers_Exist()
    {
        Assert.NotEmpty(Controllers);
    }

    [Fact]
    public void EveryAction_RequiresAuthentication_UnlessWhitelisted()
    {
        var offenders = Actions()
            .Where(x =>
            {
                var isAnonymous = x.Action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                    || x.Controller.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
                var hasAuthorize = x.Action.GetCustomAttributes<AuthorizeAttribute>().Any()
                    || x.Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
                var name = $"{x.Controller.Name}.{x.Action.Name}";

                return isAnonymous ? !AnonymousWhitelist.Contains(name) : !hasAuthorize;
            })
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void AnonymousWhitelist_PointsAtRealAnonymousActions()
    {
        var anonymous = Actions()
            .Where(x => x.Action.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .ToHashSet();

        Assert.Equal(AnonymousWhitelist.Order(), anonymous.Order());
    }

    [Fact]
    public void Roles_UseKnownRoleNames()
    {
        string[] known = ["Admin", "ParishPriest", "ChoirDirector", "ChoirMember"];
        var offenders = Controllers
            .SelectMany(c => c.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(c.GetMethods().SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>())))
            .Where(a => a.Roles is not null)
            .SelectMany(a => a.Roles!.Split(',', StringSplitOptions.TrimEntries))
            .Where(role => !known.Contains(role))
            .Distinct();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Controllers_NameEndsWithControllerAndDeriveFromBase()
    {
        var offenders = Controllers
            .Where(c => !c.Name.EndsWith("Controller") || !c.IsSubclassOf(typeof(ApiControllerBase)))
            .Select(c => c.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void Routes_AreKebabCaseUnderApi()
    {
        var offenders = Controllers
            .Select(c => (c.Name, Template: c.GetCustomAttribute<RouteAttribute>()?.Template))
            .Where(x => x.Template is null || !KebabRoute().IsMatch(x.Template))
            .Select(x => $"{x.Name}: {x.Template ?? "(no [Route])"}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void ActionTemplates_AreKebabCase()
    {
        var offenders = Actions()
            .SelectMany(x => x.Action.GetCustomAttributes<HttpMethodAttribute>()
                .Where(a => a.Template is not null)
                .Select(a => (Name: $"{x.Controller.Name}.{x.Action.Name}", a.Template)))
            .Where(x => !KebabSegments().IsMatch(x.Template!))
            .Select(x => $"{x.Name}: {x.Template}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void Actions_NeverTakeEntities()
    {
        // 03-security.md: requests come in as <X>Request, never as an entity (over-posting).
        var offenders = Actions()
            .SelectMany(x => x.Action.GetParameters().Select(p => (x, p)))
            .Where(y => y.p.ParameterType.Namespace == "Harmonia.Domain.Entities")
            .Select(y => $"{y.x.Controller.Name}.{y.x.Action.Name}({y.p.Name})");

        Assert.Empty(offenders);
    }

    [Fact]
    public void AsyncActions_TakeCancellationToken()
    {
        var offenders = Actions()
            .Where(x => typeof(Task).IsAssignableFrom(x.Action.ReturnType)
                && x.Action.GetParameters().All(p => p.ParameterType != typeof(CancellationToken)))
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void Hubs_RequireAuthorization()
    {
        var hubs = ApiAssembly.GetTypes()
            .Where(t => !t.IsAbstract && IsHub(t))
            .ToArray();

        Assert.NotEmpty(hubs);
        Assert.All(hubs, h => Assert.NotNull(h.GetCustomAttribute<AuthorizeAttribute>()));
        Assert.All(hubs, h => Assert.EndsWith("Hub", h.Name));
    }

    private static bool IsHub(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t == typeof(Hub) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Hub<>)))
            {
                return true;
            }
        }

        return false;
    }

    // api/<kebab>(/<kebab>)* — plural is a naming rule for reviewers, not something a regex can judge.
    [GeneratedRegex("^api/[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$")]
    private static partial Regex KebabRoute();

    // Segments are kebab-case literals or {parameters}.
    [GeneratedRegex(@"^(([a-z0-9]+(-[a-z0-9]+)*|\{[a-zA-Z]+(:[a-z]+)?\})(/|$))+$")]
    private static partial Regex KebabSegments();
}
