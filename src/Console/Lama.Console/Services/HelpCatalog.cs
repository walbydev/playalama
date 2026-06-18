namespace Lama.Console.Services;

/// <summary>
/// Catalogue centralise de l'aide CLI.
/// Source unique pour les descriptions de groupes/commandes,
/// options, exemples, ACL et formats de sortie.
/// </summary>
public static class HelpCatalog
{
    public static IReadOnlyList<HelpGroup> Groups { get; } =
    [
        new("game", "Gerer les parties", "create, join, list, show, pause, save, end"),
        new("play", "Jouer un tour", "move, pass, swap, challenge, check"),
        new("show", "Afficher l'etat de jeu", "board, rack, scores, history"),
        new("dict", "Dictionnaire", "check, search, anagram"),
        new("player", "Profil joueur local", "create"),
        new("tournament", "Tournoi", "create"),
        new("system", "Administration systeme", "setup, status, restart, account.*")
    ];

    public static IReadOnlyList<HelpOption> GlobalOptions { get; } =
    [
        new("-h, --help", "Aide contextuelle"),
        new("-v, --version", "Version du jeu"),
        new("-V, --verbose", "Mode verbeux"),
        new("-q, --quiet", "Mode silencieux"),
        new("--no-color", "Desactive les couleurs ANSI"),
        new("--high-contrast", "Mode contraste eleve"),
        new("-l, --lang <code>", "Langue (fr, en, de, es, it)"),
        new("-o, --output <fmt>", "Format de sortie (text, json, csv)"),
        new("--game-id <id>", "Surcharge l'identifiant de partie (session)"),
        new("--player <id>", "Surcharge l'identifiant joueur (session)")
    ];

    public static IReadOnlyList<HelpCommand> Commands { get; } =
    [
        new(
            CommandId: "game.create",
            Group: "game",
            ActionPath: "create",
            Usage: "lama game create [<hote>] [--level casual|standard|competitive|tournament]",
            Description: "Cree une nouvelle partie et initialise la session locale.",
            AllowedRoles: "Public (cree une session Host)",
            OutputFormats: "text",
            Options:
            [
                new("--level", "Niveau de partie (defaut: standard)")
            ],
            Examples:
            [
                "lama game create Alice",
                "lama game create --level tournament"
            ]),
        new(
            CommandId: "play.move",
            Group: "play",
            ActionPath: "move",
            Usage: "lama play move <case> <mot> <direction>",
            Description: "Pose un mot sur le plateau.",
            AllowedRoles: "Host, Player",
            OutputFormats: "text",
            Options:
            [
                new("--dry-run", "Simule sans jouer le coup")
            ],
            Examples:
            [
                "lama play move H8 LAMA H",
                "lama play move H8 LAMA V --dry-run"
            ]),
        new(
            CommandId: "system.status",
            Group: "system",
            ActionPath: "status",
            Usage: "lama system status [--output text|json|csv]",
            Description: "Affiche l'etat systeme: init, comptes, parties persistees, session.",
            AllowedRoles: "Admin, SuperAdmin",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples:
            [
                "lama system status",
                "lama system status --output json"
            ]),
        new(
            CommandId: "system.restart",
            Group: "system",
            ActionPath: "restart",
            Usage: "lama system restart",
            Description: "Redemarrage logique in-process (purge cache memoire + tentative de restauration session active).",
            AllowedRoles: "Admin, SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples:
            [
                "lama system restart"
            ],
            Notes:
            [
                "Ne redemarre pas un service OS externe.",
                "Conserve les donnees persistees sur disque."
            ]),
        new(
            CommandId: "player.create",
            Group: "player",
            ActionPath: "create",
            Usage: "lama player create <nom>",
            Description: "Cree un profil joueur local hors partie active.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples:
            [
                "lama player create Carla"
            ]),
        new(
            CommandId: "tournament.create",
            Group: "tournament",
            ActionPath: "create",
            Usage: "lama tournament create <nom> [--host <nom>]",
            Description: "Cree une partie de niveau Tournament et initialise la session hote.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options:
            [
                new("--host", "Nom de l'hote (defaut: session courante)")
            ],
            Examples:
            [
                "lama tournament create OpenLama",
                "lama tournament create OpenLama --host Alice"
            ]),
        new(
            CommandId: "system.account.create",
            Group: "system",
            ActionPath: "account create",
            Usage: "lama system account create <username>",
            Description: "Cree un compte Admin.",
            AllowedRoles: "SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples:
            [
                "lama system account create admin2"
            ])
    ];

    public static bool TryGetGroup(string group, out HelpGroup? helpGroup)
    {
        helpGroup = Groups.FirstOrDefault(g =>
            g.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
        return helpGroup is not null;
    }

    public static bool TryGetCommand(string group, string actionPath, out HelpCommand? command)
    {
        command = Commands.FirstOrDefault(c =>
            c.Group.Equals(group, StringComparison.OrdinalIgnoreCase) &&
            c.ActionPath.Equals(actionPath, StringComparison.OrdinalIgnoreCase));
        return command is not null;
    }

    public static IEnumerable<HelpCommand> GetGroupCommands(string group) =>
        Commands
            .Where(c => c.Group.Equals(group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.ActionPath, StringComparer.OrdinalIgnoreCase);
}

public sealed record HelpGroup(string Group, string Summary, string ActionsSummary);

public sealed record HelpOption(string Name, string Description);

public sealed record HelpCommand(
    string CommandId,
    string Group,
    string ActionPath,
    string Usage,
    string Description,
    string AllowedRoles,
    string OutputFormats,
    IReadOnlyList<HelpOption> Options,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string>? Notes = null);

