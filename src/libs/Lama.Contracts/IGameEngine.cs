namespace Lama.Contracts;

/// <summary>
/// Moteur du jeu Scrabble - Contient toute la logique métier.
/// Zéro dépendance sur UI ou communication.
/// </summary>
public interface IGameEngine
{
    /// <summary>
    /// Obtient l'état actuel du jeu.
    /// </summary>
    GameState GetGameState();

    /// <summary>
    /// Initialise une nouvelle partie.
    /// </summary>
    void InitializeGame(List<string> playerNames);

    /// <summary>
    /// Joue un coup (placement de lettres).
    /// Retourne le nouvel état du jeu.
    /// Lance GameException si le coup est invalide.
    /// </summary>
    GameState PlayMove(Dictionary<Position, char> letters);

    /// <summary>
    /// Valide un coup sans le jouer.
    /// Retourne les points si valide, null si invalide.
    /// </summary>
    (bool IsValid, string ErrorMessage, int Score) ValidateMove(Dictionary<Position, char> letters);

    /// <summary>
    /// Passe le tour au joueur suivant.
    /// </summary>
    void PassTurn();

    /// <summary>
    /// Echange des lettres du rack du joueur courant avec le sac.
    /// Consomme le tour en cas de succes.
    /// </summary>
    /// <param name="lettersToSwap">Lettres a echanger.</param>
    void SwapLetters(IReadOnlyList<char> lettersToSwap);

    /// <summary>
    /// Conteste le dernier mot joué.
    /// Retourne l'état résultant après résolution du challenge.
    /// </summary>
    ChallengeResult ChallengeLastMove();

    /// <summary>
    /// Obtient le joueur actuel.
    /// </summary>
    Player GetCurrentPlayer();

    /// <summary>
    /// Crée les lettres initiales pour les joueurs.
    /// </summary>
    List<char> CreatePlayerRack(int size = 7);

    /// <summary>
    /// Retourne le nombre de tuiles restantes dans le sac.
    /// </summary>
    int GetBagCount();

    /// <summary>
    /// Termine la partie actuelle.
    /// </summary>
    void EndGame();
}

/// <summary>
/// Exception métier du jeu Scrabble.
/// </summary>
public class GameException : Exception
{
    public GameException(string message) : base(message) { }
}

