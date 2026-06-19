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
        new("play", "Jouer un tour", "move, pass, swap, challenge, check, suggest"),
        new("show", "Afficher l'etat de jeu", "board, rack, scores, history"),
        new("rating", "Classement global", "show, leaderboard, stats"),
        new("dict", "Dictionnaire", "check, search, anagram"),
        new("player", "Profil joueur local", "create, list, show, update"),
        new("tournament", "Tournoi", "create"),
        new("system", "Administration systeme", "setup, status, restart, clean, account.*")
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
        new("--server-url <url>", "Active le mode online et persiste l'URL serveur"),
        new("--server-ip <url>", "Alias de --server-url"),
        new("--game-id <id>", "Surcharge l'identifiant de partie (session)"),
        new("--player <id>", "Surcharge l'identifiant joueur (session)")
    ];

    public static IReadOnlyList<HelpCommand> Commands { get; } =
    [
        new(
            CommandId: "login",
            Group: "system",
            ActionPath: "login",
            Usage: "lama login",
            Description: "Authentifie un compte Admin ou SuperAdmin.",
            AllowedRoles: "Public",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama login"]),
        new(
            CommandId: "logout",
            Group: "system",
            ActionPath: "logout",
            Usage: "lama logout",
            Description: "Ferme la session d'authentification en cours.",
            AllowedRoles: "Public",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama logout"]),
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
            CommandId: "game.join",
            Group: "game",
            ActionPath: "join",
            Usage: "lama game join <nom>",
            Description: "Ajoute un joueur a la partie active.",
            AllowedRoles: "Public / Host / Player / Admin / SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama game join Bob"]),
        new(
            CommandId: "game.list",
            Group: "game",
            ActionPath: "list",
            Usage: "lama game list [--output text|json|csv]",
            Description: "Liste les parties persistees.",
            AllowedRoles: "Public + tous roles",
            OutputFormats: "text, json, csv",
            Options: [new("--output", "Format de sortie (text|json|csv)")],
            Examples: ["lama game list", "lama game list --output csv"]),
        new(
            CommandId: "game.show",
            Group: "game",
            ActionPath: "show",
            Usage: "lama game show [--game-id <id>] [--output text|json|csv]",
            Description: "Affiche les details d'une partie.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--game-id", "Identifiant de partie (sinon session courante)"),
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples: ["lama game show", "lama game show --output json"]),
        new(
            CommandId: "game.pause",
            Group: "game",
            ActionPath: "pause",
            Usage: "lama game pause",
            Description: "Persist un snapshot immediat de la partie active.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama game pause"]),
        new(
            CommandId: "game.save",
            Group: "game",
            ActionPath: "save",
            Usage: "lama game save [--file <chemin>]",
            Description: "Sauvegarde explicitement la partie active.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options: [new("--file", "Export JSON du snapshot vers un fichier")],
            Examples: ["lama game save", "lama game save --file /tmp/game.json"]),
        new(
            CommandId: "game.end",
            Group: "game",
            ActionPath: "end",
            Usage: "lama game end",
            Description: "Termine la partie courante.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama game end"]),
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
            ],
            Notes:
            [
                "Notation joker explicite: une lettre minuscule force l'utilisation d'un joker '*' (ex: lAMA)."
            ]),
        new(
            CommandId: "play.pass",
            Group: "play",
            ActionPath: "pass",
            Usage: "lama play pass",
            Description: "Passe le tour du joueur courant.",
            AllowedRoles: "Host, Player",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama play pass"]),
        new(
            CommandId: "play.swap",
            Group: "play",
            ActionPath: "swap",
            Usage: "lama play swap <lettres...> [--all]",
            Description: "Echange des lettres du rack avec le sac.",
            AllowedRoles: "Host, Player",
            OutputFormats: "text",
            Options: [new("--all", "Echange toutes les lettres du rack")],
            Examples: ["lama play swap A E", "lama play swap --all"]),
        new(
            CommandId: "play.challenge",
            Group: "play",
            ActionPath: "challenge",
            Usage: "lama play challenge",
            Description: "Conteste le dernier mot joue.",
            AllowedRoles: "Host, Player",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama play challenge"]),
        new(
            CommandId: "play.check",
            Group: "play",
            ActionPath: "check",
            Usage: "lama play check <case> <mot> <direction>",
            Description: "Valide un coup sans le jouer.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin (Casual)",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama play check H8 LAMA H"]),
        new(
            CommandId: "play.suggest",
            Group: "play",
            ActionPath: "suggest",
            Usage: "lama play suggest [--top <n>] [--sort score|length|balanced] [--output text|json|csv]",
            Description: "Propose les prochains coups (stub local/online).",
            AllowedRoles: "Host, Player, Admin, SuperAdmin (Casual)",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--top", "Nombre maximal de suggestions (defaut: 2)"),
                new("--sort", "Strategie de tri (score|length|balanced)"),
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples:
            [
                "lama play suggest",
                "lama play suggest --top 5 --sort balanced --output json"
            ]),
        new(
            CommandId: "show.board",
            Group: "show",
            ActionPath: "board",
            Usage: "lama show board",
            Description: "Affiche le plateau.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama show board"]),
        new(
            CommandId: "show.rack",
            Group: "show",
            ActionPath: "rack",
            Usage: "lama show rack",
            Description: "Affiche le rack du joueur courant.",
            AllowedRoles: "Host, Player",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama show rack"]),
        new(
            CommandId: "show.scores",
            Group: "show",
            ActionPath: "scores",
            Usage: "lama show scores",
            Description: "Affiche les scores des joueurs.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama show scores"]),
        new(
            CommandId: "show.history",
            Group: "show",
            ActionPath: "history",
            Usage: "lama show history [--last <n>] [--output text|json|csv]",
            Description: "Affiche l'historique des coups.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--last", "Nombre de coups a afficher"),
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples: ["lama show history", "lama show history --last 10 --output csv"]),
        new(
            CommandId: "rating.show",
            Group: "rating",
            ActionPath: "show",
            Usage: "lama rating show [playerId] [--output text|json|csv]",
            Description: "Affiche les ratings open/tournoi et le prestige global d'un joueur.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options: [new("--output", "Format de sortie (text|json|csv)")],
            Examples: ["lama rating show", "lama rating show alice --output json"]),
        new(
            CommandId: "rating.leaderboard",
            Group: "rating",
            ActionPath: "leaderboard",
            Usage: "lama rating leaderboard [--queue open|tournament|global] [--top <n>] [--output text|json|csv]",
            Description: "Affiche le classement mondial par file de classement.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--queue", "File de classement (open|tournament|global, defaut: global)"),
                new("--top", "Nombre de joueurs a afficher (defaut: 20)"),
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples: ["lama rating leaderboard", "lama rating leaderboard --queue tournament --top 50 --output csv"]),
        new(
            CommandId: "rating.stats",
            Group: "rating",
            ActionPath: "stats",
            Usage: "lama rating stats [playerId] [--7d|--30d|--365d] [--output text|json|csv]",
            Description: "Affiche les statistiques d'un joueur sur une periode.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--7d", "Filtre les stats sur les 7 derniers jours"),
                new("--30d", "Filtre les stats sur les 30 derniers jours"),
                new("--365d", "Filtre les stats sur les 365 derniers jours"),
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples: ["lama rating stats", "lama rating stats bob --30d --output json"]),
        new(
            CommandId: "dict.check",
            Group: "dict",
            ActionPath: "check",
            Usage: "lama dict check <mot>",
            Description: "Verifie la presence d'un mot dans le dictionnaire.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin (Casual)",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama dict check lama"]),
        new(
            CommandId: "dict.search",
            Group: "dict",
            ActionPath: "search",
            Usage: "lama dict search <motif>",
            Description: "Recherche des mots correspondant a un motif.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin (Casual)",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama dict search ?AMA"]),
        new(
            CommandId: "dict.anagram",
            Group: "dict",
            ActionPath: "anagram",
            Usage: "lama dict anagram <lettres> [--min-length <n>]",
            Description: "Trouve des anagrammes.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin (Casual)",
            OutputFormats: "text",
            Options: [new("--min-length", "Longueur minimale")],
            Examples: ["lama dict anagram lama --min-length 2"]),
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
            CommandId: "system.clean",
            Group: "system",
            ActionPath: "clean",
            Usage: "lama system clean",
            Description: "Nettoie toutes les parties actives et reinitialise les sessions.",
            AllowedRoles: "Admin, SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples:
            [
                "lama system clean"
            ],
            Notes:
            [
                "Supprime TOUS les fichiers de parties persistees.",
                "Reinitialise les sessions en memoire et la session courante.",
                "Operation irreversible."
            ]),
        new(
            CommandId: "system.setup",
            Group: "system",
            ActionPath: "setup",
            Usage: "lama system setup",
            Description: "Initialise le systeme et cree le compte SuperAdmin.",
            AllowedRoles: "Public (premiere execution)",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama system setup"]),
        new(
            CommandId: "player.create",
            Group: "player",
            ActionPath: "create",
            Usage: "lama player create <nom> [--pseudo <pseudo>] [--country <pays>] [--region <region>] [--birth-year <yyyy>]",
            Description: "Cree un profil joueur local hors partie active avec metadonnees optionnelles.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options:
            [
                new("--pseudo", "Pseudo public optionnel"),
                new("--country", "Pays optionnel"),
                new("--region", "Region optionnelle"),
                new("--birth-year", "Annee de naissance optionnelle")
            ],
            Examples:
            [
                "lama player create Carla",
                "lama player create Carla --pseudo Krl --country FR --region Bretagne --birth-year 1995"
            ]),
        new(
            CommandId: "player.list",
            Group: "player",
            ActionPath: "list",
            Usage: "lama player list [--country <pays>] [--region <region>] [--output text|json|csv]",
            Description: "Liste les profils joueurs avec leur niveau et rating.",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options:
            [
                new("--country", "Filtre par pays"),
                new("--region", "Filtre par region"),
                new("--output", "Format de sortie (text|json|csv)")
            ],
            Examples:
            [
                "lama player list",
                "lama player list --country FR",
                "lama player list --region Bretagne --output json"
            ]),
        new(
            CommandId: "player.show",
            Group: "player",
            ActionPath: "show",
            Usage: "lama player show [playerId] [--output text|json|csv]",
            Description: "Affiche le profil complet d'un joueur (identite + rating + stats).",
            AllowedRoles: "Tous roles",
            OutputFormats: "text, json, csv",
            Options: [new("--output", "Format de sortie (text|json|csv)")],
            Examples: ["lama player show", "lama player show alice --output json"]),
        new(
            CommandId: "player.update",
            Group: "player",
            ActionPath: "update",
            Usage: "lama player update [playerId] [--name <nom>] [--pseudo <pseudo>] [--country <pays>] [--region <region>] [--birth-year <yyyy>]",
            Description: "Met a jour les informations de profil d'un joueur.",
            AllowedRoles: "Host, Player, Admin, SuperAdmin",
            OutputFormats: "text",
            Options:
            [
                new("--name", "Nom principal (display name)"),
                new("--pseudo", "Pseudo public"),
                new("--country", "Pays"),
                new("--region", "Region"),
                new("--birth-year", "Annee de naissance")
            ],
            Examples:
            [
                "lama player update --pseudo LlamaKing",
                "lama player update alice --country FR --region Occitanie"
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
            ]),
        new(
            CommandId: "system.account.list",
            Group: "system",
            ActionPath: "account list",
            Usage: "lama system account list [--output text|json]",
            Description: "Liste les comptes systeme.",
            AllowedRoles: "SuperAdmin",
            OutputFormats: "text, json",
            Options: [new("--output", "Format de sortie (text|json)")],
            Examples: ["lama system account list", "lama system account list --output json"]),
        new(
            CommandId: "system.account.revoke",
            Group: "system",
            ActionPath: "account revoke",
            Usage: "lama system account revoke <username>",
            Description: "Revoque un compte Admin.",
            AllowedRoles: "SuperAdmin",
            OutputFormats: "text",
            Options: [],
            Examples: ["lama system account revoke admin2"])
    ];

    public static bool TryGetGroup(string group, out HelpGroup? helpGroup)
    {
        helpGroup = Groups.FirstOrDefault(g =>
            g.Group.Equals(group, StringComparison.OrdinalIgnoreCase));
        return helpGroup is not null;
    }

    public static bool TryGetCommand(string group, string actionPath, out HelpCommand? command)
    {
        var normalizedActionPath = NormalizeActionPath(actionPath);
        command = Commands.FirstOrDefault(c =>
            c.Group.Equals(group, StringComparison.OrdinalIgnoreCase) &&
            c.ActionPath.Equals(normalizedActionPath, StringComparison.OrdinalIgnoreCase));
        return command is not null;
    }

    public static bool TryGetSingleLevelCommand(string commandId, out HelpCommand? command)
    {
        command = Commands.FirstOrDefault(c =>
            c.CommandId.Equals(commandId, StringComparison.OrdinalIgnoreCase));
        return command is not null;
    }

    public static IEnumerable<HelpCommand> GetGroupCommands(string group) =>
        Commands
            .Where(c => c.Group.Equals(group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.ActionPath, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeActionPath(string actionPath) =>
        string.Join(' ', actionPath
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

