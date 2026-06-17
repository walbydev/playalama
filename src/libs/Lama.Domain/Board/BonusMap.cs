using Lama.Contracts;

namespace Lama.Domain.Board;

/// <summary>
/// Carte des cases bonus du plateau Scrabble standard 15×15.
/// Coordonnées : ligne 0..14 (1..15), colonne 0..14 (A..O).
/// La case centrale est H8 → (row=7, col=7).
///
/// Disposition officielle Scrabble :
///   TW = Triple Mot  (rouge)        : coins + bords réguliers
///   DW = Double Mot  (rose)         : diagonales
///   TL = Triple Lettre (bleu foncé) : positions internes
///   DL = Double Lettre (bleu clair) : positions intermédiaires
///   ST = Départ      (étoile)       : H8 (7,7) — compte comme DW
/// </summary>
public static class BonusMap
{
    private static readonly BonusSquare[,] _map = BuildMap();

    /// <summary>
    /// Retourne la case bonus à la position donnée.
    /// Retourne <see cref="BonusSquare.None"/> si la position est hors plateau.
    /// </summary>
    public static BonusSquare GetBonus(Position position)
    {
        if (!position.IsValid)
            return BonusSquare.None;
        return _map[position.Row, position.Column];
    }

    /// <summary>
    /// Retourne la case bonus à la position (row, col).
    /// </summary>
    public static BonusSquare GetBonus(int row, int col) =>
        GetBonus(new Position(row, col));

    // ── Construction de la carte ──────────────────────────────────────────────

    private static BonusSquare[,] BuildMap()
    {
        var map = new BonusSquare[15, 15];

        // Initialiser tout à None
        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
                map[r, c] = BonusSquare.None;

        // ── Triple Mot (TW) ───────────────────────────────────────────────────
        // Coins et positions sur les bords, tous les 7 cases
        foreach (var (r, c) in TripleWordPositions())
            map[r, c] = BonusSquare.TripleWord;

        // ── Double Mot (DW) ───────────────────────────────────────────────────
        // Diagonales depuis le centre
        foreach (var (r, c) in DoubleWordPositions())
            map[r, c] = BonusSquare.DoubleWord;

        // ── Triple Lettre (TL) ────────────────────────────────────────────────
        foreach (var (r, c) in TripleLetterPositions())
            map[r, c] = BonusSquare.TripleLetter;

        // ── Double Lettre (DL) ────────────────────────────────────────────────
        foreach (var (r, c) in DoubleLetterPositions())
            map[r, c] = BonusSquare.DoubleLetter;

        // ── Case de départ H8 (7, 7) ──────────────────────────────────────────
        map[7, 7] = BonusSquare.Start;

        return map;
    }

    private static IEnumerable<(int r, int c)> TripleWordPositions() =>
    [
        (0, 0), (0, 7), (0, 14),
        (7, 0),          (7, 14),
        (14, 0),(14, 7),(14, 14)
    ];

    private static IEnumerable<(int r, int c)> DoubleWordPositions() =>
    [
        // Diagonale descendante
        (1, 1),  (2, 2),  (3, 3),  (4, 4),
        (10, 10),(11, 11),(12, 12),(13, 13),
        // Diagonale montante
        (1, 13), (2, 12), (3, 11), (4, 10),
        (10, 4), (11, 3), (12, 2), (13, 1)
    ];

    private static IEnumerable<(int r, int c)> TripleLetterPositions() =>
    [
        (1, 5),  (1, 9),
        (5, 1),  (5, 5),  (5, 9),  (5, 13),
        (9, 1),  (9, 5),  (9, 9),  (9, 13),
        (13, 5), (13, 9)
    ];

    private static IEnumerable<(int r, int c)> DoubleLetterPositions() =>
    [
        // Ligne 0
        (0, 3), (0, 11),
        // Ligne 2
        (2, 6), (2, 8),
        // Ligne 3
        (3, 0), (3, 7), (3, 14),
        // Ligne 6
        (6, 2), (6, 6), (6, 8), (6, 12),
        // Ligne 7 (gauche et droite, pas le centre)
        (7, 3), (7, 11),
        // Ligne 8 (symétrique ligne 6)
        (8, 2), (8, 6), (8, 8), (8, 12),
        // Ligne 11 (symétrique ligne 3)
        (11, 0),(11, 7),(11, 14),
        // Ligne 12 (symétrique ligne 2)
        (12, 6),(12, 8),
        // Ligne 14 (symétrique ligne 0)
        (14, 3),(14, 11)
    ];
}
