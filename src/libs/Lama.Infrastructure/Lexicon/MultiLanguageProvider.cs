using System.Collections;
using Lama.Contracts;

namespace Lama.Infrastructure.Lexicon;

/// <summary>
/// Fournisseur multi-langues : un mot est valide s'il existe dans au moins une langue.
/// Scores des lettres : moyenne arrondie au plafond sur toutes les langues qui définissent la lettre.
/// Distribution et locale proviennent de la langue primaire (première de la liste).
/// </summary>
public sealed class MultiLanguageProvider : IGameLanguageProvider
{
    private readonly IGameLanguageProvider _primary;
    private readonly IReadOnlySet<string> _union;
    private readonly IReadOnlyDictionary<char, int> _mergedScores;
    private readonly string _name;

    public MultiLanguageProvider(IReadOnlyList<IGameLanguageProvider> providers)
    {
        if (providers.Count == 0) throw new ArgumentException("Au moins une langue requise.", nameof(providers));
        _primary = providers[0];
        _union = providers.Count == 1
            ? providers[0].GetDictionary()
            : new UnionWordSet(providers.Select(p => p.GetDictionary()).ToList());
        _name = string.Join(" + ", providers.Select(p => p.GetLanguageName()));
        _mergedScores = providers.Count == 1
            ? providers[0].GetLetterScores()
            : BuildCeilingAverageScores(providers.Select(p => p.GetLetterScores()).ToList());
    }

    public IReadOnlySet<string> GetDictionary() => _union;
    public IReadOnlyDictionary<char, int> GetLetterScores() => _mergedScores;
    public IReadOnlyDictionary<char, int> GetTileDistribution() => _primary.GetTileDistribution();
    public IReadOnlyDictionary<char, int> GetTileDistribution(TileDistributionProfile profile) => _primary.GetTileDistribution(profile);
    public string GetLanguageName() => _name;
    public string GetLocale() => _primary.GetLocale();

    /// <summary>
    /// Pour chaque lettre présente dans au moins une langue, calcule la moyenne de ses valeurs
    /// dans toutes les langues qui la définissent, arrondie au plafond (entier supérieur).
    /// Exemple : K vaut 10 (FR) et 4 (DE) → moyenne = 7.0 → plafond = 7.
    /// </summary>
    private static IReadOnlyDictionary<char, int> BuildCeilingAverageScores(
        IReadOnlyList<IReadOnlyDictionary<char, int>> allScores)
    {
        var result = new Dictionary<char, int>();
        var allLetters = allScores.SelectMany(s => s.Keys).Distinct();
        foreach (var letter in allLetters)
        {
            var values = allScores
                .Where(s => s.ContainsKey(letter))
                .Select(s => s[letter])
                .ToList();
            result[letter] = (int)Math.Ceiling(values.Average());
        }
        return result;
    }

    private sealed class UnionWordSet(IReadOnlyList<IReadOnlySet<string>> sets) : IReadOnlySet<string>
    {
        public int Count => sets.Sum(s => s.Count);
        public bool Contains(string item) => sets.Any(s => s.Contains(item));
        public IEnumerator<string> GetEnumerator() => sets.SelectMany(s => s).Distinct().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<string> other) => throw new NotSupportedException();
        public bool IsProperSupersetOf(IEnumerable<string> other) => throw new NotSupportedException();
        public bool IsSubsetOf(IEnumerable<string> other) => throw new NotSupportedException();
        public bool IsSupersetOf(IEnumerable<string> other) => other.All(Contains);
        public bool Overlaps(IEnumerable<string> other) => other.Any(Contains);
        public bool SetEquals(IEnumerable<string> other) => throw new NotSupportedException();
    }
}
