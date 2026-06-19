using System.Collections.Concurrent;
using System.Threading.Channels;
using Lama.Contracts;
using Lama.Domain.Engine;

public sealed class GameHubState
{
    private static readonly IReadOnlyDictionary<char, int> FrenchDistribution = new Dictionary<char, int>
    {
        ['A'] = 9,  ['B'] = 2,  ['C'] = 2,  ['D'] = 3,  ['E'] = 15,
        ['F'] = 2,  ['G'] = 2,  ['H'] = 2,  ['I'] = 8,  ['J'] = 1,
        ['K'] = 1,  ['L'] = 5,  ['M'] = 3,  ['N'] = 6,  ['O'] = 6,
        ['P'] = 2,  ['Q'] = 1,  ['R'] = 6,  ['S'] = 6,  ['T'] = 6,
        ['U'] = 6,  ['V'] = 2,  ['W'] = 1,  ['X'] = 1,  ['Y'] = 1,
        ['Z'] = 1,  ['*'] = 2
    };

    private readonly IGameLanguageProvider _languageProvider;
    private readonly ConcurrentDictionary<string, OnlineGame> _games = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EventSubscribers> _subscribers = new(StringComparer.Ordinal);

    public GameHubState(IGameLanguageProvider languageProvider)
    {
        _languageProvider = languageProvider;
    }

    public IGameEngine CreateEngine() =>
        new GameEngine(
            _languageProvider.GetDictionary(),
            _languageProvider.GetLetterScores(),
            FrenchDistribution);

    public void Create(OnlineGame game)
    {
        _games[game.Id] = game;
        _subscribers.TryAdd(game.Id, new EventSubscribers());
    }

    public bool Exists(string gameId) => _games.ContainsKey(gameId);

    public bool TryGet(string gameId, out OnlineGame game) => _games.TryGetValue(gameId, out game!);

    public IReadOnlyList<OnlineGame> ListGames() => _games.Values.ToList();

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

