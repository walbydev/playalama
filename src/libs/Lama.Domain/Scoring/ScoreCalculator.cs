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
/// 5. Ajouter le score de chaque mot croisé formé (avec bonus sur les cases nouvellement posées).
///
/// Note : les bonus (DL, TL, DW, TW) ne s'appliquent que sur les cases nouvellement occupées
/// par ce coup. Une case déjà couverte par une tuile existante n'active pas son bonus.
/// </summary>
public sealed class ScoreCalculator
{
    private const int ScrabbleBonus      = 50;
    private const int ScrabbleBonusTiles = 7;

    private readonly IReadOnlyDictionary<char, int> _letterScores;

    public ScoreCalculator(IReadOnlyDictionary<char, int> letterScores)
    {
        _letterScores = letterScores;
    }

    /// <summary>Calcule le score du mot principal uniquement (sans mots croisés ni bonus Scrabble).</summary>
    public int Calculate(IReadOnlyDictionary<Position, char> placements, BoardState board)
        => Calculate(placements, board, wildcardPositions: null);

    /// <summary>
    /// Calcule le score du mot principal en tenant compte des jokers.
    /// Les positions marquées joker valent toujours 0 point.
    /// Inclut le bonus Scrabble si 7 lettres ou plus sont posées.
    /// </summary>
    public int Calculate(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions)
    {
        if (placements.Count == 0) return 0;
        var score = CalculateWordScore(placements, board, wildcardPositions, out var newlyPlaced);
        if (newlyPlaced >= ScrabbleBonusTiles) score += ScrabbleBonus;
        return score;
    }

    /// <summary>
    /// Calcule le score total d'un coup : mot principal + tous les mots croisés + bonus Scrabble.
    /// C'est cette méthode qui doit être utilisée dans le moteur de jeu.
    /// </summary>
    public int CalculateTotal(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions,
        bool isHorizontal)
    {
        if (placements.Count == 0) return 0;

        var mainScore = CalculateWordScore(placements, board, wildcardPositions, out var newlyPlacedTiles);
        var crossScore = CalculateCrossWordsScore(placements, board, wildcardPositions, isHorizontal);

        var total = mainScore + crossScore;
        if (newlyPlacedTiles >= ScrabbleBonusTiles)
            total += ScrabbleBonus;

        return total;
    }

    // ── Calcul du score d'un mot (principal ou croisé) ─────────────────────

    private int CalculateWordScore(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions,
        out int newlyPlacedTiles)
    {
        var wordScore      = 0;
        var wordMultiplier = 1;
        newlyPlacedTiles   = 0;

        foreach (var (pos, letter) in placements)
        {
            var isWildcard  = wildcardPositions?.Contains(pos) == true;
            var letterValue = isWildcard ? 0 : GetLetterValue(letter);
            var bonus       = BonusMap.GetBonus(pos);
            var existingTile = board.Grid[pos.Row, pos.Column];
            var isNewSquare  = existingTile is null;

            if (isNewSquare)
            {
                newlyPlacedTiles++;
                wordScore      += letterValue * bonus.LetterMultiplier;
                wordMultiplier *= bonus.WordMultiplier;
            }
            else
            {
                var existingLetterValue = existingTile!.IsWildcard
                    ? 0
                    : GetLetterValue(existingTile.Letter);
                wordScore += existingLetterValue;
            }
        }

        return wordScore * wordMultiplier;
    }

    /// <summary>
    /// Calcule la somme des scores de tous les mots croisés formés par ce coup.
    /// </summary>
    private int CalculateCrossWordsScore(
        IReadOnlyDictionary<Position, char> placements,
        BoardState board,
        ISet<Position>? wildcardPositions,
        bool mainIsHorizontal)
    {
        var total = 0;

        foreach (var (pos, letter) in placements)
        {
            // Seules les nouvelles cases peuvent former des croisements
            if (board.Grid[pos.Row, pos.Column] is not null) continue;

            int crossScore    = 0;
            int crossMulti    = 1;
            bool hasCross     = false;

            if (mainIsHorizontal)
            {
                // Croisement vertical : chercher vers le haut et le bas
                var minRow = pos.Row;
                var maxRow = pos.Row;
                while (minRow > 0 && board.Grid[minRow - 1, pos.Column] is not null) minRow--;
                while (maxRow < 14 && board.Grid[maxRow + 1, pos.Column] is not null) maxRow++;
                if (maxRow == minRow) continue; // pas de croisement

                hasCross = true;
                for (var r = minRow; r <= maxRow; r++)
                {
                    var tilePos = new Position(r, pos.Column);
                    var isNewPos = board.Grid[r, pos.Column] is null;
                    var bonus    = BonusMap.GetBonus(tilePos);

                    if (isNewPos)
                    {
                        // C'est la tuile nouvellement posée à (pos.Row, pos.Column)
                        var isWildcard  = wildcardPositions?.Contains(tilePos) == true;
                        var letterVal   = isWildcard ? 0 : GetLetterValue(letter);
                        crossScore     += letterVal * bonus.LetterMultiplier;
                        crossMulti     *= bonus.WordMultiplier;
                    }
                    else
                    {
                        var existingTile = board.Grid[r, pos.Column]!;
                        crossScore += existingTile.IsWildcard ? 0 : GetLetterValue(existingTile.Letter);
                    }
                }
            }
            else
            {
                // Croisement horizontal
                var minCol = pos.Column;
                var maxCol = pos.Column;
                while (minCol > 0 && board.Grid[pos.Row, minCol - 1] is not null) minCol--;
                while (maxCol < 14 && board.Grid[pos.Row, maxCol + 1] is not null) maxCol++;
                if (maxCol == minCol) continue;

                hasCross = true;
                for (var c = minCol; c <= maxCol; c++)
                {
                    var tilePos = new Position(pos.Row, c);
                    var isNewPos = board.Grid[pos.Row, c] is null;
                    var bonus    = BonusMap.GetBonus(tilePos);

                    if (isNewPos)
                    {
                        var isWildcard  = wildcardPositions?.Contains(tilePos) == true;
                        var letterVal   = isWildcard ? 0 : GetLetterValue(letter);
                        crossScore     += letterVal * bonus.LetterMultiplier;
                        crossMulti     *= bonus.WordMultiplier;
                    }
                    else
                    {
                        var existingTile = board.Grid[pos.Row, c]!;
                        crossScore += existingTile.IsWildcard ? 0 : GetLetterValue(existingTile.Letter);
                    }
                }
            }

            if (hasCross)
                total += crossScore * crossMulti;
        }

        return total;
    }

    private int GetLetterValue(char letter) =>
        _letterScores.TryGetValue(char.ToUpperInvariant(letter), out var value) ? value : 0;
}
