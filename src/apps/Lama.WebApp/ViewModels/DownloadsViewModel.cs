namespace Lama.WebApp.ViewModels;

public sealed class DownloadsViewModel
{
    public IReadOnlyList<DownloadPlatform> Platforms { get; } =
    [
        new("🐧 Linux",   "x64 / ARM64",
        [
            new("linux-x64",   "curl -fsSL https://get.playalama.io/linux | bash",         "/downloads/lama-linux-x64.zip"),
            new("linux-arm64", "curl -fsSL https://get.playalama.io/linux-arm64 | bash",   "/downloads/lama-linux-arm64.zip"),
        ]),
        new("🪟 Windows", "x64",
        [
            new("win-x64",   null, "/downloads/lama-win-x64.zip"),
        ]),
        new("🍎 macOS",   "Intel / Apple Silicon",
        [
            new("osx-x64",   null, "/downloads/lama-osx-x64.zip"),
            new("osx-arm64", null, "/downloads/lama-osx-arm64.zip"),
        ]),
    ];

    public IReadOnlyList<CliCommandGroup> CommandGroups { get; } =
    [
        new("🎮 game", "Gérer les parties",
        [
            new("Créer une partie",          "lama game create Alice"),
            new("Créer (options)",           "lama game create Alice --level casual --with-ai --max-players 4"),
            new("Rejoindre une partie",      "lama game join <game-id> Alice"),
            new("Rejoindre (privée)",        "lama game join <game-id> Bob --password <secret>"),
            new("Lister les parties",        "lama game list"),
            new("Afficher le plateau",       "lama game show"),
            new("Afficher (JSON)",           "lama game show --output json"),
            new("Démarrer une partie (lobby)", "lama game start"),
            new("Sauvegarder",               "lama game save"),
            new("Exporter un snapshot",      "lama game save --file partie.json"),
            new("Terminer la partie",        "lama game end"),
        ]),

        new("▶ play", "Jouer un tour",
        [
            new("Poser un mot",              "lama play move H8 BONJOUR H"),
            new("Poser avec joker",          "lama play move H8 bonJOUR H"),
            new("Simuler un coup",           "lama play move H8 BONJOUR H --dry-run"),
            new("Passer son tour",           "lama play pass"),
            new("Échanger des lettres",      "lama play swap ABCDE"),
            new("Échanger tout le rack",     "lama play swap --all"),
            new("Contester un mot",          "lama play challenge"),
            new("Vérifier un coup",          "lama play check H8 BONJOUR H"),
            new("Demander une suggestion",   "lama play suggest --top 5"),
        ]),

        new("👁 show", "Afficher l'état",
        [
            new("Plateau",                   "lama show board"),
            new("Rack",                      "lama show rack"),
            new("Scores",                    "lama show scores"),
            new("Historique",                "lama show history"),
            new("Historique (10 derniers)",  "lama show history --last 10"),
        ]),

        new("🏆 rating", "Classement et statistiques",
        [
            new("Mon classement",            "lama rating show"),
            new("Classement d'un joueur",    "lama rating show <playerId>"),
            new("Classement mondial",        "lama rating leaderboard --queue open --top 20"),
            new("Statistiques (30 jours)",   "lama rating stats --30d"),
        ]),

        new("📖 dict", "Dictionnaire",
        [
            new("Vérifier un mot",           "lama dict check BONJOUR"),
            new("Rechercher (joker ?)",      "lama dict search ?AMA"),
            new("Anagrammes",                "lama dict anagram AEILNRT"),
            new("Anagrammes (min 4 lettres)", "lama dict anagram AEILNRT --min-length 4"),
        ]),

        new("👤 player", "Profil joueur",
        [
            new("Créer un profil",           "lama player create Alice"),
            new("Créer avec métadonnées",    "lama player create Alice --country FR --region 75"),
            new("Lister les joueurs",        "lama player list"),
            new("Voir un profil",            "lama player show <playerId>"),
            new("Modifier un profil",        "lama player update <playerId> --pseudo Aly"),
        ]),

        new("⚔ tournament", "Tournoi",
        [
            new("Créer un tournoi",          "lama tournament create Coupe"),
        ]),

        new("⚙ system", "Système & administration",
        [
            new("Connexion (admin)",         "lama login"),
            new("Déconnexion",               "lama logout"),
            new("État du système",           "lama system status"),
            new("Initialiser le système",    "lama system setup"),
            new("Voir le serveur",           "lama system server show"),
            new("Effacer le serveur",        "lama system server clear"),
            new("Créer un admin",            "lama system account create <username>"),
            new("Lister les comptes",        "lama system account list"),
            new("Révoquer un admin",         "lama system account revoke <username>"),
            new("Mode en ligne",             "LAMA_RUNTIME_MODE=online lama game create Alice"),
            new("Aide complète",             "lama --help"),
        ]),
    ];

    public HashSet<string> CopiedCommands { get; } = [];

    public void MarkCopied(string key)   => CopiedCommands.Add(key);
    public void ClearCopied(string key)  => CopiedCommands.Remove(key);
    public bool IsCopied(string key)     => CopiedCommands.Contains(key);
}

public sealed record DownloadPlatform(string Os, string Arch, IReadOnlyList<DownloadItem> Items);
public sealed record DownloadItem(string Label, string? InstallCmd, string ZipUrl);
public sealed record CliCommandExample(string Description, string Command);
public sealed record CliCommandGroup(string Name, string Description, IReadOnlyList<CliCommandExample> Commands);
