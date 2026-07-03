namespace Lama.Contracts;

/// <summary>
/// Représente une position sur le plateau (15x15).
/// </summary>
public record Position(int Row, int Column)
{
    public bool IsValid => Row >= 0 && Row < 15 && Column >= 0 && Column < 15;
}

/// <summary>
/// Représente une lettre placée sur le plateau.
/// </summary>
public record Tile(char Letter, bool IsWildcard = false);

/// <summary>
/// Représente un coup joué: placement de lettres sur le plateau.
/// </summary>
public record Move(Dictionary<Position, char> Letters, int Score = 0);

/// <summary>
/// Représente la pose d'une lettre à une position précise.
/// </summary>
public record MovePlacement(int Row, int Column, char Letter);

/// <summary>
/// Représente un coup historisé pour l'affichage et le challenge.
/// </summary>
public record GameMove(
    int TurnNumber,
    string PlayerId,
    string PlayerName,
    IReadOnlyList<MovePlacement> Placements,
    int Score,
    DateTimeOffset PlayedAt);

/// <summary>
/// Snapshot complet d'une partie utilisé pour restaurer un état précédent.
/// </summary>
/// <param name="IsFirstMove">Indique si aucun coup n'a encore été joué.</param>
/// <param name="IsGameOver">Indique si la partie est terminée.</param>
/// <param name="CurrentPlayerIndex">Index du joueur dont c'est le tour.</param>
/// <param name="TurnNumber">Numéro du tour courant.</param>
/// <param name="Players">Liste des joueurs persistés.</param>
/// <param name="Board">Tuiles du plateau.</param>
/// <param name="RemainingTiles">Lettres restantes dans le sac.</param>
/// <param name="TimePerPlayerSeconds">Temps alloué par joueur en mode Blitz (null sinon).</param>
/// <param name="PlayerTimeUsed">Temps consommé par chaque joueur en secondes.</param>
/// <param name="TurnStartAt">Date/heure de début du tour courant (pour calculer le temps écoulé).</param>
/// <param name="ForfeitedPlayerIndex">Index du joueur qui a perdu par timeout en Blitz (null si aucun).</param>
public record GameStateSnapshot(
    bool IsFirstMove,
    bool IsGameOver,
    int CurrentPlayerIndex,
    int TurnNumber,
    List<PersistedPlayer> Players,
    List<PersistedTile> Board,
    List<char> RemainingTiles,
    int? TimePerPlayerSeconds = null,
    List<int>? PlayerTimeUsed = null,
    DateTimeOffset? TurnStartAt = null,
    int? ForfeitedPlayerIndex = null);

/// <summary>
/// Résultat d'un challenge.
/// </summary>
public record ChallengeResult(
    bool ChallengeSucceeded,
    string Message,
    GameMove? ChallengedMove,
    GameState GameState);

/// <summary>
/// Représente l'état immutable du plateau de jeu.
/// </summary>
public class BoardState
{
    public Tile?[,] Grid { get; }
    
    public BoardState()
    {
        Grid = new Tile[15, 15];
    }
    
    public BoardState(Tile?[,] grid)
    {
        Grid = (Tile?[,])grid.Clone();
    }
}

/// <summary>
/// Représente un joueur avec son score et son rack.
/// </summary>
public record Player(string Name, int Score = 0, List<char>? RackLetters = null)
{
    public List<char> Rack { get; } = RackLetters ?? new List<char>();
}

/// <summary>
/// Représente l'état complet du jeu.
/// Immuable pour support futur replay/undo.
/// </summary>
public record GameState
{
    public required BoardState Board { get; init; }
    public required List<Player> Players { get; init; }
    public required int CurrentPlayerIndex { get; init; }
    public required int TurnNumber { get; init; }
    public bool IsGameOver { get; init; } = false;
    public List<GameMove> History { get; init; } = [];
    /// <summary>Temps alloué par joueur en secondes en mode Blitz (null dans les autres modes).</summary>
    public int? TimePerPlayerSeconds { get; init; }
    /// <summary>Temps consommé par chaque joueur en secondes (compteur dans tous les modes).</summary>
    public List<int> PlayerTimeUsed { get; init; } = [];
    /// <summary>Date/heure de début du tour courant (pour calculer le temps écoulé entre deux coups).</summary>
    public DateTimeOffset? TurnStartAt { get; init; }
    /// <summary>Index du joueur qui a perdu par timeout en Blitz (null si aucun).</summary>
    public int? ForfeitedPlayerIndex { get; init; }
}

