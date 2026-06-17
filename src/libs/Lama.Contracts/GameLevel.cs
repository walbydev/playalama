namespace Lama.Contracts;

/// <summary>
/// Niveau de jeu d'une partie LAMA.
/// Détermine quelles aides et fonctionnalités sont disponibles durant la partie.
/// </summary>
public enum GameLevel
{
    /// <summary>
    /// Mode décontracté : toutes les aides activées (hints, dict check, dry-run, simulate).
    /// Idéal pour les débutants ou les parties entre amis.
    /// </summary>
    Casual,

    /// <summary>
    /// Mode standard : aides désactivées, challenge autorisé.
    /// Équilibre entre accessibilité et compétition.
    /// </summary>
    Standard,

    /// <summary>
    /// Mode compétitif : aides désactivées, challenge obligatoire, logs stricts.
    /// Pour les joueurs confirmés.
    /// </summary>
    Competitive,

    /// <summary>
    /// Mode tournoi : règles figées par l'organisateur, aucune déviation possible.
    /// </summary>
    Tournament
}
