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
}

