using Lama.Console.Services;
using Lama.Contracts;
using Microsoft.Extensions.Logging;

namespace Lama.Console.Commands.Dict;

/// <summary>
/// Commande <c>lama dict check &lt;mot&gt;</c> — vérifie si un mot est présent dans le dictionnaire.
/// Arguments : mot à vérifier (positionnel, requis).
/// Options : --lang &lt;code&gt; (langue, défaut fr).
/// Accessible aux joueurs en mode Casual et aux admins uniquement.
/// </summary>
public sealed class DictCheckCommand : ICommand
{
    /// <inheritdoc />
    public string CommandId => "dict.check";

    private readonly IGameLanguageProvider _languageProvider;
    private readonly ILogger<DictCheckCommand> _logger;

    /// <summary>Initialise la commande avec le provider de langue.</summary>
    public DictCheckCommand(IGameLanguageProvider languageProvider, ILogger<DictCheckCommand> logger)
    {
        _languageProvider = languageProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        var word = context.GetArgument(0);

        if (string.IsNullOrWhiteSpace(word))
        {
            global::System.Console.Error.WriteLine("[dict check] Argument requis : <mot>");
            return Task.FromResult(ExitCodes.InvalidArgument);
        }

        word = word.Trim().ToUpperInvariant();

        var dictionary = _languageProvider.GetDictionary();
        var isValid = dictionary.Contains(word);

        if (context.OutputFormat == "json")
        {
            global::System.Console.WriteLine($"{{\"word\":\"{word}\",\"valid\":{isValid.ToString().ToLower()}}}");
        }
        else
        {
            if (isValid)
                global::System.Console.WriteLine($"✓ \"{word}\" est dans le dictionnaire {_languageProvider.GetLanguageName()}.");
            else
                global::System.Console.WriteLine($"✗ \"{word}\" n'est pas dans le dictionnaire {_languageProvider.GetLanguageName()}.");
        }

        _logger.LogDebug("dict.check : {Word} → {Result}", word, isValid);
        return Task.FromResult(isValid ? ExitCodes.Success : ExitCodes.WordNotInDictionary);
    }
}
