using Lama.Contracts;
using Lama.Domain.Rating;
using Xunit;

namespace Lama.Domain.UnitTests.Rating;

public class LevelDeterminerTests
{
    private readonly LevelDeterminer _determiner = new();

    [Theory]
    [InlineData(1050, LevelEnum.NotRanked)]
    [InlineData(1100, LevelEnum.JeuneLama)]
    [InlineData(1200, LevelEnum.JeuneLama)]
    [InlineData(1300, LevelEnum.LamaAcrobate)]
    [InlineData(1500, LevelEnum.LamaMaitre)]
    [InlineData(1700, LevelEnum.LamaSeigneur)]
    [InlineData(1900, LevelEnum.LamaMythique)]
    [InlineData(2100, LevelEnum.LamaEternel)]
    [InlineData(2500, LevelEnum.LamaEternel)]
    public void DetermineLevel_ShouldReturnCorrectLevel(double elo, LevelEnum expected)
    {
        var (level, _, _) = _determiner.DetermineLevel(elo);
        Assert.Equal(expected, level);
    }

    [Fact]
    public void DetermineLevel_ShouldReturnLevelName()
    {
        var (_, name, _) = _determiner.DetermineLevel(1200);
        Assert.NotNull(name);
        Assert.NotEmpty(name);
        Assert.Contains("🌱", name);
    }

    [Fact]
    public void DetermineLevel_ShouldReturnEmoji()
    {
        var (_, _, emoji) = _determiner.DetermineLevel(1500);
        Assert.NotNull(emoji);
        Assert.NotEmpty(emoji);
        // Les emojis prennent souvent 2 chars UTF-16
        Assert.True(emoji.Length >= 1);
    }

    [Theory]
    [InlineData(1100)]
    [InlineData(1300)]
    [InlineData(1500)]
    [InlineData(1700)]
    [InlineData(1900)]
    [InlineData(2100)]
    public void GetLevelThresholds_ShouldReturnValidRanges(double elo)
    {
        var (level, _, _) = _determiner.DetermineLevel(elo);
        var thresholds = _determiner.GetLevelThresholds(level);

        Assert.NotNull(thresholds);
        Assert.True(elo >= thresholds.Value.min);
        Assert.True(elo < thresholds.Value.max);
    }

    [Fact]
    public void GetLevelDescription_ShouldReturnDescription()
    {
        var description = _determiner.GetLevelDescription(LevelEnum.LamaMaitre);
        Assert.NotNull(description);
        Assert.NotEmpty(description);
    }

    [Fact]
    public void GetProgressToNextLevel_ReturnsZeroToHundred()
    {
        var progress = _determiner.GetProgressToNextLevel(1200);
        Assert.True(progress >= 0);
        Assert.True(progress <= 100);
    }

    [Fact]
    public void GetProgressToNextLevel_AtLevelStart_ShouldBeNearZero()
    {
        var progress = _determiner.GetProgressToNextLevel(1100);
        Assert.True(progress < 10, "Au début d'un niveau, le progrès devrait être faible");
    }

    [Fact]
    public void GetProgressToNextLevel_NearLevelEnd_ShouldBeHigh()
    {
        var progress = _determiner.GetProgressToNextLevel(1290);
        Assert.True(progress > 80, "Proche de la fin d'un niveau, le progrès devrait être élevé");
    }

    [Fact]
    public void GetProgressToNextLevel_AtMaxLevel_ShouldBe100()
    {
        var progress = _determiner.GetProgressToNextLevel(2500);
        Assert.Equal(100, progress);
    }

    [Theory]
    [InlineData(1050)]
    [InlineData(1100)]
    [InlineData(1200)]
    [InlineData(1500)]
    [InlineData(1800)]
    [InlineData(2200)]
    public void AllLevelTransitions_ShouldBeSmooth(double elo)
    {
        // Chaque transition devrait être définie
        var (level, name, emoji) = _determiner.DetermineLevel(elo);

        Assert.True(level >= LevelEnum.NotRanked && level <= LevelEnum.LamaEternel);
        Assert.NotEmpty(name);
        Assert.NotEmpty(emoji);
    }
}

