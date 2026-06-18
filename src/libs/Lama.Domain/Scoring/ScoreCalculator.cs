using Lama.Contracts;
using Lama.Domain.Board;

namespace Lama.Domain.Scoring;

/// <summary>
/// Calcule le score d'un coup selon les règles officielles Scrabble.
///
/// Règles appliquées :
/// 1. Pour chaque lettre nouvellement posée, appliquer le multiplicateur de lettre.
/// 2. Sommer tous les points du mot (lettres posées + lettres existantes adjacentes).
/// 3. Appliquer le(s) multiplicateur(s) de mot (uniquement pour les cases nouvellement posées).
/// 4. Si 7 lettres posées en un seul coup → +50 points (bonus Scrabble).
///
/// Note : les bonus (DL, TL, DW, TW) ne s'appliquent que sur les cases nouvellement occupées
/// par ce coup. Une case déjà couverte par une tuile existante n'active pas son bonus.
/// </summary>
public sealed class ScoreCalculator
{
    private const int ScrabbleBonus      = 50;
    private const int ScrabbleBonusTiles = 7;

    private readonly IReadOnlyDictionary<char, int> _letterScores;

    /// <summary>
    /// Initialise le calculateur avec les valeurs de lettres de la langue choisie.
    /// </summary>
    public ScoreCalculator(IReadOnlyDictionary<char, int> letterScores)
    {
        _letterScores = letterScores;
    }

    /// <summary>
    /// Calcule le score total d'un coup.
    /// </summary>
    /// <param name="placements">Les lettres nouvellement posées (Position → lettre).</param>
    /// <param name="board">L'état du plateau AVANT ce coup.</param>
    /// <returns>Le score total du coup, incluant le bonus Scrabble si applicable.</returns>
    public int Calculate(IReadOnlyDictionary<Position, char> placements, BoardState board)
        => Calculate(placements, board, wildcardPositions: null);

    /// <summary>
    /// Calcule le score total en tenant compte des lettres issues de jokers.
    /// Les positions marquées joker valent toujours 0 point.
    /// </summary>
    public int Calculate(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions)
    {
        if (placements.Count == 0) return 0;

        var wordScore      = 0;
        var wordMultiplier = 1;
        var newlyPlacedTiles = 0;

        foreach (var (pos, letter) in placements)
        {
            var isWildcard = wildcardPositions?.Contains(pos) == true;
            var letterValue = isWildcard ? 0 : GetLetterValue(letter);
            var bonus       = BonusMap.GetBonus(pos);

            // Le bonus s'applique seulement si la case était libre avant ce coup
            var existingTile = board.Grid[pos.Row, pos.Column];
            var isNewSquare = existingTile is null;

            if (isNewSquare)
            {
                newlyPlacedTiles++;
                wordScore      += letterValue * bonus.LetterMultiplier;
                wordMultiplier *= bonus.WordMultiplier;
            }
            else
            {
                // Case déjà occupée : pas de bonus, et un joker déjà posé vaut toujours 0.
                var existingLetterValue = existingTile!.IsWildcard
                    ? 0
                    : GetLetterValue(existingTile.Letter);
                wordScore += existingLetterValue;
            }
        }

        var total = wordScore * wordMultiplier;

        // Bonus Scrabble : +50 pts si 7 lettres posées en un coup
        if (newlyPlacedTiles >= ScrabbleBonusTiles)
            total += ScrabbleBonus;

        return total;
    }

    private int GetLetterValue(char letter) =>
        _letterScores.TryGetValue(char.ToUpperInvariant(letter), out var value) ? value : 0;
}
