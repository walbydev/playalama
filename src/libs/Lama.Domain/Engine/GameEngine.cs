using Lama.Contracts;
using Lama.Domain.Bag;
using Lama.Domain.Scoring;
using Lama.Domain.Validation;

namespace Lama.Domain.Engine;

/// <summary>
/// Moteur de jeu LAMA — implémente <see cref="IGameEngine"/>.
///
/// Orchestre :
/// <list type="bullet">
///   <item><see cref="TileBag"/> — gestion du sac de lettres</item>
///   <item><see cref="MoveValidator"/> — validation des coups</item>
///   <item><see cref="ScoreCalculator"/> — calcul des scores</item>
/// </list>
///
/// Contient l'état mutable de la partie. <see cref="GetGameState"/> retourne
/// un snapshot immuable pour les couches supérieures.
/// </summary>
public sealed class GameEngine : IGameEngine
{
    private const int RackSize    = 7;
    private const int MinPlayers  = 1; // La contrainte 2 joueurs min est dans Lama.Core

    private readonly MoveValidator   _moveValidator;
    private readonly ScoreCalculator _scoreCalculator;
    private readonly IReadOnlyDictionary<char, int> _tileDistribution;

    // ── État mutable de la partie ─────────────────────────────────────────────
    private TileBag?        _bag;
    private BoardState?     _board;
    private List<PlayerState>? _players;
    private int             _currentPlayerIndex;
    private int             _turnNumber;
    private bool            _isGameOver;
    private bool            _isInitialized;
    private bool            _isFirstMove;
    private List<GameMove>  _history = [];
    private GameStateSnapshot? _lastMoveSnapshot;

    /// <summary>
    /// Initialise le moteur avec le dictionnaire, les scores et la distribution.
    /// </summary>
    public GameEngine(
        IReadOnlySet<string> dictionary,
        IReadOnlyDictionary<char, int> letterScores,
        IReadOnlyDictionary<char, int> tileDistribution)
    {
        _moveValidator    = new MoveValidator(dictionary);
        _scoreCalculator  = new ScoreCalculator(letterScores);
        _tileDistribution = tileDistribution;
    }

    // ── IGameEngine ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void InitializeGame(List<string> playerNames)
    {
        if (playerNames.Count < MinPlayers)
            throw new GameException(
                $"Une partie nécessite au moins {MinPlayers} joueurs. " +
                $"Reçu : {playerNames.Count}.");

        _bag   = new TileBag(_tileDistribution);
        _board = new BoardState();

        _players = playerNames
            .Select(name => new PlayerState(name, 0, _bag.Draw(RackSize)))
            .ToList();

        _currentPlayerIndex = 0;
        _turnNumber         = 1;
        _isGameOver         = false;
        _isFirstMove        = true;
        _history            = [];
        _lastMoveSnapshot   = null;
        _isInitialized      = true;
    }

    /// <inheritdoc />
    public GameState GetGameState()
    {
        EnsureInitialized();

        return new GameState
        {
            Board              = new BoardState(_board!.Grid),
            Players            = _players!
                .Select(p => new Player(p.Name, p.Score, new List<char>(p.Rack)))
                .ToList(),
            CurrentPlayerIndex = _currentPlayerIndex,
            TurnNumber         = _turnNumber,
            IsGameOver         = _isGameOver,
            History            = _history
                .Select(move => move with { Placements = move.Placements.ToList() })
                .ToList()
        };
    }

    /// <inheritdoc />
    public Player GetCurrentPlayer()
    {
        EnsureInitialized();
        var p = _players![_currentPlayerIndex];
        return new Player(p.Name, p.Score, new List<char>(p.Rack));
    }

    /// <inheritdoc />
    public (bool IsValid, string ErrorMessage, int Score) ValidateMove(
        Dictionary<Position, char> letters)
    {
        EnsureInitialized();

        if (letters.Count == 0)
            return (false, "Le coup ne peut pas être vide.", 0);

        var result = _moveValidator.Validate(letters, _board!, _isFirstMove);
        if (!result.IsValid)
            return (false, result.ErrorMessage ?? "Coup invalide.", 0);

        var isHorizontal = letters.Keys.Select(p => p.Row).Distinct().Count() == 1;
        var score = _scoreCalculator.CalculateTotal(letters, _board!, null, isHorizontal);
        return (true, string.Empty, score);
    }

    /// <inheritdoc />
    public GameState PlayMove(Dictionary<Position, char> letters)
    {
        EnsureInitialized();
        EnsureNotGameOver();

        // 1. Valider le coup (avant toute mutation du rack)
        var result = _moveValidator.Validate(letters, _board!, _isFirstMove);
        if (!result.IsValid)
            throw new GameException(result.ErrorMessage ?? "Coup invalide.");

        // 2. Isoler les tuiles réellement nouvelles (les croisements existants ne consomment pas le rack)
        var newPlacements = letters
            .Where(kv => _board!.Grid[kv.Key.Row, kv.Key.Column] is null)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // 3. Vérifier que les nouvelles lettres sont bien dans le rack du joueur courant
        var wildcardPositions = ConsumeLettersFromRack(newPlacements);

        var isHorizontal = letters.Keys.Select(p => p.Row).Distinct().Count() == 1;
        var score = _scoreCalculator.CalculateTotal(letters, _board!, wildcardPositions, isHorizontal);

        // 2b. Mémoriser l'état précédent pour permettre un challenge
        _lastMoveSnapshot = CaptureSnapshot();

        // 4. Appliquer les nouvelles lettres sur le plateau
        var newGrid = (Tile?[,])_board!.Grid.Clone();
        foreach (var (pos, letter) in newPlacements)
            newGrid[pos.Row, pos.Column] =
                new Tile(char.ToUpperInvariant(letter), wildcardPositions.Contains(pos));
        _board = new BoardState(newGrid);

        // 5. Mettre à jour le score du joueur courant
        _players![_currentPlayerIndex] = _players[_currentPlayerIndex] with
        {
            Score = _players[_currentPlayerIndex].Score + score
        };

        // 6. Recompléter le rack selon le nombre réel de nouvelles tuiles posées
        var player  = _players[_currentPlayerIndex];
        var newRack = new List<char>(player.Rack);

        var refill = _bag!.Draw(newPlacements.Count);
        newRack.AddRange(refill);
        _players[_currentPlayerIndex] = player with { Rack = newRack };

        // 7. Le premier coup est joué
        _isFirstMove = false;

        // 8. Vérifier si la partie est terminée (sac vide + rack vide)
        if (_bag.IsEmpty && _players[_currentPlayerIndex].Rack.Count == 0)
        {
            _isGameOver = true;
        }

        var playedTurn = _turnNumber;

        // 9. Passer au joueur suivant
        AdvancePlayer();

        _history.Add(new GameMove(
            TurnNumber: playedTurn,
            PlayerId: string.Empty,
            PlayerName: player.Name,
            Placements: newPlacements
                .OrderBy(kv => kv.Key.Row)
                .ThenBy(kv => kv.Key.Column)
                .Select(kv => new MovePlacement(kv.Key.Row, kv.Key.Column, char.ToUpperInvariant(kv.Value)))
                .ToList(),
            Score: score,
            PlayedAt: DateTimeOffset.UtcNow));

        return GetGameState();
    }

    /// <inheritdoc />
    public void PassTurn()
    {
        EnsureInitialized();
        EnsureNotGameOver();

        _lastMoveSnapshot = null;
        AdvancePlayer();
    }

    /// <inheritdoc />
    public void SwapLetters(IReadOnlyList<char> lettersToSwap)
    {
        EnsureInitialized();
        EnsureNotGameOver();

        if (lettersToSwap.Count == 0)
            throw new GameException("Au moins une lettre doit etre fournie pour l'echange.");

        if (_bag is null)
            throw new GameException("Le sac de lettres est indisponible.");

        if (_bag.Count < lettersToSwap.Count)
            throw new GameException("Le sac ne contient pas assez de tuiles pour cet echange.");

        var player = _players![_currentPlayerIndex];
        var newRack = new List<char>(player.Rack);

        foreach (var letter in lettersToSwap)
        {
            if (!newRack.Remove(letter))
                throw new GameException(
                    $"La lettre '{letter}' n'est pas dans le rack du joueur courant.");
        }

        var replacements = _bag.Swap(lettersToSwap);
        if (replacements.Count != lettersToSwap.Count)
            throw new GameException("Echange impossible: le sac n'a pas pu fournir assez de lettres.");

        newRack.AddRange(replacements);
        _players[_currentPlayerIndex] = player with { Rack = newRack };

        _lastMoveSnapshot = null;

        AdvancePlayer();
    }

    /// <inheritdoc />
    public ChallengeResult ChallengeLastMove()
    {
        EnsureInitialized();
        EnsureNotGameOver();

        if (_lastMoveSnapshot is null || _history.Count == 0)
            throw new GameException("Aucun dernier coup contestable n'est disponible.");

        var challengerIndex = _currentPlayerIndex;
        var currentTurn = _turnNumber;
        var lastMove = _history[^1];
        var challengeMove = lastMove.Placements.ToDictionary(
            p => new Position(p.Row, p.Column),
            p => p.Letter);

        var validation = _moveValidator.Validate(
            challengeMove,
            BuildBoardFromSnapshot(_lastMoveSnapshot.Board),
            _lastMoveSnapshot.IsFirstMove);

        if (!validation.IsValid)
        {
            RestoreSnapshot(_lastMoveSnapshot);
            _currentPlayerIndex = challengerIndex;
            _turnNumber = currentTurn;
            _history.RemoveAt(_history.Count - 1);
            _lastMoveSnapshot = null;

            return new ChallengeResult(
                ChallengeSucceeded: true,
                Message: "Challenge réussi : le mot était invalide et a été annulé.",
                ChallengedMove: lastMove,
                GameState: GetGameState());
        }

        _lastMoveSnapshot = null;
        AdvancePlayer();

        return new ChallengeResult(
            ChallengeSucceeded: false,
            Message: "Challenge raté : le mot était valide.",
            ChallengedMove: lastMove,
            GameState: GetGameState());
    }

    /// <inheritdoc />
    public List<char> CreatePlayerRack(int size = 7)
    {
        // Si le moteur est initialisé, pioche depuis le sac en cours
        if (_isInitialized && _bag is not null)
            return _bag.Draw(size);

        // Sinon, crée un sac temporaire
        var tempBag = new TileBag(_tileDistribution);
        return tempBag.Draw(size);
    }

    /// <inheritdoc />
    public void EndGame()
    {
        EnsureInitialized();
        _isGameOver = true;
    }

    // ── Méthode interne pour les tests ───────────────────────────────────────

    /// <summary>
    /// Force le rack d'un joueur à une liste de lettres spécifiques.
    /// Réservé aux tests unitaires (internal + InternalsVisibleTo).
    /// </summary>
    internal void ForceRackForTest(int playerIndex, char[] letters)
    {
        EnsureInitialized();
        _players![playerIndex] = _players[playerIndex] with
        {
            Rack = new List<char>(letters)
        };
    }

    /// <summary>
    /// Restaure l'état complet du moteur depuis la persistance.
    /// Appelé par <c>CreateGameUseCase</c> lors de la reconstruction après un redémarrage.
    /// </summary>
    internal void RestoreState(
        int currentPlayerIndex,
        int turnNumber,
        bool isFirstMove,
        bool isGameOver,
        List<int> scores)
    {
        EnsureInitialized();
        _currentPlayerIndex = currentPlayerIndex;
        _turnNumber         = turnNumber;
        _isFirstMove        = isFirstMove;
        _isGameOver         = isGameOver;

        for (var i = 0; i < Math.Min(scores.Count, _players!.Count); i++)
            _players[i] = _players[i] with { Score = scores[i] };
    }

    /// <summary>
    /// Restaure le plateau depuis un dictionnaire de tuiles persistées.
    /// Appelé par <c>CreateGameUseCase</c> lors de la reconstruction.
    /// </summary>
    internal void RestoreBoard(IReadOnlyDictionary<Position, char> tiles)
    {
        EnsureInitialized();
        var newGrid = new Tile?[15, 15];
        foreach (var (pos, letter) in tiles)
            newGrid[pos.Row, pos.Column] = new Tile(letter);
        _board = new BoardState(newGrid);
    }

    /// <summary>
    /// Restaure le plateau depuis les tuiles persistées en conservant l'information joker.
    /// </summary>
    internal void RestoreBoardTiles(IEnumerable<PersistedTile> tiles)
    {
        EnsureInitialized();
        var newGrid = new Tile?[15, 15];
        foreach (var tile in tiles)
            newGrid[tile.Row, tile.Col] = new Tile(tile.Letter, tile.IsWildcard);
        _board = new BoardState(newGrid);
    }

    /// <summary>
    /// Restaure l'historique et le dernier snapshot contestable.
    /// </summary>
    internal void RestoreHistory(List<GameMove> history, GameStateSnapshot? lastMoveSnapshot)
    {
        EnsureInitialized();
        _history = history
            .Select(move => move with { Placements = move.Placements.ToList() })
            .ToList();
        _lastMoveSnapshot = lastMoveSnapshot;
    }

    /// <summary>
    /// Retourne le snapshot du dernier coup contestable.
    /// </summary>
    internal GameStateSnapshot? GetLastMoveSnapshot() => _lastMoveSnapshot;

    /// <summary>Retourne le nombre de tuiles restantes dans le sac.</summary>
    public int GetBagCount() => _bag?.Count ?? 0;

    /// <summary>
    /// Retourne les lettres restantes dans le sac (sans les consommer).
    /// Utilisé par <c>CreateGameUseCase</c> pour la persistance.
    /// </summary>
    internal List<char> GetRemainingTiles()
    {
        if (_bag is null) return [];
        return _bag.SnapshotTiles();
    }

    private GameStateSnapshot CaptureSnapshot()
    {
        return new GameStateSnapshot(
            IsFirstMove: _isFirstMove,
            IsGameOver: _isGameOver,
            CurrentPlayerIndex: _currentPlayerIndex,
            TurnNumber: _turnNumber,
            Players: _players!
                .Select((p, i) => new PersistedPlayer(
                    PlayerId: i.ToString(),
                    Name: p.Name,
                    Score: p.Score,
                    Rack: new List<char>(p.Rack)))
                .ToList(),
            Board: CaptureBoard(),
            RemainingTiles: _bag!.SnapshotTiles());
    }

    private void RestoreSnapshot(GameStateSnapshot snapshot)
    {
        _isFirstMove = snapshot.IsFirstMove;
        _isGameOver = snapshot.IsGameOver;
        _currentPlayerIndex = snapshot.CurrentPlayerIndex;
        _turnNumber = snapshot.TurnNumber;
        _players = snapshot.Players
            .Select(p => new PlayerState(p.Name, p.Score, new List<char>(p.Rack)))
            .ToList();
        _board = BuildBoardFromSnapshot(snapshot.Board);
        _bag!.RestoreTiles(snapshot.RemainingTiles);
    }

    private List<PersistedTile> CaptureBoard()
    {
        var tiles = new List<PersistedTile>();
        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
            {
                var tile = _board!.Grid[r, c];
                if (tile is not null)
                    tiles.Add(new PersistedTile(r, c, tile.Letter, tile.IsWildcard));
            }

        return tiles;
    }

    private static BoardState BuildBoardFromSnapshot(IEnumerable<PersistedTile> tiles)
    {
        var newGrid = new Tile?[15, 15];
        foreach (var tile in tiles)
            newGrid[tile.Row, tile.Col] = new Tile(tile.Letter, tile.IsWildcard);
        return new BoardState(newGrid);
    }

    // ── Helpers privés ────────────────────────────────────────────────────────

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new GameException(
                "La partie n'a pas encore été initialisée. " +
                "Appelez InitializeGame() d'abord.");
    }

    private void EnsureNotGameOver()
    {
        if (_isGameOver)
            throw new GameException("La partie est terminée.");
    }

    private HashSet<Position> ConsumeLettersFromRack(Dictionary<Position, char> letters)
    {
        var rack = new List<char>(_players![_currentPlayerIndex].Rack);
        var wildcardPositions = new HashSet<Position>();

        foreach (var (pos, letter) in letters)
        {
            // Convention CLI: une lettre minuscule force l'usage d'un joker.
            if (char.IsLower(letter))
            {
                if (!rack.Remove('*'))
                    throw new GameException(
                        $"La lettre '{char.ToUpperInvariant(letter)}' requiert un joker '*' dans le rack.");

                wildcardPositions.Add(pos);
                continue;
            }

            var normalized = char.ToUpperInvariant(letter);

            if (rack.Remove(normalized))
                continue;

            if (rack.Remove('*'))
            {
                wildcardPositions.Add(pos);
                continue;
            }

            throw new GameException(
                $"La lettre '{normalized}' n'est pas dans le rack du joueur courant.");
        }

        _players[_currentPlayerIndex] = _players[_currentPlayerIndex] with { Rack = rack };
        return wildcardPositions;
    }

    private void AdvancePlayer()
    {
        var previousIndex = _currentPlayerIndex;
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _players!.Count;

        // Incrémenter le tour quand on revient au premier joueur
        if (_currentPlayerIndex == 0 && previousIndex != 0)
            _turnNumber++;
    }

    // ── État interne des joueurs ──────────────────────────────────────────────

    /// <summary>
    /// Représentation interne mutable d'un joueur.
    /// Distincte de <see cref="Player"/> (record Contracts immuable).
    /// </summary>
    private record PlayerState(string Name, int Score, List<char> Rack);
}
