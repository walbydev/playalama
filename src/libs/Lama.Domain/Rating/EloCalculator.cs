namespace Lama.Domain.Rating;

/// <summary>
/// Calcule les variations de rating Elo.
/// Formule Elo standard avec K-factor adapté au jeu Lama.
/// </summary>
public sealed class EloCalculator
{
    /// <summary>
    /// K-factor pour les parties classées.
    /// Plus élevé = plus de variabilité (32 Elo standard, 40 ici pour plus de dynamique).
    /// </summary>
    private const double KFactor = 40.0;

    /// <summary>
    /// K-factor réduit pour joueurs très élevés (2400+).
    /// </summary>
    private const double KFactorElite = 20.0;

    /// <summary>
    /// Rating minimum initial.
    /// </summary>
    private const double MinRating = 400;

    /// <summary>
    /// Rating de base initial pour un nouveau joueur.
    /// </summary>
    public const double InitialRating = 1200;

    /// <summary>
    /// Calcule le score réel basé sur le rang du joueur.
    /// Pour une partie à N joueurs : score = (N - rang) / (N - 1)
    /// - 1er de 2 : 1
    /// - 2e de 2 : 0
    /// - 1er de 4 : 1
    /// - 2e de 4 : 2/3
    /// - 4e de 4 : 0
    /// </summary>
    private double CalculateActualScore(int playerRank, int totalPlayers)
    {
        if (totalPlayers <= 1)
            return 0.5; // Défaut si problème

        // Formule : (N - rang) / (N - 1)
        // Gagnant (rang 1) → (N - 1) / (N - 1) = 1
        // Dernier → (N - N) / (N - 1) = 0
        return (totalPlayers - playerRank) / (double)(totalPlayers - 1);
    }

    /// <summary>
    /// Calcule le score attendu contre une liste d'adversaires.
    /// Moyenne des résultats attendus contre chaque adversaire.
    /// </summary>
    private double CalculateExpectedScore(
        double playerRating,
        IReadOnlyList<double> opponentRatings)
    {
        if (opponentRatings.Count == 0)
            return 0.5;

        var totalExpected = 0.0;

        foreach (var opponentRating in opponentRatings)
        {
            // Formule Elo : E = 1 / (1 + 10^((opponent - player) / 400))
            var diff = opponentRating - playerRating;
            var expected = 1.0 / (1.0 + Math.Pow(10, diff / 400.0));
            totalExpected += expected;
        }

        return totalExpected / opponentRatings.Count;
    }

    /// <summary>
    /// Applique le changement Elo au rating actuel.
    /// </summary>
    public double ApplyRatingChange(double currentRating, double change)
    {
        var newRating = currentRating + change;
        return Math.Max(newRating, MinRating);
    }

    /// <summary>
    /// Calcule la variation Elo après une partie.
    /// </summary>
    /// <param name="playerRating">Rating actuel du joueur.</param>
    /// <param name="opponentRatings">Ratings des adversaires.</param>
    /// <param name="playerRank">Rang du joueur (1 = gagnant, 2 = 2è, etc.).</param>
    /// <param name="totalPlayers">Nombre total de joueurs dans la partie.</param>
    /// <returns>La variation Elo (peut être négative).</returns>
    public double CalculateRatingChange(
        double playerRating,
        IReadOnlyList<double> opponentRatings,
        int playerRank,
        int totalPlayers)
    {
        if (opponentRatings.Count == 0)
            return 0;

        // Score attendu basé sur les ratings des adversaires
        var expectedScore = CalculateExpectedScore(playerRating, opponentRatings);

        // Score réel basé sur le rang
        var actualScore = CalculateActualScore(playerRank, totalPlayers);

        // K-factor adapté au rating du joueur
        var k = playerRating >= 2400 ? KFactorElite : KFactor;

        // Changement Elo
        var ratingChange = k * (actualScore - expectedScore);

        return ratingChange;
    }
}
