namespace Lama.Server.Bots;

/// <summary>
/// Catalogue des joueurs IA disponibles dans Lama.
/// Chaque bot est une entité permanente avec un identifiant fixe.
/// </summary>
public static class BotCatalog
{
    public static readonly IReadOnlyList<BotProfile> All =
    [
        new("bot-karim",  "Karim",  Level: 1, InitialElo: 900,  BeamWidth: 3,  PassRate: 0.20),
        // Niveau 2-5 : à implémenter en Phase 2
        // new("bot-sophie",  "Sophie",  Level: 2, InitialElo: 1100, BeamWidth: 8,  PassRate: 0.12),
        // new("bot-thomas",  "Thomas",  Level: 3, InitialElo: 1300, BeamWidth: 15, PassRate: 0.06),
        // new("bot-leila",   "Leïla",   Level: 4, InitialElo: 1600, BeamWidth: 25, PassRate: 0.03),
        // new("bot-victor",  "Victor",  Level: 5, InitialElo: 1900, BeamWidth: 50, PassRate: 0.00),
    ];

    public static BotProfile? Find(string botId) =>
        All.FirstOrDefault(b => string.Equals(b.BotId, botId, StringComparison.Ordinal));

    /// <summary>Bot par défaut proposé à la création de partie.</summary>
    public static BotProfile Default => All[0];
}
