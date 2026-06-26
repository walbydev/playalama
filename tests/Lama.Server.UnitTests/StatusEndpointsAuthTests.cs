using Lama.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Lama.Server.UnitTests;

/// <summary>
/// Tests du mécanisme d'autorisation de StatusEndpoints via X-Admin-Secret.
/// On teste la logique interne via réflexion ou directement via l'endpoint isolé.
/// </summary>
public sealed class StatusEndpointsAuthTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DefaultHttpContext MakeContext(string? headerValue = null)
    {
        var ctx = new DefaultHttpContext();
        if (headerValue is not null)
            ctx.Request.Headers["X-Admin-Secret"] = new StringValues(headerValue);
        return ctx;
    }

    private static IConfiguration MakeConfig(string? secret)
    {
        var dict = new Dictionary<string, string?>();
        if (secret is not null)
            dict["LAMA_ADMIN_SECRET"] = secret;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // ── Tests via accès à la méthode IsAuthorized (accessible par internal reflection) ──

    [Fact]
    public void IsAuthorized_CorrectSecret_ReturnsTrue()
    {
        var ctx    = MakeContext("my_secret");
        var config = MakeConfig("my_secret");

        var result = InvokeIsAuthorized(ctx, config);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_WrongSecret_ReturnsFalse()
    {
        var ctx    = MakeContext("wrong_secret");
        var config = MakeConfig("my_secret");

        var result = InvokeIsAuthorized(ctx, config);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_MissingHeader_ReturnsFalse()
    {
        var ctx    = MakeContext(null);
        var config = MakeConfig("my_secret");

        var result = InvokeIsAuthorized(ctx, config);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_SecretNotConfigured_ReturnsFalse()
    {
        var ctx    = MakeContext("any_value");
        var config = MakeConfig(null); // aucun secret configuré

        var result = InvokeIsAuthorized(ctx, config);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_EmptyHeader_ReturnsFalse()
    {
        var ctx    = MakeContext("");
        var config = MakeConfig("my_secret");

        var result = InvokeIsAuthorized(ctx, config);

        result.Should().BeFalse();
    }

    // ── Accès à la méthode privée ─────────────────────────────────────────────

    private static bool InvokeIsAuthorized(HttpContext ctx, IConfiguration config)
    {
        var method = typeof(StatusEndpoints)
            .GetMethod("IsAuthorized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("Méthode IsAuthorized introuvable.");

        return (bool)method.Invoke(null, [ctx, config])!;
    }
}
