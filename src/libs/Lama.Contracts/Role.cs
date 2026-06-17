namespace Lama.Contracts;

/// <summary>
/// Rôle d'un utilisateur dans le système LAMA.
/// Détermine les commandes accessibles indépendamment du contexte de partie.
/// </summary>
public enum Role
{
    /// <summary>
    /// Accès complet : gestion système, parties, joueurs, tournois.
    /// </summary>
    Admin,

    /// <summary>
    /// Joueur actif : jouer, voir son rack, voir le plateau.
    /// </summary>
    Player,

    /// <summary>
    /// Spectateur : lecture seule, aucune action de jeu.
    /// </summary>
    Spectator
}
