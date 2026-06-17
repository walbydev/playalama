using FluentAssertions;
using Lama.Contracts;
using Lama.Domain.Board;
using Lama.Domain.Scoring;

namespace Lama.Domain.UnitTests.Scoring;

/// <summary>
/// Tests unitaires pour <see cref="ScoreCalculator"/>.
/// Vérifie le calcul des scores selon les règles officielles Scrabble :
/// score = Σ(valeur_lettre × multiplicateur_lettre) × multiplicateur_mot
/// + bonus Scrabble (50 pts si 7 lettres posées en un coup).
/// </summary>
public class ScoreCalculatorTests
{
    // Scores des lettres françaises (simplifiés pour les tests)
    private static readonly IReadOnlyDictionary<char, int> Scores = new Dictionary<char, int>
    {
        ['A'] = 1, ['B'] = 3, ['C'] = 3, ['D'] = 2, ['E'] = 1,
        ['F'] = 4, ['G'] = 2, ['H'] = 4, ['I'] = 1, ['J'] = 8,
        ['K'] = 10,['L'] = 1, ['M'] = 2, ['N'] = 1, ['O'] = 1,
        ['P'] = 3, ['Q'] = 8, ['R'] = 1, ['S'] = 1, ['T'] = 1,
        ['U'] = 1, ['V'] = 4, ['W'] = 10,['X'] = 10,['Y'] = 10,
        ['Z'] = 10,['*'] = 0  // joker
    };

    private readonly ScoreCalculator _sut = new(Scores);

    #region Score de base — case normale

    [Fact]
    public void Score_SingleLetter_OnNormalSquare()
    {
        // (0,1) est une case normale (pas de bonus)
        // A (1pt) sur case normale → 1pt
        BonusMap.GetBonus(0, 1).Type.Should().Be(BonusType.None);
        var placements = new Dictionary<Position, char>
        {
            [new Position(0, 1)] = 'A'
        };
        var board = new BoardState();

        var score = _sut.Calculate(placements, board);

        score.Should().Be(1, because: "A vaut 1 point sur une case normale");
    }

    [Fact]
    public void Score_MultipleLetters_OnNormalSquares()
    {
        // Positions (1,2), (1,3), (1,4), (1,6) — toutes cases normales (on évite (1,5)=TL)
        // L(1) + A(1) + M(2) + A(1) = 5 pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(1, 2)] = 'L',
            [new Position(1, 3)] = 'A',
            [new Position(1, 4)] = 'M',
            [new Position(1, 6)] = 'A'
        };

        // Vérifier les positions avant le test
        BonusMap.GetBonus(1, 2).Type.Should().Be(BonusType.None);
        BonusMap.GetBonus(1, 3).Type.Should().Be(BonusType.None);
        BonusMap.GetBonus(1, 4).Type.Should().Be(BonusType.None);
        BonusMap.GetBonus(1, 6).Type.Should().Be(BonusType.None);

        var board = new BoardState();
        var score = _sut.Calculate(placements, board);

        score.Should().Be(5, because: "LAMA = L(1)+A(1)+M(2)+A(1) = 5 pts sur cases normales");
    }

    #endregion

    #region Double Lettre (×2 sur la lettre)

    [Fact]
    public void Score_OnDoubleLetterSquare_DoublesLetterValue()
    {
        // Case (0,3) est DL — A(1) × 2 = 2 pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(0, 3)] = 'A'
        };
        var board = new BoardState();

        // Vérifier d'abord que c'est bien DL
        BonusMap.GetBonus(0, 3).Type.Should().Be(BonusType.DoubleLetter);

        var score = _sut.Calculate(placements, board);
        score.Should().Be(2, because: "A(1pt) sur DL = 2pts");
    }

    [Fact]
    public void Score_OnDoubleLetterSquare_OnlyMultipliesOneLetter()
    {
        // L(1) + A(1)×DL(0,3) + M(2) = 1 + 2 + 2 = 5pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(0, 2)] = 'L',
            [new Position(0, 3)] = 'A', // DL
            [new Position(0, 4)] = 'M'
        };
        var board = new BoardState();

        var score = _sut.Calculate(placements, board);
        score.Should().Be(5, because: "L(1)+A×2(2)+M(2) = 5pts");
    }

    #endregion

    #region Triple Lettre (×3 sur la lettre)

    [Fact]
    public void Score_OnTripleLetterSquare_TriplesLetterValue()
    {
        // Case (1,5) est TL — J(8) × 3 = 24 pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(1, 5)] = 'J'
        };
        var board = new BoardState();

        BonusMap.GetBonus(1, 5).Type.Should().Be(BonusType.TripleLetter);

        var score = _sut.Calculate(placements, board);
        score.Should().Be(24, because: "J(8pts) sur TL = 24pts");
    }

    #endregion

    #region Double Mot (×2 sur tout le mot)

    [Fact]
    public void Score_OnDoubleWordSquare_DoublesWholeWord()
    {
        // Case (1,1) est DW — A(1) × DW(2) = 2pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(1, 1)] = 'A'
        };
        var board = new BoardState();

        BonusMap.GetBonus(1, 1).Type.Should().Be(BonusType.DoubleWord);

        var score = _sut.Calculate(placements, board);
        score.Should().Be(2, because: "A(1pt) sur DW(×2) = 2pts");
    }

    [Fact]
    public void Score_WordCrossingDoubleWord_DoublesEntireWord()
    {
        // L(1) + A(1)×DW(1,1) + M(2) → total = (1+1+2) × 2 = 8pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(1, 0)] = 'L',
            [new Position(1, 1)] = 'A', // DW
            [new Position(1, 2)] = 'M'
        };
        var board = new BoardState();

        var score = _sut.Calculate(placements, board);
        score.Should().Be(8, because: "(L(1)+A(1)+M(2)) × DW(2) = 8pts");
    }

    #endregion

    #region Triple Mot (×3 sur tout le mot)

    [Fact]
    public void Score_OnTripleWordSquare_TriplesWholeWord()
    {
        // Case (0,0) est TW — A(1) × TW(3) = 3pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(0, 0)] = 'A'
        };
        var board = new BoardState();

        BonusMap.GetBonus(0, 0).Type.Should().Be(BonusType.TripleWord);

        var score = _sut.Calculate(placements, board);
        score.Should().Be(3, because: "A(1pt) sur TW(×3) = 3pts");
    }

    #endregion

    #region Combinaisons DL + DW

    [Fact]
    public void Score_DL_And_DW_InSameWord_AppliesLetterFirst_ThenWord()
    {
        // Case (2,2) DW, case (2,6) DL — mot de 2 lettres
        // A(1)×DW(2,2) + J(8)×DL(2,6) = (1 + 8×2) × 2 = (1+16)×2 = 34pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(2, 2)] = 'A', // DW
            [new Position(2, 6)] = 'J'  // DL
        };
        // Simuler un plateau vide avec des tuiles intermédiaires déjà posées
        // Pour simplifier le test, on pose uniquement les lettres actives
        // Le ScoreCalculator calcule sur les positions placées.
        var board = new BoardState();

        BonusMap.GetBonus(2, 2).Type.Should().Be(BonusType.DoubleWord);
        BonusMap.GetBonus(2, 6).Type.Should().Be(BonusType.DoubleLetter);

        var score = _sut.Calculate(placements, board);
        // (A×1 + J×2) × mot×2 = (1 + 16) × 2 = 34
        score.Should().Be(34, because: "(A(1)+J(8)×DL(2)) × DW(2) = 34pts");
    }

    #endregion

    #region Case de départ H8

    [Fact]
    public void Score_OnStartSquare_CountsAsDoubleWord()
    {
        // H8 (7,7) = Start = DW — A(1) × 2 = 2pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'A'
        };
        var board = new BoardState();

        var score = _sut.Calculate(placements, board);
        score.Should().Be(2, because: "la case de départ H8 compte comme Double Mot");
    }

    #endregion

    #region Bonus cases déjà occupées

    [Fact]
    public void Score_BonusNotApplied_ForLettersAlreadyOnBoard()
    {
        // Si une lettre est déjà sur le plateau (tuile existante) sur une case bonus,
        // le bonus NE s'applique PAS pour ce coup.
        var grid = new Tile?[15, 15];
        grid[1, 1] = new Tile('A'); // DW déjà occupée
        var board = new BoardState(grid);

        // On pose B en (1,2) — case normale
        var placements = new Dictionary<Position, char>
        {
            [new Position(1, 2)] = 'B'
        };

        var score = _sut.Calculate(placements, board);
        // B(3) sur case normale → 3pts (pas de DW car la case DW est déjà occupée)
        score.Should().Be(3,
            because: "les bonus ne s'appliquent que sur les cases nouvellement occupées");
    }

    #endregion

    #region Joker (valeur 0)

    [Fact]
    public void Score_Wildcard_CountsAsZeroPoints()
    {
        // Joker '*' → 0 pts
        var placements = new Dictionary<Position, char>
        {
            [new Position(0, 1)] = '*'
        };
        var board = new BoardState();

        var score = _sut.Calculate(placements, board);
        score.Should().Be(0, because: "un joker vaut toujours 0 point");
    }

    #endregion

    #region Bonus Scrabble (7 lettres)

    [Fact]
    public void Score_Scrabble_Adds50Points_When7LettersPlaced()
    {
        // Positions (10,7) à (10,13) — toutes cases normales
        // (10,7) = None, (10,8) = None, (10,9) = None, (10,10) = DW, (10,11) = None ...
        // On choisit des positions explicitement sans bonus pour un calcul déterministe.
        // Positions sans bonus sur la ligne 10 : cols 7,8,9,11,12,13,14
        // On vérifie : toutes doivent être None sauf (10,10)=DW → on évite la col 10
        var positions7 = new[] { 7, 8, 9, 11, 12, 13, 14 };
        var placements = new Dictionary<Position, char>();
        foreach (var col in positions7)
        {
            BonusMap.GetBonus(10, col).Type.Should().Be(BonusType.None,
                because: $"(10,{col}) doit être une case normale pour ce test");
            placements[new Position(10, col)] = 'A';
        }

        var board = new BoardState();
        var score = _sut.Calculate(placements, board);

        // 7 × A(1pt) + bonus Scrabble(50pts) = 57pts
        score.Should().Be(57,
            because: "7×A(1pt) sur cases normales + bonus Scrabble(50pts) = 57pts");
    }

    [Fact]
    public void Score_NoScrabbleBonus_When6LettersPlaced()
    {
        // 6 lettres sur cases normales — pas de bonus Scrabble
        var positions6 = new[] { 7, 8, 9, 11, 12, 13 }; // (10,col) toutes None
        var placements = new Dictionary<Position, char>();
        foreach (var col in positions6)
            placements[new Position(10, col)] = 'A';

        var board = new BoardState();
        var score = _sut.Calculate(placements, board);

        // 6 × A(1pt) = 6pts, pas de bonus Scrabble
        score.Should().Be(6,
            because: "6×A(1pt) sans bonus Scrabble = 6pts");
    }

    #endregion
}
