using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : jouer un coup (poser des lettres sur le plateau).
/// Sauvegarde la partie après chaque coup réussi.
/// </summary>
public sealed class PlayMoveUseCase
{
    private readonly CreateGameUseCase _createUseCase;

    /// <summary>Initialise le cas d'usage.</summary>
    public PlayMoveUseCase(CreateGameUseCase createUseCase)
    {
        _createUseCase = createUseCase;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public Task<PlayMoveResponse> ExecuteAsync(PlayMoveRequest request)
    {
        var session = _createUseCase.RequireSession(request.GameId);
        var engine  = session.Engine;

        EnsureCurrentPlayer(engine, request.GameId, request.PlayerId);

        var (isValid, errorMessage, moveScore) = engine.ValidateMove(request.Letters);
        if (!isValid)
            throw new GameException(errorMessage);

        var newState    = engine.PlayMove(request.Letters);
        var playerIndex = _createUseCase.GetPlayerIndex(request.GameId, request.PlayerId);
        var newRack     = newState.Players[playerIndex].Rack;

        // Persister après le coup
        _createUseCase.SaveGame(request.GameId, isFirstMove: false);

        return Task.FromResult(new PlayMoveResponse(
            Score:     moveScore,
            NewRack:   newRack,
            GameState: newState));
    }

    private void EnsureCurrentPlayer(IGameEngine engine, string gameId, string playerId)
    {
        var state       = engine.GetGameState();
        var playerIndex = _createUseCase.GetPlayerIndex(gameId, playerId);
        if (state.CurrentPlayerIndex != playerIndex)
            throw new GameException(
                "Ce n'est pas votre tour. " +
                $"C'est au tour de {state.Players[state.CurrentPlayerIndex].Name}.");
    }
}
