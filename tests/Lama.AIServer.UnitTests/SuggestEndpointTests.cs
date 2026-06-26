using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Lama.AIServer.Models;
using Lama.AIServer.Services;
using Lama.Domain.Engine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lama.AIServer.UnitTests;

/// <summary>
/// Tests d'intégration HTTP pour les endpoints du AIServer.
/// Utilise <see cref="WebApplicationFactory{TEntryPoint}"/> avec un moteur de suggestion
/// minimal (petit dictionnaire) pour éviter le chargement du dictionnaire français complet.
/// </summary>
[Collection("AIServerEndpoints")]
public sealed class SuggestEndpointTests : IClassFixture<AiServerFactory>
{
    private readonly HttpClient _client;

    public SuggestEndpointTests(AiServerFactory factory)
        => _client = factory.CreateClient();

    // ── /health ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_Health_Returns_StatusHealthy()
    {
        var response = await _client.GetAsync("/health");
        var json     = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        json.GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task GET_Health_Returns_ServiceName()
    {
        var response = await _client.GetAsync("/health");
        var json     = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        json.GetProperty("service").GetString().Should().Be("Lama.AIServer");
    }

    [Fact]
    public async Task GET_Health_Returns_Language()
    {
        var response = await _client.GetAsync("/health");
        var json     = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        json.GetProperty("language").GetString().Should().NotBeNullOrEmpty();
    }

    // ── POST /suggest ─────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_Suggest_Returns200_WithValidRequest()
    {
        var request = new SuggestRequest(
            Rack: ['L', 'A', 'M', 'A', 'S', 'E', 'R'],
            PlacedTiles: [],
            IsFirstMove: true,
            TopPerCategory: 2,
            TimeoutSeconds: 15);

        var response = await _client.PostAsJsonAsync("/suggest", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_Suggest_Response_HasValidShape()
    {
        var request = new SuggestRequest(
            Rack: ['L', 'A', 'M', 'A', 'S', 'E', 'R'],
            PlacedTiles: [],
            IsFirstMove: true,
            TopPerCategory: 2,
            TimeoutSeconds: 15);

        var json = JsonDocument.Parse(
            await (await _client.PostAsJsonAsync("/suggest", request))
                .Content.ReadAsStringAsync()).RootElement;

        json.TryGetProperty("suggestions", out _).Should().BeTrue();
        json.TryGetProperty("message",     out _).Should().BeTrue();
        json.TryGetProperty("language",    out _).Should().BeTrue();
    }

    [Fact]
    public async Task POST_Suggest_EmptyRack_Returns200_WithEmptySuggestions()
    {
        var request = new SuggestRequest(
            Rack: [],
            PlacedTiles: [],
            IsFirstMove: true,
            TopPerCategory: 2,
            TimeoutSeconds: 15);

        var response = await _client.PostAsJsonAsync("/suggest", request);
        var json     = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("suggestions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task POST_Suggest_WithPlacedTiles_Returns200()
    {
        var request = new SuggestRequest(
            Rack: ['L', 'A', 'M', 'S', 'E', 'R'],
            PlacedTiles: [new PlacedTileDto(7, 7, 'A')],
            IsFirstMove: false,
            TopPerCategory: 2,
            TimeoutSeconds: 10);

        var response = await _client.PostAsJsonAsync("/suggest", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>
/// Test isolé du comportement 503 — crée sa propre factory avec maxConcurrent=1.
/// Ne partage pas la fixture pour éviter la contrainte xUnit "un seul constructeur public".
/// </summary>
public sealed class SuggestBusyEndpointTests
{
    [Fact]
    public async Task POST_Suggest_Returns503_WhenServiceBusy()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                AiServerFactory.ConfigureForTest(builder, maxConcurrent: 1));

        var client    = factory.CreateClient();
        var svc       = factory.Services.GetRequiredService<SuggestionService>();
        var semaphore = (SemaphoreSlim)typeof(SuggestionService)
            .GetField("_semaphore", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(svc)!;

        await semaphore.WaitAsync();
        try
        {
            var request = new SuggestRequest(
                Rack: ['L', 'A', 'M', 'A'],
                PlacedTiles: [],
                IsFirstMove: true,
                TopPerCategory: 2,
                TimeoutSeconds: 5);

            var response = await client.PostAsJsonAsync("/suggest", request);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
                because: "le sémaphore est saturé → le service répond 503");
        }
        finally
        {
            semaphore.Release();
        }
    }
}

// ── Factory ────────────────────────────────────────────────────────────────────

/// <summary>
/// Factory partagée pour les tests d'intégration AIServer.
/// Remplace <see cref="MoveSuggestionEngine"/> par un moteur léger (petit dictionnaire).
/// Doit avoir UN SEUL constructeur public (contrainte xUnit IClassFixture).
/// </summary>
public sealed class AiServerFactory : WebApplicationFactory<Program>
{
    private static readonly IReadOnlySet<string> TestDict = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "LA", "LAMA", "AMAS", "SERA", "REMS", "LAME", "ARMES", "RAMES", "SALE", "SEAM"
    };

    private static readonly IReadOnlyDictionary<char, int> TestScores =
        new Dictionary<char, int>
        {
            ['A'] = 1, ['E'] = 1, ['L'] = 1, ['M'] = 3, ['R'] = 1, ['S'] = 1
        };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => ConfigureForTest(builder, maxConcurrent: 3);

    /// <summary>
    /// Configure le host de test avec un moteur de suggestion léger.
    /// Exposé en interne pour <see cref="SuggestBusyEndpointTests"/> qui a besoin d'un maxConcurrent différent.
    /// </summary>
    internal static void ConfigureForTest(IWebHostBuilder builder, int maxConcurrent)
    {
        builder.UseSetting("LAMA_AI_MAX_CONCURRENT", maxConcurrent.ToString());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<MoveSuggestionEngine>();
            services.AddSingleton(new MoveSuggestionEngine(TestDict, TestScores));
            services.RemoveAll<SuggestionService>();
            services.AddSingleton<SuggestionService>();
        });
    }
}
