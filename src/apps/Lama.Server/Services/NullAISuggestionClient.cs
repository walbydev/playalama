using Lama.Contracts;

namespace Lama.Server.Services;

/// <summary>
/// Implémentation nulle du client IA.
/// Utilisée lorsque <c>LAMA_AI_SERVER_URL</c> n'est pas configuré.
/// Retourne toujours une liste vide sans erreur.
/// </summary>
public sealed class NullAISuggestionClient : IAISuggestionClient
{
    private readonly ILogger<NullAISuggestionClient> _logger;
    private readonly LocalAISuggestionClient? _localFallback;

    public NullAISuggestionClient(
        ILogger<NullAISuggestionClient> logger,
        LocalAISuggestionClient? localFallback = null)
    {
        _logger = logger;
        _localFallback = localFallback;
    }

    public Task<IReadOnlyList<AISuggestion>> SuggestAsync(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        int timeoutSeconds,
        string languageCode,
        CancellationToken ct)
    {
        var local = _localFallback?.Suggest(rack, board, isFirstMove, topPerCategory, languageCode, ct) ?? [];
        if (local.Count == 0)
            _logger.LogDebug("NullAISuggestionClient : LAMA_AI_SERVER_URL non configuré et fallback local vide.");
        return Task.FromResult<IReadOnlyList<AISuggestion>>(local);
    }
}
