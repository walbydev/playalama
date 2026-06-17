using Lama.Contracts;
using Lama.Core.Models;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : un joueur rejoint une partie existante.
///
/// Responsabilités :
/// - Vérifier que la partie existe
/// - Valider le nom du joueur
/// - Vérifier que la partie accepte encore des joueurs
/// - Attribuer un PlayerId unique
/// - Distribuer un rack de 7 lettres
/// - Retourner le PlayerId, le rack et l'état mis à jour
/// </summary>
public sealed class JoinGameUseCase
{
    private readonly CreateGameUseCase _createUseCase;

    /// <summary>Initialise le cas d'usage.</summary>
    public JoinGameUseCase(CreateGameUseCase createUseCase)
    {
        _createUseCase = createUseCase;
    }

    /// <summary>Exécute le cas d'usage.</summary>
    public Task<JoinGameResponse> ExecuteAsync(JoinGameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
            throw new GameException("Le nom du joueur ne peut pas être vide.");

        var session = _createUseCase.RequireSession(request.GameId);
        var engine  = session.Engine;

        if (engine.GetGameState().IsGameOver)
            throw new GameException("Impossible de rejoindre une partie terminée.");

        // Attribuer un ID unique au joueur
        var playerId = Guid.NewGuid().ToString("N");

        // Enregistrer le joueur dans la session avec son index dans le moteur
        // Le moteur a déjà l'hôte à l'index 0 — les suivants s'ajoutent
        var state = engine.GetGameState();
        var playerIndex = state.Players.Count;

        // Réinitialiser le moteur avec tous les joueurs
        // (approche simple : reconstruire la liste de joueurs)
        var allPlayerNames = state.Players.Select(p => p.Name).ToList();
        allPlayerNames.Add(request.PlayerName);
        engine.InitializeGame(allPlayerNames);

        // Enregistrer l'index du nouveau joueur
        _createUseCase.AddPlayer(request.GameId, playerId);

        var newState   = engine.GetGameState();
        var playerRack = newState.Players[playerIndex].Rack;

        return Task.FromResult(new JoinGameResponse(
            PlayerId:  playerId,
            Rack:      playerRack,
            GameState: newState));
    }
}
