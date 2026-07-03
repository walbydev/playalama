using Lama.Contracts;
using Lama.Core.Models;
using Lama.Domain.Engine;

namespace Lama.Core.UseCases;

/// <summary>
/// Cas d'usage : créer une nouvelle partie.
///
/// Responsabilités :
/// - Valider la requête (nom de l'hôte non vide)
/// - Créer un <see cref="GameEngine"/> avec les paramètres de langue
/// - Initialiser la partie avec l'hôte comme premier joueur
/// - Persister la partie via <see cref="IGameRepository"/>
/// - Maintenir un cache en mémoire pour les appels suivants du même processus
/// - Retourner le GameId, l'ID de l'hôte et l'état initial
///
/// Architecture : cache mémoire + persistance JSON.
/// Si le moteur n'est pas en cache, il est reconstruit depuis le repository.
/// Cela permet au mode commande par commande de fonctionner entre les processus.
/// </summary>
public sealed class CreateGameUseCase
{
    private readonly IReadOnlySet<string>           _dictionary;
    private readonly IReadOnlyDictionary<char, int> _letterScores;
    private readonly IReadOnlyDictionary<char, int> _tileDistribution;
    private readonly IGameLanguageProvider?          _languageProvider;
    private readonly IGameRepository                _repository;

    // Cache mémoire : GameId → GameSession
    private readonly Dictionary<string, GameSession> _sessions = new();

    /// <summary>Initialise le cas d'usage avec les données de langue.</summary>
    public CreateGameUseCase(
        IReadOnlySet<string>           dictionary,
        IReadOnlyDictionary<char, int> letterScores,
        IReadOnlyDictionary<char, int> tileDistribution,
        IGameRepository                repository)
    {
        _dictionary       = dictionary;
        _letterScores     = letterScores;
        _tileDistribution = tileDistribution;
        _repository       = repository;
    }

    /// <summary>
    /// Initialise le cas d'usage avec un provider de langue pour une distribution adaptable.
    /// </summary>
    public CreateGameUseCase(
        IGameLanguageProvider languageProvider,
        IGameRepository repository)
    {
        _languageProvider = languageProvider;
        _dictionary = languageProvider.GetDictionary();
        _letterScores = languageProvider.GetLetterScores();
        _tileDistribution = languageProvider.GetTileDistribution();
        _repository = repository;
    }

    /// <summary>Exécute le cas d'usage de création de partie.</summary>
    public Task<CreateGameResponse> ExecuteAsync(CreateGameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.HostPlayerName))
            throw new GameException("Le nom de l'hôte ne peut pas être vide.");

        var gameId = Guid.NewGuid().ToString("N");
        var hostId = Guid.NewGuid().ToString("N");

        var tileDistribution = ResolveTileDistribution(request);
        var engine = new GameEngine(_dictionary, _letterScores, tileDistribution);
        engine.InitializeGame([request.HostPlayerName]);
        engine.SetTimeConfig(request.TimePerPlayerSeconds);

        var state   = engine.GetGameState();
        var session = new GameSession(
            engine,
            new Dictionary<string, int> { [hostId] = 0 });

        _sessions[gameId] = session;

        // Persister immédiatement
        _repository.Save(BuildPersistedGame(gameId, request.Language, request.GameLevel,
            session, engine, isFirstMove: true, timePerPlayerSeconds: request.TimePerPlayerSeconds));

        return Task.FromResult(new CreateGameResponse(gameId, hostId, state));
    }

    /// <summary>
    /// Retourne le moteur de jeu associé à un GameId.
    /// Cherche d'abord en cache mémoire, puis reconstruit depuis le repository si absent.
    /// Retourne null si la partie n'existe pas du tout.
    /// </summary>
    public GameEngine? GetEngine(string gameId)
    {
        // Cache hit
        if (_sessions.TryGetValue(gameId, out var session))
            return session.Engine;

        // Cache miss → reconstruire depuis le repository
        return RestoreFromRepository(gameId)?.Engine;
    }

    /// <summary>Persiste l'état courant de la partie.</summary>
    public void SaveGame(string gameId, bool isFirstMove = false)
    {
        var session = RequireSession(gameId);
        var engine  = session.Engine;
        var state = engine.GetGameState();
        var persisted = BuildPersistedGame(gameId, "fr", GameLevel.Standard,
            session, engine, isFirstMove, state.TimePerPlayerSeconds);
        _repository.Save(persisted);
    }

    /// <summary>Sauvegarde avec le GameLevel correct.</summary>
    public void SaveGame(string gameId, GameLevel level, bool isFirstMove)
    {
        var session   = RequireSession(gameId);
        var state = session.Engine.GetGameState();
        var persisted = BuildPersistedGame(gameId, "fr", level,
            session, session.Engine, isFirstMove, state.TimePerPlayerSeconds);
        _repository.Save(persisted);
    }

    /// <summary>Supprime la partie du repository (fin de partie).</summary>
    public void DeleteGame(string gameId)
    {
        _sessions.Remove(gameId);
        _repository.Delete(gameId);
    }

    /// <summary>
    /// Vide le cache mémoire des parties actives.
    /// Les données persistées restent intactes et pourront être rechargées.
    /// </summary>
    public int ResetInMemorySessions()
    {
        var count = _sessions.Count;
        _sessions.Clear();
        return count;
    }

    /// <summary>
    /// Retourne la session complète associée à un GameId.
    /// Reconstruit depuis le repository si absente du cache.
    /// </summary>
    internal GameSession? GetSession(string gameId)
    {
        if (_sessions.TryGetValue(gameId, out var session))
            return session;
        return RestoreFromRepository(gameId);
    }

    /// <summary>
    /// Retourne l'index du joueur correspondant à un PlayerId.
    /// </summary>
    internal int GetPlayerIndex(string gameId, string playerId)
    {
        var session = RequireSession(gameId);
        if (!session.PlayerIndexById.TryGetValue(playerId, out var index))
            throw new GameException($"Joueur inconnu : '{playerId}'.");
        return index;
    }

    /// <summary>Ajoute un joueur à la session.</summary>
    internal int AddPlayer(string gameId, string playerId)
    {
        var session = RequireSession(gameId);
        var index   = session.PlayerIndexById.Count;
        session.PlayerIndexById[playerId] = index;
        return index;
    }

    /// <summary>Retourne la session ou lève une exception.</summary>
    internal GameSession RequireSession(string gameId)
    {
        var session = GetSession(gameId);
        if (session is null)
            throw new GameException($"Partie introuvable : '{gameId}'.");
        return session;
    }

    // ── Helpers privés ────────────────────────────────────────────────────────

    /// <summary>
    /// Reconstruit un GameEngine depuis une partie persistée et l'ajoute au cache.
    /// </summary>
    private GameSession? RestoreFromRepository(string gameId)
    {
        var persisted = _repository.Load(gameId);
        if (persisted is null) return null;

        // Reconstruire le moteur avec l'état persisté
        var engine = new GameEngine(_dictionary, _letterScores, _tileDistribution);

        // Reconstruire la liste des joueurs pour InitializeGame
        var playerNames = persisted.Players.Select(p => p.Name).ToList();
        engine.InitializeGame(playerNames);

        // Restaurer les racks des joueurs
        for (var i = 0; i < persisted.Players.Count; i++)
            engine.ForceRackForTest(i, [.. persisted.Players[i].Rack]);

        // Restaurer le plateau
        RestoreBoard(engine, persisted);

        // Restaurer l'état du moteur
        engine.RestoreState(
            persisted.CurrentPlayerIndex,
            persisted.TurnNumber,
            persisted.IsFirstMove,
            persisted.IsGameOver,
            persisted.Players.Select(p => p.Score).ToList());

        // Restaurer les données de timer
        engine.RestoreTimeState(
            persisted.TimePerPlayerSeconds,
            persisted.PlayerTimeUsed,
            persisted.TurnStartAt,
            persisted.ForfeitedPlayerIndex);

        // Restaurer l'historique + snapshot de challenge
        engine.RestoreHistory(persisted.History, persisted.LastMoveSnapshot);

        // Reconstruire le mapping PlayerId → index
        var playerIndexById = persisted.Players
            .Select((p, i) => (p.PlayerId, i))
            .ToDictionary(t => t.PlayerId, t => t.i);

        var session = new GameSession(engine, playerIndexById);
        _sessions[gameId] = session;

        return session;
    }

    /// <summary>
    /// Reconstruit le plateau à partir des tuiles persistées.
    /// </summary>
    private static void RestoreBoard(GameEngine engine, PersistedGame persisted)
    {
        if (persisted.Board.Count == 0) return;

        engine.RestoreBoardTiles(persisted.Board);
    }

    private IReadOnlyDictionary<char, int> ResolveTileDistribution(CreateGameRequest request)
    {
        if (_languageProvider is null)
            return _tileDistribution;

        var profile = new TileDistributionProfile(
            Language: string.IsNullOrWhiteSpace(request.Language) ? "fr" : request.Language,
            BoardSize: request.BoardSize,
            RackSize: request.RackSize,
            GameLevel: request.GameLevel,
            GameType: "classic");

        return _languageProvider.GetTileDistribution(profile);
    }

    /// <summary>Construit un <see cref="PersistedGame"/> depuis l'état courant.</summary>
    private static PersistedGame BuildPersistedGame(
        string gameId, string language, GameLevel level,
        GameSession session, GameEngine engine, bool isFirstMove,
        int? timePerPlayerSeconds = null)
    {
        var state = engine.GetGameState();

        var players = session.PlayerIndexById
            .Select(kv => new PersistedPlayer(
                kv.Key,
                state.Players[kv.Value].Name,
                state.Players[kv.Value].Score,
                new List<char>(state.Players[kv.Value].Rack)))
            .ToList();

        var board = new List<PersistedTile>();
        for (var r = 0; r < 15; r++)
            for (var c = 0; c < 15; c++)
            {
                var tile = state.Board.Grid[r, c];
                if (tile is not null)
                    board.Add(new PersistedTile(r, c, tile.Letter, tile.IsWildcard));
            }

        // Les tuiles restantes sont extraites du moteur
        var remaining = engine.GetRemainingTiles();

        return new PersistedGame(
            GameId:             gameId,
            Language:           language,
            GameLevel:          level,
            IsFirstMove:        isFirstMove,
            IsGameOver:         state.IsGameOver,
            CurrentPlayerIndex: state.CurrentPlayerIndex,
            TurnNumber:         state.TurnNumber,
            Players:            players,
            Board:              board,
            RemainingTiles:     remaining,
            History:            state.History.ToList(),
            LastMoveSnapshot:   engine.GetLastMoveSnapshot(),
            CreatedAt:          DateTimeOffset.UtcNow,
            UpdatedAt:          DateTimeOffset.UtcNow,
            TimePerPlayerSeconds: timePerPlayerSeconds ?? state.TimePerPlayerSeconds,
            PlayerTimeUsed:     new List<int>(state.PlayerTimeUsed),
            TurnStartAt:        state.TurnStartAt,
            ForfeitedPlayerIndex: state.ForfeitedPlayerIndex);
    }
}

/// <summary>Représentation interne d'une session de jeu en mémoire.</summary>
internal sealed class GameSession
{
    public GameEngine               Engine           { get; }
    public Dictionary<string, int>  PlayerIndexById  { get; }
    /// <summary>Indique qu'au moins une suggestion a été utilisée pendant la partie
    /// (l'Elo n'est alors pas approvisionné, hors mode Tournament).</summary>
    public bool                     SuggestionsUsed  { get; set; }

    public GameSession(
        GameEngine engine,
        Dictionary<string, int> playerIndexById)
    {
        Engine          = engine;
        PlayerIndexById = playerIndexById;
    }
}
