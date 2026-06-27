using System.Net;
using System.Text;
using System.Text.Json;
using Lama.Contracts;
using Lama.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lama.Server.UnitTests;

/// <summary>
/// Tests unitaires pour <see cref="HttpAISuggestionClient"/>.
/// Vérifie la gestion des réponses HTTP et la dégradation gracieuse.
/// </summary>
public sealed class HttpAISuggestionClientTests
{
    private static readonly BoardState EmptyBoard = new();
    private static readonly IReadOnlyList<char> SampleRack = ['L', 'A', 'M', 'A', 'S', 'E', 'R'];

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HttpAISuggestionClient BuildClient(HttpResponseMessage response)
    {
        var handler = new StubHandler(_ => response);
        var http    = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5203") };
        return new HttpAISuggestionClient(http, NullLogger<HttpAISuggestionClient>.Instance);
    }

    private static string SuggestResponseJson(IEnumerable<object> suggestions, string message = "ok") =>
        JsonSerializer.Serialize(new
        {
            suggestions,
            message,
            language = "fr"
        });

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_Returns_Suggestions_On_200()
    {
        var body = SuggestResponseJson(new[]
        {
            new { word = "LAMAS", score = 7, length = 5, startRow = 7, startCol = 7, isHorizontal = true,  category = "score"  },
            new { word = "REMAS", score = 5, length = 5, startRow = 7, startCol = 7, isHorizontal = false, category = "length" }
        });

        var sut = BuildClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

        var result = await sut.SuggestAsync(SampleRack, EmptyBoard, false, 2, 15, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Word.Should().Be("LAMAS");
        result[0].Category.Should().Be("score");
        result[1].Category.Should().Be("length");
    }

    [Fact]
    public async Task SuggestAsync_Returns_Empty_On_503()
    {
        var sut = BuildClient(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var result = await sut.SuggestAsync(SampleRack, EmptyBoard, false, 2, 15, CancellationToken.None);

        result.Should().BeEmpty(because: "503 indique AIServer surchargé → dégradation gracieuse");
    }

    [Fact]
    public async Task SuggestAsync_Returns_Empty_On_Network_Error()
    {
        var handler = new ThrowingHandler(new HttpRequestException("Connection refused"));
        var http    = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5203") };
        var sut     = new HttpAISuggestionClient(http, NullLogger<HttpAISuggestionClient>.Instance);

        var result = await sut.SuggestAsync(SampleRack, EmptyBoard, false, 2, 15, CancellationToken.None);

        result.Should().BeEmpty(because: "une erreur réseau ne doit pas lever d'exception");
    }

    [Fact]
    public async Task SuggestAsync_Returns_Empty_On_404()
    {
        var sut = BuildClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await sut.SuggestAsync(SampleRack, EmptyBoard, false, 2, 15, CancellationToken.None);

        result.Should().BeEmpty(because: "404 → pas de suggestions disponibles, pas d'exception");
    }

    [Fact]
    public async Task SuggestAsync_Returns_Empty_On_Empty_Suggestions_Array()
    {
        var body = SuggestResponseJson(Array.Empty<object>(), "Aucune suggestion.");
        var sut  = BuildClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });

        var result = await sut.SuggestAsync(SampleRack, EmptyBoard, false, 2, 15, CancellationToken.None);

        result.Should().BeEmpty();
    }
}

// ── Stubs HTTP ────────────────────────────────────────────────────────────────

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        => _factory = factory;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_factory(request));
}

internal sealed class ThrowingHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => throw _exception;
}
