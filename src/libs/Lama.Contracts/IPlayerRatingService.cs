namespace Lama.Contracts;

/// <summary>
/// Service pour gérer les ratings et statistiques des joueurs.
/// </summary>
public interface IPlayerRatingService
{
    /// <summary>
    /// Obtient le rating actuel d'un joueur.
    /// Retourne un rating par défaut (1200, Jeune Lama) si le joueur n'existe pas.
    /// </summary>
    Task<PlayerRating> GetRatingAsync(string playerId);

    /// <summary>
    /// Enregistre le résultat d'une partie et met à jour les ratings des joueurs.
    /// </summary>
    Task UpdateRatingsAsync(IReadOnlyList<GameResult> gameResults);

    /// <summary>
    /// Obtient les statistiques d'un joueur.
    /// </summary>
    Task<PlayerStatistics> GetStatisticsAsync(string playerId);

    /// <summary>
    /// Obtient un classement mondial par Elo (top N).
    /// </summary>
    Task<IReadOnlyList<PlayerRating>> GetLeaderboardAsync(
        RankingQueue queue = RankingQueue.GlobalPrestige,
        int topCount = 100);

    /// <summary>
    /// Obtient les joueurs du même niveau.
    /// </summary>
    Task<IReadOnlyList<PlayerRating>> GetPlayersByLevelAsync(int level);

    /// <summary>
    /// Réinitialise les ratings (admin only).
    /// </summary>
    Task ResetRatingsAsync();
}

