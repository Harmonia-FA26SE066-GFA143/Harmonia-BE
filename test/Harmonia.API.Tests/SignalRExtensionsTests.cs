using Harmonia.API.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Harmonia.API.Tests;

public class SignalRExtensionsTests
{
    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }

    private static IConfiguration Configuration(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Azure:SignalR:ConnectionString"] = connectionString })
            .Build();

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void AddApiSignalR_NoConnectionStringOutsideDevelopment_ThrowsNamingTheKey(string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddApiSignalR(Configuration(null), Environment(environmentName)));

        Assert.Contains("Azure__SignalR__ConnectionString", ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void AddApiSignalR_NoConnectionStringInDevelopmentOrTesting_RunsInProcess(string environmentName)
    {
        var ex = Record.Exception(() => new ServiceCollection().AddApiSignalR(Configuration(""), Environment(environmentName)));

        Assert.Null(ex);
    }

    [Fact]
    public void AddApiSignalR_WithConnectionString_DoesNotThrowInProduction()
    {
        var ex = Record.Exception(() => new ServiceCollection().AddApiSignalR(
            Configuration("Endpoint=https://test.service.signalr.net;AccessKey=dGVzdA==;Version=1.0;"),
            Environment("Production")));

        Assert.Null(ex);
    }
}
