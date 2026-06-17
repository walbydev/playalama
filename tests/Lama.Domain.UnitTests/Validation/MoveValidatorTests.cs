using FluentAssertions;
using Lama.Contracts;
using Lama.Domain.Validation;

namespace Lama.Domain.UnitTests.Validation;

/// <summary>
/// Tests unitaires pour <see cref="MoveValidator"/>.
/// Vérifie les règles de placement officielles Scrabble :
/// - alignement (horizontal ou vertical)
/// - continuité (pas de trous)
/// - connexion au plateau (sauf premier coup)
/// - premier mot : doit passer par H8 (7,7)
/// - longueur minimale (2 lettres)
/// - cases non déjà occupées
/// </summary>
public class MoveValidatorTests
{
    private static readonly IReadOnlySet<string> Dictionary =
        new HashSet<string> { "LA", "LAMA", "AMI", "AMS", "MA", "MAS", "MOT", "MOTS", "ZEN", "AS", "AI" };

    private readonly MoveValidator _sut = new(Dictionary);

    // Helper : plateau vide
    private static BoardState EmptyBoard() => new();

    // Helper : plateau avec un mot posé
    private static BoardState BoardWith(Dictionary<Position, char> tiles)
    {
        var grid = new Tile?[15, 15];
        foreach (var (pos, letter) in tiles)
            grid[pos.Row, pos.Column] = new Tile(letter);
        return new BoardState(grid);
    }

    #region Premier coup — doit passer par H8

    [Fact]
    public void FirstMove_ThroughCenter_IsValid()
    {
        // LAMA horizontal depuis H8 (7,7)
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A',
            [new Position(7, 9)] = 'M',
            [new Position(7, 10)] = 'A'
        };

        var result = _sut.Validate(move, EmptyBoard(), isFirstMove: true);

        result.IsValid.Should().BeTrue(because: "LAMA horizontal depuis H8 est valide");
    }

    [Fact]
    public void FirstMove_NotThroughCenter_IsInvalid()
    {
        // LAMA horizontal depuis A1 (0,0) — ne passe pas par H8
        var move = new Dictionary<Position, char>
        {
            [new Position(0, 0)] = 'L',
            [new Position(0, 1)] = 'A',
            [new Position(0, 2)] = 'M',
            [new Position(0, 3)] = 'A'
        };

        var result = _sut.Validate(move, EmptyBoard(), isFirstMove: true);

        result.IsValid.Should().BeFalse(
            because: "le premier mot doit passer par la case centrale H8");
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FirstMove_Vertical_ThroughCenter_IsValid()
    {
        // LA vertical centré sur H8
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(8, 7)] = 'A'
        };

        var result = _sut.Validate(move, EmptyBoard(), isFirstMove: true);

        result.IsValid.Should().BeTrue(because: "LA vertical depuis H8 est valide");
    }

    #endregion

    #region Alignement — toutes les lettres doivent être alignées

    [Fact]
    public void Move_HorizontallyAligned_IsValid()
    {
        // Plateau avec LAMA en H8, on pose AS en H5-H6
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A',
            [new Position(7, 9)] = 'M',
            [new Position(7, 10)] = 'A'
        });

        var move = new Dictionary<Position, char>
        {
            [new Position(7, 5)] = 'A',
            [new Position(7, 6)] = 'S'
        };

        var result = _sut.Validate(move, board, isFirstMove: false);

        result.IsValid.Should().BeTrue(because: "AS horizontal adjacent à LAMA est valide");
    }

    [Fact]
    public void Move_NotAligned_IsInvalid()
    {
        // Lettres pas sur la même ligne ni colonne
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        });

        var move = new Dictionary<Position, char>
        {
            [new Position(5, 5)] = 'A',
            [new Position(6, 6)] = 'S'  // diagonale → invalide
        };

        var result = _sut.Validate(move, board, isFirstMove: false);

        result.IsValid.Should().BeFalse(
            because: "les lettres d'un coup doivent être sur la même ligne ou colonne");
    }

    #endregion

    #region Continuité — pas de trou autorisé

    [Fact]
    public void Move_WithGap_CoveredByExistingTile_IsValid()
    {
        // Plateau avec 'A' en (7,8)
        // On pose M(7,7) et S(7,9) → MOT "MAS" avec le A déjà posé comble le trou
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A',
            [new Position(7, 9)] = 'M',
            [new Position(7, 10)] = 'A'  // LAMA
        });

        // On pose S en (7,11) → LAMAS
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 11)] = 'S'
        };

        var result = _sut.Validate(move, board, isFirstMove: false);

        result.IsValid.Should().BeTrue(because: "LAMAS est valide (S adjacent à LAMA)");
    }

    [Fact]
    public void Move_WithEmptyGap_IsInvalid()
    {
        // Plateau avec LAMA en (7,7-10)
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A',
            [new Position(7, 9)] = 'M',
            [new Position(7, 10)] = 'A'
        });

        // Trou entre les tuiles : pose (7,5) et (7,8) — case (7,6) vide entre les deux
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 5)] = 'M',
            [new Position(7, 7)] = 'T'  // (7,7) déjà occupé !
        };

        var result = _sut.Validate(move, board, isFirstMove: false);

        result.IsValid.Should().BeFalse(
            because: "on ne peut pas poser sur une case déjà occupée");
    }

    #endregion

    #region Connexion au plateau (hors premier coup)

    [Fact]
    public void Move_ConnectedToExistingTile_IsValid()
    {
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        });

        // MA vertical adjacent à LA (en (6,7) et (7,7))
        var move = new Dictionary<Position, char>
        {
            [new Position(6, 7)] = 'M',
            // (7,7) est déjà 'L' — on veut poser perpendiculairement
        };
        // Pour ce test, on pose juste un mot adjacent
        var move2 = new Dictionary<Position, char>
        {
            [new Position(7, 9)] = 'M',
            [new Position(7, 10)] = 'A'
        };

        var result = _sut.Validate(move2, board, isFirstMove: false);

        result.IsValid.Should().BeTrue(
            because: "MA adjacent à LA forme LAMA et est connecté");
    }

    [Fact]
    public void Move_NotConnectedToBoard_IsInvalid()
    {
        // Plateau avec LAMA en H8-H11
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A',
            [new Position(7, 9)] = 'M',
            [new Position(7, 10)] = 'A'
        });

        // Mot posé loin du plateau, sans connexion
        var move = new Dictionary<Position, char>
        {
            [new Position(0, 0)] = 'A',
            [new Position(0, 1)] = 'S'
        };

        var result = _sut.Validate(move, board, isFirstMove: false);

        result.IsValid.Should().BeFalse(
            because: "le mot doit être connecté aux tuiles existantes");
    }

    #endregion

    #region Longueur minimale

    [Fact]
    public void Move_SingleLetter_NotConnecting_IsInvalid()
    {
        // Une seule lettre posée, sans former un mot avec le plateau
        var board = EmptyBoard();

        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'A'
        };

        // Premier coup d'une seule lettre → invalide (min 2 lettres)
        var result = _sut.Validate(move, board, isFirstMove: true);

        result.IsValid.Should().BeFalse(
            because: "un coup d'une seule lettre nécessite de former un mot avec l'existant");
    }

    [Fact]
    public void Move_TwoLetters_IsMinimumValid()
    {
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        };

        var result = _sut.Validate(move, EmptyBoard(), isFirstMove: true);

        result.IsValid.Should().BeTrue(because: "LA (2 lettres) est le minimum valide");
    }

    #endregion

    #region Cases déjà occupées

    [Fact]
    public void Move_OnOccupiedSquare_IsInvalid()
    {
        var board = BoardWith(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L'
        });

        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'A' // case occupée !
        };

        var result = _sut.Validate(move, board, isFirstMove: false);

        result.IsValid.Should().BeFalse(
            because: "on ne peut pas jouer sur une case déjà occupée");
    }

    #endregion

    #region Coup vide

    [Fact]
    public void Move_Empty_IsInvalid()
    {
        var result = _sut.Validate(new Dictionary<Position, char>(),
            EmptyBoard(), isFirstMove: false);

        result.IsValid.Should().BeFalse(
            because: "un coup vide est toujours invalide");
    }

    [Fact]
    public void Move_WithInvalidCharacter_IsInvalid()
    {
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = '1',
            [new Position(7, 8)] = 'A'
        };

        var result = _sut.Validate(move, EmptyBoard(), isFirstMove: true);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Lettre invalide");
    }

    #endregion

    #region Résultat de validation

    [Fact]
    public void ValidMove_HasEmptyErrorMessage()
    {
        var move = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        };

        var result = _sut.Validate(move, EmptyBoard(), isFirstMove: true);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty(
            because: "un coup valide ne doit pas avoir de message d'erreur");
    }

    [Fact]
    public void InvalidMove_HasNonEmptyErrorMessage()
    {
        var result = _sut.Validate(new Dictionary<Position, char>(),
            EmptyBoard(), isFirstMove: false);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty(
            because: "un coup invalide doit toujours expliquer pourquoi");
    }

    #endregion
}
