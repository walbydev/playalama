using Lama.Domain.Rating;
using Xunit;

namespace Lama.Domain.UnitTests.Rating;

public class EloCalculatorTests
{
    private readonly EloCalculator _calculator = new();

    [Fact]
    public void InitialRating_ShouldBe1200()
    {
        Assert.Equal(1200, EloCalculator.InitialRating);
    }

    [Fact]
    public void CalculateRatingChange_WinAgainstEqual_ShouldGainPoints()
    {
        var opponentRatings = new[] { 1200.0 };

        var change = _calculator.CalculateRatingChange(
            1200.0,
            opponentRatings,
            playerRank: 1, // Gagnant
            totalPlayers: 2);

        Assert.True(change > 0, "Gagner contre un adversaire de même niveau devrait donner des points");
    }

    [Fact]
    public void CalculateRatingChange_LoseAgainstEqual_ShouldLosePoints()
    {
        var opponentRatings = new[] { 1200.0 };

        var change = _calculator.CalculateRatingChange(
            1200.0,
            opponentRatings,
            playerRank: 2, // Deuxième
            totalPlayers: 2);

        Assert.True(change < 0, "Perdre contre un adversaire de même niveau devrait coûter des points");
    }

    [Fact]
    public void CalculateRatingChange_WinAgainstWeaker_ShouldGainLess()
    {
        var opponentRatings = new[] { 1000.0 }; // Plus faible

        var changeVsEqual = _calculator.CalculateRatingChange(1200, new[] { 1200.0 }, 1, 2);
        var changeVsWeaker = _calculator.CalculateRatingChange(1200, new[] { 1000.0 }, 1, 2);

        Assert.True(changeVsWeaker < changeVsEqual, 
            "Gagner contre un plus faible devrait donner moins de points");
    }

    [Fact]
    public void CalculateRatingChange_LoseAgainstStronger_ShouldLossLess()
    {
        var changeVsEqual = _calculator.CalculateRatingChange(1200, new[] { 1200.0 }, 2, 2);
        var changeVsStronger = _calculator.CalculateRatingChange(1200, new[] { 1400.0 }, 2, 2);

        // Perdre contre un plus fort coûte MOINS (c'est attendu)
        // Perdre contre un égal coûte plus (c'est inattendu)
        Assert.True(Math.Abs(changeVsStronger) < Math.Abs(changeVsEqual), 
            "Perdre contre un plus fort devrait coûter moins de points (résultat attendu)"); 
    }

    [Fact]
    public void ApplyRatingChange_ShouldAddChange()
    {
        var current = 1200.0;
        var change = 20.0;

        var result = _calculator.ApplyRatingChange(current, change);

        Assert.Equal(1220.0, result);
    }

    [Fact]
    public void ApplyRatingChange_ShouldNotGoBelowMinimum()
    {
        var current = 410.0;
        var change = -50.0;

        var result = _calculator.ApplyRatingChange(current, change);

        Assert.True(result >= 400, "Le rating ne devrait pas descendre en dessous de 400");
    }

    [Fact]
    public void CalculateRatingChange_WithMultipleOpponents_ShouldAverageExpectedScore()
    {
        var playerRating = 1200.0;
        var opponentRatings = new[] { 1100.0, 1200.0, 1300.0 };

        var change = _calculator.CalculateRatingChange(
            playerRating,
            opponentRatings,
            playerRank: 1,
            totalPlayers: 4);

        Assert.NotEqual(0, change);
    }
}

