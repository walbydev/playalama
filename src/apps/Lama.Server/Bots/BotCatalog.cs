namespace Lama.Server.Bots;

/// <summary>
/// Catalogue des joueurs IA disponibles dans Lama.
/// Chaque bot possède un identifiant fixe et un GUID stable pour la persistance Elo.
/// </summary>
public static class BotCatalog
{
    public static readonly IReadOnlyList<BotProfile> All =
    [
        // Niveau 1 — Débutant : évite les gros coups, joue court, échange souvent.
        new("bot-karim",  new Guid("00000000-0000-0000-0000-000000000001"), "B'Karim",  Level: 1, InitialElo: 700,
            BeamWidth: 1, PassRate: 0.15, CandidateWindow: 8, WeakPoolSize: 5, WeakMoveRate: 0.90,
            SwapOnNoSuggestionRate: 0.90, SwapOnWeakMoveRate: 0.45, WeakMoveScoreThreshold: 20, SwapMaxLetters: 4,
            BigMoveScoreThreshold: 22, BigMoveSkipRate: 0.85),

        // Niveau 2 — Novice : encore faible mais commence à exploiter quelques coups.
        new("bot-sophie", new Guid("00000000-0000-0000-0000-000000000002"), "B'Ingrid", Level: 2, InitialElo: 1000,
            BeamWidth: 3, PassRate: 0.10, CandidateWindow: 8, WeakPoolSize: 5, WeakMoveRate: 0.70,
            SwapOnNoSuggestionRate: 0.75, SwapOnWeakMoveRate: 0.30, WeakMoveScoreThreshold: 16, SwapMaxLetters: 3,
            BigMoveScoreThreshold: 30, BigMoveSkipRate: 0.60),

        // Niveau 3 — Intermédiaire : équilibre entre coups forts et coups modérés.
        new("bot-thomas", new Guid("00000000-0000-0000-0000-000000000003"), "B'Thomas", Level: 3, InitialElo: 1300,
            BeamWidth: 10, PassRate: 0.06, CandidateWindow: 8, WeakPoolSize: 4, WeakMoveRate: 0.40,
            SwapOnNoSuggestionRate: 0.50, SwapOnWeakMoveRate: 0.18, WeakMoveScoreThreshold: 12, SwapMaxLetters: 3,
            BigMoveScoreThreshold: 45, BigMoveSkipRate: 0.25),

        // Niveau 4 — Avancé : joue généralement le meilleur coup, gros points acceptés.
        new("bot-leila",  new Guid("00000000-0000-0000-0000-000000000004"), "B'Liv",    Level: 4, InitialElo: 1600,
            BeamWidth: 25, PassRate: 0.03, CandidateWindow: 6, WeakPoolSize: 3, WeakMoveRate: 0.15,
            SwapOnNoSuggestionRate: 0.25, SwapOnWeakMoveRate: 0.08, WeakMoveScoreThreshold: 8, SwapMaxLetters: 2,
            BigMoveScoreThreshold: 0, BigMoveSkipRate: 0.0),

        // Niveau 5 — Expert : always optimal, never skips big moves.
        new("bot-victor", new Guid("00000000-0000-0000-0000-000000000005"), "B'Victor", Level: 5, InitialElo: 1900,
            BeamWidth: 50, PassRate: 0.00, CandidateWindow: 6, WeakPoolSize: 2, WeakMoveRate: 0.0,
            SwapOnNoSuggestionRate: 0.12, SwapOnWeakMoveRate: 0.03, WeakMoveScoreThreshold: 6, SwapMaxLetters: 2,
            BigMoveScoreThreshold: 0, BigMoveSkipRate: 0.0),
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
