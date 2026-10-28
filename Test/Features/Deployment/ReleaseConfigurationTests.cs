using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkFlow.API.Security;
using Xunit;

namespace Test.Features.Deployment;

public class ReleaseConfigurationTests
{
    private static IConfiguration Config(string? url) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Release:PublicApiUrl"] = url }).Build();
    private static IHostEnvironment Host(string name) => new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = name };
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://example.invalid/api")]
    [InlineData("https:example.invalid/api")]
    [InlineData("https://example.invalid")]
    [InlineData("https://user:synthetic@example.invalid/api")]
    [InlineData("https://example.invalid/api?token=synthetic")]
    [InlineData("https://example.invalid/api#fragment")]
    [InlineData("https://example.invalid\\api")]
    [InlineData("https://exam\nple.invalid/api")]
    public void ProductionEndpointFailsClosedWithoutEchoingConfiguration(string? url)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ReleaseConfiguration.Validate(Config(url), Host(Environments.Production)));
        Assert.Contains("Release:PublicApiUrl", error.Message); Assert.DoesNotContain("synthetic", error.Message);
    }
    [Fact]
    public void ProductionRequiresTheApiRootWhileExplicitDevelopmentRemainsAvailable()
    {
        Assert.Equal("https://api.example.invalid/api/", ReleaseConfiguration.Validate(Config("https://api.example.invalid/api/"), Host(Environments.Production))!.AbsoluteUri);
        Assert.Null(ReleaseConfiguration.Validate(Config(null), Host(Environments.Development)));
    }
    [Fact]
    public void ProductionSampleDataAndInsecureCorsAreRefused()
    {
        var config = Config("https://api.example.invalid/api"); config["Seeding:IncludeTestData"] = "true";
        Assert.Throws<InvalidOperationException>(() => ReleaseConfiguration.Validate(config, Host(Environments.Production)));
        config["Seeding:IncludeTestData"] = "false"; config["Cors:AllowedOrigins:0"] = "http://admin.example.invalid";
        Assert.Throws<InvalidOperationException>(() => ReleaseConfiguration.Validate(config, Host(Environments.Production)));
        config["Cors:AllowedOrigins:0"] = "https://admin.example.invalid";
        Assert.NotNull(ReleaseConfiguration.Validate(config, Host(Environments.Production)));
    }
    [Fact]
    public async Task UnknownProxyCannotMarkAnHttpRequestAsSecure()
    {
        var options = new ForwardedHeadersOptions(); ReleaseConfiguration.ConfigureProxy(options, Config(null));
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.12");
        context.Request.Scheme = "http"; context.Request.Headers["X-Forwarded-Proto"] = "https";
        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options)).Invoke(context);
        Assert.False(context.Request.IsHttps);
    }
    [Fact]
    public void ProxyConfigurationRequiresAnExplicitAddress()
    {
        var config = Config(null); config["Release:KnownProxies:0"] = "*";
        Assert.Throws<InvalidOperationException>(() => ReleaseConfiguration.ConfigureProxy(new(), config));
    }
}
