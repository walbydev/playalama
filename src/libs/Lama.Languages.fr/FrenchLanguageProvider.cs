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
    private static readonly IReadOnlyDictionary<char, int> TileDistribution = new Dictionary<char, int>
    {
        ['A'] = 9,  ['B'] = 2,  ['C'] = 2,  ['D'] = 3,  ['E'] = 15,
        ['F'] = 2,  ['G'] = 2,  ['H'] = 2,  ['I'] = 8,  ['J'] = 1,
        ['K'] = 1,  ['L'] = 5,  ['M'] = 3,  ['N'] = 6,  ['O'] = 6,
        ['P'] = 2,  ['Q'] = 1,  ['R'] = 6,  ['S'] = 6,  ['T'] = 6,
        ['U'] = 6,  ['V'] = 2,  ['W'] = 1,  ['X'] = 1,  ['Y'] = 1,
        ['Z'] = 1,  ['*'] = 2
    };

    private readonly IReadOnlySet<string> _dictionary;
    private readonly IReadOnlyDictionary<char, int> _letterScores;
    private readonly string _basePath;

    public FrenchLanguageProvider(string basePath = "assets/languages/fr")
    {
        _basePath = basePath;
        _dictionary = LoadDictionary();
        _letterScores = LoadLetterScores();
    }

    public IReadOnlySet<string> GetDictionary() => _dictionary;

    public IReadOnlyDictionary<char, int> GetLetterScores() => _letterScores;

    public IReadOnlyDictionary<char, int> GetTileDistribution() => TileDistribution;

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
}

