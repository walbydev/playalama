namespace Lama.Server.Bots;

/// <summary>
/// Profil statique d'un joueur IA : identité, niveau et paramètres de difficulté.
/// </summary>
public sealed record BotProfile(
    string BotId,
    /// <summary>GUID stable utilisé pour la persistance DB (Elo, historique).</summary>
    Guid BotGuid,
    string Name,
    int Level,
    double InitialElo,
    /// <summary>Nombre maximal de suggestions à considérer (beam search width).</summary>
    int BeamWidth,
    /// <summary>Probabilité de passer même si des coups sont disponibles (0 = jamais, 1 = toujours).</summary>
    double PassRate,
    /// <summary>Nombre de suggestions de tête considérées avant sélection finale.</summary>
    int CandidateWindow = 6,
    /// <summary>Taille du sous-ensemble "faible" (petits scores/mots courts) pour les coups dégradés.</summary>
    int WeakPoolSize = 3,
    /// <summary>Probabilité de choisir un coup volontairement sous-optimal.</summary>
    double WeakMoveRate = 0.0)
{
    /// <summary>Identifiant persistant au format N (sans tirets) pour le mapping joueur↔moteur.</summary>
    public string PersistentPlayerId => BotGuid.ToString("N");
}
