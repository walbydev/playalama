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

        // Construire le move d'échange : swap = pass + redistribution
        // Le moteur n'a pas de méthode SwapLetters dédiée dans IGameEngine —
        // on implémente via ReturnTiles + Draw directement sur TileBag n'est
        // pas accessible depuis Core. On passe le tour pour l'instant.
        // TODO: ajouter SwapLetters à IGameEngine dans la prochaine itération.
        engine.PassTurn();
        var newState = engine.GetGameState();

        // Retourner le rack inchangé pour l'instant (le swap complet sera fait dans IGameEngine)
        var newRack = newState.Players[playerIndex].Rack;

        return Task.FromResult(new SwapLettersResponse(
            NewRack:   newRack,
            GameState: newState));
    }
}
