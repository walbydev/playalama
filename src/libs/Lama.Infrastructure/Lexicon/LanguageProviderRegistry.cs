using System.Collections.Concurrent;
using Lama.Contracts;
using Lama.Contracts.Lexicon;

namespace Lama.Infrastructure.Lexicon;

/// <summary>
/// Registry des fournisseurs de langue, dictionnaire chargé depuis Postgres et mis en cache.
/// </summary>
public sealed class LanguageProviderRegistry : ILanguageProviderRegistry
{
    private static readonly IReadOnlyDictionary<string, (string Name, string Locale)> Meta = new Dictionary<string, (string, string)>
    {
        ["fr"] = ("Français", "fr-FR"),
        ["en"] = ("English", "en-US"),
        ["de"] = ("Deutsch", "de-DE"),
    };

    private readonly ILexiconReader _reader;
    private readonly string _assetsRoot;
    private readonly ConcurrentDictionary<string, IGameLanguageProvider> _cache = new();

    public LanguageProviderRegistry(ILexiconReader reader, string assetsRoot)
    {
        _reader = reader;
        _assetsRoot = assetsRoot;
    }

    public IReadOnlyList<string> SupportedLanguages => Meta.Keys.ToList();
    public bool IsSupported(string code) => Meta.ContainsKey(code.ToLowerInvariant());

    public IGameLanguageProvider GetProvider(string code) => _cache.GetOrAdd(code.ToLowerInvariant(), Build);

    public IGameLanguageProvider GetProvider(IReadOnlyList<string> codes)
    {
        var clean = codes.Select(c => c.ToLowerInvariant()).Where(IsSupported).Distinct().ToList();
        if (clean.Count == 0) clean.Add("fr");
        var providers = clean.Select(GetProvider).ToList();
        return providers.Count == 1 ? providers[0] : new MultiLanguageProvider(providers);
    }

    private IGameLanguageProvider Build(string code)
    {
        var (name, locale) = Meta[code];
        var basePath = Path.Combine(_assetsRoot, "assets", "languages", code);
        var dict = _reader.LoadDictionary(code);
        return new AssetLanguageProvider(code, name, locale, basePath, dict);
    }
}
