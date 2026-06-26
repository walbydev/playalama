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
    double PassRate)
{
    /// <summary>Identifiant persistant au format N (sans tirets) pour le mapping joueur↔moteur.</summary>
    public string PersistentPlayerId => BotGuid.ToString("N");
}
