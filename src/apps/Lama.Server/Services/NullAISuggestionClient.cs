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

    public NullAISuggestionClient(ILogger<NullAISuggestionClient> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<AISuggestion>> SuggestAsync(
        IReadOnlyList<char> rack,
        BoardState board,
        bool isFirstMove,
        int topPerCategory,
        int timeoutSeconds,
        CancellationToken ct)
    {
        _logger.LogDebug(
            "NullAISuggestionClient : LAMA_AI_SERVER_URL non configuré, suggestions désactivées.");
        return Task.FromResult<IReadOnlyList<AISuggestion>>([]);
    }
}
