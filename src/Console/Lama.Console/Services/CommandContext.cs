using Lama.Contracts;

namespace Lama.Console.Services;

/// <summary>
/// Représente le contexte d'exécution d'une commande CLI.
/// Encapsule les arguments parsés, le rôle de l'utilisateur et les options globales.
/// </summary>
public sealed class CommandContext
{
    /// <summary>
    /// Le groupe de la commande (ex: "game", "play", "show").
    /// </summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// L'action de la commande (ex: "create", "move", "board").
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// L'identifiant complet de la commande au format "groupe.action" (ex: "play.move").
    /// </summary>
    public string CommandId => $"{Group}.{Action}";

    /// <summary>
    /// Les arguments positionnels après le groupe et l'action.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Les options nommées (ex: --lang fr, --output json).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Options { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>
    /// Le rôle de l'utilisateur courant.
    /// </summary>
    public Role Role { get; init; } = Role.Player;

    /// <summary>
    /// Le niveau de la partie en cours, si applicable.
    /// </summary>
    public GameLevel? GameLevel { get; init; }

    /// <summary>
    /// Indique si le mode verbeux est activé (--verbose).
    /// </summary>
    public bool Verbose => Options.ContainsKey("verbose") || Options.ContainsKey("V");

    /// <summary>
    /// Indique si le mode silencieux est activé (--quiet).
    /// </summary>
    public bool Quiet => Options.ContainsKey("quiet") || Options.ContainsKey("q");

    /// <summary>
    /// Indique si les couleurs ANSI sont désactivées (--no-color).
    /// </summary>
    public bool NoColor => Options.ContainsKey("no-color");

    /// <summary>
    /// Indique si le mode contraste élevé est activé (--high-contrast).
    /// </summary>
    public bool HighContrast => Options.ContainsKey("high-contrast");

    /// <summary>
    /// La langue sélectionnée (--lang, défaut : "fr").
    /// </summary>
    public string Lang => Options.TryGetValue("lang", out var lang) && lang is not null
        ? lang
        : Options.TryGetValue("l", out var l) && l is not null ? l : "fr";

    /// <summary>
    /// Le format de sortie (--output : text, json, csv — défaut : text).
    /// </summary>
    public string OutputFormat => Options.TryGetValue("output", out var fmt) && fmt is not null
        ? fmt
        : Options.TryGetValue("o", out var o) && o is not null ? o : "text";

    /// <summary>
    /// Retourne la valeur d'un argument positionnel par index, ou null si absent.
    /// </summary>
    public string? GetArgument(int index) =>
        index < Arguments.Count ? Arguments[index] : null;

    /// <summary>
    /// Indique si une option booléenne est présente (ex: --dry-run).
    /// </summary>
    public bool HasOption(string name) => Options.ContainsKey(name);

    /// <summary>
    /// Retourne la valeur d'une option nommée, ou null si absente.
    /// </summary>
    public string? GetOption(string name) =>
        Options.TryGetValue(name, out var value) ? value : null;
}
