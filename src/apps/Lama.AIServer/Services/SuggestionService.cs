using Lama.AIServer.Models;
using Lama.Contracts;
using Lama.Domain.Engine;

namespace Lama.AIServer.Services;

/// <summary>
/// Orchestre les suggestions de coups avec contrôle de concurrence.
/// Un <see cref="SemaphoreSlim"/> limite le nombre de calculs simultanés
/// pour éviter la saturation CPU lors de parties concurrentes.
/// </summary>
public sealed class SuggestionService
{
    private readonly MoveSuggestionEngine _engine;
    private readonly SemaphoreSlim _semaphore;
    private readonly string _language;
    private readonly ILogger<SuggestionService> _logger;

    public SuggestionService(
        MoveSuggestionEngine engine,
        IConfiguration config,
        ILogger<SuggestionService> logger)
    {
        _engine   = engine;
        _language = config["LAMA_AI_LANGUAGE"]
                 ?? Environment.GetEnvironmentVariable("LAMA_AI_LANGUAGE")
                 ?? "fr";

        var maxConcurrent = 3;
        var envMaxConcurrent = config.GetValue<int?>("LAMA_AI_MAX_CONCURRENT")
                            ?? (int.TryParse(
                                   Environment.GetEnvironmentVariable("LAMA_AI_MAX_CONCURRENT"),
                                   out var parsed) ? parsed : (int?)null);
        if (envMaxConcurrent.HasValue)
            maxConcurrent = envMaxConcurrent.Value;
        maxConcurrent = Math.Max(1, Math.Min(maxConcurrent, 20));
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        _logger    = logger;
    }

    /// <summary>
    /// Retourne les meilleures suggestions pour le rack et le plateau fournis.
    /// Retourne 503 si tous les slots de calcul sont occupés.
    /// </summary>
    public async Task<(bool Busy, SuggestResponse Response)> SuggestAsync(
        SuggestRequest request,
        CancellationToken ct)
    {
        if (!await _semaphore.WaitAsync(0, ct))
        {
            _logger.LogWarning("AIServer saturé ({Language}) — toutes les capacités de calcul sont occupées.", _language);
            return (Busy: true, new SuggestResponse([], "Service temporairement surchargé, réessayez.", _language));
        }

        try
        {
            var board       = BuildBoardState(request.PlacedTiles);
            var player      = new Player("ai-player", RackLetters: [.. request.Rack]);
            var gameState   = new GameState
            {
                Board                = board,
                Players              = [player],
                CurrentPlayerIndex   = 0,
                TurnNumber           = 1,
            };

            var topPerCategory = Math.Max(1, Math.Min(request.TopPerCategory, 10));
            var poolSize       = topPerCategory * 2 + 4;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, request.TimeoutSeconds)));

            IReadOnlyList<MoveSuggestion> pool;
            try
            {
                pool = await Task.Run(
                    () => _engine.SuggestTopMoves(
                        gameState, player, poolSize, MoveSuggestionStrategy.Balanced, cts.Token),
                    cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Suggestion annulée (timeout {Timeout}s).", request.TimeoutSeconds);
                pool = [];
            }

            var suggestions = Categorize(pool, topPerCategory);

            var message = suggestions.Count > 0
                ? $"{suggestions.Count} suggestion(s) trouvée(s)."
                : "Aucune suggestion disponible pour ce rack.";

            return (Busy: false, new SuggestResponse(suggestions, message, _language));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BoardState BuildBoardState(IReadOnlyList<PlacedTileDto> placedTiles)
    {
        var grid = new Tile?[15, 15];
        foreach (var t in placedTiles)
        {
            if (t.Row is >= 0 and < 15 && t.Col is >= 0 and < 15)
                grid[t.Row, t.Col] = new Tile(char.ToUpperInvariant(t.Letter), t.IsWildcard);
        }
        return new BoardState(grid);
    }

    private static List<SuggestionDto> Categorize(
        IReadOnlyList<MoveSuggestion> pool,
        int topPerCategory)
    {
        var byScore = pool
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Length)
            .Take(topPerCategory)
            .ToList();

        var byScoreKeys = new HashSet<string>(
            byScore.Select(s => s.Word), StringComparer.OrdinalIgnoreCase);

        var byLength = pool
            .OrderByDescending(s => s.Length)
            .ThenByDescending(s => s.Score)
            .Where(s => !byScoreKeys.Contains(s.Word))
            .Take(topPerCategory)
            .ToList();

        return byScore.Select(s => ToDto(s, "score"))
            .Concat(byLength.Select(s => ToDto(s, "length")))
            .ToList();
    }

    private static SuggestionDto ToDto(MoveSuggestion s, string category)
    {
        // Déduire position de départ et direction depuis Placements
        var positions  = s.Placements.Keys.ToList();
        var minRow     = positions.Min(p => p.Row);
        var minCol     = positions.Min(p => p.Column);
        var isHoriz    = positions.Select(p => p.Row).Distinct().Count() == 1;

        return new SuggestionDto(s.Word, s.Score, s.Length, minRow, minCol, isHoriz, category);
    }
}
