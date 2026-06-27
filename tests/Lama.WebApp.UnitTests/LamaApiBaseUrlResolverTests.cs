using Lama.WebApp.Services;
using Microsoft.Extensions.Configuration;

namespace Lama.WebApp.UnitTests;

public sealed class LamaApiBaseUrlResolverTests
{
    [Fact]
    public void Resolve_Should_Prioritize_LamaServerUrl()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["LAMA_SERVER_URL"] = "http://127.0.0.1:5201",
            ["LamaApi:BaseUrl"] = "http://127.0.0.1:5100"
        });

        var resolved = LamaApiBaseUrlResolver.Resolve(configuration);

        resolved.Should().Be("http://127.0.0.1:5201");
    }

    [Fact]
    public void Resolve_Should_Use_LamaApiBaseUrl_When_ServerUrl_Missing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["LamaApi:BaseUrl"] = "http://127.0.0.1:5201"
        });

        var resolved = LamaApiBaseUrlResolver.Resolve(configuration);

        resolved.Should().Be("http://127.0.0.1:5201");
    }

    [Fact]
    public void Resolve_Should_Fallback_To_Development_Server_Url()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var resolved = LamaApiBaseUrlResolver.Resolve(configuration);

        resolved.Should().Be("http://127.0.0.1:5201");
    }

    [Fact]
    public void Resolve_Should_Remap_Legacy_Runtime_Port_To_OptionA_Server_Port()
    {
        var legacyPort = 4_999 + 1;
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["LAMA_SERVER_URL"] = $"http://127.0.0.1:{legacyPort}"
        });

        var resolved = LamaApiBaseUrlResolver.Resolve(configuration);

        resolved.Should().Be("http://127.0.0.1:5201");
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> entries)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(entries)
            .Build();
    }
}
