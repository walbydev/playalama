using Lama.Contracts;

namespace Lama.Console.Services;

/// <summary>
/// Représente le contexte d'exécution d'une commande CLI.
/// Encapsule les arguments parsés, la session chargée (game, joueur, rôle)
/// et les options globales.
///
/// Priorité de résolution pour GameId, PlayerId, Role et GameLevel :
/// les options CLI (<c>--game-id</c>, <c>--player</c>) surchargent
/// toujours la session persistée.
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
    /// L'identifiant complet de la commande.
    ///
    /// Cas standard : <c>groupe.action</c> (ex: "play.move").
    /// Cas spéciaux supportés par le parser :
    /// - mono-niveau : "login", "logout"
    /// - trois niveaux : "system.account.create"
    /// </summary>
    private readonly string? _commandId;

    public string CommandId
    {
        get => string.IsNullOrWhiteSpace(_commandId)
            ? $"{Group}.{Action}"
            : _commandId;
        init => _commandId = value;
    }

    /// <summary>
    /// Les arguments positionnels après le groupe et l'action.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>
    /// Les options nommées (ex: --lang fr, --output json, --game-id abc123).
    /// </summary>
    public IReadOnlyDictionary<string, string?> Options { get; init; } =
        new Dictionary<string, string?>();

    // ─── Contexte de session ─────────────────────────────────────────────────

    /// <summary>
    /// Identifiant de la partie en cours.
    /// Résolu depuis la session persistée, surchargeable via <c>--game-id</c>.
    /// Null si aucune partie n'est active.
    /// </summary>
    public string? GameId { get; init; }

    /// <summary>
    /// Identifiant du joueur courant.
    /// Résolu depuis la session persistée, surchargeable via <c>--player</c>.
    /// Null si aucune session n'est active.
    /// </summary>
    public string? PlayerId { get; init; }

    /// <summary>
    /// Nom d'affichage du joueur courant.
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// Le rôle du joueur courant (Admin, Player, Spectator).
    /// Résolu depuis la session persistée.
    /// </summary>
    public Role Role { get; init; } = Role.Player;

    /// <summary>
    /// Le niveau de la partie en cours (Casual, Standard, Competitive, Tournament).
    /// Résolu depuis la session persistée. Null si hors partie.
    /// </summary>
    public GameLevel? GameLevel { get; init; }

    /// <summary>
    /// Indique si le contexte possède une session active (partie en cours).
    /// </summary>
    public bool HasActiveSession => GameId is not null && PlayerId is not null;

    // ─── Options globales ────────────────────────────────────────────────────

    /// <summary>Indique si le mode verbeux est activé (--verbose / -V).</summary>
    public bool Verbose => Options.ContainsKey("verbose") || Options.ContainsKey("V");

    /// <summary>Indique si le mode silencieux est activé (--quiet / -q).</summary>
    public bool Quiet => Options.ContainsKey("quiet") || Options.ContainsKey("q");

    /// <summary>Indique si les couleurs ANSI sont désactivées (--no-color).</summary>
    public bool NoColor => Options.ContainsKey("no-color");

    /// <summary>Indique si le mode contraste élevé est activé (--high-contrast).</summary>
    public bool HighContrast => Options.ContainsKey("high-contrast");

    /// <summary>
    /// La langue sélectionnée (--lang / -l, défaut : "fr").
    /// </summary>
    public string Lang =>
        Options.TryGetValue("lang", out var lang) && lang is not null ? lang :
        Options.TryGetValue("l",    out var l)    && l    is not null ? l    : "fr";

    /// <summary>
    /// Le format de sortie (--output / -o : text, json, csv — défaut : text).
    /// </summary>
    public string OutputFormat =>
        Options.TryGetValue("output", out var fmt) && fmt is not null ? fmt :
        Options.TryGetValue("o",      out var o)   && o   is not null ? o   : "text";

    // ─── Helpers ─────────────────────────────────────────────────────────────

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
