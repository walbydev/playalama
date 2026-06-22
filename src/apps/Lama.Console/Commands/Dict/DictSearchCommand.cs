using System.Text.RegularExpressions;
using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Dict;

/// <summary>
/// Commande <c>lama dict search &lt;motif&gt;</c> — recherche des mots correspondant à un motif.
/// Le caractère <c>?</c> remplace une lettre quelconque.
/// Arguments : motif (positionnel, requis, ex: ?OISETTE).
/// Options : --lang &lt;code&gt;, --output (text|json|csv).
/// Accessible aux joueurs en mode Casual et aux admins uniquement.
/// </summary>
public sealed class DictSearchCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "dict.search";

    private readonly IGameLanguageProvider _languageProvider;
    private readonly ILogger<DictSearchCommand> _logger;

    /// <summary>Initialise la commande avec le provider de langue.</summary>
    public DictSearchCommand(IGameLanguageProvider languageProvider, ILogger<DictSearchCommand> logger)
    {
        _languageProvider = languageProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var pattern = context.GetArgument(0);

        if (string.IsNullOrWhiteSpace(pattern))
        {
            global::System.Console.Error.WriteLine(
                "[dict search] Argument requis : <motif> (ex: ?OISETTE)");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        pattern = pattern.Trim().ToUpperInvariant();

        // Convertit le motif (? = une lettre quelconque) en regex
        var regexPattern = "^" + string.Concat(pattern.Select(c => c == '?' ? "[A-Z]" : c.ToString())) + "$";
        var regex = new Regex(regexPattern);

        var matches = _languageProvider.GetDictionary()
            .Where(w => regex.IsMatch(w))
            .OrderBy(w => w)
            .ToList();

        _logger.LogDebug("dict.search : {Pattern} → {Count} résultats", pattern, matches.Count);

        if (context.OutputFormat == "json")
        {
            var json = "[" + string.Join(",", matches.Select(w => $"\"{w}\"")) + "]";
            global::System.Console.WriteLine(json);
        }
        else
        {
            if (matches.Count == 0)
            {
                global::System.Console.WriteLine($"Aucun résultat pour le motif \"{pattern}\".");
            }
            else
            {
                global::System.Console.WriteLine($"{matches.Count} mot(s) trouvé(s) pour \"{pattern}\" :");
                foreach (var word in matches)
                    global::System.Console.WriteLine($"  {word}");
            }
        }

        return Task.FromResult(ExitCodes.Success);
    }
}
