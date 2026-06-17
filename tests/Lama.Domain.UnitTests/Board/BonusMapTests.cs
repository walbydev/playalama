using FluentAssertions;
using Lama.Contracts;
using Lama.Domain.Board;

namespace Lama.Domain.UnitTests.Board;

/// <summary>
/// Tests unitaires pour <see cref="BonusMap"/> et <see cref="BonusSquare"/>.
/// Vérifie que le plateau 15×15 a les bonnes cases bonus aux bonnes positions.
/// Référence : disposition officielle Scrabble (symétrie 4 axes).
/// </summary>
public class BonusMapTests
{
    #region BonusSquare — multiplicateurs

    [Fact]
    public void BonusSquare_None_HasNoMultipliers()
    {
        BonusSquare.None.LetterMultiplier.Should().Be(1);
        BonusSquare.None.WordMultiplier.Should().Be(1);
    }

    [Fact]
    public void BonusSquare_DoubleLetter_HasLetterMultiplier2()
    {
        BonusSquare.DoubleLetter.LetterMultiplier.Should().Be(2);
        BonusSquare.DoubleLetter.WordMultiplier.Should().Be(1);
    }

    [Fact]
    public void BonusSquare_TripleLetter_HasLetterMultiplier3()
    {
        BonusSquare.TripleLetter.LetterMultiplier.Should().Be(3);
        BonusSquare.TripleLetter.WordMultiplier.Should().Be(1);
    }

    [Fact]
    public void BonusSquare_DoubleWord_HasWordMultiplier2()
    {
        BonusSquare.DoubleWord.LetterMultiplier.Should().Be(1);
        BonusSquare.DoubleWord.WordMultiplier.Should().Be(2);
    }

    [Fact]
    public void BonusSquare_TripleWord_HasWordMultiplier3()
    {
        BonusSquare.TripleWord.LetterMultiplier.Should().Be(1);
        BonusSquare.TripleWord.WordMultiplier.Should().Be(3);
    }

    [Fact]
    public void BonusSquare_Start_CountsAsDoubleWord()
    {
        BonusSquare.Start.LetterMultiplier.Should().Be(1);
        BonusSquare.Start.WordMultiplier.Should().Be(2,
            because: "la case de départ compte comme Double Mot");
    }

    #endregion

    #region BonusMap — case de départ H8

    [Fact]
    public void H8_IsStartSquare()
    {
        // H8 = colonne H (index 7), ligne 8 (index 7) → (row=7, col=7)
        var bonus = BonusMap.GetBonus(row: 7, col: 7);

        bonus.Type.Should().Be(BonusType.Start,
            because: "H8 est la case de départ (centre du plateau)");
        bonus.WordMultiplier.Should().Be(2);
    }

    #endregion

    #region BonusMap — Triple Mot (8 positions)

    [Theory]
    [InlineData(0,  0,  "A1  — coin supérieur gauche")]
    [InlineData(0,  7,  "H1  — bord supérieur milieu")]
    [InlineData(0,  14, "O1  — coin supérieur droit")]
    [InlineData(7,  0,  "A8  — bord gauche milieu")]
    [InlineData(7,  14, "O8  — bord droit milieu")]
    [InlineData(14, 0,  "A15 — coin inférieur gauche")]
    [InlineData(14, 7,  "H15 — bord inférieur milieu")]
    [InlineData(14, 14, "O15 — coin inférieur droit")]
    public void TripleWord_AtCorrectPositions(int row, int col, string label)
    {
        var bonus = BonusMap.GetBonus(row, col);

        bonus.Type.Should().Be(BonusType.TripleWord,
            because: $"{label} doit être une case Triple Mot");
        bonus.WordMultiplier.Should().Be(3);
    }

    #endregion

    #region BonusMap — Double Mot (16 positions sur les diagonales)

    [Theory]
    [InlineData(1,  1)]
    [InlineData(2,  2)]
    [InlineData(3,  3)]
    [InlineData(4,  4)]
    [InlineData(10, 10)]
    [InlineData(11, 11)]
    [InlineData(12, 12)]
    [InlineData(13, 13)]
    [InlineData(1,  13)]
    [InlineData(2,  12)]
    [InlineData(3,  11)]
    [InlineData(4,  10)]
    [InlineData(10, 4)]
    [InlineData(11, 3)]
    [InlineData(12, 2)]
    [InlineData(13, 1)]
    public void DoubleWord_AtDiagonalPositions(int row, int col)
    {
        var bonus = BonusMap.GetBonus(row, col);

        bonus.Type.Should().Be(BonusType.DoubleWord,
            because: $"({row},{col}) doit être une case Double Mot (diagonale)");
        bonus.WordMultiplier.Should().Be(2);
    }

    #endregion

    #region BonusMap — Triple Lettre (12 positions)

    [Theory]
    [InlineData(1,  5)]
    [InlineData(1,  9)]
    [InlineData(5,  1)]
    [InlineData(5,  5)]
    [InlineData(5,  9)]
    [InlineData(5,  13)]
    [InlineData(9,  1)]
    [InlineData(9,  5)]
    [InlineData(9,  9)]
    [InlineData(9,  13)]
    [InlineData(13, 5)]
    [InlineData(13, 9)]
    public void TripleLetter_AtCorrectPositions(int row, int col)
    {
        var bonus = BonusMap.GetBonus(row, col);

        bonus.Type.Should().Be(BonusType.TripleLetter,
            because: $"({row},{col}) doit être une case Triple Lettre");
        bonus.LetterMultiplier.Should().Be(3);
    }

    #endregion

    #region BonusMap — Double Lettre (quelques positions clés)

    [Theory]
    [InlineData(0,  3)]
    [InlineData(0,  11)]
    [InlineData(2,  6)]
    [InlineData(2,  8)]
    [InlineData(3,  0)]
    [InlineData(3,  7)]
    [InlineData(3,  14)]
    [InlineData(6,  2)]
    [InlineData(6,  6)]
    [InlineData(6,  8)]
    [InlineData(6,  12)]
    [InlineData(7,  3)]
    [InlineData(7,  11)]
    public void DoubleLetter_AtCorrectPositions(int row, int col)
    {
        var bonus = BonusMap.GetBonus(row, col);

        bonus.Type.Should().Be(BonusType.DoubleLetter,
            because: $"({row},{col}) doit être une case Double Lettre");
        bonus.LetterMultiplier.Should().Be(2);
    }

    #endregion

    #region BonusMap — Positions normales (pas de bonus)

    [Theory]
    [InlineData(0,  1,  "A2")]
    [InlineData(1,  0,  "A2")]
    [InlineData(7,  6,  "G8 — à côté du centre")]
    [InlineData(7,  8,  "I8 — à côté du centre")]
    public void NormalSquares_HaveNoBonus(int row, int col, string label)
    {
        var bonus = BonusMap.GetBonus(row, col);

        bonus.Type.Should().Be(BonusType.None,
            because: $"{label} est une case normale sans bonus");
        bonus.LetterMultiplier.Should().Be(1);
        bonus.WordMultiplier.Should().Be(1);
    }

    #endregion

    #region BonusMap — Cas limites

    [Theory]
    [InlineData(-1, 7)]
    [InlineData(7,  -1)]
    [InlineData(15, 7)]
    [InlineData(7,  15)]
    [InlineData(-1, -1)]
    [InlineData(15, 15)]
    public void GetBonus_OutOfBounds_ReturnsNone(int row, int col)
    {
        var bonus = BonusMap.GetBonus(row, col);

        bonus.Type.Should().Be(BonusType.None,
            because: "une position hors plateau doit retourner None sans exception");
    }

    [Fact]
    public void GetBonus_AcceptsPositionRecord()
    {
        var pos   = new Position(7, 7);
        var bonus = BonusMap.GetBonus(pos);

        bonus.Type.Should().Be(BonusType.Start);
    }

    #endregion

    #region BonusMap — Symétrie du plateau

    [Theory]
    [InlineData(1, 5)]   // TL — doit être symétrique
    [InlineData(0, 3)]   // DL — doit être symétrique
    [InlineData(0, 0)]   // TW — coin
    public void BonusMap_IsSymmetric_HorizontallyAndVertically(int row, int col)
    {
        var original       = BonusMap.GetBonus(row, col);
        var mirrorH        = BonusMap.GetBonus(row,      14 - col); // symétrie horizontale
        var mirrorV        = BonusMap.GetBonus(14 - row, col);      // symétrie verticale
        var mirrorDiag     = BonusMap.GetBonus(14 - row, 14 - col); // symétrie centrale

        mirrorH.Type.Should().Be(original.Type,
            because: $"({row},{col}) et ({row},{14 - col}) doivent être symétriques");
        mirrorV.Type.Should().Be(original.Type,
            because: $"({row},{col}) et ({14 - row},{col}) doivent être symétriques");
        mirrorDiag.Type.Should().Be(original.Type,
            because: "le plateau doit être symétrique centralement");
    }

    [Fact]
    public void BonusMap_HasExactly8TripleWordSquares()
    {
        var count = CountSquaresOfType(BonusType.TripleWord);
        count.Should().Be(8, because: "le plateau Scrabble a exactement 8 cases Triple Mot");
    }

    [Fact]
    public void BonusMap_HasExactly16DoubleWordSquares()
    {
        var count = CountSquaresOfType(BonusType.DoubleWord);
        count.Should().Be(16, because: "le plateau Scrabble a exactement 16 cases Double Mot");
    }

    [Fact]
    public void BonusMap_HasExactly12TripleLetterSquares()
    {
        var count = CountSquaresOfType(BonusType.TripleLetter);
        count.Should().Be(12, because: "le plateau Scrabble a exactement 12 cases Triple Lettre");
    }

    [Fact]
    public void BonusMap_HasExactly1StartSquare()
    {
        var count = CountSquaresOfType(BonusType.Start);
        count.Should().Be(1, because: "il n'y a qu'une seule case de départ (H8)");
    }

    private static int CountSquaresOfType(BonusType type)
    {
        var count = 0;
        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
                if (BonusMap.GetBonus(r, c).Type == type)
                    count++;
        return count;
    }

    #endregion
}
