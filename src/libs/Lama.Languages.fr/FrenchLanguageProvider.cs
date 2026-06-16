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
    private readonly string _basePath;

    public FrenchLanguageProvider(string basePath = "assets/languages/fr")
    {
        _basePath = basePath;
        _dictionary = LoadDictionary();
        _letterScores = LoadLetterScores();
    }

    public IReadOnlySet<string> GetDictionary() => _dictionary;

    public IReadOnlyDictionary<char, int> GetLetterScores() => _letterScores;

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

