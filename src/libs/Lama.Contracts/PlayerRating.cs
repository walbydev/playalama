namespace Lama.Contracts;

/// <summary>
/// Représente le rating (Elo) et le niveau d'un joueur.
/// </summary>
/// <param name="PlayerId">Identifiant du joueur.</param>
/// <param name="EloRating">Rating Elo actuel (base 1200).</param>
/// <param name="Level">Niveau délibéré (1-6, voir LevelEnum).</param>
/// <param name="LevelName">Nom du niveau (ex: "Jeune Lama").</param>
/// <param name="WinsCount">Nombre total de victoires.</param>
/// <param name="LossesCount">Nombre total de défaites.</param>
/// <param name="AbandonedCount">Nombre de parties abandonnées.</param>
/// <param name="CurrentStreak">Série actuelle de victoires (négatif si défaites).</param>
/// <param name="HighestStreak">Meilleure série atteinte.</param>
/// <param name="HighScore">Meilleur score obtenu dans une partie.</param>
/// <param name="AverageScore">Score moyen par partie.</param>
/// <param name="LastGameAt">Date de la dernière partie joué.</param>
/// <param name="UpdatedAt">Date de la dernière mise à jour du rating.</param>
public record PlayerRating(
    string PlayerId,
    double EloRating,
    int Level,
    string LevelName,
    int WinsCount = 0,
    int LossesCount = 0,
    int AbandonedCount = 0,
    int CurrentStreak = 0,
    int HighestStreak = 0,
    int HighScore = 0,
    double AverageScore = 0,
    DateTimeOffset? LastGameAt = null,
    DateTimeOffset UpdatedAt = default)
{
    /// <summary>Taux de victoire (0-100%).</summary>
    public double WinRate => WinsCount + LossesCount > 0 
        ? (double)WinsCount / (WinsCount + LossesCount) * 100 
        : 0;

    /// <summary>Total de parties jouées (moins les abandons).</summary>
    public int TotalGames => WinsCount + LossesCount;
}

/// <summary>
/// Énumération des niveaux Lama.
/// </summary>
public enum LevelEnum
{
    NotRanked = 0,
    JeuneLama = 1,          // 1100-1300
    LamaAcrobate = 2,        // 1300-1500
    LamaMaitre = 3,          // 1500-1700
    LamaSeigneur = 4,        // 1700-1900
    LamaMythique = 5,        // 1900-2100
    LamaEternel = 6          // 2100+
}

/// <summary>
/// Résultat d'une partie, utilisé pour mettre à jour les ratings.
/// </summary>
/// <param name="GameId">Identifiant unique de la partie.</param>
/// <param name="PlayerId">Identifiant du joueur.</param>
/// <param name="PlayerName">Nom du joueur au moment de la partie.</param>
/// <param name="Rank">Rang final (1 = gagnant, 2 = 2è, etc.).</param>
/// <param name="IsAbandoned">Si la partie a été abandonnée.</param>
/// <param name="Score">Score final du joueur.</param>
/// <param name="OpponentIds">Liste des adversaires rencontrés.</param>
/// <param name="OpponentRatings">Ratings Elo des adversaires.</param>
/// <param name="PlayedAt">Date/heure de la partie.</param>
/// <param name="DurationSeconds">Durée de la partie en secondes.</param>
public record GameResult(
    string GameId,
    string PlayerId,
    string PlayerName,
    int Rank,
    bool IsAbandoned,
    int Score,
    IReadOnlyList<string> OpponentIds,
    IReadOnlyList<double> OpponentRatings,
    DateTimeOffset PlayedAt,
    int DurationSeconds);

/// <summary>
/// Résumé des statistiques d'un joueur sur différentes périodes.
/// </summary>
/// <param name="PlayerId">Identifiant du joueur.</param>
/// <param name="All">Stats toutes les périodes.</param>
/// <param name="Last7Days">Stats derniers 7 jours.</param>
/// <param name="Last30Days">Stats derniers 30 jours.</param>
/// <param name="Last365Days">Stats dernière année.</param>
public record PlayerStatistics(
    string PlayerId,
    PeriodStats All,
    PeriodStats Last7Days,
    PeriodStats Last30Days,
    PeriodStats Last365Days);

/// <summary>
/// Statistiques sur une période donnée.
/// </summary>
/// <param name="Wins">Victoires.</param>
/// <param name="Losses">Défaites.</param>
/// <param name="Abandoned">Abandons.</param>
/// <param name="HighScore">Meilleur score.</param>
/// <param name="AverageScore">Score moyen.</param>
public record PeriodStats(
    int Wins,
    int Losses,
    int Abandoned,
    int HighScore,
    double AverageScore)
{
    public int TotalGames => Wins + Losses;
    public double WinRate => TotalGames > 0 ? (double)Wins / TotalGames * 100 : 0;
}

