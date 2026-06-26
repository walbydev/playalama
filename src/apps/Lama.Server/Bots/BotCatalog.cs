namespace Lama.Server.Bots;

/// <summary>
/// Catalogue des joueurs IA disponibles dans Lama.
/// Chaque bot possède un identifiant fixe et un GUID stable pour la persistance Elo.
/// </summary>
public static class BotCatalog
{
    public static readonly IReadOnlyList<BotProfile> All =
    [
        new("bot-karim",  new Guid("00000000-0000-0000-0000-000000000001"), "Karim",  Level: 1, InitialElo: 900,  BeamWidth: 3,  PassRate: 0.20),
        new("bot-sophie", new Guid("00000000-0000-0000-0000-000000000002"), "Sophie", Level: 2, InitialElo: 1100, BeamWidth: 8,  PassRate: 0.12),
        new("bot-thomas", new Guid("00000000-0000-0000-0000-000000000003"), "Thomas", Level: 3, InitialElo: 1300, BeamWidth: 15, PassRate: 0.06),
        new("bot-leila",  new Guid("00000000-0000-0000-0000-000000000004"), "Leïla",  Level: 4, InitialElo: 1600, BeamWidth: 25, PassRate: 0.03),
        new("bot-victor", new Guid("00000000-0000-0000-0000-000000000005"), "Victor", Level: 5, InitialElo: 1900, BeamWidth: 50, PassRate: 0.00),
    ];

    public static BotProfile? Find(string botId) =>
        All.FirstOrDefault(b => string.Equals(b.BotId, botId, StringComparison.Ordinal));

    public static BotProfile? FindByGuid(Guid guid) =>
        All.FirstOrDefault(b => b.BotGuid == guid);

    /// <summary>Bot par défaut (niveau 1) proposé quand <c>EnableAi = true</c> sans sélection explicite.</summary>
    public static BotProfile Default => All[0];

    public static bool IsBot(string playerId) =>
        All.Any(b => string.Equals(b.BotId, playerId, StringComparison.Ordinal));
}
