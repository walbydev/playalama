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
            IsGameOver         = _isGameOver
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

        var score = _scoreCalculator.Calculate(letters, _board!);
        return (true, string.Empty, score);
    }

    /// <inheritdoc />
    public GameState PlayMove(Dictionary<Position, char> letters)
    {
        EnsureInitialized();
        EnsureNotGameOver();

        // 1. Vérifier que les lettres sont dans le rack du joueur courant
        EnsureLettersInRack(letters);

        // 2. Valider le coup
        var (isValid, errorMessage, score) = ValidateMove(letters);
        if (!isValid)
            throw new GameException(errorMessage);

        // 3. Appliquer le coup sur le plateau
        var newGrid = (Tile?[,])_board!.Grid.Clone();
        foreach (var (pos, letter) in letters)
            newGrid[pos.Row, pos.Column] = new Tile(letter);
        _board = new BoardState(newGrid);

        // 4. Mettre à jour le score du joueur courant
        _players![_currentPlayerIndex] = _players[_currentPlayerIndex] with
        {
            Score = _players[_currentPlayerIndex].Score + score
        };

        // 5. Retirer les lettres jouées du rack et recompléter depuis le sac
        var player  = _players[_currentPlayerIndex];
        var newRack = new List<char>(player.Rack);
        foreach (var letter in letters.Values)
            newRack.Remove(letter);

        var refill = _bag!.Draw(letters.Count);
        newRack.AddRange(refill);
        _players[_currentPlayerIndex] = player with { Rack = newRack };

        // 6. Le premier coup est joué
        _isFirstMove = false;

        // 7. Vérifier si la partie est terminée (sac vide + rack vide)
        if (_bag.IsEmpty && _players[_currentPlayerIndex].Rack.Count == 0)
        {
            _isGameOver = true;
        }

        // 8. Passer au joueur suivant
        AdvancePlayer();

        return GetGameState();
    }

    /// <inheritdoc />
    public void PassTurn()
    {
        EnsureInitialized();
        EnsureNotGameOver();

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

        AdvancePlayer();
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
    /// Retourne les lettres restantes dans le sac (sans les consommer).
    /// Utilisé par <c>CreateGameUseCase</c> pour la persistance.
    /// </summary>
    internal List<char> GetRemainingTiles()
    {
        if (_bag is null) return [];
        // On pioche tout puis on remet tout — lecture non destructive
        var tiles = _bag.Draw(_bag.Count);
        _bag.ReturnTiles(tiles);
        return new List<char>(tiles);
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

    private void EnsureLettersInRack(Dictionary<Position, char> letters)
    {
        var rack = new List<char>(_players![_currentPlayerIndex].Rack);
        foreach (var letter in letters.Values)
        {
            if (!rack.Remove(letter))
                throw new GameException(
                    $"La lettre '{letter}' n'est pas dans le rack du joueur courant.");
        }
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
