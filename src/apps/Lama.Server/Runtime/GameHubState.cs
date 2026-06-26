using System.Collections.Concurrent;
using System.Threading.Channels;
using Lama.Contracts;
using Lama.Domain.Engine;
using Lama.Server.Contracts.Api;

namespace Lama.Server.Runtime;

public sealed class GameHubState
{
    private readonly IGameLanguageProvider _languageProvider;
    private readonly ConcurrentDictionary<string, OnlineGame> _games = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EventSubscribers> _subscribers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _activeGameByPlayerId = new(StringComparer.Ordinal);

    public GameHubState(IGameLanguageProvider languageProvider)
    {
        _languageProvider = languageProvider;
    }

    public IGameEngine CreateEngine(TileDistributionProfile? profile = null) =>
        new GameEngine(
            _languageProvider.GetDictionary(),
            _languageProvider.GetLetterScores(),
            profile is null
                ? _languageProvider.GetTileDistribution()
                : _languageProvider.GetTileDistribution(profile));

    public void Create(OnlineGame game)
    {
        _games[game.Id] = game;
        _subscribers.TryAdd(game.Id, new EventSubscribers());
    }

    public bool Exists(string gameId) => _games.ContainsKey(gameId);

    public bool TryGet(string gameId, out OnlineGame game) => _games.TryGetValue(gameId, out game!);

    public IReadOnlyList<OnlineGame> ListGames() => _games.Values.ToList();

    public int ActivePlayerCount => _activeGameByPlayerId.Count;

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

