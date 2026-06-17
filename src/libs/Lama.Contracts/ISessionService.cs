namespace Lama.Contracts;

/// <summary>
/// Représente le contexte de session persisté entre deux invocations
/// du mode commande par commande.
///
/// Deux types de session coexistent :
/// <list type="bullet">
///   <item>
///     <b>Session d'authentification</b> : token Admin/SuperAdmin, indépendante d'une partie.
///     Créée par <c>lama login</c>, effacée par <c>lama logout</c>.
///   </item>
///   <item>
///     <b>Session de partie</b> : GameId, PlayerId, rôle Host/Player/Spectator.
///     Créée par <c>lama game create/join</c>, effacée par <c>lama game end</c>.
///   </item>
/// </list>
///
/// Les deux peuvent coexister : un Admin peut s'authentifier puis rejoindre une partie
/// (dans ce cas son rôle de partie remplace son rôle Admin pour les commandes de jeu).
/// </summary>
/// <param name="GameId">Identifiant de la partie en cours. Null si hors partie.</param>
/// <param name="PlayerId">Identifiant du joueur courant. Null si hors partie.</param>
/// <param name="PlayerName">Nom d'affichage. Null si hors partie.</param>
/// <param name="Role">Rôle courant (SuperAdmin, Admin, Host, Player, Spectator).</param>
/// <param name="GameLevel">Niveau de la partie. Null si hors partie.</param>
/// <param name="AuthToken">Token HMAC signé. Null si non authentifié (Admin/SuperAdmin).</param>
/// <param name="TokenExpiresAt">Date d'expiration du token. Null si pas de token.</param>
/// <param name="CreatedAt">Date de création de la session (UTC).</param>
/// <param name="UpdatedAt">Date de dernière mise à jour (UTC).</param>
public record SessionContext(
    string?          GameId,
    string?          PlayerId,
    string?          PlayerName,
    Role             Role,
    GameLevel?       GameLevel,
    string?          AuthToken,
    DateTimeOffset?  TokenExpiresAt,
    DateTimeOffset   CreatedAt,
    DateTimeOffset   UpdatedAt);

/// <summary>
/// Service de gestion de la session locale.
/// Permet de persister et de charger le contexte de session entre
/// deux invocations du mode commande par commande.
///
/// La session est stockée dans un fichier JSON dans le répertoire
/// de configuration utilisateur (cross-platform via
/// <see cref="Environment.SpecialFolder.ApplicationData"/>).
///
/// Chemin typique :
/// <list type="bullet">
///   <item>Linux   : <c>~/.config/lama/session.json</c></item>
///   <item>Windows : <c>%APPDATA%\lama\session.json</c></item>
///   <item>macOS   : <c>~/Library/Application Support/lama/session.json</c></item>
/// </list>
///
/// La variable d'environnement <c>LAMA_SESSION_DIR</c> surcharge le répertoire
/// (tests automatisés, CI).
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Charge la session depuis le fichier de session local.
    /// Retourne <c>null</c> si aucune session n'existe ou si elle est corrompue.
    /// </summary>
    SessionContext? LoadSession();

    /// <summary>
    /// Persiste la session dans le fichier de session local.
    /// Crée le répertoire de destination si nécessaire.
    /// </summary>
    void SaveSession(SessionContext session);

    /// <summary>
    /// Supprime le fichier de session local.
    /// À appeler lors d'un <c>lama game end</c> ou <c>lama logout</c>.
    /// </summary>
    void ClearSession();

    /// <summary>
    /// Retourne le chemin complet du fichier de session (utile pour les diagnostics).
    /// </summary>
    string SessionFilePath { get; }
}
