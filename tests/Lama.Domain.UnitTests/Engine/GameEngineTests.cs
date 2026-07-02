using FluentAssertions;
using Lama.Contracts;
using Lama.Domain.Engine;

namespace Lama.Domain.UnitTests.Engine;

/// <summary>
/// Tests unitaires pour <see cref="GameEngine"/>.
/// Vérifie l'orchestration complète du jeu : initialisation, tour de jeu,
/// calcul des scores, passage de tour et fin de partie.
///
/// Les tests utilisent un dictionnaire minimal et des scores fixes
/// pour rendre les assertions déterministes.
/// </summary>
public class GameEngineTests
{
    // ── Fixtures partagées ────────────────────────────────────────────────────

    private static readonly IReadOnlySet<string> Dictionary =
        new HashSet<string>
        {
            "LA", "LAMA", "MA", "MOT", "MOTS", "AS", "SA",
            "AMI", "MAS", "ZEN", "AI", "RI", "LI", "AME"
        };

    private static readonly IReadOnlyDictionary<char, int> LetterScores =
        new Dictionary<char, int>
        {
            ['A'] = 1, ['B'] = 3, ['C'] = 3, ['D'] = 2, ['E'] = 1,
            ['F'] = 4, ['G'] = 2, ['H'] = 4, ['I'] = 1, ['J'] = 8,
            ['K'] = 10,['L'] = 1, ['M'] = 2, ['N'] = 1, ['O'] = 1,
            ['R'] = 1, ['S'] = 1, ['T'] = 1, ['U'] = 1, ['Z'] = 10,
            ['*'] = 0
        };

    // Distribution minimale pour les tests (évite l'aléatoire)
    private static readonly IReadOnlyDictionary<char, int> TestDistribution =
        new Dictionary<char, int>
        {
            ['A'] = 9, ['L'] = 5, ['M'] = 3, ['I'] = 8,
            ['S'] = 6, ['Z'] = 1, ['T'] = 6, ['O'] = 6,
            ['N'] = 6, ['R'] = 6, ['*'] = 2
        };

    private static GameEngine CreateEngine() =>
        new(Dictionary, LetterScores, TestDistribution);

    #region InitializeGame

    [Fact]
    public void InitializeGame_WithTwoPlayers_CreatesCorrectState()
    {
        var engine = CreateEngine();

        engine.InitializeGame(["Alice", "Bob"], 0);
        var state = engine.GetGameState();

        state.Should().NotBeNull();
        state.Players.Should().HaveCount(2);
        state.Players[0].Name.Should().Be("Alice");
        state.Players[1].Name.Should().Be("Bob");
        state.CurrentPlayerIndex.Should().Be(0);
        state.TurnNumber.Should().Be(1);
        state.IsGameOver.Should().BeFalse();
    }

    [Fact]
    public void InitializeGame_EachPlayerReceives7Letters()
    {
        var engine = CreateEngine();

        engine.InitializeGame(["Alice", "Bob"], 0);
        var state = engine.GetGameState();

        state.Players[0].Rack.Should().HaveCount(7,
            because: "chaque joueur démarre avec 7 lettres dans son rack");
        state.Players[1].Rack.Should().HaveCount(7);
    }

    [Fact]
    public void InitializeGame_BoardIsEmpty()
    {
        var engine = CreateEngine();

        engine.InitializeGame(["Alice", "Bob"], 0);
        var state = engine.GetGameState();

        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
                state.Board.Grid[r, c].Should().BeNull(
                    because: "le plateau est vide en début de partie");
    }

    [Fact]
    public void InitializeGame_WithThreePlayers_Works()
    {
        var engine = CreateEngine();

        engine.InitializeGame(["Alice", "Bob", "Charlie"], 0);
        var state = engine.GetGameState();

        state.Players.Should().HaveCount(3);
        state.Players.Should().AllSatisfy(p =>
            p.Rack.Should().HaveCount(7));
    }

    [Fact]
    public void InitializeGame_WithEmptyPlayerList_ThrowsException()
    {
        var engine = CreateEngine();

        var act = () => engine.InitializeGame([], 0);

        act.Should().Throw<GameException>(
            because: "une liste vide de joueurs est toujours invalide");
    }

    [Fact]
    public void InitializeGame_WithOnePlayer_Works()
    {
        // La contrainte '2 joueurs minimum' est dans Lama.Core (CreateGameUseCase),
        // pas dans le moteur. Le moteur accepte 1 joueur (cas de création de partie
        // avant que d'autres joueurs ne rejoignent).
        var engine = CreateEngine();

        engine.InitializeGame(["Solo"], 0);
        var state = engine.GetGameState();

        state.Players.Should().HaveCount(1);
        state.Players[0].Name.Should().Be("Solo");
        state.Players[0].Rack.Should().HaveCount(7);
    }

    [Fact]
    public void InitializeGame_AllPlayersStartWithZeroScore()
    {
        var engine = CreateEngine();

        engine.InitializeGame(["Alice", "Bob"], 0);
        var state = engine.GetGameState();

        state.Players.Should().AllSatisfy(p =>
            p.Score.Should().Be(0,
                because: "tous les joueurs commencent avec 0 point"));
    }

    #endregion

    #region GetCurrentPlayer

    [Fact]
    public void GetCurrentPlayer_ReturnsFirstPlayer_AfterInit()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        var current = engine.GetCurrentPlayer();

        current.Name.Should().Be("Alice");
    }

    [Fact]
    public void GetCurrentPlayer_ThrowsException_BeforeInit()
    {
        var engine = CreateEngine();

        var act = () => engine.GetCurrentPlayer();

        act.Should().Throw<GameException>(
            because: "GetCurrentPlayer nécessite une partie initialisée");
    }

    #endregion

    #region ValidateMove

    [Fact]
    public void ValidateMove_ValidFirstMove_ReturnsTrue()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // Premier coup : LA horizontal depuis H8 (7,7)
        var letters = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        };

        var (isValid, error, score) = engine.ValidateMove(letters);

        isValid.Should().BeTrue(because: "LA depuis H8 est un premier coup valide");
        error.Should().BeNullOrEmpty();
        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidateMove_NotThroughCenter_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // Premier coup loin de H8
        var letters = new Dictionary<Position, char>
        {
            [new Position(0, 0)] = 'L',
            [new Position(0, 1)] = 'A'
        };

        var (isValid, error, _) = engine.ValidateMove(letters);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateMove_ThrowsException_BeforeInit()
    {
        var engine = CreateEngine();

        var act = () => engine.ValidateMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L'
        });

        act.Should().Throw<GameException>();
    }

    [Fact]
    public void ValidateMove_EmptyMove_ReturnsFalse()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        var (isValid, error, score) = engine.ValidateMove(new Dictionary<Position, char>());

        isValid.Should().BeFalse();
        score.Should().Be(0);
    }

    #endregion

    #region PlayMove

    [Fact]
    public void PlayMove_ValidFirstMove_UpdatesBoardAndScore()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // On force le rack d'Alice à contenir L et A
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var letters = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        };

        var state = engine.PlayMove(letters);

        // Le plateau a les lettres posées
        state.Board.Grid[7, 7]!.Letter.Should().Be('L');
        state.Board.Grid[7, 8]!.Letter.Should().Be('A');

        // Alice a un score > 0
        state.Players[0].Score.Should().BeGreaterThan(0,
            because: "jouer LA depuis H8 (DW) doit rapporter des points");
    }

    [Fact]
    public void PlayMove_ValidMove_AdvancesToNextPlayer()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var letters = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        };

        var state = engine.PlayMove(letters);

        state.CurrentPlayerIndex.Should().Be(1,
            because: "après le coup d'Alice, c'est au tour de Bob");
    }

    [Fact]
    public void PlayMove_ValidMove_RefillsRack()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var letters = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        };

        var state = engine.PlayMove(letters);

        // Alice a joué 2 lettres et devrait avoir recompléte son rack
        state.Players[0].Rack.Should().HaveCount(7,
            because: "le rack se recharge automatiquement après un coup");
    }

    [Fact]
    public void PlayMove_InvalidMove_ThrowsGameException()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // Coup invalide : pas sur H8 pour le premier coup
        var letters = new Dictionary<Position, char>
        {
            [new Position(0, 0)] = 'L',
            [new Position(0, 1)] = 'A'
        };

        var act = () => engine.PlayMove(letters);

        act.Should().Throw<GameException>(
            because: "un coup invalide doit lever une GameException");
    }

    [Fact]
    public void PlayMove_LetterNotInRack_ThrowsGameException()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // Force un rack sans 'Z'
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        // Tente de jouer 'Z' qui n'est pas dans le rack
        var letters = new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'Z',
            [new Position(7, 8)] = 'A'
        };

        var act = () => engine.PlayMove(letters);

        act.Should().Throw<GameException>(
            because: "on ne peut pas jouer une lettre absente du rack");
    }

    [Fact]
    public void PlayMove_UsesWildcardFromRack_WhenLetterMissing()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // Pas de 'L' en rack, mais un joker '*'.
        ForceRack(engine, 0, ['*', 'A', 'M', 'I', 'S', 'T', 'O']);

        var state = engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        });

        state.Board.Grid[7, 7]!.Letter.Should().Be('L');
        state.Board.Grid[7, 7]!.IsWildcard.Should().BeTrue();
        state.Players[0].Score.Should().Be(2,
            because: "L joué via joker vaut 0, A vaut 1, mot doublé en H8 => 2 points");
    }

    [Fact]
    public void PlayMove_LowercaseLetter_ForcesWildcardEvenIfLetterExistsInRack()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        // Le rack contient L et * : la notation minuscule doit consommer * et garder L.
        ForceRack(engine, 0, ['L', '*', 'A', 'I', 'S', 'T', 'O']);

        var state = engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'l',
            [new Position(7, 8)] = 'A'
        });

        state.Board.Grid[7, 7]!.Letter.Should().Be('L');
        state.Board.Grid[7, 7]!.IsWildcard.Should().BeTrue();
        state.Players[0].Rack.Should().Contain('L',
            because: "la lettre L du rack ne doit pas etre consommee quand 'l' force le joker");
    }

    [Fact]
    public void PlayMove_CrossingExistingLetter_DoesNotRequireLetterInRack()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice"], 0);

        // Premier mot: LA en H8-I8
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);
        engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        });

        // Coup croisé: AME vertical depuis I8 (A déjà présent en I8).
        // Le rack ne contient pas de A: seules M et E doivent être consommées.
        ForceRack(engine, 0, ['M', 'E', 'T', 'O', 'N', 'S', 'R']);

        var state = engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 8)] = 'A',
            [new Position(8, 8)] = 'M',
            [new Position(9, 8)] = 'E'
        });

        state.Board.Grid[8, 8]!.Letter.Should().Be('M');
        state.Board.Grid[9, 8]!.Letter.Should().Be('E');
        state.Players[0].Rack.Should().HaveCount(7,
            because: "seules les nouvelles lettres posees doivent etre consommees puis repiochees");
    }

    [Fact]
    public void PlayMove_CrossingExistingWildcardTile_DoesNotCountWildcardPointsTwice()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice"], 0);

        // Premier coup: "lA" en H8-I8, avec joker force sur 'l'.
        // Score attendu premier coup: (0 + 1) x 2 = 2.
        ForceRack(engine, 0, ['*', 'A', 'M', 'I', 'S', 'T', 'O']);
        var first = engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'l',
            [new Position(7, 8)] = 'A'
        });
        first.Players[0].Score.Should().Be(2);

        // Coup suivant: vertical depuis H8, on croise la tuile joker existante ('L').
        // On joue "LA": seul le nouveau 'A' doit rapporter 1 point.
        ForceRack(engine, 0, ['A', 'B', 'C', 'D', 'E', 'F', 'G']);
        var second = engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(8, 7)] = 'A'
        });

        second.Players[0].Score.Should().Be(3,
            because: "la tuile existante issue d'un joker vaut 0 et le nouveau A vaut 1");
    }

    [Fact]
    public void PlayMove_IncrementsTurnNumber_AfterFullRound()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);
        ForceRack(engine, 1, ['M', 'A', 'S', 'I', 'T', 'O', 'N']);

        // Tour 1 : Alice joue LA
        engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        });

        // Tour 1 : Bob joue MA adjacent (7,9)-(7,10)... 
        // On passe simplement le tour de Bob pour simplifier
        engine.PassTurn();

        var state = engine.GetGameState();

        state.TurnNumber.Should().Be(2,
            because: "le numéro de tour s'incrémente après un tour complet (tous les joueurs ont joué)");
    }

    #endregion

    #region PassTurn

    [Fact]
    public void PassTurn_AdvancesToNextPlayer()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        engine.PassTurn();
        var state = engine.GetGameState();

        state.CurrentPlayerIndex.Should().Be(1,
            because: "passer son tour avance au joueur suivant");
    }

    [Fact]
    public void PassTurn_WrapsAroundToFirstPlayer()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        engine.PassTurn(); // → Bob
        engine.PassTurn(); // → Alice (wraparound)

        var state = engine.GetGameState();

        state.CurrentPlayerIndex.Should().Be(0,
            because: "après le dernier joueur, on revient au premier");
    }

    [Fact]
    public void PassTurn_ThrowsException_BeforeInit()
    {
        var engine = CreateEngine();

        var act = () => engine.PassTurn();

        act.Should().Throw<GameException>();
    }

    [Fact]
    public void PassTurn_DoesNotModifyBoard()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        engine.PassTurn();
        var state = engine.GetGameState();

        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
                state.Board.Grid[r, c].Should().BeNull(
                    because: "passer son tour ne modifie pas le plateau");
    }

    [Fact]
    public void PassTurn_DoesNotChangeScore()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        engine.PassTurn();
        var state = engine.GetGameState();

        state.Players[0].Score.Should().Be(0,
            because: "passer son tour ne rapporte aucun point");
    }

    #endregion

    #region SwapLetters

    [Fact]
    public void SwapLetters_ValidLetters_KeepsRackSizeAndAdvancesPlayer()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        engine.SwapLetters(['L', 'A']);
        var state = engine.GetGameState();

        state.Players[0].Rack.Should().HaveCount(7,
            because: "un echange conserve la taille du rack");
        state.CurrentPlayerIndex.Should().Be(1,
            because: "un echange valide consomme le tour");
    }

    [Fact]
    public void SwapLetters_LetterNotInRack_ThrowsGameException()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        var act = () => engine.SwapLetters(['Z']);

        act.Should().Throw<GameException>(
            because: "on ne peut pas echanger une lettre absente du rack");
    }

    [Fact]
    public void SwapLetters_EmptySelection_ThrowsGameException()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        var act = () => engine.SwapLetters([]);

        act.Should().Throw<GameException>(
            because: "un echange sans lettre n'est pas valide");
    }

    [Fact]
    public void SwapLetters_ThrowsException_BeforeInit()
    {
        var engine = CreateEngine();

        var act = () => engine.SwapLetters(['A']);

        act.Should().Throw<GameException>();
    }

    #endregion

    #region CreatePlayerRack

    [Fact]
    public void CreatePlayerRack_Returns7Letters_ByDefault()
    {
        var engine = CreateEngine();

        var rack = engine.CreatePlayerRack();

        rack.Should().HaveCount(7,
            because: "le rack standard contient 7 lettres");
    }

    [Fact]
    public void CreatePlayerRack_ReturnsRequestedCount()
    {
        var engine = CreateEngine();

        var rack = engine.CreatePlayerRack(size: 5);

        rack.Should().HaveCount(5);
    }

    [Fact]
    public void CreatePlayerRack_ReturnsOnlyValidLetters()
    {
        var engine = CreateEngine();

        var rack = engine.CreatePlayerRack();

        rack.Should().OnlyContain(c => TestDistribution.ContainsKey(c),
            because: "les lettres du rack doivent venir de la distribution configurée");
    }

    #endregion

    #region EndGame

    [Fact]
    public void EndGame_SetsIsGameOverToTrue()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        engine.EndGame();
        var state = engine.GetGameState();

        state.IsGameOver.Should().BeTrue();
    }

    [Fact]
    public void EndGame_ThrowsException_BeforeInit()
    {
        var engine = CreateEngine();

        var act = () => engine.EndGame();

        act.Should().Throw<GameException>();
    }

    [Fact]
    public void EndGame_PreventsPlayMove()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);
        ForceRack(engine, 0, ['L', 'A', 'M', 'I', 'S', 'T', 'O']);

        engine.EndGame();

        var act = () => engine.PlayMove(new Dictionary<Position, char>
        {
            [new Position(7, 7)] = 'L',
            [new Position(7, 8)] = 'A'
        });

        act.Should().Throw<GameException>(
            because: "on ne peut pas jouer dans une partie terminée");
    }

    [Fact]
    public void EndGame_PreventPassTurn()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        engine.EndGame();

        var act = () => engine.PassTurn();

        act.Should().Throw<GameException>(
            because: "on ne peut pas passer son tour dans une partie terminée");
    }

    #endregion

    #region GetGameState

    [Fact]
    public void GetGameState_ThrowsException_BeforeInit()
    {
        var engine = CreateEngine();

        var act = () => engine.GetGameState();

        act.Should().Throw<GameException>(
            because: "GetGameState nécessite une partie initialisée");
    }

    [Fact]
    public void GetGameState_ReturnsImmutableCopy()
    {
        var engine = CreateEngine();
        engine.InitializeGame(["Alice", "Bob"], 0);

        var state1 = engine.GetGameState();
        engine.PassTurn();
        var state2 = engine.GetGameState();

        state1.CurrentPlayerIndex.Should().Be(0,
            because: "GetGameState retourne un snapshot — l'ancien état ne doit pas changer");
        state2.CurrentPlayerIndex.Should().Be(1);
    }

    #endregion

    // ── Helper privé ──────────────────────────────────────────────────────────

    /// <summary>
    /// Force le rack d'un joueur à une liste de lettres spécifiques.
    /// Utilisé pour rendre les tests déterministes.
    /// </summary>
    private static void ForceRack(GameEngine engine, int playerIndex, char[] letters)
    {
        engine.ForceRackForTest(playerIndex, letters);
    }
}
