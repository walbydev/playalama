using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : terminer la partie.
/// Marque la partie comme terminée, calcule le classement final
/// et supprime la partie du repository.
/// </summary>
public sealed class EndGameUseCase
{
    private readonly CreateGameUseCase _createUseCase;

    /// <summary>Initialise le cas d'usage.</summary>
    public EndGameUseCase(CreateGameUseCase createUseCase)
    {
        _createUseCase = createUseCase;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public Task<EndGameResponse> ExecuteAsync(EndGameRequest request)
    {
        var session = _createUseCase.RequireSession(request.GameId);
        var engine  = session.Engine;

        engine.EndGame();
        var finalState = engine.GetGameState();

        var scores = finalState.Players
            .OrderByDescending(p => p.Score)
            .Select(p => (p.Name, p.Score))
            .ToList()
            .AsReadOnly();

        string? winner = null;
        if (scores.Count > 0)
        {
            var topScore   = scores[0].Score;
            var topPlayers = scores.Where(s => s.Score == topScore).ToList();
            if (topPlayers.Count == 1)
                winner = topPlayers[0].Name;
        }

        // Supprimer la partie du repository — elle est terminée
        _createUseCase.DeleteGame(request.GameId);

        return Task.FromResult(new EndGameResponse(
            FinalState: finalState,
            Winner:     winner,
            Scores:     scores));
    }
}
