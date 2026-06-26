namespace Lama.Server.Bots;

/// <summary>
/// Profil statique d'un joueur IA : identité, niveau et paramètres de difficulté.
/// </summary>
public sealed record BotProfile(
    string BotId,
    string Name,
    int Level,
    double InitialElo,
    /// <summary>Nombre maximal de suggestions à considérer (beam search width).</summary>
    int BeamWidth,
    /// <summary>Probabilité de passer même si des coups sont disponibles (0 = jamais, 1 = toujours).</summary>
    double PassRate);
