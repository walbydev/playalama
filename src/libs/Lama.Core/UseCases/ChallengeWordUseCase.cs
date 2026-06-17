using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : contester le dernier mot joué.
/// Le moteur décide si le challenge réussit, et l'appelant persiste ensuite l'état.
/// </summary>
public sealed class ChallengeWordUseCase
{
    private readonly CreateGameUseCase _createUseCase;

    /// <summary>Initialise le cas d'usage.</summary>
    public ChallengeWordUseCase(CreateGameUseCase createUseCase)
    {
        _createUseCase = createUseCase;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public Task<ChallengeResult> ExecuteAsync(ChallengeWordRequest request)
    {
        var session = _createUseCase.RequireSession(request.GameId);
        var engine = session.Engine;

        var state = engine.GetGameState();
        var playerIndex = _createUseCase.GetPlayerIndex(request.GameId, request.PlayerId);
        if (state.CurrentPlayerIndex != playerIndex)
            throw new GameException(
                "Ce n'est pas votre tour. " +
                $"C'est au tour de {state.Players[state.CurrentPlayerIndex].Name}.");

        return Task.FromResult(engine.ChallengeLastMove());
    }
}

