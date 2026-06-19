using Lama.Contracts;

namespace Lama.Languages.fr;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Implémentation FrenchLanguageProvider pour Scrabble français.
/// Charge dictionnaire et scoring depuis assets linguistiques.
/// </summary>
public class FrenchLanguageProvider : IGameLanguageProvider
{
    private readonly IReadOnlySet<string> _dictionary;
    private readonly IReadOnlyDictionary<char, int> _letterScores;
    private readonly IReadOnlyDictionary<char, int> _baseTileDistribution;
    private readonly DistributionScalingRules _scalingRules;
    private readonly string _basePath;

    public FrenchLanguageProvider(string basePath = "assets/languages/fr")
    {
        _basePath = basePath;
        _dictionary = LoadDictionary();
        _letterScores = LoadLetterScores();
        (_baseTileDistribution, _scalingRules) = LoadTileDistributionSettings();
    }

    public IReadOnlySet<string> GetDictionary() => _dictionary;

    public IReadOnlyDictionary<char, int> GetLetterScores() => _letterScores;

    public IReadOnlyDictionary<char, int> GetTileDistribution() =>
        GetTileDistribution(TileDistributionProfile.Default("fr"));

    public IReadOnlyDictionary<char, int> GetTileDistribution(TileDistributionProfile profile)
    {
        var boardSize = profile.BoardSize > 0 ? profile.BoardSize : 15;
        var rackSize = profile.RackSize > 0 ? profile.RackSize : 7;
        var gameType = string.IsNullOrWhiteSpace(profile.GameType)
            ? "classic"
            : profile.GameType.Trim().ToLowerInvariant();

        var boardRatio = (boardSize * boardSize) /
                         (double)(_scalingRules.BoardReferenceSize * _scalingRules.BoardReferenceSize);
        var boardMultiplier = Math.Pow(boardRatio, _scalingRules.BoardExponent);

        var rackDelta = (rackSize - _scalingRules.RackReferenceSize) /
                        (double)_scalingRules.RackReferenceSize;
        var rackMultiplier = 1d + rackDelta * _scalingRules.RackWeight;

        var typeMultiplier = _scalingRules.GameTypeMultipliers.TryGetValue(gameType, out var knownTypeMultiplier)
            ? knownTypeMultiplier
            : 1d;

        var levelKey = profile.GameLevel.ToString();
        var levelMultiplier = _scalingRules.LevelMultipliers.TryGetValue(levelKey, out var knownLevelMultiplier)
            ? knownLevelMultiplier
            : 1d;

        var combined = boardMultiplier * rackMultiplier * typeMultiplier * levelMultiplier;
        var clamped = Math.Clamp(combined, _scalingRules.MinMultiplier, _scalingRules.MaxMultiplier);

        var baseTotal = _baseTileDistribution.Values.Sum();
        var targetTotal = (int)Math.Round(baseTotal * clamped, MidpointRounding.AwayFromZero);

        return ScaleDistribution(_baseTileDistribution, targetTotal);
    }

    public string GetLanguageName() => "Français";

    public string GetLocale() => "fr-FR";

    private IReadOnlySet<string> LoadDictionary()
    {
        var dictionaryPath = Path.Combine(_basePath, "assets/dictionary.txt");
        if (!File.Exists(dictionaryPath))
        {
            throw new FileNotFoundException($"Dictionnaire non trouvé: {dictionaryPath}");
        }

        var words = File.ReadAllLines(dictionaryPath)
            .Select(line => line.Trim().ToUpper())
            .Where(line => !string.IsNullOrWhiteSpace(line) && Regex.IsMatch(line, "^[A-Z]+$"))
            .ToHashSet();

        return words;
    }

    private IReadOnlyDictionary<char, int> LoadLetterScores()
    {
        var scoresPath = Path.Combine(_basePath, "assets/scores.json");
        if (!File.Exists(scoresPath))
        {
            throw new FileNotFoundException($"Fichier scoring non trouvé: {scoresPath}");
        }

        var json = File.ReadAllText(scoresPath);
        using var jsonDoc = JsonDocument.Parse(json);
        var scoresElement = jsonDoc.RootElement.GetProperty("scores");

        var scores = new Dictionary<char, int>();
        foreach (var property in scoresElement.EnumerateObject())
        {
            if (char.TryParse(property.Name, out var letter))
            {
                scores[letter] = property.Value.GetInt32();
            }
        }

        return scores;
    }

    private (IReadOnlyDictionary<char, int> Distribution, DistributionScalingRules Rules) LoadTileDistributionSettings()
    {
        var distributionPath = Path.Combine(_basePath, "assets/tile-distribution.json");
        if (!File.Exists(distributionPath))
            throw new FileNotFoundException($"Fichier distribution non trouvé: {distributionPath}");

        var json = File.ReadAllText(distributionPath);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        var baseDistribution = new Dictionary<char, int>();
        var baseElement = root.GetProperty("baseDistribution");
        foreach (var property in baseElement.EnumerateObject())
        {
            if (char.TryParse(property.Name, out var letter))
                baseDistribution[char.ToUpperInvariant(letter)] = property.Value.GetInt32();
        }

        var scalingElement = root.GetProperty("scaling");
        var rules = new DistributionScalingRules(
            MinMultiplier: scalingElement.GetProperty("minMultiplier").GetDouble(),
            MaxMultiplier: scalingElement.GetProperty("maxMultiplier").GetDouble(),
            BoardExponent: scalingElement.GetProperty("boardExponent").GetDouble(),
            BoardReferenceSize: scalingElement.GetProperty("boardReferenceSize").GetInt32(),
            RackReferenceSize: scalingElement.GetProperty("rackReferenceSize").GetInt32(),
            RackWeight: scalingElement.GetProperty("rackWeight").GetDouble(),
            GameTypeMultipliers: ReadMultiplierMap(scalingElement.GetProperty("gameTypeMultipliers"), normalizeLower: true),
            LevelMultipliers: ReadMultiplierMap(scalingElement.GetProperty("levelMultipliers"), normalizeLower: false));

        return (baseDistribution, rules);
    }

    private static IReadOnlyDictionary<string, double> ReadMultiplierMap(JsonElement source, bool normalizeLower)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source.EnumerateObject())
        {
            var key = normalizeLower ? item.Name.ToLowerInvariant() : item.Name;
            map[key] = item.Value.GetDouble();
        }

        return map;
    }

    private static IReadOnlyDictionary<char, int> ScaleDistribution(
        IReadOnlyDictionary<char, int> baseDistribution,
        int targetTotal)
    {
        var baseTotal = baseDistribution.Values.Sum();
        if (baseTotal <= 0)
            throw new InvalidOperationException("La distribution de base doit contenir au moins une tuile.");

        var minTotal = baseDistribution.Count;
        targetTotal = Math.Max(targetTotal, minTotal);

        var candidates = baseDistribution
            .Select(kv => new AllocationCandidate(
                kv.Key,
                kv.Value * targetTotal / (double)baseTotal,
                Math.Max(1, (int)Math.Floor(kv.Value * targetTotal / (double)baseTotal))))
            .ToList();

        var currentTotal = candidates.Sum(c => c.Count);

        if (currentTotal < targetTotal)
        {
            var missing = targetTotal - currentTotal;
            foreach (var candidate in candidates
                         .OrderByDescending(c => c.Fraction)
                         .ThenBy(c => c.Letter)
                         .Take(missing))
            {
                candidate.Count++;
            }
        }
        else if (currentTotal > targetTotal)
        {
            var overflow = currentTotal - targetTotal;
            foreach (var candidate in candidates
                         .OrderBy(c => c.Fraction)
                         .ThenByDescending(c => c.Letter))
            {
                if (overflow == 0)
                    break;

                if (candidate.Count <= 1)
                    continue;

                candidate.Count--;
                overflow--;
            }
        }

        return candidates.ToDictionary(c => c.Letter, c => c.Count);
    }

    private sealed class AllocationCandidate(char letter, double rawCount, int count)
    {
        public char Letter { get; } = letter;
        public int Count { get; set; } = count;
        public double Fraction => rawCount - Math.Floor(rawCount);
    }

    private sealed record DistributionScalingRules(
        double MinMultiplier,
        double MaxMultiplier,
        double BoardExponent,
        int BoardReferenceSize,
        int RackReferenceSize,
        double RackWeight,
        IReadOnlyDictionary<string, double> GameTypeMultipliers,
        IReadOnlyDictionary<string, double> LevelMultipliers);
}

