using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : échanger des lettres avec le sac.
/// Vérifie le tour du joueur, délègue au moteur,
/// retourne le nouveau rack et l'état.
/// </summary>
public sealed class SwapLettersUseCase
{
    private readonly CreateGameUseCase _createUseCase;

    /// <summary>Initialise le cas d'usage.</summary>
    public SwapLettersUseCase(CreateGameUseCase createUseCase)
    {
        _createUseCase = createUseCase;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public Task<SwapLettersResponse> ExecuteAsync(SwapLettersRequest request)
    {
        var session = _createUseCase.RequireSession(request.GameId);
        var engine  = session.Engine;

        // Vérifier que c'est le tour du bon joueur
        var state       = engine.GetGameState();
        var playerIndex = _createUseCase.GetPlayerIndex(request.GameId, request.PlayerId);

        if (state.CurrentPlayerIndex != playerIndex)
            throw new GameException(
                "Ce n'est pas votre tour. " +
                $"C'est au tour de {state.Players[state.CurrentPlayerIndex].Name}.");

        var lettersToSwap = request.SwapAll
            ? new List<char>(state.Players[playerIndex].Rack)
            : (request.Letters ?? [])
                .Select(char.ToUpperInvariant)
                .ToList();

        if (lettersToSwap.Count == 0)
            throw new GameException("Aucune lettre a echanger.");

        engine.SwapLetters(lettersToSwap);
        var newState = engine.GetGameState();
        _createUseCase.SaveGame(request.GameId, isFirstMove: state.TurnNumber == 1 && !state.IsGameOver);

        var newRack = newState.Players[playerIndex].Rack;

        return Task.FromResult(new SwapLettersResponse(
            NewRack:   newRack,
            GameState: newState));
    }
}
