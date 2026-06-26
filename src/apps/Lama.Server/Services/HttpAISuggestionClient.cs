using System.Net.Http.Json;
using Lama.Contracts;

namespace Lama.Server.Services;

/// <summary>
/// Implémentation HTTP du client IA : appelle POST /suggest sur Lama.AIServer.
/// En cas d'erreur réseau ou de réponse invalide, retourne une liste vide (dégradation gracieuse).
/// </summary>
public sealed class HttpAISuggestionClient : IAISuggestionClient
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpAISuggestionClient> _logger;

    public HttpAISuggestionClient(
        HttpClient http,
        ILogger<HttpAISuggestionClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AISuggestion>> SuggestAsync(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        int timeoutSeconds,
        CancellationToken ct)
    {
        var placedTiles = ExtractPlacedTiles(board);
        var request = new
        {
            rack,
            placedTiles,
            isFirstMove,
            topPerCategory,
            timeoutSeconds
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 3)); // marge réseau

        try
        {
            var response = await _http.PostAsJsonAsync("/suggest", request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AIServer a retourné {Status} pour /suggest.", response.StatusCode);
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<AISuggestResponse>(cts.Token);
            if (body?.Suggestions is null) return [];

            return body.Suggestions
                .Select(s => new AISuggestion(
                    s.Word, s.Score, s.Length,
                    s.StartRow, s.StartCol, s.IsHorizontal, s.Category))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "AIServer injoignable ou timeout lors de /suggest.");
            return [];
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<object> ExtractPlacedTiles(BoardState board)
    {
        var tiles = new List<object>();
        for (var r = 0; r < 15; r++)
        for (var c = 0; c < 15; c++)
        {
            var tile = board.Grid[r, c];
            if (tile is not null)
                tiles.Add(new { row = r, col = c, letter = tile.Letter, isWildcard = tile.IsWildcard });
        }
        return tiles;
    }

    // ── DTO interne pour la désérialisation ───────────────────────────────────

    private record AISuggestResponse(
        IReadOnlyList<AISuggestionDto> Suggestions,
        string Message,
        string Language);

    private record AISuggestionDto(
        string Word,
        int Score,
        int Length,
        int StartRow,
        int StartCol,
        bool IsHorizontal,
        string Category);
}
