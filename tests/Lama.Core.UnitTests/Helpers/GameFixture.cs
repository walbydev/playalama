using Lama.Contracts;
using Lama.Core.Models;
using Lama.Core.UseCases;

namespace Lama.Core.UnitTests.Helpers;

/// <summary>
/// Fixture partagée pour les tests Core.
/// Fournit un moteur de jeu préconfiguré avec Alice et Bob,
/// avec des racks déterministes pour les tests.
/// </summary>
public static class GameFixture
{
    public static readonly IReadOnlySet<string> Dictionary =
        new HashSet<string>
        {
            "LA", "LAMA", "MA", "MOT", "MOTS", "AS", "SA",
            "AMI", "MAS", "AI", "LI", "RI", "MI", "SI"
        };

    public static readonly IReadOnlyDictionary<char, int> LetterScores =
        new Dictionary<char, int>
        {
            ['A'] = 1, ['L'] = 1, ['M'] = 2, ['O'] = 1, ['T'] = 1,
            ['I'] = 1, ['S'] = 1, ['N'] = 1, ['R'] = 1, ['*'] = 0
        };

    public static readonly IReadOnlyDictionary<char, int> Distribution =
        new Dictionary<char, int>
        {
            ['A'] = 9, ['L'] = 5, ['M'] = 3, ['O'] = 6,
            ['T'] = 6, ['I'] = 8, ['S'] = 6, ['N'] = 6,
            ['R'] = 6, ['*'] = 2
        };

    /// <summary>
    /// Crée une partie avec Alice (hôte) et Bob, déjà prête à jouer.
    /// Retourne le GameId, l'ID d'Alice et l'ID de Bob.
    /// </summary>
    public static async Task<(string GameId, string AliceId, string BobId,
        CreateGameUseCase CreateUc, JoinGameUseCase JoinUc,
        PlayMoveUseCase PlayUc, PassTurnUseCase PassUc,
        SwapLettersUseCase SwapUc, EndGameUseCase EndUc)>
        CreateReadyGame()
    {
        var createUc = new CreateGameUseCase(Dictionary, LetterScores, Distribution,
            new InMemoryGameRepository());
        var joinUc   = new JoinGameUseCase(createUc);
        var playUc   = new PlayMoveUseCase(createUc);
        var passUc   = new PassTurnUseCase(createUc);
        var swapUc   = new SwapLettersUseCase(createUc);
        var endUc    = new EndGameUseCase(createUc);

        var game  = await createUc.ExecuteAsync(new CreateGameRequest("Alice"));
        var bob   = await joinUc.ExecuteAsync(new JoinGameRequest(game.GameId, "Bob"));

        return (game.GameId, game.HostPlayerId, bob.PlayerId,
                createUc, joinUc, playUc, passUc, swapUc, endUc);
    }
}
