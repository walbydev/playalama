namespace Lama.Domain.Bag;

/// <summary>
/// Sac de lettres du jeu LAMA.
/// Gère la distribution initiale des tuiles, la pioche et l'échange.
/// Thread-safety : non thread-safe (accès single-threaded par partie).
/// </summary>
public sealed class TileBag
{
    private readonly List<char> _tiles;
    private readonly Random _random;

    /// <summary>Nombre de tuiles restantes dans le sac.</summary>
    public int Count => _tiles.Count;

    /// <summary>Indique si le sac est vide.</summary>
    public bool IsEmpty => _tiles.Count == 0;

    /// <summary>
    /// Initialise le sac avec la distribution fournie.
    /// </summary>
    /// <param name="distribution">
    /// Dictionnaire lettre → quantité (ex: 'A' → 9, '*' → 2 pour les jokers).
    /// </param>
    /// <param name="random">Générateur aléatoire optionnel (pour les tests déterministes).</param>
    public TileBag(IReadOnlyDictionary<char, int> distribution, Random? random = null)
    {
        _random = random ?? Random.Shared;
        _tiles  = [];

        foreach (var (letter, count) in distribution)
            for (var i = 0; i < count; i++)
                _tiles.Add(letter);

        Shuffle();
    }

    /// <summary>
    /// Pioche jusqu'à <paramref name="count"/> tuiles du sac.
    /// Si le sac contient moins que demandé, retourne ce qui reste.
    /// </summary>
    public List<char> Draw(int count)
    {
        if (count <= 0) return [];

        var taken = Math.Min(count, _tiles.Count);
        var drawn = _tiles.GetRange(_tiles.Count - taken, taken);
        _tiles.RemoveRange(_tiles.Count - taken, taken);
        return drawn;
    }

    /// <summary>
    /// Remet des tuiles dans le sac et les mélange.
    /// </summary>
    public void ReturnTiles(IEnumerable<char> tiles)
    {
        _tiles.AddRange(tiles);
        Shuffle();
    }

    /// <summary>
    /// Échange des tuiles : remet les anciennes dans le sac et pioche le même nombre.
    /// Si le sac est vide, retourne une liste vide (l'échange est refusé).
    /// </summary>
    public List<char> Swap(IReadOnlyList<char> tilesToReturn)
    {
        if (IsEmpty) return [];

        ReturnTiles(tilesToReturn);
        return Draw(tilesToReturn.Count);
    }

    /// <summary>
    /// Retourne le nombre de tuiles restantes par lettre.
    /// </summary>
    public Dictionary<char, int> GetRemainingCounts()
    {
        var counts = new Dictionary<char, int>();
        foreach (var tile in _tiles)
        {
            counts.TryGetValue(tile, out var current);
            counts[tile] = current + 1;
        }
        return counts;
    }

    // ── Shuffle (Fisher-Yates) ────────────────────────────────────────────────

    private void Shuffle()
    {
        for (var i = _tiles.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_tiles[i], _tiles[j]) = (_tiles[j], _tiles[i]);
        }
    }
}
