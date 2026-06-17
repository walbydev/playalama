using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : passer son tour.
/// Vérifie que c'est bien le tour du joueur demandeur,
/// délègue au moteur et sauvegarde.
/// </summary>
public sealed class PassTurnUseCase
{
    private readonly CreateGameUseCase _createUseCase;

    /// <summary>Initialise le cas d'usage.</summary>
    public PassTurnUseCase(CreateGameUseCase createUseCase)
    {
        _createUseCase = createUseCase;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public Task<GameState> ExecuteAsync(PassTurnRequest request)
    {
        var session     = _createUseCase.RequireSession(request.GameId);
        var engine      = session.Engine;
        var state       = engine.GetGameState();
        var playerIndex = _createUseCase.GetPlayerIndex(request.GameId, request.PlayerId);

        if (state.CurrentPlayerIndex != playerIndex)
            throw new GameException(
                "Ce n'est pas votre tour. " +
                $"C'est au tour de {state.Players[state.CurrentPlayerIndex].Name}.");

        engine.PassTurn();

        // Persister
        _createUseCase.SaveGame(request.GameId, isFirstMove: state.IsGameOver is false && state.TurnNumber == 1);

        return Task.FromResult(engine.GetGameState());
    }
}
