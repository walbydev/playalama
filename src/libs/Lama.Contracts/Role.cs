namespace Lama.Contracts;

/// <summary>
/// Rôle d'un utilisateur dans le système LAMA.
/// Détermine les commandes accessibles via <see cref="IAccessControlService"/>.
///
/// Hiérarchie :
/// <code>
/// SuperAdmin  → administration globale du système (compte unique)
/// Admin       → administration déléguée (0..N comptes, créés par SuperAdmin)
/// Host        → créateur d'une partie : joue ET gère sa partie
/// Player      → joueur inscrit à une partie
/// Spectator   → observateur en lecture seule
/// </code>
///
/// Règle anti-triche : SuperAdmin et Admin ne peuvent pas jouer.
/// Host joue comme Player mais dispose en plus des droits de gestion de sa partie.
/// show.rack n'affiche que le rack du joueur courant — jamais celui des adversaires.
/// </summary>
public enum Role
{
    /// <summary>
    /// Super-administrateur — compte unique créé à l'initialisation (<c>lama system setup</c>).
    /// Peut créer, modifier et révoquer des comptes Admin.
    /// Accès à toutes les commandes système.
    /// Ne peut pas jouer (protection anti-triche).
    /// </summary>
    SuperAdmin,

    /// <summary>
    /// Administrateur délégué — créé par le SuperAdmin.
    /// Accès aux commandes système et de gestion.
    /// Ne peut pas jouer (protection anti-triche).
    /// </summary>
    Admin,

    /// <summary>
    /// Hôte de la partie — joueur qui a créé la partie (<c>lama game create</c>).
    /// Joue comme un Player ET peut gérer sa propre partie
    /// (pause, fin forcée, expulsion d'un joueur).
    /// N'a pas accès aux commandes système.
    /// </summary>
    Host,

    /// <summary>
    /// Joueur actif — inscrit à une partie (<c>lama game join</c>).
    /// Peut jouer, voir son propre rack et le plateau.
    /// Ne peut pas voir le rack des adversaires.
    /// </summary>
    Player,

    /// <summary>
    /// Spectateur — observateur en lecture seule.
    /// Peut voir le plateau et les scores, pas les racks.
    /// Aucune action de jeu autorisée.
    /// </summary>
    Spectator
}
