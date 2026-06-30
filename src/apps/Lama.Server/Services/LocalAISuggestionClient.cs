using System.Collections.Concurrent;
using Lama.Contracts;
using Lama.Domain.Engine;

namespace Lama.Server.Services;

/// <summary>
/// Fallback local des suggestions côté serveur, sans dépendre de Lama.AIServer.
/// </summary>
public sealed class LocalAISuggestionClient(ILanguageProviderRegistry registry, ILogger<LocalAISuggestionClient> logger)
{
    private readonly ConcurrentDictionary<string, MoveSuggestionEngine> _engines = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AISuggestion> Suggest(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        string languageCode,
        CancellationToken ct)
    {
        var provider = ResolveProvider(languageCode);
        if (provider is null)
            return [];

        var key = BuildLanguageCacheKey(languageCode);
        var engine = _engines.GetOrAdd(key, _ =>
            new MoveSuggestionEngine(provider.GetDictionary(), provider.GetLetterScores()));

        var player = new Player("server-fallback", RackLetters: [.. rack]);
        var state = new GameState
        {
            Board = board,
            Players = [player],
            CurrentPlayerIndex = 0,
            TurnNumber = 1
        };

        var safeTop = Math.Clamp(topPerCategory, 1, 10);
        var poolSize = safeTop * 2 + 4;
        var pool = engine.SuggestTopMoves(state, player, poolSize, MoveSuggestionStrategy.Balanced, ct);
        return Categorize(pool, safeTop);
    }

    private IGameLanguageProvider? ResolveProvider(string languageCode)
    {
        try
        {
            var codes = languageCode
                .ToLowerInvariant()
                .Split([',', ';', '+', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (codes.Count == 0)
                codes.Add("fr");

            return codes.Count == 1 ? registry.GetProvider(codes[0]) : registry.GetProvider(codes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallback local suggestions indisponible pour languageCode={LanguageCode}", languageCode);
            return null;
        }
    }

    private static string BuildLanguageCacheKey(string languageCode)
    {
        var normalized = languageCode
            .ToLowerInvariant()
            .Split([',', ';', '+', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        return string.Join("+", normalized);
    }

    private static List<AISuggestion> Categorize(IReadOnlyList<MoveSuggestion> pool, int topPerCategory)
    {
        var byScore = pool
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Length)
            .Take(topPerCategory)
            .ToList();

        var byScoreKeys = new HashSet<string>(byScore.Select(BuildKey), StringComparer.Ordinal);

        var byLength = pool
            .OrderByDescending(s => s.Length)
            .ThenByDescending(s => s.Score)
            .Where(s => !byScoreKeys.Contains(BuildKey(s)))
            .Take(topPerCategory)
            .ToList();

        return byScore.Select(s => ToDto(s, "score"))
            .Concat(byLength.Select(s => ToDto(s, "length")))
            .ToList();
    }

    private static string BuildKey(MoveSuggestion suggestion)
    {
        var coords = suggestion.Placements
            .OrderBy(kv => kv.Key.Row)
            .ThenBy(kv => kv.Key.Column)
            .Select(kv => $"{kv.Key.Row}:{kv.Key.Column}:{kv.Value}");
        return $"{suggestion.Word}|{string.Join(";", coords)}";
    }

    private static AISuggestion ToDto(MoveSuggestion s, string category)
    {
        var positions = s.Placements.Keys.ToList();
        var minRow = positions.Min(p => p.Row);
        var minCol = positions.Min(p => p.Column);
        var isHoriz = positions.Select(p => p.Row).Distinct().Count() == 1;
        return new AISuggestion(s.Word, s.Score, s.Length, minRow, minCol, isHoriz, category);
    }
}

