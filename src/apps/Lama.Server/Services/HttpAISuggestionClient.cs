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
    private readonly LocalAISuggestionClient? _localFallback;

    public HttpAISuggestionClient(
        HttpClient http,
        ILogger<HttpAISuggestionClient> logger,
        LocalAISuggestionClient? localFallback = null)
    {
        _http   = http;
        _logger = logger;
        _localFallback = localFallback;
    }

    public async Task<IReadOnlyList<AISuggestion>> SuggestAsync(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        int timeoutSeconds,
        string languageCode,
        CancellationToken ct)
    {
        var placedTiles = ExtractPlacedTiles(board);
        var request = new
        {
            rack,
            placedTiles,
            isFirstMove,
            topPerCategory,
            timeoutSeconds,
            language = languageCode
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
                return TryLocalFallback(rack, board, isFirstMove, topPerCategory, languageCode, ct);
            }

            var body = await response.Content.ReadFromJsonAsync<AISuggestResponse>(cts.Token);
            if (body?.Suggestions is null)
                return TryLocalFallback(rack, board, isFirstMove, topPerCategory, languageCode, ct);

            // If the AI server used a different language than the game requires, its words may fail
            // validation. Fall back to the local engine which always respects the game language.
            if (!string.IsNullOrWhiteSpace(body.Language) && !LanguageMatches(body.Language, languageCode))
            {
                _logger.LogInformation(
                    "AIServer suggère en '{AiLang}' mais le jeu est en '{GameLang}' — fallback local.",
                    body.Language, languageCode);
                return TryLocalFallback(rack, board, isFirstMove, topPerCategory, languageCode, ct);
            }

            return body.Suggestions
                .Select(s => new AISuggestion(
                    s.Word, s.Score, s.Length,
                    s.StartRow, s.StartCol, s.IsHorizontal, s.Category))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "AIServer injoignable ou timeout lors de /suggest.");
            return TryLocalFallback(rack, board, isFirstMove, topPerCategory, languageCode, ct);
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

    private IReadOnlyList<AISuggestion> TryLocalFallback(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        string languageCode,
        CancellationToken ct)
    {
        if (_localFallback is null)
            return [];

        var local = _localFallback.Suggest(rack, board, isFirstMove, topPerCategory, languageCode, ct);
        if (local.Count > 0)
            _logger.LogInformation("Fallback local suggestions utilisé ({Count} résultat(s)).", local.Count);
        return local;
    }

    private static bool LanguageMatches(string aiLang, string gameLang)
    {
        var aiCodes   = ParseLanguageCodes(aiLang);
        var gameCodes = ParseLanguageCodes(gameLang);
        return aiCodes.Overlaps(gameCodes);
    }

    private static HashSet<string> ParseLanguageCodes(string lang) =>
        lang.ToLowerInvariant()
            .Split([',', ';', '+', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
