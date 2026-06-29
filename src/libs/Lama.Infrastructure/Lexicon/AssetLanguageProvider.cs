using System.Text.Json;
using Lama.Contracts;

namespace Lama.Infrastructure.Lexicon;

/// <summary>
/// Fournisseur de langue générique : dictionnaire injecté (Postgres), scores et
/// distribution chargés depuis assets/languages/{code}.
/// </summary>
public sealed class AssetLanguageProvider : IGameLanguageProvider
{
    private readonly IReadOnlySet<string> _dictionary;
    private readonly IReadOnlyDictionary<char, int> _letterScores;
    private readonly IReadOnlyDictionary<char, int> _baseTileDistribution;
    private readonly DistributionScalingRules _scalingRules;
    private readonly string _code;
    private readonly string _name;
    private readonly string _locale;

    public AssetLanguageProvider(string code, string name, string locale, string basePath, IReadOnlySet<string> dictionary)
    {
        _code = code;
        _name = name;
        _locale = locale;
        _dictionary = dictionary;
        _letterScores = LoadLetterScores(Path.Combine(basePath, "scores.json"));
        (_baseTileDistribution, _scalingRules) = LoadTileDistribution(Path.Combine(basePath, "tile-distribution.json"));
    }

    public IReadOnlySet<string> GetDictionary() => _dictionary;
    public IReadOnlyDictionary<char, int> GetLetterScores() => _letterScores;
    public IReadOnlyDictionary<char, int> GetTileDistribution() => GetTileDistribution(TileDistributionProfile.Default(_code));
    public string GetLanguageName() => _name;
    public string GetLocale() => _locale;

    public IReadOnlyDictionary<char, int> GetTileDistribution(TileDistributionProfile profile)
    {
        var boardSize = profile.BoardSize > 0 ? profile.BoardSize : 15;
        var rackSize = profile.RackSize > 0 ? profile.RackSize : 7;
        var gameType = string.IsNullOrWhiteSpace(profile.GameType) ? "classic" : profile.GameType.Trim().ToLowerInvariant();

        var boardRatio = (boardSize * boardSize) / (double)(_scalingRules.BoardReferenceSize * _scalingRules.BoardReferenceSize);
        var boardMultiplier = Math.Pow(boardRatio, _scalingRules.BoardExponent);
        var rackDelta = (rackSize - _scalingRules.RackReferenceSize) / (double)_scalingRules.RackReferenceSize;
        var rackMultiplier = 1d + rackDelta * _scalingRules.RackWeight;
        var typeMultiplier = _scalingRules.GameTypeMultipliers.TryGetValue(gameType, out var t) ? t : 1d;
        var levelMultiplier = _scalingRules.LevelMultipliers.TryGetValue(profile.GameLevel.ToString(), out var l) ? l : 1d;

        var combined = boardMultiplier * rackMultiplier * typeMultiplier * levelMultiplier;
        var clamped = Math.Clamp(combined, _scalingRules.MinMultiplier, _scalingRules.MaxMultiplier);
        var targetTotal = (int)Math.Round(_baseTileDistribution.Values.Sum() * clamped, MidpointRounding.AwayFromZero);
        return ScaleDistribution(_baseTileDistribution, targetTotal);
    }

    private static IReadOnlyDictionary<char, int> LoadLetterScores(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var scores = new Dictionary<char, int>();
        foreach (var p in doc.RootElement.GetProperty("scores").EnumerateObject())
            if (char.TryParse(p.Name, out var c)) scores[c] = p.Value.GetInt32();
        return scores;
    }

    private static (IReadOnlyDictionary<char, int>, DistributionScalingRules) LoadTileDistribution(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var dist = new Dictionary<char, int>();
        foreach (var p in root.GetProperty("baseDistribution").EnumerateObject())
            if (char.TryParse(p.Name, out var c)) dist[char.ToUpperInvariant(c)] = p.Value.GetInt32();
        var s = root.GetProperty("scaling");
        var rules = new DistributionScalingRules(
            s.GetProperty("minMultiplier").GetDouble(), s.GetProperty("maxMultiplier").GetDouble(),
            s.GetProperty("boardExponent").GetDouble(), s.GetProperty("boardReferenceSize").GetInt32(),
            s.GetProperty("rackReferenceSize").GetInt32(), s.GetProperty("rackWeight").GetDouble(),
            ReadMap(s.GetProperty("gameTypeMultipliers"), true), ReadMap(s.GetProperty("levelMultipliers"), false));
        return (dist, rules);
    }

    private static IReadOnlyDictionary<string, double> ReadMap(JsonElement src, bool lower)
    {
        var m = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in src.EnumerateObject()) m[lower ? i.Name.ToLowerInvariant() : i.Name] = i.Value.GetDouble();
        return m;
    }

    private static IReadOnlyDictionary<char, int> ScaleDistribution(IReadOnlyDictionary<char, int> baseDist, int targetTotal)
    {
        var baseTotal = baseDist.Values.Sum();
        targetTotal = Math.Max(targetTotal, baseDist.Count);
        var cands = baseDist.Select(kv => new Cand(kv.Key, kv.Value * targetTotal / (double)baseTotal,
            Math.Max(1, (int)Math.Floor(kv.Value * targetTotal / (double)baseTotal)))).ToList();
        var cur = cands.Sum(c => c.Count);
        if (cur < targetTotal)
            foreach (var c in cands.OrderByDescending(c => c.Fraction).ThenBy(c => c.Letter).Take(targetTotal - cur)) c.Count++;
        else if (cur > targetTotal)
        {
            var over = cur - targetTotal;
            foreach (var c in cands.OrderBy(c => c.Fraction).ThenByDescending(c => c.Letter))
            { if (over == 0) break; if (c.Count <= 1) continue; c.Count--; over--; }
        }
        return cands.ToDictionary(c => c.Letter, c => c.Count);
    }

    private sealed class Cand(char letter, double raw, int count)
    {
        public char Letter { get; } = letter;
        public int Count { get; set; } = count;
        public double Fraction => raw - Math.Floor(raw);
    }

    private sealed record DistributionScalingRules(double MinMultiplier, double MaxMultiplier, double BoardExponent,
        int BoardReferenceSize, int RackReferenceSize, double RackWeight,
        IReadOnlyDictionary<string, double> GameTypeMultipliers, IReadOnlyDictionary<string, double> LevelMultipliers);
}
