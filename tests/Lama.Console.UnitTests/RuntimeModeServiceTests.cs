using FluentAssertions;
using Lama.Console.Services;

namespace Lama.Console.UnitTests;

public sealed class RuntimeModeServiceTests : IDisposable
{
    private readonly string _tempDir;

    public RuntimeModeServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LamaRuntimeModeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", _tempDir);
        Environment.SetEnvironmentVariable("LAMA_RUNTIME_MODE", null);
        Environment.SetEnvironmentVariable("LAMA_SERVER_URL", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LAMA_SESSION_DIR", null);
        Environment.SetEnvironmentVariable("LAMA_RUNTIME_MODE", null);
        Environment.SetEnvironmentVariable("LAMA_SERVER_URL", null);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Mode_IsLocal_WhenNoEnvAndNoPersistedUrl()
    {
        var service = new RuntimeModeService();

        service.Mode.Should().Be(RuntimeExecutionMode.Local);
        service.ServerBaseUrl.Should().BeNull();
    }

    [Fact]
    public void Mode_IsOnline_WhenPersistedUrlExists()
    {
        RuntimeServerConfigStore.SaveServerUrl("http://127.0.0.1:5055");
        var service = new RuntimeModeService();

        service.Mode.Should().Be(RuntimeExecutionMode.Online);
        service.ServerBaseUrl.Should().Be("http://127.0.0.1:5055");
    }

    [Fact]
    public void EnvModeLocal_OverridesPersistedUrl()
    {
        RuntimeServerConfigStore.SaveServerUrl("http://127.0.0.1:5055");
        Environment.SetEnvironmentVariable("LAMA_RUNTIME_MODE", "local");

        var service = new RuntimeModeService();

        service.Mode.Should().Be(RuntimeExecutionMode.Local);
    }

    [Fact]
    public void ClearServerUrl_RestoresLocalMode_WhenNoEnvOverrides()
    {
        RuntimeServerConfigStore.SaveServerUrl("http://127.0.0.1:5055");
        RuntimeServerConfigStore.ClearServerUrl().Should().BeTrue();

        var service = new RuntimeModeService();

        service.Mode.Should().Be(RuntimeExecutionMode.Local);
        service.ServerBaseUrl.Should().BeNull();
    }
}

