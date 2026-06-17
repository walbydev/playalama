using Lama.Contracts;

namespace Lama.Console.Services;

/// <summary>
/// Implémentation du service de contrôle d'accès.
///
/// Matrice de permissions complète :
///
///   Commande                    | SuperAdmin | Admin | Host  | Player+Casual | Player+Std/Compet/Tourn | Spectator
///   ----------------------------|------------|-------|-------|---------------|-------------------------|----------
///   system.*                    |    ✅      |  ✅   |  ✗    |      ✗        |           ✗             |    ✗
///   system.account.*            |    ✅      |  ✗    |  ✗    |      ✗        |           ✗             |    ✗
///   game.create                 |    ✅      |  ✅   |  ✅   |      ✗        |           ✗             |    ✗
///   game.end.force / kick       |    ✅      |  ✅   |  ✅   |      ✗        |           ✗             |    ✗
///   game.join / list / show     |    ✅      |  ✅   |  ✅   |      ✅       |           ✅            |    ✅
///   game.pause / save           |    ✅      |  ✅   |  ✅   |      ✅       |           ✅            |    ✗
///   play.move / pass / swap     |    ✗       |  ✗    |  ✅   |      ✅       |           ✅            |    ✗
///   play.challenge              |    ✗       |  ✗    |  ✅   |      ✅       |           ✅            |    ✗
///   play.check / simulate       |    ✅      |  ✅   |  ✅   |      ✅       |           ✗             |    ✗
///   show.board / scores / hist  |    ✅      |  ✅   |  ✅   |      ✅       |           ✅            |    ✅
///   show.rack                   |    ✗       |  ✗    |  ✅   |      ✅       |           ✅            |    ✗
///   show.hints                  |    ✅      |  ✅   |  ✅   |      ✅       |           ✗             |    ✗
///   dict.check / search /anagram|    ✅      |  ✅   |  ✅   |      ✅       |           ✗             |    ✗
///   dict.install / remove       |    ✅      |  ✅   |  ✗    |      ✗        |           ✗             |    ✗
///   player.*                    |    ✅      |  ✅   |  ✅   |      ✅       |           ✅            |    ✅(lecture)
///   tournament.*                |    ✅      |  ✅   |  ✅   |      ✅       |           ✅            |    ✅(lecture)
///   login / logout              |    ✅      |  ✅   |  ✗    |      ✗        |           ✗             |    ✗
/// </summary>
public sealed class AccessControlService : IAccessControlService
{
    // ── Commandes réservées au SuperAdmin uniquement ──────────────────────────
    private static readonly HashSet<string> SuperAdminOnlyCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "system.account.create",
            "system.account.list",
            "system.account.revoke",
            "system.account.reset-password"
        };

    // ── Commandes publiques (pas d'authentification requise) ─────────────────
    // system.setup est accessible sans token car c'est le point d'entrée
    // du système. La commande elle-même refuse si un SuperAdmin existe déjà.
    private static readonly HashSet<string> PublicCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "system.setup"
        };

    // ── Commandes réservées à SuperAdmin + Admin (pas les joueurs) ────────────
    private static readonly HashSet<string> AdminCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "system.status",
            "system.restart",
            "system.shutdown",
            "system.config",
            "system.logs",
            "system.diagnostics",
            "system.update",
            "dict.install",
            "dict.remove",
            "dict.update",
            "dict.add-word",
            "dict.remove-word",
            "login",
            "logout"
        };

    // ── Commandes de gestion de partie réservées à Admin + Host ──────────────
    private static readonly HashSet<string> HostManagementCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "game.create",
            "game.end.force",
            "game.kick"
        };

    // ── Commandes d'aide/assistance bloquées hors mode Casual ────────────────
    private static readonly HashSet<string> CasualOnlyCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "play.check",
            "play.simulate",
            "show.hints",
            "dict.check",
            "dict.search",
            "dict.anagram"
        };

    // ── Commandes accessibles en lecture seule (spectateurs inclus) ──────────
    private static readonly HashSet<string> ReadOnlyCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "game.list",
            "game.show",
            "show.board",
            "show.scores",
            "show.history",
            "tournament.list",
            "tournament.show",
            "tournament.standings",
            "player.list",
            "player.show",
            "player.stats"
        };

    // ── Commandes de jeu — Host et Player (pas Admin ni Spectator) ───────────
    private static readonly HashSet<string> PlayCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "play.move",
            "play.pass",
            "play.swap",
            "play.challenge",
            "show.rack"
        };

    // ── Commandes accessibles à tous les joueurs actifs (Host + Player) ──────
    private static readonly HashSet<string> PlayerCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "game.join",
            "game.pause",
            "game.resume",
            "game.save",
            "game.end",
            "player.create",
            "player.delete",
            "player.rename",
            "player.reset-stats",
            "player.export",
            "player.import",
            "tournament.join",
            "tournament.create",
            "tournament.start",
            "tournament.end"
        };

    /// <inheritdoc />
    public AccessResult CheckAccess(string command, Role role, GameLevel? gameLevel = null)
    {
        if (string.IsNullOrWhiteSpace(command))
            return AccessResult.Denied("La commande ne peut pas être vide.");

        var cmd = command.Trim();

        // Commandes publiques : accessibles sans authentification ni rôle particulier
        // (ex: system.setup — accessible uniquement si aucun SuperAdmin n'existe encore)
        if (PublicCommands.Contains(cmd))
            return AccessResult.Allowed;

        // SuperAdmin : accès total
        if (role == Role.SuperAdmin)
            return AccessResult.Allowed;

        // Commandes SuperAdmin uniquement
        if (IsSuperAdminOnly(cmd))
            return AccessResult.Denied(
                $"La commande '{cmd}' est réservée au SuperAdmin.");

        // Admin : accès à tout sauf SuperAdmin-only et commandes de jeu
        if (role == Role.Admin)
        {
            // Un Admin ne peut pas jouer (protection anti-triche)
            if (PlayCommands.Contains(cmd))
                return AccessResult.Denied(
                    $"La commande '{cmd}' est réservée aux joueurs actifs. " +
                    "Les administrateurs ne peuvent pas jouer.");
            return AccessResult.Allowed;
        }

        // Commandes Admin-only
        if (IsAdminOnly(cmd))
            return AccessResult.Denied(
                $"La commande '{cmd}' est réservée aux administrateurs.");

        // Spectateur : lecture seule uniquement
        if (role == Role.Spectator)
        {
            return ReadOnlyCommands.Contains(cmd)
                ? AccessResult.Allowed
                : AccessResult.Denied(
                    $"La commande '{cmd}' n'est pas accessible en mode spectateur.");
        }

        // Host et Player : vérifications communes
        // Commandes de gestion de partie : Host uniquement
        if (HostManagementCommands.Contains(cmd))
        {
            return role == Role.Host
                ? AccessResult.Allowed
                : AccessResult.Denied(
                    $"La commande '{cmd}' est réservée à l'hôte de la partie.");
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

        // Commandes accessibles à Host + Player
        if (ReadOnlyCommands.Contains(cmd) ||
            PlayCommands.Contains(cmd)     ||
            PlayerCommands.Contains(cmd))
            return AccessResult.Allowed;

        // Commande inconnue : refus par défaut (fail-safe)
        return AccessResult.Denied($"Commande '{cmd}' inconnue ou non autorisée.");
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetAllowedCommands(Role role, GameLevel? gameLevel = null)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Les commandes publiques sont toujours disponibles, quel que soit le rôle
        allowed.UnionWith(PublicCommands);

        if (role == Role.SuperAdmin)
        {
            allowed.UnionWith(SuperAdminOnlyCommands);
            allowed.UnionWith(AdminCommands);
            allowed.UnionWith(HostManagementCommands);
            allowed.UnionWith(CasualOnlyCommands);
            allowed.UnionWith(ReadOnlyCommands);
            allowed.UnionWith(PlayerCommands);
            // SuperAdmin ne joue pas
            return allowed;
        }

        if (role == Role.Admin)
        {
            allowed.UnionWith(AdminCommands);
            allowed.UnionWith(HostManagementCommands);
            allowed.UnionWith(CasualOnlyCommands);
            allowed.UnionWith(ReadOnlyCommands);
            allowed.UnionWith(PlayerCommands);
            // Admin ne joue pas : PlayCommands non inclus
            return allowed;
        }

        allowed.UnionWith(ReadOnlyCommands);

        if (role == Role.Spectator)
            return allowed;

        // Host et Player
        allowed.UnionWith(PlayerCommands);
        allowed.UnionWith(PlayCommands);

        if (role == Role.Host)
            allowed.UnionWith(HostManagementCommands);

        if (gameLevel is null or GameLevel.Casual)
            allowed.UnionWith(CasualOnlyCommands);

        return allowed;
    }

    // ─── Helpers privés ──────────────────────────────────────────────────────

    private static bool IsSuperAdminOnly(string command) =>
        SuperAdminOnlyCommands.Any(c =>
            command.Equals(c, StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith(c + ".", StringComparison.OrdinalIgnoreCase));

    private static bool IsAdminOnly(string command) =>
        AdminCommands.Any(c =>
            command.Equals(c, StringComparison.OrdinalIgnoreCase) ||
            command.StartsWith(c + ".", StringComparison.OrdinalIgnoreCase));
}
