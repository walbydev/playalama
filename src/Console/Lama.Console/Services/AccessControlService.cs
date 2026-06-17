using Lama.Contracts;

namespace Lama.Console.Services;

/// <summary>
/// Implémentation du service de contrôle d'accès.
///
/// Matrice de permissions :
///
///   Commande                  | Admin | Player+Casual | Player+Standard | Player+Competitive | Player+Tournament | Spectator
///   --------------------------|-------|---------------|-----------------|--------------------|-------------------|----------
///   system.*                  |  ✅   |      ✗        |       ✗         |        ✗           |        ✗          |    ✗
///   game.create               |  ✅   |      ✗        |       ✗         |        ✗           |        ✗          |    ✗
///   game.end (forcé)          |  ✅   |      ✗        |       ✗         |        ✗           |        ✗          |    ✗
///   game.join / list / show   |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✅
///   game.pause / save         |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✗
///   play.move                 |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✗
///   play.pass / swap          |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✗
///   play.challenge            |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✗
///   play.check                |  ✅   |      ✅       |       ✗         |        ✗           |        ✗          |    ✗
///   play.simulate / dry-run   |  ✅   |      ✅       |       ✗         |        ✗           |        ✗          |    ✗
///   show.board / scores / hist|  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✅
///   show.rack                 |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✗
///   show.hints                |  ✅   |      ✅       |       ✗         |        ✗           |        ✗          |    ✗
///   dict.check / search       |  ✅   |      ✅       |       ✗         |        ✗           |        ✗          |    ✗
///   dict.anagram              |  ✅   |      ✅       |       ✗         |        ✗           |        ✗          |    ✗
///   dict.install / remove     |  ✅   |      ✗        |       ✗         |        ✗           |        ✗          |    ✗
///   player.*                  |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✗
///   tournament.*              |  ✅   |      ✅       |       ✅        |        ✅          |        ✅         |    ✅
/// </summary>
public sealed class AccessControlService : IAccessControlService
{
    // Commandes réservées aux admins (aucun niveau de partie ne les débloque)
    private static readonly HashSet<string> AdminOnlyCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "system.status",
        "system.restart",
        "system.shutdown",
        "system.config",
        "system.logs",
        "system.diagnostics",
        "system.update",
        "game.create",
        "game.end.force",
        "dict.install",
        "dict.remove",
        "dict.update",
        "dict.add-word",
        "dict.remove-word"
    };

    // Commandes d'aide bloquées en Standard, Competitive et Tournament
    private static readonly HashSet<string> CasualOnlyCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "play.check",
        "play.simulate",
        "show.hints",
        "dict.check",
        "dict.search",
        "dict.anagram"
    };

    // Commandes accessibles en lecture seule (spectateurs inclus)
    private static readonly HashSet<string> ReadOnlyCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "game.list",
        "game.show",
        "show.board",
        "show.scores",
        "show.history",
        "show.game",
        "tournament.list",
        "tournament.show",
        "tournament.standings"
    };

    // Commandes interdites aux spectateurs mais ouvertes aux joueurs
    private static readonly HashSet<string> PlayerCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "game.join",
        "game.pause",
        "game.resume",
        "game.save",
        "play.move",
        "play.pass",
        "play.swap",
        "play.challenge",
        "show.rack",
        "player.create",
        "player.delete",
        "player.show",
        "player.list",
        "player.stats",
        "player.rename",
        "player.reset-stats",
        "player.export",
        "player.import",
        "tournament.join",
        "tournament.create",
        "tournament.start",
        "tournament.end"
    };

    public AccessResult CheckAccess(string command, Role role, GameLevel? gameLevel = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return AccessResult.Denied("La commande ne peut pas être vide.");

        var cmd = command.Trim();

        // Les admins ont accès à tout
        if (role == Role.Admin)
            return AccessResult.Allowed;

        // Commandes réservées aux admins
        if (IsAdminOnly(cmd))
            return AccessResult.Denied(
                $"La commande '{cmd}' est réservée aux administrateurs.");

        // Les spectateurs n'ont accès qu'aux commandes en lecture seule
        if (role == Role.Spectator)
        {
            return ReadOnlyCommands.Contains(cmd)
                ? AccessResult.Allowed
                : AccessResult.Denied(
                    $"La commande '{cmd}' n'est pas accessible en mode spectateur.");
        }

        // Commandes d'aide bloquées hors mode Casual
        if (CasualOnlyCommands.Contains(cmd))
        {
            if (gameLevel is null or GameLevel.Casual)
                return AccessResult.Allowed;

            return AccessResult.Denied(
                $"La commande '{cmd}' est désactivée en mode {gameLevel}. " +
                "Les aides sont réservées au mode Casual.");
        }

        // Commandes joueur (lecture + actions)
        if (ReadOnlyCommands.Contains(cmd) || PlayerCommands.Contains(cmd))
            return AccessResult.Allowed;

        // Commande inconnue : accès refusé par défaut (fail-safe)
        return AccessResult.Denied($"Commande '{cmd}' inconnue ou non autorisée.");
    }

    public IReadOnlySet<string> GetAllowedCommands(Role role, GameLevel? gameLevel = null)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (role == Role.Admin)
        {
            allowed.UnionWith(AdminOnlyCommands);
            allowed.UnionWith(CasualOnlyCommands);
            allowed.UnionWith(ReadOnlyCommands);
            allowed.UnionWith(PlayerCommands);
            return allowed;
        }

        allowed.UnionWith(ReadOnlyCommands);

        if (role == Role.Spectator)
            return allowed;

        // Joueur
        allowed.UnionWith(PlayerCommands);

        if (gameLevel is null or GameLevel.Casual)
            allowed.UnionWith(CasualOnlyCommands);

        return allowed;
    }

    // Vérifie si une commande correspond à une commande admin-only
    // (supporte les préfixes, ex: "system.config.show" → bloqué car préfixe "system.config")
    private static bool IsAdminOnly(string command) =>
        AdminOnlyCommands.Any(admin =>
            command.Equals(admin, StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith(admin + ".", StringComparison.OrdinalIgnoreCase));
}
