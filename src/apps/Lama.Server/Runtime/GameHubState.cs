using System.Collections.Concurrent;
using System.Threading.Channels;
using Lama.Contracts;
using Lama.Domain.Engine;
using Lama.Server.Contracts.Api;
using Microsoft.Extensions.Logging;

namespace Lama.Server.Runtime;

public sealed class GameHubState
{
    private readonly IGameLanguageProvider _languageProvider;
    private readonly ILanguageProviderRegistry? _registry;
    private readonly ConcurrentDictionary<string, OnlineGame> _games = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EventSubscribers> _subscribers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _activeGameByPlayerId = new(StringComparer.Ordinal);

    /// <summary>Mode maintenance : empêche la création de nouvelles parties.
    /// Les parties en cours ne sont pas affectées.</summary>
    public volatile bool IsDraining;

    public GameHubState(IGameLanguageProvider languageProvider, ILanguageProviderRegistry? registry = null, ILogger<GameHubState>? logger = null)
    {
        _languageProvider = languageProvider;
        _registry = registry;
    }

    public IGameEngine CreateEngine(TileDistributionProfile? profile = null, IReadOnlyList<string>? languages = null)
    {
        IGameLanguageProvider provider;
        if (languages is { Count: > 0 } && _registry is not null)
        {
            provider = _registry.GetProvider(languages);
        }
        else
        {
            provider = _languageProvider;
        }

        return new GameEngine(
            provider.GetDictionary(),
            provider.GetLetterScores(),
            profile is null
                ? provider.GetTileDistribution()
                : provider.GetTileDistribution(profile));
    }

    public void Create(OnlineGame game)
    {
        _games[game.Id] = game;
        _subscribers.TryAdd(game.Id, new EventSubscribers());
    }

    public bool Exists(string gameId) => _games.ContainsKey(gameId);

    public bool TryGet(string gameId, out OnlineGame game) => _games.TryGetValue(gameId, out game!);

    public IReadOnlyList<OnlineGame> ListGames() => _games.Values.ToList();

    /// <summary>
    /// Clôture et supprime toutes les parties actives de la mémoire.
    /// Retourne le nombre de parties supprimées.
    /// </summary>
    public int ClearAll()
    {
        var count = 0;
        foreach (var (gameId, game) in _games)
        {
            lock (game)
            {
                if (!game.IsClosed)
                {
                    game.IsClosed  = true;
                    game.EndReason = "admin_reset";
                    count++;
                }
            }

            // Notifie les clients SSE éventuellement connectés
            Publish(gameId, new ServerEvent("game.ended", new
            {
                gameId,
                endedAt = DateTimeOffset.UtcNow,
                reason  = "admin_reset"
            }));
        }

        _games.Clear();
        _subscribers.Clear();
        _activeGameByPlayerId.Clear();
        return count;
    }

    public int ActivePlayerCount => _activeGameByPlayerId.Count;

    /// <summary>
    /// Retourne les PlayerIds (parsés en Guid) actuellement actifs dans une partie.
    /// </summary>
    public HashSet<Guid> GetActivePlayerIds()
    {
        var result = new HashSet<Guid>();
        foreach (var playerIdStr in _activeGameByPlayerId.Keys)
        {
            if (Guid.TryParse(playerIdStr, out var gid))
                result.Add(gid);
        }
        return result;
    }

    public SubscriberToken Subscribe(string gameId)
    {
        var subscribers = _subscribers.GetOrAdd(gameId, _ => new EventSubscribers());
        return subscribers.Add();
    }

    public void Unsubscribe(string gameId, string subscriberId)
    {
        if (_subscribers.TryGetValue(gameId, out var subscribers))
            subscribers.Remove(subscriberId);
    }

    public void Publish(string gameId, ServerEvent evt)
    {
        if (_subscribers.TryGetValue(gameId, out var subscribers))
            subscribers.Broadcast(evt);
    }

    public bool TryReservePlayerForGame(string playerId, string gameId, out string? blockingGameId)
    {
        blockingGameId = null;

        while (true)
        {
            if (_activeGameByPlayerId.TryGetValue(playerId, out var existingGameId))
            {
                if (string.Equals(existingGameId, gameId, StringComparison.Ordinal))
                    return true;

                if (!TryGet(existingGameId, out var existingGame))
                {
                    _activeGameByPlayerId.TryRemove(playerId, out _);
                    continue;
                }

                lock (existingGame)
                {
                    if (existingGame.IsClosed || existingGame.Engine.GetGameState().IsGameOver)
                    {
                        _activeGameByPlayerId.TryRemove(playerId, out _);
                        continue;
                    }
                }

                blockingGameId = existingGameId;
                return false;
            }

            if (_activeGameByPlayerId.TryAdd(playerId, gameId))
                return true;
        }
    }

    public void ReleasePlayerReservation(string playerId, string gameId)
    {
        if (_activeGameByPlayerId.TryGetValue(playerId, out var existingGameId) &&
            string.Equals(existingGameId, gameId, StringComparison.Ordinal))
            _activeGameByPlayerId.TryRemove(playerId, out _);
    }

    public void ReleaseAllPlayerReservations(string gameId, IReadOnlyCollection<string> playerIds)
    {
        foreach (var playerId in playerIds)
            ReleasePlayerReservation(playerId, gameId);

        if (TryGet(gameId, out var game))
        {
            lock (game)
            {
                var activePlayersInGame = _activeGameByPlayerId.Any(kv =>
                    string.Equals(kv.Value, gameId, StringComparison.Ordinal));
                if (!activePlayersInGame)
                    game.IsClosed = true;
            }
        }
    }

    private sealed class EventSubscribers
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, Channel<ServerEvent>> _channels = new(StringComparer.Ordinal);

        public SubscriberToken Add()
        {
            var id = Guid.NewGuid().ToString("N");
            var channel = Channel.CreateUnbounded<ServerEvent>();

            lock (_sync)
            {
                _channels[id] = channel;
            }

            return new SubscriberToken(id, channel.Reader);
        }

        public void Remove(string id)
        {
            Channel<ServerEvent>? channel = null;
            lock (_sync)
            {
                if (_channels.TryGetValue(id, out channel))
                    _channels.Remove(id);
            }

            channel?.Writer.TryComplete();
        }

        public void Broadcast(ServerEvent evt)
        {
            lock (_sync)
            {
                foreach (var channel in _channels.Values)
                    channel.Writer.TryWrite(evt);
            }
        }
    }
}

public sealed record SubscriberToken(string Id, ChannelReader<ServerEvent> Reader);
