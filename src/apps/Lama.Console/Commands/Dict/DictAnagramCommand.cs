using System.Text.RegularExpressions;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Dict;

/// <summary>
/// Commande <c>lama dict anagram &lt;lettres&gt;</c> — trouve les anagrammes possibles
/// à partir d'un ensemble de lettres.
/// Arguments : lettres disponibles (positionnel, requis, ex: NOISETTE).
/// Options : --min-length N (longueur minimale des résultats, défaut : 2),
///           --lang &lt;code&gt;, --output (text|json|csv).
/// Accessible aux joueurs en mode Casual et aux admins uniquement.
/// </summary>
public sealed class DictAnagramCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "dict.anagram";

    private readonly IGameLanguageProvider _languageProvider;
    private readonly ILogger<DictAnagramCommand> _logger;

    /// <summary>Initialise la commande avec le provider de langue.</summary>
    public DictAnagramCommand(IGameLanguageProvider languageProvider, ILogger<DictAnagramCommand> logger)
    {
        _languageProvider = languageProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var letters = context.GetArgument(0);

        if (string.IsNullOrWhiteSpace(letters))
        {
            global::System.Console.Error.WriteLine(
                "[dict anagram] Argument requis : <lettres> (ex: NOISETTE)");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        letters = letters.Trim().ToUpperInvariant();

        if (!Regex.IsMatch(letters, "^[A-Z]+$"))
        {
            global::System.Console.Error.WriteLine(
                "[dict anagram] Les lettres doivent être alphabétiques.");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        var minLength = 2;
        var minLengthStr = context.GetOption("min-length");
        if (minLengthStr is not null && int.TryParse(minLengthStr, out var parsed))
            minLength = parsed;

        var sortedInput = string.Concat(letters.OrderBy(c => c));
        var dictionary = _languageProvider.GetDictionary();

        var anagrams = dictionary
            .Where(word => word.Length >= minLength &&
                           word.Length <= letters.Length &&
                           IsAnagramSubset(word, sortedInput))
            .OrderByDescending(w => w.Length)
            .ThenBy(w => w)
            .ToList();

        _logger.LogDebug("dict.anagram : {Letters} → {Count} résultats", letters, anagrams.Count);

        if (context.OutputFormat == "json")
        {
            var json = "[" + string.Join(",", anagrams.Select(w => $"\"{w}\"")) + "]";
            global::System.Console.WriteLine(json);
        }
        else
        {
            if (anagrams.Count == 0)
            {
                global::System.Console.WriteLine(
                    $"Aucun anagramme trouvé pour \"{letters}\" (longueur min: {minLength}).");
            }
            else
            {
                global::System.Console.WriteLine(
                    $"{anagrams.Count} anagramme(s) trouvé(s) pour \"{letters}\" :");
                foreach (var word in anagrams)
                    global::System.Console.WriteLine($"  {word}");
            }
        }

        return Task.FromResult(ExitCodes.Success);
    }

    /// <summary>
    /// Vérifie si le mot peut être formé avec un sous-ensemble des lettres disponibles.
    /// </summary>
    private static bool IsAnagramSubset(string word, string sortedAvailable)
    {
        var sortedWord = string.Concat(word.OrderBy(c => c));
        var available = sortedAvailable.ToList();

        foreach (var ch in sortedWord)
        {
            var idx = available.IndexOf(ch);
            if (idx < 0) return false;
            available.RemoveAt(idx);
        }
        return true;
    }
}
