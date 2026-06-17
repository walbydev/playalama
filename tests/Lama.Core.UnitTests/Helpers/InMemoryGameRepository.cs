using Lama.Contracts;

namespace Lama.Core.UnitTests.Helpers;

/// <summary>
/// Implémentation en mémoire de <see cref="IGameRepository"/> pour les tests.
/// Évite toute I/O disque et garantit l'isolation entre les tests.
/// </summary>
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly Dictionary<string, PersistedGame> _store = new();

    public void Save(PersistedGame game) => _store[game.GameId] = game;

    public PersistedGame? Load(string gameId) =>
        _store.TryGetValue(gameId, out var game) ? game : null;

    public void Delete(string gameId) => _store.Remove(gameId);

    public IReadOnlyList<string> ListGameIds() => [.. _store.Keys];

    public bool Exists(string gameId) => _store.ContainsKey(gameId);
}
